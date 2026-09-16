using System.Collections.ObjectModel;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents activation context.
/// </summary>
public sealed class ActivationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivationContext"/> type.
    /// </summary>
    public ActivationContext(
        IActivationScope scope,
        string? source = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(scope);

        Scope = scope;
        Source = source;
        Properties = new ReadOnlyDictionary<string, object?>(
            properties is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(properties, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets scope.
    /// </summary>
    public IActivationScope Scope { get; }

    /// <summary>
    /// Gets source.
    /// </summary>
    public string? Source { get; }

    /// <summary>
    /// Gets properties.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }
}
