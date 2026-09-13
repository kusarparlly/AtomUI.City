namespace AtomUI.City.Security;

public sealed class SecurityPersistenceOptions
{
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

    public string? RootPath { get; }

    public string DefaultCredentialResource { get; }
}
