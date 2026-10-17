using System.Runtime.ExceptionServices;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Hosting;

internal sealed class DefaultApplicationHost : IApplicationHost
{
    private readonly IHostDiagnostics _diagnostics;
    private readonly IHost _genericHost;
    private readonly LifecyclePipeline _lifecyclePipeline;
    private readonly IModuleLifecycleController _moduleLifecycle;
    private readonly ApplicationHostOptions _options;
    private readonly object _stateSync = new();
    private IServiceScope? _applicationServiceScope;
    private bool _cleanupCompleted;
    private Task? _disposeTask;
    private bool _genericHostStarted;
    private Task? _startTask;
    private CancellationTokenSource? _startupCancellation;
    private ApplicationHostState _state = ApplicationHostState.Created;
    private Task? _stopTask;

    public DefaultApplicationHost(
        IHost genericHost,
        IApplicationContext context,
        IHostDiagnostics diagnostics,
        LifecycleScope hostScope,
        IModuleLifecycleController moduleLifecycle,
        LifecyclePipeline lifecyclePipeline,
        ApplicationHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(genericHost);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(hostScope);
        ArgumentNullException.ThrowIfNull(moduleLifecycle);
        ArgumentNullException.ThrowIfNull(lifecyclePipeline);
        ArgumentNullException.ThrowIfNull(options);

        _genericHost = genericHost;
        _diagnostics = diagnostics;
        _moduleLifecycle = moduleLifecycle;
        _lifecyclePipeline = lifecyclePipeline;
        _options = options;
        Context = context;
        HostScope = hostScope;
    }

    public IServiceProvider Services => _genericHost.Services;

    public IApplicationContext Context { get; }

    public LifecycleScope HostScope { get; }

    public LifecycleScope? ApplicationScope { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Start);

        DeferredLifecycleOperation? operation = null;
        CancellationTokenSource? startupCancellation = null;
        Task startTask;

        lock (_stateSync)
        {
            ThrowIfDisposed();

            switch (_state)
            {
                case ApplicationHostState.Running:
                    return Task.CompletedTask;
                case ApplicationHostState.Starting:
                    startTask = _startTask
                        ?? throw new InvalidOperationException("Host start transaction was not published.");
                    break;
                case ApplicationHostState.Created:
                    startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    operation = new DeferredLifecycleOperation();
                    _startupCancellation = startupCancellation;
                    _startTask = operation.Task;
                    _state = ApplicationHostState.Starting;
                    startTask = _startTask;
                    break;
                case ApplicationHostState.Stopping:
                case ApplicationHostState.Stopped:
                    throw new InvalidOperationException("Application host cannot be started after it has begun stopping.");
                case ApplicationHostState.Faulted:
                    throw new InvalidOperationException("A faulted application host must be stopped or disposed.");
                default:
                    throw new InvalidOperationException($"Application host cannot start from state '{_state}'.");
            }
        }

        operation?.Start(
            this,
            LifecycleOperationKind.Start,
            () => StartCoreAsync(startupCancellation!.Token));

