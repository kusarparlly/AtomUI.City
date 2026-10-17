using System.Collections.Concurrent;
using System.ComponentModel;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Creates CommunityToolkit commands with City execution, lifecycle, and diagnostics semantics.
/// </summary>
public static class CommandFactory
{
    private const string CommandCanceledExceptionDataKey = "AtomUI.City.Mvvm.CommandCanceled";
    private const string CommandOperationIdExceptionDataKey = "AtomUI.City.Mvvm.CommandOperationId";

    /// <summary>
    /// Creates a synchronous command.
    /// </summary>
    public static IRelayCommand Create(
        Action execute,
        Func<bool>? canExecute = null,
        CommandExecutionState? state = null,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        var executionState = state ?? new CommandExecutionState();
        return new RelayCommand(
            () => ExecuteSync(execute, executionState, diagnostics),
            () => !executionState.IsExecuting && (canExecute?.Invoke() ?? true));
    }

    /// <summary>
    /// Creates a synchronous typed command.
    /// </summary>
    public static IRelayCommand<T> Create<T>(
        Action<T?> execute,
        Predicate<T?>? canExecute = null,
        CommandExecutionState? state = null,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        var executionState = state ?? new CommandExecutionState();
        return new RelayCommand<T>(
            parameter => ExecuteSync(() => execute(parameter), executionState, diagnostics),
            parameter => !executionState.IsExecuting && (canExecute?.Invoke(parameter) ?? true));
    }

    /// <summary>
    /// Creates an asynchronous command bound to an explicit fixed activation scope.
    /// This overload is intended for one-shot or low-level use; ViewModels should use the owner overload.
    /// </summary>
    public static IAsyncRelayCommand CreateAsync(
        Func<CancellationToken, Task> execute,
        CommandExecutionState? state = null,
        IActivationScope? activationScope = null,
        IHostDiagnostics? diagnostics = null,
        IUiDispatcher? notificationDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        var runtime = new AsyncCommandRuntime<object?>(
            (_, cancellationToken) => execute(cancellationToken),
            canExecute: null,
            state ?? new CommandExecutionState(),
            activationOwner: null,
            fixedActivationScope: activationScope,
            CommandConcurrencyPolicy.Reject,
            concurrencyKeySelector: null,
            exceptionHandler: null,
            diagnostics,
            notificationDispatcher);

        return new TrackedAsyncRelayCommand<object?>(runtime);
    }

    /// <summary>
    /// Creates an asynchronous command that resolves the owner's current activation scope per execution.
    /// </summary>
    public static IAsyncRelayCommand CreateAsync(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        ICommandExceptionHandler? exceptionHandler = null,
        IHostDiagnostics? diagnostics = null,
        IUiDispatcher? notificationDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        Func<object?, object?>? concurrencyKeySelector =
            concurrencyPolicy == CommandConcurrencyPolicy.RejectByKey
                ? static _ => "command"
                : null;
        ValidateConcurrency(concurrencyPolicy, concurrencyKeySelector);

        var runtime = new AsyncCommandRuntime<object?>(
            (_, cancellationToken) => execute(cancellationToken),
            canExecute is null ? null : _ => canExecute(),
            state ?? new CommandExecutionState(),
            activationOwner,
            fixedActivationScope: null,
            concurrencyPolicy,
            concurrencyKeySelector,
            exceptionHandler,
            diagnostics,
            notificationDispatcher);

        return new TrackedAsyncRelayCommand<object?>(runtime);
    }

    /// <summary>
    /// Creates an asynchronous typed command that resolves the owner's current activation scope per execution.
    /// </summary>
    public static IAsyncRelayCommand<T> CreateAsync<T>(
        Func<T?, CancellationToken, Task> execute,
        Predicate<T?>? canExecute = null,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        Func<T?, object?>? concurrencyKeySelector = null,
        ICommandExceptionHandler? exceptionHandler = null,
        IHostDiagnostics? diagnostics = null,
        IUiDispatcher? notificationDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ValidateConcurrency(concurrencyPolicy, concurrencyKeySelector);

        var runtime = new AsyncCommandRuntime<T>(
            execute,
            canExecute,
            state ?? new CommandExecutionState(),
            activationOwner,
            fixedActivationScope: null,
            concurrencyPolicy,
            concurrencyKeySelector,
            exceptionHandler,
            diagnostics,
            notificationDispatcher);

        return new TrackedAsyncRelayCommand<T>(runtime);
    }

