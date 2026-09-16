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
    /// Executes the add operation.
    /// </summary>
    void Add(IDisposable disposable);

    /// <summary>
    /// Executes the add async operation.
    /// </summary>
    void AddAsync(IAsyncDisposable disposable);
}
