using System.Globalization;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using Avalonia.Threading;

namespace AtomUI.City.Presentation;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;
    private readonly IPresentationRuntime? _runtime;
    private readonly IHostDiagnostics? _diagnostics;
    private long _nextOperationId;

    public AvaloniaUiDispatcher()
        : this(Dispatcher.UIThread, runtime: null, diagnostics: null)
    {
    }

    public AvaloniaUiDispatcher(IPresentationRuntime runtime)
        : this(Dispatcher.UIThread, runtime, diagnostics: null)
    {
    }

    public AvaloniaUiDispatcher(Dispatcher dispatcher)
        : this(dispatcher, runtime: null, diagnostics: null)
    {
    }

    public AvaloniaUiDispatcher(Dispatcher dispatcher, IPresentationRuntime? runtime)
        : this(dispatcher, runtime, diagnostics: null)
    {
    }

    public AvaloniaUiDispatcher(
        Dispatcher dispatcher,
        IPresentationRuntime? runtime,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
        _runtime = runtime;
        _diagnostics = diagnostics;
    }

    public bool CheckAccess()
    {
        return _dispatcher.CheckAccess();
    }

    public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var context = CreateOperationContext(nameof(InvokeAsync));

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        var runtimeException = CreateRuntimeException();
        if (runtimeException is not null)
        {
            WriteOperationRejectedDiagnostic(runtimeException, context);

            return ValueTask.FromException(runtimeException);
        }

        if (_dispatcher.CheckAccess())
        {
            return ExecuteInline(callback, context);
        }

        try
        {
            var operation = _dispatcher.InvokeAsync(
                () => ExecuteCallback(callback, context),
                DispatcherPriority.Default,
                cancellationToken);

            return AwaitDispatcherOperationAsync(operation.GetTask(), context, cancellationToken);
        }
        catch (Exception exception)
        {
            var dispatcherException = CreateDispatcherUnavailableException(exception);
            WriteOperationRejectedDiagnostic(dispatcherException, context);

            return ValueTask.FromException(dispatcherException);
        }
    }

    public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var context = CreateOperationContext(nameof(InvokeAsync));

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<T>(cancellationToken);
        }

        var runtimeException = CreateRuntimeException();
        if (runtimeException is not null)
        {
            WriteOperationRejectedDiagnostic(runtimeException, context);

            return ValueTask.FromException<T>(runtimeException);
        }

        if (_dispatcher.CheckAccess())
        {
            return ExecuteInline(callback, context);
        }

        try
        {
            var operation = _dispatcher.InvokeAsync(
                () => ExecuteCallback(callback, context),
                DispatcherPriority.Default,
                cancellationToken);

            return AwaitDispatcherOperationAsync(operation.GetTask(), context, cancellationToken);
        }
        catch (Exception exception)
        {
            var dispatcherException = CreateDispatcherUnavailableException(exception);
            WriteOperationRejectedDiagnostic(dispatcherException, context);

            return ValueTask.FromException<T>(dispatcherException);
        }
    }

    public ValueTask PostAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var context = CreateOperationContext(nameof(PostAsync));

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        var runtimeException = CreateRuntimeException();
        if (runtimeException is not null)
        {
            WriteOperationRejectedDiagnostic(runtimeException, context);

            return ValueTask.FromException(runtimeException);
        }

        if (_dispatcher.CheckAccess())
        {
            return ExecuteInline(callback, cancellationToken, context);
        }

        try
        {
            var operation = _dispatcher.InvokeAsync<Task>(
                () => ExecutePostedCallbackAsync(callback, cancellationToken, context),
                DispatcherPriority.Default,
                cancellationToken);

            return new ValueTask(AwaitNestedDispatcherOperationAsync(
                operation.GetTask(),
                context,
                cancellationToken));
        }
        catch (Exception exception)
        {
            var dispatcherException = CreateDispatcherUnavailableException(exception);
            WriteOperationRejectedDiagnostic(dispatcherException, context);

            return ValueTask.FromException(dispatcherException);
        }
    }

    private async Task AwaitNestedDispatcherOperationAsync(
        Task<Task> operation,
        DispatcherOperationContext context,
        CancellationToken cancellationToken)
    {
        var callbackTask = await AwaitDispatcherOperationAsync(
                operation,
                context,
                cancellationToken)
            .ConfigureAwait(false);

        await callbackTask.ConfigureAwait(false);
    }

    private async Task ExecutePostedCallbackAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DispatcherOperationContext context)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await callback(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteCallbackFailedDiagnostic(exception, context);

            throw;
        }
    }

    private DispatcherOperationContext CreateOperationContext(string targetAction)
    {
        return new DispatcherOperationContext(
            Interlocked.Increment(ref _nextOperationId),
            targetAction,
            Environment.CurrentManagedThreadId);
    }

    private ValueTask ExecuteInline(Action callback, DispatcherOperationContext context)
    {
        try
        {
            ExecuteCallback(callback, context);

            return ValueTask.CompletedTask;
        }
        catch (Exception exception)
        {
            return ValueTask.FromException(exception);
        }
    }

    private ValueTask<T> ExecuteInline<T>(Func<T> callback, DispatcherOperationContext context)
    {
        try
        {
            return ValueTask.FromResult(ExecuteCallback(callback, context));
        }
        catch (Exception exception)
        {
            return ValueTask.FromException<T>(exception);
        }
    }

    private ValueTask ExecuteInline(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken,
        DispatcherOperationContext context)
    {
        try
        {
            var operation = callback(cancellationToken);

            return operation.IsCompletedSuccessfully
                ? operation
                : AwaitInlinePostAsync(operation, context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            WriteCallbackFailedDiagnostic(exception, context);

            return ValueTask.FromException(exception);
        }
    }

    private async ValueTask AwaitInlinePostAsync(
        ValueTask operation,
        DispatcherOperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteCallbackFailedDiagnostic(exception, context);

            throw;
        }
    }

    private ValueTask AwaitDispatcherOperationAsync(
        Task operation,
        DispatcherOperationContext context,
        CancellationToken cancellationToken)
    {
        return operation.IsCompletedSuccessfully
            ? ValueTask.CompletedTask
            : new ValueTask(AwaitDispatcherOperationSlowAsync(operation, context, cancellationToken));
    }

    private ValueTask<T> AwaitDispatcherOperationAsync<T>(
        Task<T> operation,
        DispatcherOperationContext context,
        CancellationToken cancellationToken)
    {
        return operation.IsCompletedSuccessfully
            ? ValueTask.FromResult(operation.Result)
            : new ValueTask<T>(AwaitDispatcherOperationSlowAsync(operation, context, cancellationToken));
    }

    private async Task AwaitDispatcherOperationSlowAsync(
        Task operation,
        DispatcherOperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            var dispatcherException = CreateDispatcherUnavailableException(exception);
            WriteOperationRejectedDiagnostic(dispatcherException, context);

            throw dispatcherException;
        }
    }

    private async Task<T> AwaitDispatcherOperationSlowAsync<T>(
        Task<T> operation,
        DispatcherOperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation.ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            var dispatcherException = CreateDispatcherUnavailableException(exception);
            WriteOperationRejectedDiagnostic(dispatcherException, context);

            throw dispatcherException;
        }
    }

    private void ExecuteCallback(Action callback, DispatcherOperationContext context)
    {
        try
        {
            callback();
        }
        catch (Exception exception)
        {
            WriteCallbackFailedDiagnostic(exception, context);

            throw;
        }
    }

    private T ExecuteCallback<T>(Func<T> callback, DispatcherOperationContext context)
    {
        try
        {
            return callback();
        }
        catch (Exception exception)
        {
            WriteCallbackFailedDiagnostic(exception, context);

            throw;
        }
    }

    private PresentationException? CreateRuntimeException()
    {
        return _runtime?.State switch
        {
            PresentationRuntimeState.NotReady => new PresentationException(
                PresentationError.RuntimeNotReady,
                "Presentation runtime is not ready."),
            PresentationRuntimeState.Stopped or
                PresentationRuntimeState.Faulted => new PresentationException(
                    PresentationError.RuntimeStopping,
                    "Presentation runtime is not accepting UI dispatcher operations."),
            _ => null,
        };
    }

    private static PresentationException CreateDispatcherUnavailableException(Exception exception)
    {
        return new PresentationException(
            PresentationError.DispatcherUnavailable,
            "Avalonia UI dispatcher is not available.",
            exception);
    }

    private void WriteOperationRejectedDiagnostic(
        PresentationException exception,
        DispatcherOperationContext context)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.DispatcherOperationRejected,
            exception.Message,
            HostDiagnosticSeverity.Warning,
            ScopeId: _runtime?.PresentationScope?.Id)
        {
            Context = CreateDiagnosticContext(context, exception.Error.ToString()),
        });
    }

    private void WriteCallbackFailedDiagnostic(
        Exception exception,
        DispatcherOperationContext context)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.DispatcherCallbackFailed,
            exception.Message,
            HostDiagnosticSeverity.Error,
            ScopeId: _runtime?.PresentationScope?.Id)
        {
            Context = CreateDiagnosticContext(context, exception.GetType().FullName),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        DispatcherOperationContext context,
        string? error)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["operationId"] = context.OperationId.ToString(CultureInfo.InvariantCulture),
            ["targetAction"] = context.TargetAction,
            ["callingThreadId"] = context.CallingThreadId.ToString(CultureInfo.InvariantCulture),
            ["dispatcherThreadId"] = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture),
            ["error"] = error,
        };
    }

    private sealed record DispatcherOperationContext(
        long OperationId,
        string TargetAction,
        int CallingThreadId);
}
