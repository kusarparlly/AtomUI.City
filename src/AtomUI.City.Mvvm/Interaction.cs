using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents interaction&lt;trequest, tresult&gt;.
/// </summary>
public sealed class Interaction<TRequest, TResult>
{
    private readonly object _gate = new();
    private readonly List<HandlerRegistration> _handlers = [];
    private readonly IHostDiagnostics? _diagnostics;

    /// <summary>
    /// Executes the interaction operation.
    /// </summary>
    public Interaction(IHostDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the register handler operation.
    /// </summary>
    public IDisposable RegisterHandler(
        Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
        IActivationScope? activationScope = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var registration = new HandlerRegistration(this, handler, activationScope);

        lock (_gate)
        {
            _handlers.Add(registration);
        }

        activationScope?.Add(registration);

        return registration;
    }

    /// <summary>
    /// Executes the request async operation.
    /// </summary>
    public async ValueTask<InteractionResult<TResult>> RequestAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        HandlerRegistration? registration;

        lock (_gate)
        {
            registration = _handlers.LastOrDefault();
        }

        if (registration is null)
        {
            WriteInteractionDiagnostic(
                MvvmDiagnosticIds.InteractionNotHandled,
                $"Interaction request of type '{typeof(TRequest).FullName}' has no registered handler.",
                HostDiagnosticSeverity.Warning,
                request,
                null,
                null);
            return InteractionResult<TResult>.NotHandled();
        }

        using var linkedCancellationTokenSource = registration.ActivationScope is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, registration.ActivationScope.CancellationToken);

        try
        {
            var context = new InteractionContext<TRequest>(
                request,
                Guid.NewGuid(),
                registration.ActivationScope?.Id,
                registration.HandlerType);
            var handlerTask = registration.Handler(context, linkedCancellationTokenSource.Token).AsTask();
            var cancellationTask = Task.Delay(
                Timeout.InfiniteTimeSpan,
                linkedCancellationTokenSource.Token);

            if (await Task.WhenAny(handlerTask, cancellationTask).ConfigureAwait(false) != handlerTask)
            {
                ObserveHandlerFault(handlerTask);
                return InteractionResult<TResult>.Canceled();
            }

            var value = await handlerTask.ConfigureAwait(false);
            linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
            return InteractionResult<TResult>.Completed(value);
        }
        catch (OperationCanceledException)
            when (linkedCancellationTokenSource.IsCancellationRequested)
        {
            return InteractionResult<TResult>.Canceled();
        }
        catch (Exception exception)
        {
            WriteInteractionDiagnostic(
                MvvmDiagnosticIds.InteractionFailed,
                $"Interaction request of type '{typeof(TRequest).FullName}' handler failed: {exception.Message}",
                HostDiagnosticSeverity.Error,
                request,
                registration.ActivationScope?.Id,
                exception);
            return InteractionResult<TResult>.Failed(exception);
        }
    }

    private void WriteInteractionDiagnostic(
        string code,
        string message,
        HostDiagnosticSeverity severity,
        TRequest request,
        Guid? activationScopeId,
        Exception? exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            code,
            message,
            severity)
        {
            Context = new Dictionary<string, string?>
            {
                ["requestType"] = typeof(TRequest).FullName,
                ["resultType"] = typeof(TResult).FullName,
                ["activationScopeId"] = activationScopeId?.ToString(),
                ["exceptionType"] = exception?.GetType().FullName,
            }
        });
    }

    private void Remove(HandlerRegistration registration)
    {
        lock (_gate)
        {
            _handlers.Remove(registration);
        }
    }

    private static void ObserveHandlerFault(Task handlerTask)
    {
        _ = handlerTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed class HandlerRegistration : IDisposable
    {
        private readonly Interaction<TRequest, TResult> _interaction;
        private bool _disposed;

        public HandlerRegistration(
            Interaction<TRequest, TResult> interaction,
            Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
            IActivationScope? activationScope)
        {
            _interaction = interaction;
            Handler = handler;
            ActivationScope = activationScope;
            HandlerType = handler.Target?.GetType() ?? handler.Method.DeclaringType;
        }

        public Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> Handler { get; }

        public IActivationScope? ActivationScope { get; }

        public Type? HandlerType { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _interaction.Remove(this);
        }
    }
}