    private static void ExecuteSync(
        Action execute,
        CommandExecutionState state,
        IHostDiagnostics? diagnostics)
    {
        using var operation = OperationScope.Start(CancellationToken.None);

        if (!state.TryBegin(operation.CancellationToken))
        {
            var rejected = operation.Reject();
            state.Reject(rejected);
            WriteCommandDiagnostic(
                diagnostics,
                MvvmDiagnosticIds.CommandRejected,
                state,
                operation,
                exception: null);
            return;
        }

        try
        {
            execute();
            state.Complete(operation.Complete());
        }
        catch (Exception exception)
        {
            state.Complete(operation.Fail(exception));
            WriteCommandDiagnostic(
                diagnostics,
                MvvmDiagnosticIds.CommandFailed,
                state,
                operation,
                exception);
            throw;
        }
    }

    private static void ValidateConcurrency<T>(
        CommandConcurrencyPolicy concurrencyPolicy,
        Func<T?, object?>? concurrencyKeySelector)
    {
        if (!Enum.IsDefined(concurrencyPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(concurrencyPolicy), concurrencyPolicy, null);
        }

        if (concurrencyPolicy == CommandConcurrencyPolicy.RejectByKey
            && concurrencyKeySelector is null)
        {
            throw new ArgumentNullException(
                nameof(concurrencyKeySelector),
                "RejectByKey requires a concurrency key selector.");
        }
    }

    private static void WriteCommandDiagnostic(
        IHostDiagnostics? diagnostics,
        string code,
        CommandExecutionState state,
        OperationScope operation,
        Exception? exception,
        HostDiagnosticSeverity severity = HostDiagnosticSeverity.Error)
    {
        diagnostics?.Write(new HostDiagnosticRecord(
            code,
            $"Command '{state.CommandName ?? "<unnamed>"}' on '{state.OwnerType?.FullName ?? "<unknown>"}' finished as {operation.Status}: {exception?.Message ?? "no exception"}.",
            severity)
        {
            Context = new Dictionary<string, string?>
            {
                ["commandName"] = state.CommandName,
                ["ownerType"] = state.OwnerType?.FullName,
                ["operationId"] = operation.Id.ToString(),
            }
        });
    }

    private sealed class AsyncCommandRuntime<T>
    {
        private readonly Func<T?, CancellationToken, Task> _execute;
        private readonly Predicate<T?>? _canExecute;
        private readonly IActivationScopeOwner? _activationOwner;
        private readonly IActivationScope? _fixedActivationScope;
        private readonly CommandConcurrencyPolicy _concurrencyPolicy;
        private readonly Func<T?, object?>? _concurrencyKeySelector;
        private readonly ICommandExceptionHandler? _exceptionHandler;
        private readonly IHostDiagnostics? _diagnostics;
        private readonly IUiDispatcher? _notificationDispatcher;
        private readonly ConcurrencyGate _concurrencyGate = new();

        public AsyncCommandRuntime(
            Func<T?, CancellationToken, Task> execute,
            Predicate<T?>? canExecute,
            CommandExecutionState state,
            IActivationScopeOwner? activationOwner,
            IActivationScope? fixedActivationScope,
            CommandConcurrencyPolicy concurrencyPolicy,
            Func<T?, object?>? concurrencyKeySelector,
            ICommandExceptionHandler? exceptionHandler,
            IHostDiagnostics? diagnostics,
            IUiDispatcher? notificationDispatcher)
        {
            _execute = execute;
            _canExecute = canExecute;
            State = state;
            _activationOwner = activationOwner;
            _fixedActivationScope = fixedActivationScope;
            _concurrencyPolicy = concurrencyPolicy;
            _concurrencyKeySelector = concurrencyKeySelector;
            _exceptionHandler = exceptionHandler;
            _diagnostics = diagnostics;
            _notificationDispatcher = notificationDispatcher;
        }

        public CommandExecutionState State { get; }

        public ICommandExceptionHandler? ExceptionHandler => _exceptionHandler;

        public IActivationScopeOwner? ActivationOwner => _activationOwner;

        public IUiDispatcher? NotificationDispatcher => _notificationDispatcher;

        public bool CanExecute(T? parameter)
        {
            if (!TryResolveActivationScope(out _))
            {
                return false;
            }

            if (!(_canExecute?.Invoke(parameter) ?? true))
            {
                return false;
            }

            var key = SelectConcurrencyKey(parameter);
            return _concurrencyGate.CanEnter(_concurrencyPolicy, key);
        }

