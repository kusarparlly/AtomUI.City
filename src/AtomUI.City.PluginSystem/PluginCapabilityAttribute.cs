namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin capability.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PluginCapabilityAttribute : Attribute
{
    private string[] _scope = [];

    /// <summary>
    /// Initializes a new instance of the <c>PluginCapabilityAttribute</c> type.
    /// </summary>
    public PluginCapabilityAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Represents the scope value.
    /// </summary>
    public string[] Scope
    {
        get => _scope.ToArray();
        set => _scope = value?.ToArray() ?? [];
    }
}
