using System.Diagnostics;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data operation scheduler.
/// </summary>
public sealed class DataOperationScheduler : IDataOperationScheduler, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<OperationIdentity, OperationGate> _gates = [];
    private int _disposed;

    /// <summary>
    /// Executes the execute async&lt;tresponse&gt; operation.
    /// </summary>
    public ValueTask<DataResult<TResponse>> ExecuteAsync<TResponse>(
        DataRequest<TResponse> request,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);
        var options = request.Concurrency;
        if (!Enum.IsDefined(options.Policy))
        {
            return ValueTask.FromResult(DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.PolicyRejected, "Data concurrency policy is not supported.")));
        }

        var key = CreateOperationIdentity(request, options);
        var gate = AcquireGate(key);
        return ExecuteWithGateAsync(request, operation, cancellationToken, key, gate);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        OperationGate[] gates;
        lock (_syncRoot)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            gates = _gates.Values.ToArray();
            foreach (var gate in gates)
            {
                gate.UserCount++;
            }

            _gates.Clear();
        }

        foreach (var gate in gates)
        {
            try
            {
                gate.CancelAll();
            }
            finally
            {
                ReleaseGate(default, gate);
            }
        }
    }

    private async ValueTask<DataResult<TResponse>> ExecuteWithGateAsync<TResponse>(
        DataRequest<TResponse> request,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken,
        OperationIdentity key,
        OperationGate gate)
    {
        try
        {
            return request.Concurrency.Policy switch
            {
                DataConcurrencyPolicy.AllowConcurrent =>
                    await ExecuteConcurrentAsync(gate, operation, cancellationToken).ConfigureAwait(false),
                DataConcurrencyPolicy.DisallowConcurrent =>
                    await ExecuteDisallowConcurrentAsync(gate, operation, cancellationToken).ConfigureAwait(false),
                DataConcurrencyPolicy.Queue or DataConcurrencyPolicy.KeyedSerial =>
                    await ExecuteQueuedAsync(
                        gate,
                        request.Concurrency.MaximumQueueLength,
                        operation,
                        cancellationToken).ConfigureAwait(false),
                DataConcurrencyPolicy.CancelPrevious =>
                    await ExecuteLatestAsync(gate, operation, cancellationToken, cancelPrevious: true).ConfigureAwait(false),
                DataConcurrencyPolicy.LatestWins =>
                    await ExecuteLatestAsync(gate, operation, cancellationToken, cancelPrevious: false).ConfigureAwait(false),
                _ => throw new UnreachableException(),
            };
        }
        finally
        {
            ReleaseGate(key, gate);
        }
    }

    private OperationGate AcquireGate(OperationIdentity key)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (!_gates.TryGetValue(key, out var gate))
            {
                gate = new OperationGate();
                _gates.Add(key, gate);
            }

            gate.UserCount++;
            return gate;
        }
    }

    private void ReleaseGate(OperationIdentity key, OperationGate gate)
    {
        var dispose = false;
        lock (_syncRoot)
        {
            gate.UserCount--;
            if (gate.UserCount == 0)
            {
                if (_gates.TryGetValue(key, out var registered) && ReferenceEquals(registered, gate))
                {
                    _gates.Remove(key);
                }

                dispose = true;
            }
        }

        if (dispose)
        {
            gate.Dispose();
        }
    }

    private static OperationIdentity CreateOperationIdentity<TResponse>(
        DataRequest<TResponse> request,
        DataConcurrencyOptions options)
    {
        var resourceKey = options.Policy == DataConcurrencyPolicy.KeyedSerial
            ? options.ResourceKey
            : null;
        if (options.Policy == DataConcurrencyPolicy.KeyedSerial && resourceKey is null)
        {
            throw new ArgumentException("KeyedSerial requires a non-empty resource key.", nameof(DataConcurrencyOptions.ResourceKey));
        }

        return options.OperationKey is { } operationKey
            ? new OperationIdentity(null, null, operationKey, resourceKey)
            : new OperationIdentity(request.ClientId, request.OperationName, null, resourceKey);
    }

    private static async ValueTask<DataResult<TResponse>> ExecuteConcurrentAsync<TResponse>(
        OperationGate gate,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            gate.ShutdownToken);
        try
        {
            return await operation(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
    }

    private static async ValueTask<DataResult<TResponse>> ExecuteDisallowConcurrentAsync<TResponse>(
        OperationGate gate,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref gate.Active, 1, 0) != 0)
        {
            return DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.PolicyRejected, "A data operation with the same key is already running."));
        }

        try
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                gate.ShutdownToken);
            return await operation(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        finally
        {
            Volatile.Write(ref gate.Active, 0);
        }
    }

    private static async ValueTask<DataResult<TResponse>> ExecuteQueuedAsync<TResponse>(
        OperationGate gate,
        int maximumQueueLength,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken)
    {
        if (!gate.TryEnqueue(maximumQueueLength, out var ticket))
        {
            return DataResult<TResponse>.Failed(
                new DataError(DataErrorKind.PolicyRejected, "The data operation queue is full."));
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            gate.ShutdownToken);
        try
        {
            await ticket.Predecessor.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            return await operation(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return DataResult<TResponse>.Cancelled();
        }
        finally
        {
            gate.Complete(ticket);
        }
    }

    private static async ValueTask<DataResult<TResponse>> ExecuteLatestAsync<TResponse>(
        OperationGate gate,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken,
        bool cancelPrevious)
    {
        var invocation = gate.PublishInvocation(cancelPrevious);
        invocation.Previous?.Cancel();

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            invocation.Current.Token,
            gate.ShutdownToken);

        DataResult<TResponse> result;
        try
        {
            result = await operation(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = DataResult<TResponse>.Cancelled();
        }
        finally
        {
            gate.CompleteInvocation(invocation.Sequence, invocation.Current);
        }

        if (!gate.IsLatest(invocation.Sequence))
        {
            return cancelPrevious
                ? DataResult<TResponse>.Cancelled("A newer data operation replaced this operation.")
                : DataResult<TResponse>.StaleSuppressed("A newer data operation completed after this operation started.");
        }

        return result;
    }

    private sealed class OperationGate : IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly CancellationTokenSource _shutdown = new();
        private readonly HashSet<InvocationState> _invocations = [];
        private InvocationState? _currentCancellation;
        private Task _queueTail = Task.CompletedTask;
        private int _queueCount;
        private long _latestSequence;
        private int _disposed;

        public int Active;

        public int UserCount;

        public CancellationToken ShutdownToken => _shutdown.Token;

        public bool TryEnqueue(int maximumQueueLength, out QueueTicket ticket)
        {
            lock (_syncRoot)
            {
                if (_queueCount >= maximumQueueLength || _shutdown.IsCancellationRequested)
                {
                    ticket = default;
                    return false;
                }

                var completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                ticket = new QueueTicket(_queueTail, completion);
                _queueTail = completion.Task;
                _queueCount++;
                return true;
            }
        }

        public void Complete(QueueTicket ticket)
        {
            ticket.Completion.TrySetResult();
            lock (_syncRoot)
            {
                _queueCount--;
            }
        }

        public Invocation PublishInvocation(bool replaceCancellation)
        {
            lock (_syncRoot)
            {
                var sequence = ++_latestSequence;
                var previous = replaceCancellation ? _currentCancellation : null;
                var current = new InvocationState();
                _invocations.Add(current);
                if (replaceCancellation)
                {
                    _currentCancellation = current;
                }

                return new Invocation(sequence, current, previous);
            }
        }

        public bool IsLatest(long sequence)
        {
            lock (_syncRoot)
            {
                return sequence == _latestSequence;
            }
        }

        public void CompleteInvocation(long sequence, InvocationState current)
        {
            lock (_syncRoot)
            {
                if (sequence == _latestSequence && ReferenceEquals(_currentCancellation, current))
                {
                    _currentCancellation = null;
                }

                _invocations.Remove(current);
            }

            current.Dispose();
        }

        public void CancelAll()
        {
            InvocationState[] invocations;
            lock (_syncRoot)
            {
                invocations = _invocations.ToArray();
                _currentCancellation = null;
            }

            foreach (var invocation in invocations)
            {
                invocation.Cancel();
            }

            TryCancel(_shutdown);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            CancelAll();
            _shutdown.Dispose();
        }

        private static void TryCancel(CancellationTokenSource cancellation)
        {
            try
            {
                cancellation.Cancel(throwOnFirstException: false);
            }
            catch (AggregateException)
            {
                // Cancellation callbacks are external code and cannot break scheduler cleanup.
            }
        }
    }

    private readonly record struct QueueTicket(
        Task Predecessor,
        TaskCompletionSource Completion);

    private readonly record struct OperationIdentity(
        string? ClientId,
        string? OperationName,
        string? ExplicitOperationKey,
        string? ResourceKey);

    private sealed record Invocation(
        long Sequence,
        InvocationState Current,
        InvocationState? Previous);

    private sealed class InvocationState : IDisposable
    {
        private readonly object _syncRoot = new();
        private CancellationTokenSource? _cancellation = new();
        private int _activeCancellations;
        private bool _disposeRequested;

        public CancellationToken Token
        {
            get
            {
                lock (_syncRoot)
                {
                    return _cancellation?.Token ?? new CancellationToken(canceled: true);
                }
            }
        }

        public void Cancel()
        {
            CancellationTokenSource? cancellation;
            lock (_syncRoot)
            {
                cancellation = _cancellation;
                if (cancellation is null || _disposeRequested)
                {
                    return;
                }

                _activeCancellations++;
            }

            try
            {
                try
                {
                    cancellation.Cancel(throwOnFirstException: false);
                }
                catch (AggregateException)
                {
                    // Cancellation callbacks are external code and cannot break scheduler cleanup.
                }
            }
            finally
            {
                CompleteCancellation();
            }
        }

        public void Dispose()
        {
            CancellationTokenSource? cancellation = null;
            lock (_syncRoot)
            {
                _disposeRequested = true;
                if (_activeCancellations == 0)
                {
                    cancellation = _cancellation;
                    _cancellation = null;
                }
            }

            cancellation?.Dispose();
        }

        private void CompleteCancellation()
        {
            CancellationTokenSource? cancellation = null;
            lock (_syncRoot)
            {
                _activeCancellations--;
                if (_activeCancellations == 0 && _disposeRequested)
                {
                    cancellation = _cancellation;
                    _cancellation = null;
                }
            }

            cancellation?.Dispose();
        }
    }
}
