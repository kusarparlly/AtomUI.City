namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for istate factory.
/// </summary>
public interface IStateFactory
{
    /// <summary>
    /// Executes the create writable&lt;t&gt; operation.
    /// </summary>
    WritableState<T> CreateWritable<T>(
        T initialValue,
        IEqualityComparer<T>? comparer = null,
        string? stateName = null,
        StateAccessPolicy access = StateAccessPolicy.HostWrite);

    /// <summary>
    /// Executes the create computed&lt;t&gt; operation.
    /// </summary>
    ComputedState<T> CreateComputed<T>(
        Func<T> compute,
        params IReadOnlyState[] dependencies);

    /// <summary>
    /// Executes the create scope operation.
    /// </summary>
    StateScope CreateScope(string id);
}
