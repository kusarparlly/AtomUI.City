using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

internal sealed class SecurityTestWorkspace : IDisposable
{
    public SecurityTestWorkspace()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "AtomUI.City.Security.Tests",
            Guid.NewGuid().ToString("N"));
    }

    public string RootPath { get; }

    public SecurityAccountKey CreateKey(string subject, string? tenant = "tenant-a") =>
        new("oidc", "https://identity.example.test", tenant, subject);

    public AccountRecordSnapshot CreateAccount(
        SecurityAccountKey key,
        DateTimeOffset now,
        string displayName,
        IReadOnlyCollection<string>? permissions = null,
        bool expired = false) =>
        new(
            new AccountProfileSnapshot(key, displayName, lastUsedAt: now),
            new PersistedPermissionSnapshot(
                key,
                permissions ?? ["workspace.read"],
                revision: 7,
                issuedAt: now.AddHours(-2),
                expiresAt: expired ? now.AddHours(-1) : now.AddHours(2)));

    public AccountCredentialSnapshot CreateCredential(
        SecurityAccountKey key,
        DateTimeOffset now,
        string token,
        string resource = "desktop-api",
        bool expired = false) =>
        new(
            key,
            resource,
            token,
            "Bearer",
            refreshToken: $"refresh-{token}",
            expiresAt: expired ? now.AddMinutes(-1) : now.AddMinutes(30));

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
        else if (File.Exists(RootPath))
        {
            File.Delete(RootPath);
        }
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