        public async Task ExecuteAsync(T? parameter, CancellationToken cancellationToken)
        {
            if (!TryResolveActivationScope(out var activationScope))
            {
                RejectInactive();
                return;
            }

            if (!(_canExecute?.Invoke(parameter) ?? true))
            {
                Reject(MvvmDiagnosticIds.CommandRejected, "business-can-execute");
                return;
            }

            var key = SelectConcurrencyKey(parameter);
            if (!_concurrencyGate.TryEnter(_concurrencyPolicy, key, out var admission))
            {
                Reject(MvvmDiagnosticIds.CommandRejected, "concurrency");
                return;
            }

            try
            {
                if (activationScope is null)
                {
                    await ExecuteAdmittedAsync(parameter, cancellationToken).ConfigureAwait(false);
                    return;
                }

                var taskName = State.CommandName ?? $"{State.OwnerType?.Name ?? "Command"}.Execute";
                Task executionTask;
                try
                {
                    executionTask = activationScope.StartAsync(
                        taskName,
                        token => ExecuteAdmittedAsync(parameter, token),
                        cancellationToken);
                }
                catch (InvalidOperationException exception)
                    when (ActivationTaskRejection.IsScopeRejection(exception, activationScope.Id)
                        && (activationScope.IsClosing
                        || activationScope.IsDisposed
                        || activationScope.CancellationToken.IsCancellationRequested))
                {
                    RejectInactive();
                    return;
                }

                await executionTask.ConfigureAwait(false);
            }
            finally
            {
                admission.Dispose();
            }
        }

        public async ValueTask HandleExceptionAsync(Exception exception)
        {
            if (_exceptionHandler is null)
            {
                throw new InvalidOperationException(
                    "ICommand.Execute requires an ICommandExceptionHandler. Use ExecuteAsync and await it, or configure an application handler.",
                    exception);
            }

            var operationId = exception.Data[CommandOperationIdExceptionDataKey] is Guid id
                ? id
                : State.LastResult?.OperationId ?? Guid.Empty;
            try
            {
                await _exceptionHandler.HandleAsync(
                    new CommandExceptionContext(
                        exception,
                        State.CommandName,
                        State.OwnerType,
                        operationId),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception handlerException)
            {
                _diagnostics?.Write(new HostDiagnosticRecord(
                    MvvmDiagnosticIds.CommandExceptionHandlerFailed,
                    $"Command exception handler failed for '{State.CommandName ?? "<unnamed>"}': {handlerException.Message}",
                    HostDiagnosticSeverity.Error)
                {
                    Context = new Dictionary<string, string?>
                    {
                        ["commandName"] = State.CommandName,
                        ["ownerType"] = State.OwnerType?.FullName,
                        ["operationId"] = operationId.ToString(),
                        ["exceptionType"] = handlerException.GetType().FullName,
                    }
                });
            }
        }

        public void WriteNotificationFailure(Exception exception)
        {
            _diagnostics?.Write(new HostDiagnosticRecord(
                MvvmDiagnosticIds.CommandNotificationFailed,
                $"Command notification dispatch failed for '{State.CommandName ?? "<unnamed>"}': {exception.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["commandName"] = State.CommandName,
                    ["ownerType"] = State.OwnerType?.FullName,
                    ["dispatcherType"] = _notificationDispatcher?.GetType().FullName,
                    ["exceptionType"] = exception.GetType().FullName,
                }
            });
        }

        private async Task ExecuteAdmittedAsync(T? parameter, CancellationToken cancellationToken)
        {
            using var operation = OperationScope.Start(cancellationToken);
            State.Begin(operation.CancellationToken);

            try
            {
                operation.CancellationToken.ThrowIfCancellationRequested();
                await _execute(parameter, operation.CancellationToken).ConfigureAwait(false);
                State.Complete(operation.Complete());
            }
            catch (OperationCanceledException exception)
                when (operation.CancellationToken.IsCancellationRequested)
            {
                State.Complete(operation.Cancel());
                exception.Data[CommandCanceledExceptionDataKey] = true;
                throw;
            }
            catch (Exception exception)
            {
                State.Complete(operation.Fail(exception));
                exception.Data[CommandOperationIdExceptionDataKey] = operation.Id;
                WriteCommandDiagnostic(
                    _diagnostics,
                    MvvmDiagnosticIds.CommandFailed,
                    State,
                    operation,
                    exception);
                throw;
            }
        }

