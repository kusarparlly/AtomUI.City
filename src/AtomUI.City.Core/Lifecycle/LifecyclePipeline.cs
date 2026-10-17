using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents lifecycle pipeline.
/// </summary>
public sealed class LifecyclePipeline
{
    private readonly IReadOnlyList<LifecycleMiddlewareRegistration> _middleware;
    private readonly Func<LifecycleContext, CancellationToken, ValueTask> _terminalHandler;

    internal LifecyclePipeline(
        IReadOnlyList<LifecycleMiddlewareRegistration> middleware,
        Func<LifecycleContext, CancellationToken, ValueTask> terminalHandler)
    {
        _middleware = middleware;
        _terminalHandler = terminalHandler;
    }

    /// <summary>
    /// Executes the execute async operation.
    /// </summary>
    public ValueTask ExecuteAsync(
        LifecycleContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteAsync(
            context,
            _terminalHandler,
            guaranteeTerminal: false,
            cancellationToken: cancellationToken);
    }

    internal async ValueTask ExecuteAsync(
        LifecycleContext context,
        Func<LifecycleContext, CancellationToken, ValueTask> terminalHandler,
        bool guaranteeTerminal,
        IHostDiagnostics? diagnostics = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminalHandler);

        var terminalInvoked = false;
        Exception? pipelineFailure = null;
        var attributedFailures = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var terminalFailures = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var transaction = new LifecyclePipelineTransaction();

        try
        {
            await InvokeAsync(0).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            pipelineFailure = exception;
        }

        if (guaranteeTerminal && !terminalInvoked)
        {
            try
            {
                await InvokeTerminalAsync(observeCancellation: false).ConfigureAwait(false);
            }
            catch (Exception terminalFailure)
            {
                pipelineFailure = pipelineFailure is null
                    ? terminalFailure
                    : new AggregateException(pipelineFailure, terminalFailure);
            }
        }

        transaction.Complete();

        if (pipelineFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(pipelineFailure)
                .Throw();
        }

        return;

