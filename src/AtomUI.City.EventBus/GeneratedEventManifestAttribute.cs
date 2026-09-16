namespace AtomUI.City.EventBus;

/// <summary>
/// Represents generated event manifest.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GeneratedEventManifestAttribute : Attribute
{
    /// <summary>
    /// Represents the current version value.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedEventManifestAttribute"/> type.
    /// </summary>
    public GeneratedEventManifestAttribute(Type registrarType, int version)
    {
        RegistrarType = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
        Version = version == CurrentVersion
            ? version
            : throw new ArgumentOutOfRangeException(nameof(version), version, $"Generated event manifest version must be {CurrentVersion}.");
    }

    /// <summary>
    /// Gets registrar type.
    /// </summary>
    public Type RegistrarType { get; }

    /// <summary>
    /// Gets version.
    /// </summary>
    public int Version { get; }
}
