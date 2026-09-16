namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for istate scope accessor.
/// </summary>
public interface IStateScopeAccessor
{
    /// <summary>
    /// Gets current.
    /// </summary>
    IStateScope? Current { get; }

    /// <summary>
    /// Executes the push operation.
    /// </summary>
    IDisposable Push(IStateScope scope);
}
