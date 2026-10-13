namespace AtomUI.City.Mvvm;

/// <summary>
/// Defines the contract for iactivation scope.
/// </summary>
public interface IActivationScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets id.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets a value indicating whether cancellation token.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets a value indicating whether the scope is closing and no longer accepts work.
    /// </summary>
    bool IsClosing { get; }

    /// <summary>
    /// Gets a value indicating whether the scope has completed disposal.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets the number of active tasks owned by the scope.
    /// </summary>
    int ActiveTaskCount { get; }

    /// <summary>
    /// Starts and supervises an activation-scoped task.
    /// </summary>
    Task StartAsync(
        string taskName,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts and supervises a task that is unique by key within this scope.
    /// </summary>
    Task StartUniqueAsync(
        string taskKey,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the add operation.
    /// </summary>
    void Add(IDisposable disposable);

    /// <summary>
    /// Executes the add async operation.
    /// </summary>
    void AddAsync(IAsyncDisposable disposable);
}
