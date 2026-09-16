namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for istate scope.
/// </summary>
public interface IStateScope : IDisposable
{
    /// <summary>
    /// Gets id.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets state.
    /// </summary>
    StateScopeState State { get; }

    /// <summary>
    /// Executes the add operation.
    /// </summary>
    void Add(IDisposable subscription);
}
