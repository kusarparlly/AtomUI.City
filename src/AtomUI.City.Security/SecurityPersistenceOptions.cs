namespace AtomUI.City.Security;

/// <summary>
/// Represents security persistence options.
/// </summary>
public sealed class SecurityPersistenceOptions
{
    /// <summary>
    /// Initializes a new instance of the <c>SecurityPersistenceOptions</c> type.
    /// </summary>
    public SecurityPersistenceOptions(
        string? rootPath = null,
        string defaultCredentialResource = "default")
    {
        if (rootPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCredentialResource);
        RootPath = rootPath is null ? null : Path.GetFullPath(rootPath);
        DefaultCredentialResource = defaultCredentialResource.Trim();
    }

    /// <summary>
    /// Gets root path.
    /// </summary>
    public string? RootPath { get; }

    /// <summary>
    /// Gets default credential resource.
    /// </summary>
    public string DefaultCredentialResource { get; }
}
