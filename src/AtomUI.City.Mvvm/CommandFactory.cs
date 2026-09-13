using System.ComponentModel;
using AtomUI.City.Core.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Mvvm;

public static class CommandFactory
{
    public static IRelayCommand Create(
        Action execute,
        Func<bool>? canExecute = null,
        CommandExecutionState? state = null,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        var executionState = state ?? new CommandExecutionState();

        return canExecute is null
            ? new RelayCommand(() => Execute(execute, executionState, diagnostics))
            : new RelayCommand(() => Execute(execute, executionState, diagnostics), canExecute);
    }

    public static IAsyncRelayCommand CreateAsync(
        Func<CancellationToken, Task> execute,
        CommandExecutionState? state = null,
        IActivationScope? activationScope = null,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        var executionState = state ?? new CommandExecutionState();

        return new TrackedAsyncRelayCommand(
            cancellationToken => ExecuteAsync(
                execute,
                executionState,
                activationScope,
                diagnostics,
                cancellationToken),
            executionState);
    }

    private static void Execute(
        Action execute,
        CommandExecutionState state,
        IHostDiagnostics? diagnostics)
    {
        using var operation = OperationScope.Start(CancellationToken.None);

        if (!state.TryBegin(operation.CancellationToken))
        {
            var rejected = operation.Reject();
            state.Reject(rejected);
            WriteCommandDiagnostic(diagnostics, MvvmDiagnosticIds.CommandRejected, state, operation, null);
            return;
        }

        try
        {
            execute();
            state.Complete(operation.Complete());
        }
        catch (Exception exception)
        {
            var failed = operation.Fail(exception);
            state.Complete(failed);
            WriteCommandDiagnostic(diagnostics, MvvmDiagnosticIds.CommandFailed, state, operation, exception);
        }
    }

    private static async Task ExecuteAsync(
        Func<CancellationToken, Task> execute,
        CommandExecutionState state,
        IActivationScope? activationScope,
        IHostDiagnostics? diagnostics,
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource = activationScope is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activationScope.CancellationToken);
        using var operation = OperationScope.Start(linkedCancellationTokenSource.Token);

        if (!state.TryBegin(operation.CancellationToken))
        {
            var rejected = operation.Reject();
            state.Reject(rejected);
            WriteCommandDiagnostic(diagnostics, MvvmDiagnosticIds.CommandRejected, state, operation, null);
            return;
        }

        try
        {
            await execute(operation.CancellationToken).ConfigureAwait(false);
            state.Complete(operation.Complete());
        }
        catch (OperationCanceledException)
            when (operation.CancellationToken.IsCancellationRequested)
        {
            state.Complete(operation.Cancel());
        }
        catch (Exception exception)
        {
            var failed = operation.Fail(exception);
            state.Complete(failed);
            WriteCommandDiagnostic(diagnostics, MvvmDiagnosticIds.CommandFailed, state, operation, exception);
        }
    }

    private static void WriteCommandDiagnostic(
        IHostDiagnostics? diagnostics,
        string code,
        CommandExecutionState state,
        OperationScope operation,
        Exception? exception)
    {
        diagnostics?.Write(new HostDiagnosticRecord(
            code,
            $"Command '{state.CommandName ?? "<unnamed>"}' on '{state.OwnerType?.FullName ?? "<unknown>"}' finished as {operation.Status}: {exception?.Message ?? "no exception"}.",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["commandName"] = state.CommandName,
                ["ownerType"] = state.OwnerType?.FullName,
                ["operationId"] = operation.Id.ToString(),
            }
        });
    }

    private sealed class TrackedAsyncRelayCommand : IAsyncRelayCommand, INotifyPropertyChanged
    {
        private readonly Func<CancellationToken, Task> _execute;
        private readonly CommandExecutionState _state;
        private CancellationTokenSource? _runningCancellation;

        public TrackedAsyncRelayCommand(
            Func<CancellationToken, Task> execute,
            CommandExecutionState state)
        {
            _execute = execute;
            _state = state;
        }

        public event EventHandler? CanExecuteChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Task? ExecutionTask { get; private set; }

        public bool CanBeCanceled => _runningCancellation is { IsCancellationRequested: false };

        public bool IsCancellationRequested => _runningCancellation?.IsCancellationRequested ?? false;

        public bool IsRunning => _state.IsExecuting;

        public bool CanExecute(object? parameter)
        {
            return !_state.IsExecuting;
        }

        public void Execute(object? parameter)
        {
            _ = ExecuteAsync(parameter);
        }

        public Task ExecuteAsync(object? parameter)
        {
            if (_state.IsExecuting)
            {
                ExecutionTask = _execute(CancellationToken.None);
                OnPropertyChanged(nameof(ExecutionTask));
                return ExecutionTask;
            }

            var cancellation = new CancellationTokenSource();
            _runningCancellation = cancellation;

            ExecutionTask = ExecuteCoreAsync(cancellation);
            NotifyStateChanged();

            return ExecutionTask;
        }

        public void Cancel()
        {
            _runningCancellation?.Cancel();
            NotifyStateChanged();
        }

        public void NotifyCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task ExecuteCoreAsync(CancellationTokenSource cancellation)
        {
            try
            {
                // Command state notifications belong to the caller context (normally the UI thread).
                await _execute(cancellation.Token);
            }
            finally
            {
                if (ReferenceEquals(_runningCancellation, cancellation))
                {
                    _runningCancellation.Dispose();
                    _runningCancellation = null;
                }

                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged()
        {
            NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ExecutionTask));
            OnPropertyChanged(nameof(CanBeCanceled));
            OnPropertyChanged(nameof(IsCancellationRequested));
            OnPropertyChanged(nameof(IsRunning));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
