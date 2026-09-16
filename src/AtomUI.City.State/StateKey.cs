namespace AtomUI.City.State;

/// <summary>
/// Represents state key&lt;t&gt;.
/// </summary>
public readonly record struct StateKey<T>
{
    /// <summary>
    /// Executes the state key operation.
    /// </summary>
    public StateKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() => Name;

    internal static void ThrowIfDefault(StateKey<T> key, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(key.Name))
        {
            throw new ArgumentException(
                "State key must be created with a non-empty name.",
                paramName ?? nameof(key));
        }
    }
}