        return operation is not null || !cancellationToken.CanBeCanceled
            ? startTask
            : startTask.WaitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken waitCancellationToken = default)
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Stop);

        return GetOrStartStopTask(waitCancellationToken);
    }

    private Task GetOrStartStopTask(CancellationToken waitCancellationToken)
    {
        DeferredLifecycleOperation? operation = null;
        CancellationTokenSource? startupCancellation = null;
        Task? startTask = null;
        Task stopTask;

        lock (_stateSync)
        {
            ThrowIfDisposed();

            switch (_state)
            {
                case ApplicationHostState.Created:
                    operation = new DeferredLifecycleOperation();
                    _stopTask = operation.Task;
                    _state = ApplicationHostState.Stopping;
                    stopTask = _stopTask;
                    break;
                case ApplicationHostState.Stopped:
                    stopTask = _stopTask ?? Task.CompletedTask;
                    break;
                case ApplicationHostState.Stopping:
                    stopTask = _stopTask
                        ?? throw new InvalidOperationException("Host stop transaction was not published.");
                    break;
                case ApplicationHostState.Starting:
                    startupCancellation = _startupCancellation
                        ?? throw new InvalidOperationException("Host startup cancellation was not published.");
                    startTask = _startTask
                        ?? throw new InvalidOperationException("Host start transaction was not published.");
                    operation = new DeferredLifecycleOperation();
                    _stopTask = operation.Task;
                    _state = ApplicationHostState.Stopping;
                    stopTask = _stopTask;
                    break;
                case ApplicationHostState.Running:
                case ApplicationHostState.Faulted:
                    operation = new DeferredLifecycleOperation();
                    _stopTask = operation.Task;
                    _state = ApplicationHostState.Stopping;
                    stopTask = _stopTask;
                    break;
                default:
                    throw new InvalidOperationException($"Application host cannot stop from state '{_state}'.");
            }
        }

        if (operation is not null)
        {
            operation.Start(
                this,
                LifecycleOperationKind.Stop,
                startTask is null
                    ? StopCoreAsync
                    : () => StopAfterStartAsync(startTask, startupCancellation!));
        }

        return waitCancellationToken.CanBeCanceled
            ? stopTask.WaitAsync(waitCancellationToken)
            : stopTask;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);

        Exception? failure = null;

        try
        {
            await WaitForShutdownSignalAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception stopFailure)
        {
            failure = failure is null
                ? stopFailure
                : new AggregateException(failure, stopFailure);
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task WaitForShutdownSignalAsync(CancellationToken cancellationToken)
    {
        var applicationLifetime = Services.GetRequiredService<IHostApplicationLifetime>();

        if (applicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var stoppingRegistration = applicationLifetime.ApplicationStopping.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            completion);
        using var cancellationRegistration = cancellationToken.Register(
            static state =>
            {
                var (source, token) = ((TaskCompletionSource, CancellationToken))state!;
                source.TrySetCanceled(token);
            },
            (completion, cancellationToken));

        await completion.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        Task.Run(async () => await DisposeAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    public ValueTask DisposeAsync()
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Dispose);

        DeferredLifecycleOperation? operation = null;
        Task disposeTask;

        lock (_stateSync)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            if (_state == ApplicationHostState.Disposed)
            {
                return ValueTask.CompletedTask;
            }

            operation = new DeferredLifecycleOperation();
            _disposeTask = operation.Task;
            disposeTask = _disposeTask;
        }

        operation.Start(this, LifecycleOperationKind.Dispose, DisposeCoreAsync);

        return new ValueTask(disposeTask);
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var operationId = CreateOperationId();

        try
        {
            await ExecuteRequiredStageAsync(
                LifecycleStages.ApplicationStart,
                StartTransactionAsync,
                cancellationToken,
                operationId).ConfigureAwait(false);

            lock (_stateSync)
            {
                if (_state == ApplicationHostState.Starting)
                {
                    _state = ApplicationHostState.Running;
                }
            }

            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStarted,
                "Application host has started.",
                HostDiagnosticSeverity.Info)
            {
                Context = CreateOperationContext("start", operationId),
            });
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            var rollbackFailures = await RollbackStartupAsync(operationId).ConfigureAwait(false);
            TransitionStartupFailure();

            if (rollbackFailures.Count > 0)
            {
                throw CreateStartupRollbackFailure(exception, rollbackFailures);
            }

            throw;
        }
        catch (Exception exception)
        {
            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStartFailed,
                "Application host failed to start.",
                HostDiagnosticSeverity.Error)
            {
                Context = CreateExceptionContext(exception, "start", operationId),
            });

            var rollbackFailures = await RollbackStartupAsync(operationId).ConfigureAwait(false);
            TransitionStartupFailure();

            if (rollbackFailures.Count > 0)
            {
                throw CreateStartupRollbackFailure(exception, rollbackFailures);
            }

            throw;
        }
    }

    private async ValueTask StartTransactionAsync(
        LifecycleContext context,
        CancellationToken cancellationToken)
    {
        await _genericHost.StartAsync(cancellationToken).ConfigureAwait(false);
        _genericHostStarted = true;

        _applicationServiceScope = Services.CreateScope();
        var applicationScope = HostScope.CreateChild(LifecycleScopeKind.Application, "application");
        ApplicationScope = applicationScope;
        var applicationServices = _applicationServiceScope.ServiceProvider;

        await _moduleLifecycle.ConfigureContributionsAsync(
            Context,
            applicationServices,
            cancellationToken).ConfigureAwait(false);

        await ExecuteRequiredStageAsync(
            LifecycleStages.ModuleInitialize,
            async (_, stageCancellationToken) =>
            {
                await _moduleLifecycle.InitializeAsync(
                    Context,
                    applicationServices,
                    applicationScope,
                    stageCancellationToken).ConfigureAwait(false);
            },
            cancellationToken,
            context.OperationId).ConfigureAwait(false);

        await ExecuteRequiredStageAsync(
            LifecycleStages.ModuleStart,
            static (_, _) => ValueTask.CompletedTask,
            cancellationToken,
            context.OperationId).ConfigureAwait(false);
    }

    private async Task StopAfterStartAsync(
        Task startTask,
        CancellationTokenSource startupCancellation)
    {
        var failures = new List<Exception>();

        try
        {
            startupCancellation.Cancel(throwOnFirstException: false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        try
        {
            await startTask.ConfigureAwait(false);
        }
        catch
        {
            // Startup reports its own failure and performs rollback.
        }

        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Application host stop-after-start failed.",
                failures).Flatten();
        }
    }

    private async Task StopCoreAsync()
    {
        var operationId = CreateOperationId();
        var failures = new List<Exception>();

        if (!_cleanupCompleted)
        {
            using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
            await StopTransactionAsync(
                timeout.Token,
                failures,
                isRollback: false,
                operationId: operationId).ConfigureAwait(false);

            if (timeout.IsCancellationRequested &&
                !failures.Any(failure => failure is TimeoutException))
            {
                failures.Add(new TimeoutException(
                    $"Application host shutdown exceeded {_options.ShutdownTimeout}."));
            }
        }

        lock (_stateSync)
        {
            _state = ApplicationHostState.Stopped;
        }

        WriteDiagnostic(new HostDiagnosticRecord(
            HostDiagnosticIds.HostStopped,
            "Application host has stopped.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateOperationContext("stop", operationId),
        });

        if (failures.Count > 0)
        {
            var aggregate = new AggregateException(
                "One or more application host shutdown stages failed.",
                failures).Flatten();

            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStopFailed,
                "Application host stopped with cleanup failures.",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["failureCount"] = aggregate.InnerExceptions.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["exceptionType"] = aggregate.GetType().FullName,
                    ["operation"] = "stop",
                    ["operationId"] = operationId,
                },
            });

            throw aggregate;
        }
    }

    private async Task<IReadOnlyList<Exception>> RollbackStartupAsync(string operationId)
    {
        if (_cleanupCompleted)
        {
            return [];
        }

        var failures = new List<Exception>();
        using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
        await StopTransactionAsync(
            timeout.Token,
            failures,
            isRollback: true,
            operationId: operationId).ConfigureAwait(false);

        foreach (var failure in failures)
        {
            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStopFailed,
                "Application host startup rollback failed.",
                HostDiagnosticSeverity.Error)
            {
                Context = CreateExceptionContext(failure, "startupRollback", operationId),
            });
        }

        return Array.AsReadOnly(failures.ToArray());
    }

    private static AggregateException CreateStartupRollbackFailure(
        Exception startupFailure,
        IReadOnlyList<Exception> rollbackFailures)
    {
        return new AggregateException(
            "Application host startup failed and rollback was incomplete.",
            new[] { startupFailure }.Concat(rollbackFailures));
    }

    private async Task StopTransactionAsync(
        CancellationToken cancellationToken,
        ICollection<Exception> failures,
        bool isRollback,
        string operationId)
    {
        var context = new LifecycleContext(
            LifecycleStages.ApplicationStop,
            _applicationServiceScope?.ServiceProvider ?? Services,
            operationId: operationId);

        try
        {
            await _lifecyclePipeline.ExecuteAsync(
                context,
                async (_, effectiveCancellationToken) =>
                {
                    await ExecuteStopTerminalAsync(
                        effectiveCancellationToken,
                        failures,
                        operationId).ConfigureAwait(false);
                },
                guaranteeTerminal: true,
                diagnostics: _diagnostics,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            failures.Add(new TimeoutException(
                isRollback
                    ? "Application host startup rollback timed out."
                    : "Application host shutdown timed out."));
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }
        finally
        {
            _cleanupCompleted = true;
        }
    }

    private async Task ExecuteStopTerminalAsync(
        CancellationToken cancellationToken,
        ICollection<Exception> failures,
        string operationId)
    {
        try
        {
            await HostScope.StopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        var moduleStopContext = new LifecycleContext(
            LifecycleStages.ModuleStop,
            _applicationServiceScope?.ServiceProvider ?? Services,
            operationId: operationId);

        try
        {
            await _lifecyclePipeline.ExecuteAsync(
                moduleStopContext,
                async (_, effectiveCancellationToken) =>
                {
                    await _moduleLifecycle.ShutdownAsync(
                        Context,
                        _applicationServiceScope?.ServiceProvider ?? Services,
                        effectiveCancellationToken).ConfigureAwait(false);
                },
                guaranteeTerminal: true,
                diagnostics: _diagnostics,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            failures.Add(new TimeoutException("Module shutdown timed out."));
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        if (_applicationServiceScope is not null)
        {
            try
            {
                if (_applicationServiceScope is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _applicationServiceScope.Dispose();
                }
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
            finally
            {
                _applicationServiceScope = null;
            }
        }

        if (_genericHostStarted)
        {
            try
            {
                Task genericHostStop;
                using (LifecycleInvocationGuard.EnterSynchronous(this, LifecycleOperationKind.Stop))
                {
                    genericHostStop = _genericHost.StopAsync(cancellationToken);
                }

                await genericHostStop.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                failures.Add(new TimeoutException("Generic host shutdown timed out."));
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
            finally
            {
                _genericHostStarted = false;
            }
        }
    }

    private async ValueTask ExecuteRequiredStageAsync(
        LifecycleStage stage,
        Func<LifecycleContext, CancellationToken, ValueTask> terminal,
        CancellationToken cancellationToken,
        string operationId)
    {
        var terminalCompleted = false;
        var context = new LifecycleContext(
            stage,
            _applicationServiceScope?.ServiceProvider ?? Services,
            operationId: operationId);

        await _lifecyclePipeline.ExecuteAsync(
            context,
            async (stageContext, effectiveCancellationToken) =>
            {
                await terminal(stageContext, effectiveCancellationToken).ConfigureAwait(false);
                terminalCompleted = true;
            },
            guaranteeTerminal: false,
            diagnostics: _diagnostics,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!terminalCompleted)
        {
            throw new InvalidOperationException(
                $"Lifecycle stage '{stage}' did not complete its required terminal operation.");
        }
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();

        try
        {
            await GetOrStartStopTask(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        try
        {
            await _moduleLifecycle.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        if (_applicationServiceScope is not null)
        {
            try
            {
                _applicationServiceScope.Dispose();
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
            finally
            {
                _applicationServiceScope = null;
            }
        }

        try
        {
            await HostScope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        try
        {
            if (_genericHost is IAsyncDisposable asyncGenericHost)
            {
                await asyncGenericHost.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                _genericHost.Dispose();
            }
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        _startupCancellation?.Dispose();

        lock (_stateSync)
        {
            _state = ApplicationHostState.Disposed;
        }

        CompleteDiagnostics();

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more application host disposal stages failed.",
                failures).Flatten();
        }
    }

    private void CompleteDiagnostics()
    {
        try
        {
            _diagnostics.Complete();
        }
        catch
        {
            // A diagnostics sink must never interrupt Host cleanup.
        }
    }

    private void TransitionStartupFailure()
    {
        lock (_stateSync)
        {
            if (_state == ApplicationHostState.Starting)
            {
                _state = ApplicationHostState.Faulted;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_state == ApplicationHostState.Disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultApplicationHost));
        }
    }

    private void WriteDiagnostic(HostDiagnosticRecord record)
    {
        try
        {
            _diagnostics.Write(record);
        }
        catch
        {
            // Diagnostics must not prevent lifecycle cleanup.
        }
    }

    private static IReadOnlyDictionary<string, string?> CreateExceptionContext(
        Exception exception,
        string operation,
        string operationId)
    {
        return new Dictionary<string, string?>
        {
            ["exceptionType"] = exception.GetType().FullName,
            ["operation"] = operation,
            ["operationId"] = operationId,
        };
    }

    private static IReadOnlyDictionary<string, string?> CreateOperationContext(
        string operation,
        string operationId)
    {
        return new Dictionary<string, string?>
        {
            ["operation"] = operation,
            ["operationId"] = operationId,
        };
    }

    private static string CreateOperationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static void AddFailure(ICollection<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                AddFailureOnce(failures, innerException);
            }

            return;
        }

        AddFailureOnce(failures, exception);
    }

    private static void AddFailureOnce(ICollection<Exception> failures, Exception exception)
    {
        if (!failures.Any(existing => ReferenceEquals(existing, exception)))
        {
            failures.Add(exception);
        }
    }

    private enum ApplicationHostState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        Faulted,
        Disposed,
    }
}