        private bool TryResolveActivationScope(out IActivationScope? activationScope)
        {
            if (_activationOwner is not null)
            {
                activationScope = _activationOwner.CurrentActivationScope;
                return _activationOwner.IsActive
                    && _activationOwner.ActivationState == ActivationState.Active
                    && activationScope is { IsClosing: false, IsDisposed: false };
            }

            activationScope = _fixedActivationScope;
            return activationScope is null || (!activationScope.IsClosing && !activationScope.IsDisposed);
        }

        private object? SelectConcurrencyKey(T? parameter)
        {
            if (_concurrencyPolicy != CommandConcurrencyPolicy.RejectByKey)
            {
                return null;
            }

            var key = _concurrencyKeySelector!(parameter);
            if (key is null)
            {
                throw new InvalidOperationException("The command concurrency key selector returned null.");
            }

            return key;
        }

        private void RejectInactive()
        {
            using var operation = OperationScope.Start(CancellationToken.None);
            State.Reject(operation.Reject());
            _diagnostics?.Write(new HostDiagnosticRecord(
                MvvmDiagnosticIds.CommandInactive,
                $"Command '{State.CommandName ?? "<unnamed>"}' was rejected because its owner is not active.",
                HostDiagnosticSeverity.Warning)
            {
                Context = new Dictionary<string, string?>
                {
                    ["commandName"] = State.CommandName,
                    ["ownerType"] = State.OwnerType?.FullName,
                    ["activationState"] = _activationOwner?.ActivationState.ToString(),
                }
            });
        }

        private void Reject(string diagnosticId, string reason)
        {
            using var operation = OperationScope.Start(CancellationToken.None);
            State.Reject(operation.Reject());
            _diagnostics?.Write(new HostDiagnosticRecord(
                diagnosticId,
                $"Command '{State.CommandName ?? "<unnamed>"}' execution was rejected ({reason}).",
                HostDiagnosticSeverity.Warning)
            {
                Context = new Dictionary<string, string?>
                {
                    ["commandName"] = State.CommandName,
                    ["ownerType"] = State.OwnerType?.FullName,
                    ["operationId"] = operation.Id.ToString(),
                    ["reason"] = reason,
                }
            });
        }
    }

    private sealed class TrackedAsyncRelayCommand<T> : IAsyncRelayCommand<T>, INotifyPropertyChanged
    {
        private readonly AsyncCommandRuntime<T> _runtime;
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _executions = new();
        private long _nextExecutionId;

        public TrackedAsyncRelayCommand(AsyncCommandRuntime<T> runtime)
        {
            _runtime = runtime;
            if (_runtime.ActivationOwner is not null)
            {
                _runtime.ActivationOwner.ActivationStateChanged += HandleActivationStateChanged;
            }
        }

        public event EventHandler? CanExecuteChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Task? ExecutionTask { get; private set; }

        public bool CanBeCanceled => _executions.Values.Any(static source => !source.IsCancellationRequested);

        public bool IsCancellationRequested =>
            !_executions.IsEmpty
            && _executions.Values.All(static source => source.IsCancellationRequested);

        public bool IsRunning => _runtime.State.IsExecuting;

        public bool CanExecute(T? parameter) => _runtime.CanExecute(parameter);

        public void Execute(T? parameter)
        {
            ExecutionTask = StartExecution(parameter, fromCommandInterface: true);
            ObserveExecutionTask(ExecutionTask);
        }

        public Task ExecuteAsync(T? parameter)
        {
            ExecutionTask = StartExecution(parameter, fromCommandInterface: false);
            ObserveExecutionTask(ExecutionTask);
            return ExecutionTask;
        }

        bool System.Windows.Input.ICommand.CanExecute(object? parameter) =>
            CanExecute(ConvertParameter(parameter));

        void System.Windows.Input.ICommand.Execute(object? parameter) =>
            Execute(ConvertParameter(parameter));

        Task IAsyncRelayCommand.ExecuteAsync(object? parameter) =>
            ExecuteAsync(ConvertParameter(parameter));

        public void Cancel()
        {
            foreach (var cancellation in _executions.Values)
            {
                cancellation.Cancel();
            }

            NotifyStateChanged();
        }

