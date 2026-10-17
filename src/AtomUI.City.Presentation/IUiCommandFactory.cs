using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Presentation;

#pragma warning disable RS0026 // The UI-safe facade intentionally mirrors the established CommandFactory overload family.

/// <summary>
/// Creates commands for ViewModels bound to the Presentation UI runtime.
/// </summary>
public interface IUiCommandFactory
{
    /// <summary>
    /// Creates a synchronous command with Presentation diagnostics.
    /// </summary>
    IRelayCommand Create(
        Action execute,
        Func<bool>? canExecute = null,
        CommandExecutionState? state = null);

    /// <summary>
    /// Creates a typed synchronous command with Presentation diagnostics.
    /// </summary>
    IRelayCommand<T> Create<T>(
        Action<T?> execute,
        Predicate<T?>? canExecute = null,
        CommandExecutionState? state = null);

    /// <summary>
    /// Creates an asynchronous command bound to an explicit fixed activation scope.
    /// </summary>
    IAsyncRelayCommand CreateAsync(
        Func<CancellationToken, Task> execute,
        CommandExecutionState? state = null,
        IActivationScope? activationScope = null);

    /// <summary>
    /// Creates an asynchronous command that resolves the owner's current activation scope per execution.
    /// </summary>
    IAsyncRelayCommand CreateAsync(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        ICommandExceptionHandler? exceptionHandler = null);

    /// <summary>
    /// Creates a typed asynchronous command that resolves the owner's current activation scope per execution.
    /// </summary>
    IAsyncRelayCommand<T> CreateAsync<T>(
        Func<T?, CancellationToken, Task> execute,
        Predicate<T?>? canExecute = null,
        CommandExecutionState? state = null,
        IActivationScopeOwner? activationOwner = null,
        CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
        Func<T?, object?>? concurrencyKeySelector = null,
        ICommandExceptionHandler? exceptionHandler = null);
}

#pragma warning restore RS0026