        async ValueTask InvokeAsync(int index)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.IsShortCircuited)
            {
                return;
            }

            if (index == _middleware.Count)
            {
                await InvokeTerminalAsync(observeCancellation: true).ConfigureAwait(false);
                return;
            }

            var registration = _middleware[index];

            if (registration.Stage is { } stage && context.Stage != stage)
            {
                await InvokeAsync(index + 1).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var next = new LifecycleNextInvocation(
                transaction,
                () => InvokeAsync(index + 1));
            Exception? handlerFailure = null;

            try
            {
                await registration.Handler(
                    context,
                    next.InvokeAsync,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                handlerFailure = exception;
            }

            var nextCompletion = await next.CloseAsync().ConfigureAwait(false);
            var contractFailure = nextCompletion.Escaped
                ? new InvalidOperationException(
                    "Lifecycle middleware must await or return next before completing.")
                : null;
            var failure = CombineFailures(
                contractFailure,
                handlerFailure,
                nextCompletion.Failure);

            if (failure is not null)
            {
                var attributableFailure = contractFailure ?? handlerFailure;

                if (attributableFailure is not null &&
                    !IsExpectedCancellation(attributableFailure) &&
                    !terminalFailures.Contains(attributableFailure) &&
                    attributedFailures.Add(attributableFailure))
                {
                    WriteMiddlewareFailure(
                        diagnostics,
                        context,
                        registration,
                        attributableFailure);
                }

                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(failure)
                    .Throw();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        async ValueTask InvokeTerminalAsync(bool observeCancellation)
        {
            if (terminalInvoked)
            {
                throw new InvalidOperationException("Lifecycle terminal handler can run only once.");
            }

            if (observeCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            terminalInvoked = true;

            try
            {
                await terminalHandler(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                terminalFailures.Add(exception);
                throw;
            }

            if (observeCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        bool IsExpectedCancellation(Exception exception)
        {
            return exception is OperationCanceledException &&
                   cancellationToken.IsCancellationRequested;
        }
    }

    private static Exception? CombineFailures(params Exception?[] candidates)
    {
        var failures = new List<Exception>(candidates.Length);

        foreach (var candidate in candidates)
        {
            if (candidate is null || failures.Any(existing => ReferenceEquals(existing, candidate)))
            {
                continue;
            }

            failures.Add(candidate);
        }

        return failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException(
                "Lifecycle middleware and its downstream continuation both failed.",
                failures).Flatten(),
        };
    }

    private sealed class LifecyclePipelineTransaction
    {
        private int _completed;

        public bool IsActive => Volatile.Read(ref _completed) == 0;

        public void Complete()
        {
            Interlocked.Exchange(ref _completed, 1);
        }
    }

    private sealed class LifecycleNextInvocation
    {
        private const int Available = 0;
        private const int Invoked = 1;
        private const int Closed = 2;

        private readonly LifecyclePipelineTransaction _transaction;
        private readonly Func<ValueTask> _continuation;
        private readonly TaskCompletionSource<Task> _taskPublished = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _state;

        public LifecycleNextInvocation(
            LifecyclePipelineTransaction transaction,
            Func<ValueTask> continuation)
        {
            _transaction = transaction;
            _continuation = continuation;
        }

        public ValueTask InvokeAsync()
        {
            if (!_transaction.IsActive)
            {
                return ValueTask.FromException(CreateExpiredException());
            }

            var previousState = Interlocked.CompareExchange(
                ref _state,
                Invoked,
                Available);

            if (previousState != Available)
            {
                return ValueTask.FromException(previousState == Closed
                    ? CreateExpiredException()
                    : new InvalidOperationException(
                        "Lifecycle middleware can invoke next only once."));
            }

            Task continuationTask;

            if (!_transaction.IsActive)
            {
                continuationTask = Task.FromException(CreateExpiredException());
            }
            else
            {
                try
                {
                    continuationTask = _continuation().AsTask();
                }
                catch (Exception exception)
                {
                    continuationTask = Task.FromException(exception);
                }
            }

            _taskPublished.TrySetResult(continuationTask);
            return new ValueTask(continuationTask);
        }

        public async ValueTask<LifecycleNextCompletion> CloseAsync()
        {
            var previousState = Interlocked.Exchange(ref _state, Closed);

            if (previousState == Available)
            {
                return default;
            }

            if (previousState == Closed)
            {
                throw new InvalidOperationException(
                    "Lifecycle middleware next invocation was already closed.");
            }

            var continuationTask = await _taskPublished.Task.ConfigureAwait(false);
            var escaped = !continuationTask.IsCompleted;
            Exception? failure = null;

            try
            {
                await continuationTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            return new LifecycleNextCompletion(escaped, failure);
        }

        private static InvalidOperationException CreateExpiredException()
        {
            return new InvalidOperationException(
                "Lifecycle middleware cannot invoke next after its invocation has completed.");
        }
    }

    private readonly record struct LifecycleNextCompletion(
        bool Escaped,
        Exception? Failure);

    private static void WriteMiddlewareFailure(
        IHostDiagnostics? diagnostics,
        LifecycleContext context,
        LifecycleMiddlewareRegistration registration,
        Exception exception)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(new HostDiagnosticRecord(
                HostDiagnosticIds.LifecycleMiddlewareFailed,
                "Lifecycle middleware execution failed.",
                HostDiagnosticSeverity.Error,
                Stage: context.Stage)
            {
                Context = new Dictionary<string, string?>
                {
                    ["middlewareType"] = registration.MiddlewareType.FullName
                                         ?? registration.MiddlewareType.Name,
                    ["operationId"] = context.OperationId,
                    ["exceptionType"] = exception.GetType().FullName,
                },
            });
        }
        catch
        {
            // Diagnostics must not replace the original lifecycle failure.
        }
    }
}
