namespace AtomUI.City.Localization;

/// <summary>
/// Represents localized resource.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class LocalizedResourceAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>LocalizedResourceAttribute</c> type.
    /// </summary>
    public LocalizedResourceAttribute(string key, string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        Key = key;
        PackageId = packageId;
    }

    /// <summary>
    /// Gets key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets package id.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    /// Gets or sets kind.
    /// </summary>
    public LocalizedResourceKind Kind { get; set; } = LocalizedResourceKind.String;

    /// <summary>
    /// Gets or sets scope.
    /// </summary>
    public ResourceScope Scope { get; set; } = ResourceScope.Module;

    /// <summary>
    /// Gets or sets scope id.
    /// </summary>
    public string? ScopeId { get; set; }

    /// <summary>
    /// Gets or sets culture.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets critical.
    /// </summary>
    public bool Critical { get; set; }
}
