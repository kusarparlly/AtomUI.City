namespace AtomUI.City.Data;

internal sealed class DataRuntimeGate : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly CancellationTokenSource _shutdown = new();
    private int _acceptingRequests = 1;
    private int _activeRequests;
    private Task? _stopTask;
    private TaskCompletionSource? _requestsDrained;

    public bool IsAcceptingRequests => Volatile.Read(ref _acceptingRequests) != 0;

    public bool TryEnter(out IDisposable? requestLease, out CancellationToken shutdownToken)
    {
        lock (_syncRoot)
        {
            if (_acceptingRequests == 0)
            {
                requestLease = null;
                shutdownToken = new CancellationToken(canceled: true);
                return false;
            }

            _activeRequests++;
            requestLease = new RequestLease(this);
            shutdownToken = _shutdown.Token;
            return true;
        }
    }

    public ValueTask StopAsync(CancellationToken waitCancellationToken = default)
    {
        Task stopTask;
        TaskCompletionSource? completion = null;
        lock (_syncRoot)
        {
            Volatile.Write(ref _acceptingRequests, 0);
            if (_stopTask is null)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _stopTask = completion.Task;
            }

            stopTask = _stopTask;
        }

        if (completion is not null)
        {
            _ = CompleteStopAsync(completion);
        }

        return new ValueTask(stopTask.WaitAsync(waitCancellationToken));
    }

    public void Dispose()
    {
        _ = StopAsync();
    }

    private async Task CompleteStopAsync(TaskCompletionSource completion)
    {
        Exception? cancellationFailure = null;
        try
        {
            _shutdown.Cancel(throwOnFirstException: false);
        }
        catch (AggregateException exception)
        {
            cancellationFailure = exception;
        }

        Task requestsDrained;
        lock (_syncRoot)
        {
            requestsDrained = _activeRequests == 0
                ? Task.CompletedTask
                : (_requestsDrained ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        await requestsDrained.ConfigureAwait(false);
        _shutdown.Dispose();
        if (cancellationFailure is null)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(cancellationFailure);
        }
    }

    private void Exit()
    {
        TaskCompletionSource? requestsDrained = null;
        lock (_syncRoot)
        {
            _activeRequests--;
            if (_activeRequests == 0 && _acceptingRequests == 0)
            {
                requestsDrained = _requestsDrained;
            }
        }

        requestsDrained?.TrySetResult();
    }

    private sealed class RequestLease(DataRuntimeGate owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Exit();
            }
        }
    }
}
