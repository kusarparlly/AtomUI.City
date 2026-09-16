namespace AtomUI.City.State;

/// <summary>
/// Defines the contract for iapplication state writer.
/// </summary>
public interface IApplicationStateWriter
{
    /// <summary>
    /// Executes the get writable&lt;t&gt; operation.
    /// </summary>
    IWritableState<T> GetWritable<T>(StateKey<T> key);

    /// <summary>
    /// Gets or sets set&lt;t&gt;.
    /// </summary>
    bool Set<T>(StateKey<T> key, T value);

    /// <summary>
    /// Executes the update&lt;t&gt; operation.
    /// </summary>
    bool Update<T>(StateKey<T> key, Func<T, T> updater);
}
