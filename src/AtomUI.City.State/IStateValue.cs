namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for istate value&lt;t&gt;.
/// </summary>
public interface IStateValue<out T>
{
    /// <summary>
    /// Gets value.
    /// </summary>
    T Value { get; }
}
