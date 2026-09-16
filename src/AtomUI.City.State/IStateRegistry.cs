namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for istate registry.
/// </summary>
public interface IStateRegistry
{
    /// <summary>
    /// Executes the add&lt;t&gt; operation.
    /// </summary>
    void Add<T>(StateDefinition<T> definition);
}
