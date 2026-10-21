using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Presentation;

internal sealed class UiCommandFactory : IUiCommandFactory
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IHostDiagnostics? _diagnostics;

    public UiCommandFactory(
        IUiDispatcher dispatcher,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
        _diagnostics = diagnostics;
    }

    public IRelayCommand Create(
        Action execute,
        Func<bool>? canExecute = null,
        CommandExecutionState? state = null) =>
        CommandFactory.Create(execute, canExecute, state, _diagnostics);

    public IRelayCommand<T> Create<T>(
        Action<T?> execute,
        Predicate<T?>? canExecute = null,
        CommandExecutionState? state = null) =>
        CommandFactory.Create(execute, canExecute, state, _diagnostics);

    public IAsyncRelayCommand CreateAsync(
        Func<CancellationToken, Task> execute,
        CommandExecutionState? state = null,
        IActivationScope? activationScope = null) =>
        CommandFactory.CreateAsync(
            execute,
            state,
            activationScope,
            _diagnostics,
            _dispatcher);

    public IAsyncRelayCommand CreateAsync(
        CommandExecutionDomain executionDomain,
        Func<CommandExecutionLease, CancellationToken, Task> execute,
        Func<bool>? canExecute = null,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        ICommandExceptionHandler? exceptionHandler = null) =>
        CommandFactory.CreateAsync(
            executionDomain,
            execute,
            canExecute,
            state,
            activationOwner,
            concurrencyPolicy,
            exceptionHandler,
            _diagnostics,
            _dispatcher);

    public IAsyncRelayCommand CreateAsync(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        ICommandExceptionHandler? exceptionHandler = null) =>
        CommandFactory.CreateAsync(
            execute,
            canExecute,
            state,
            activationOwner,
            concurrencyPolicy,
            exceptionHandler,
            _diagnostics,
            _dispatcher);

    public IAsyncRelayCommand<T> CreateAsync<T>(
        Func<T?, CancellationToken, Task> execute,
        Predicate<T?>? canExecute = null,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        Func<T?, object?>? concurrencyKeySelector = null,
        ICommandExceptionHandler? exceptionHandler = null) =>
        CommandFactory.CreateAsync(
            execute,
            canExecute,
            state,
            activationOwner,
            concurrencyPolicy,
            concurrencyKeySelector,
            exceptionHandler,
            _diagnostics,
            _dispatcher);

    public IAsyncRelayCommand<T> CreateAsync<T>(
        CommandExecutionDomain executionDomain,
        Func<T?, CommandExecutionLease, CancellationToken, Task> execute,
        Predicate<T?>? canExecute = null,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        Func<T?, object?>? concurrencyKeySelector = null,
        ICommandExceptionHandler? exceptionHandler = null) =>
        CommandFactory.CreateAsync(
            executionDomain,
            execute,
            canExecute,
            state,
            activationOwner,
            concurrencyPolicy,
            concurrencyKeySelector,
            exceptionHandler,
            _diagnostics,
            _dispatcher);
}