        public void NotifyCanExecuteChanged()
        {
            DispatchNotification(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }

        private Task StartExecution(T? parameter, bool fromCommandInterface)
        {
            var executionId = Interlocked.Increment(ref _nextExecutionId);
            var cancellation = new CancellationTokenSource();
            if (!_executions.TryAdd(executionId, cancellation))
            {
                cancellation.Dispose();
                throw new InvalidOperationException("Could not register command execution.");
            }

            var task = ExecuteCoreAsync(
                executionId,
                cancellation,
                parameter,
                fromCommandInterface);
            NotifyStateChanged();
            return task;
        }

        private async Task ExecuteCoreAsync(
            long executionId,
            CancellationTokenSource cancellation,
            T? parameter,
            bool fromCommandInterface)
        {
            try
            {
                await _runtime.ExecuteAsync(parameter, cancellation.Token);
            }
            catch (Exception exception)
                when (fromCommandInterface && !IsCooperativeCancellation(exception))
            {
                await _runtime.HandleExceptionAsync(exception);
                throw;
            }
            finally
            {
                if (_executions.TryRemove(executionId, out var removed))
                {
                    removed.Dispose();
                }

                NotifyStateChanged();
            }
        }

        private static bool IsCooperativeCancellation(Exception exception)
        {
            return exception is OperationCanceledException
                && exception.Data.Contains(CommandCanceledExceptionDataKey);
        }

        private static T? ConvertParameter(object? parameter)
        {
            if (parameter is null)
            {
                return default;
            }

            return (T)parameter;
        }

        private static void ObserveExecutionTask(Task task)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private void NotifyStateChanged()
        {
            DispatchNotification(() =>
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged(nameof(ExecutionTask));
                OnPropertyChanged(nameof(CanBeCanceled));
                OnPropertyChanged(nameof(IsCancellationRequested));
                OnPropertyChanged(nameof(IsRunning));
            });
        }

        private void HandleActivationStateChanged(object? sender, EventArgs args)
        {
            NotifyCanExecuteChanged();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void DispatchNotification(Action notification)
        {
            var dispatcher = _runtime.NotificationDispatcher;
            if (dispatcher is null)
            {
                notification();
                return;
            }

            try
            {
                var operation = dispatcher.InvokeAsync(
                    notification,
                    CancellationToken.None);
                if (!operation.IsCompletedSuccessfully)
                {
                    _ = ObserveNotificationAsync(operation);
                }
            }
            catch (Exception exception)
            {
                _runtime.WriteNotificationFailure(exception);
            }
        }

        private async Task ObserveNotificationAsync(ValueTask operation)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _runtime.WriteNotificationFailure(exception);
            }
        }
    }

    private sealed class ConcurrencyGate
    {
        private readonly HashSet<object> _activeKeys = [];
        private readonly object _gate = new();
        private int _activeCount;

        public bool CanEnter(CommandConcurrencyPolicy policy, object? key)
        {
            lock (_gate)
            {
                return policy switch
                {
                    CommandConcurrencyPolicy.Reject => _activeCount == 0,
                    CommandConcurrencyPolicy.Allow => true,
                    CommandConcurrencyPolicy.RejectByKey => key is not null && !_activeKeys.Contains(key),
                    _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null),
                };
            }
        }

        public bool TryEnter(
            CommandConcurrencyPolicy policy,
            object? key,
            out IDisposable admission)
        {
            lock (_gate)
            {
                var accepted = policy switch
                {
                    CommandConcurrencyPolicy.Reject => _activeCount == 0,
                    CommandConcurrencyPolicy.Allow => true,
                    CommandConcurrencyPolicy.RejectByKey => key is not null && _activeKeys.Add(key),
                    _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null),
                };

                if (!accepted)
                {
                    admission = EmptyAdmission.Instance;
                    return false;
                }

                _activeCount++;
                admission = new Admission(this, policy, key);
                return true;
            }
        }

        private void Exit(CommandConcurrencyPolicy policy, object? key)
        {
            lock (_gate)
            {
                if (_activeCount > 0)
                {
                    _activeCount--;
                }

                if (policy == CommandConcurrencyPolicy.RejectByKey && key is not null)
                {
                    _activeKeys.Remove(key);
                }
            }
        }

        private sealed class Admission : IDisposable
        {
            private readonly CommandConcurrencyPolicy _policy;
            private readonly object? _key;
            private ConcurrencyGate? _owner;

            public Admission(ConcurrencyGate owner, CommandConcurrencyPolicy policy, object? key)
            {
                _owner = owner;
                _policy = policy;
                _key = key;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Exit(_policy, _key);
            }
        }

        private sealed class EmptyAdmission : IDisposable
        {
            public static EmptyAdmission Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
