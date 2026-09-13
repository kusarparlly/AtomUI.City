using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class AccountPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AccountKeyNormalizesProtocolFieldsWithoutMergingOpaqueAuthorities()
    {
        var uriKey = new SecurityAccountKey("OIDC", "HTTPS://Identity.Example.Test/", "Tenant", "Subject");
        var opaqueUpper = new SecurityAccountKey("CUSTOM", "Identity-East", null, "Subject");
        var opaqueLower = new SecurityAccountKey("custom", "identity-east", null, "Subject");

        Assert.Equal("oidc", uriKey.AuthenticationScheme);
        Assert.Equal("https://identity.example.test", uriKey.Authority);
        Assert.Equal("Identity-East", opaqueUpper.Authority);
        Assert.NotEqual(opaqueUpper, opaqueLower);
        Assert.NotEqual(opaqueUpper.ToStorageKey(), opaqueLower.ToStorageKey());
    }

    [Fact]
    public async Task FileStoresRoundTripMultipleAccountsAndResourcesWithoutCrossContamination()
    {
        using var workspace = new SecurityTestWorkspace();
        var accountStore = new FileAccountSessionStore(workspace.RootPath);
        var credentialStore = new FileCredentialStore(workspace.RootPath);
        var accountA = workspace.CreateKey("alice");
        var accountB = workspace.CreateKey("bob", "tenant-b");

        Assert.True((await accountStore.SaveAsync(
            workspace.CreateAccount(accountA, Now, "Alice", ["orders.read", "orders.write"]))).Succeeded);
        Assert.True((await accountStore.SaveAsync(
            workspace.CreateAccount(accountB, Now, "Bob", ["reports.read"]))).Succeeded);
        Assert.True((await credentialStore.SaveAsync(
            workspace.CreateCredential(accountA, Now, "alice-api", "desktop-api"))).Succeeded);
        Assert.True((await credentialStore.SaveAsync(
            workspace.CreateCredential(accountA, Now, "alice-files", "file-api"))).Succeeded);
        Assert.True((await credentialStore.SaveAsync(
            workspace.CreateCredential(accountB, Now, "bob-api", "desktop-api"))).Succeeded);

        var accounts = await accountStore.ListAsync();
        var aliceApi = await credentialStore.GetAsync(accountA, "desktop-api");
        var aliceFiles = await credentialStore.GetAsync(accountA, "file-api");
        var bobApi = await credentialStore.GetAsync(accountB, "desktop-api");

        Assert.True(accounts.Succeeded);
        Assert.Equal(2, accounts.Value!.Count);
        Assert.Equal("alice-api", aliceApi.Value!.AccessToken);
        Assert.Equal("alice-files", aliceFiles.Value!.AccessToken);
        Assert.Equal("bob-api", bobApi.Value!.AccessToken);
        Assert.NotEqual(accountA.ToStorageKey(), accountB.ToStorageKey());
    }

    [Fact]
    public async Task LastActiveAccountSurvivesStoreRecreation()
    {
        using var workspace = new SecurityTestWorkspace();
        var key = workspace.CreateKey("restart-user");
        var firstStore = new FileAccountSessionStore(workspace.RootPath);
        await firstStore.SaveAsync(workspace.CreateAccount(key, Now, "Restart User"));
        await firstStore.SetLastActiveAccountAsync(key);

        var recreatedStore = new FileAccountSessionStore(workspace.RootPath);
        var result = await recreatedStore.GetLastActiveAccountAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(key, result.Value);
    }

    [Fact]
    public async Task RemovingActiveAccountClearsPersistentPointerAndIsIdempotent()
    {
        using var workspace = new SecurityTestWorkspace();
        var key = workspace.CreateKey("removed-user");
        var store = new FileAccountSessionStore(workspace.RootPath);
        await store.SaveAsync(workspace.CreateAccount(key, Now, "Removed User"));
        await store.SetLastActiveAccountAsync(key);

        var first = await store.RemoveAsync(key);
        var second = await store.RemoveAsync(key);
        var active = await store.GetLastActiveAccountAsync();

        Assert.True(first.Succeeded);
        Assert.True(first.Value);
        Assert.True(second.Succeeded);
        Assert.False(second.Value);
        Assert.True(active.Succeeded);
        Assert.Null(active.Value);
    }

    [Fact]
    public async Task CorruptedAndFutureSchemaDocumentsReturnStableFailures()
    {
        using var workspace = new SecurityTestWorkspace();
        var key = workspace.CreateKey("schema-user");
        var accountStore = new FileAccountSessionStore(workspace.RootPath);
        var accountPath = Path.Combine(workspace.RootPath, "accounts", key.ToStorageKey(), "account.json");
        Directory.CreateDirectory(Path.GetDirectoryName(accountPath)!);
        await File.WriteAllTextAsync(accountPath, "{ not-json");

        var corrupt = await accountStore.GetAsync(key);
        await File.WriteAllTextAsync(accountPath, "{\"schemaVersion\":999}");
        var future = await accountStore.GetAsync(key);

        Assert.Equal(SecurityStoreResultStatus.InvalidData, corrupt.Status);
        Assert.Equal(SecurityStoreResultStatus.UnsupportedSchema, future.Status);
    }

    [Fact]
    public async Task PreCancelledWritesDoNotCreateStorageOrTemporaryFiles()
    {
        using var workspace = new SecurityTestWorkspace();
        var key = workspace.CreateKey("cancelled-user");
        var store = new FileCredentialStore(workspace.RootPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await store.SaveAsync(
            workspace.CreateCredential(key, Now, "must-not-be-written"),
            cancellation.Token);

        Assert.Equal(SecurityStoreResultStatus.Cancelled, result.Status);
        Assert.False(Directory.Exists(workspace.RootPath));
    }

    [Fact]
    public async Task CredentialsAreSeparatedFromAccountDocumentsAndDiagnostics()
    {
        using var workspace = new SecurityTestWorkspace();
        var diagnostics = new InMemoryHostDiagnostics();
        var key = workspace.CreateKey("redaction-user");
        var accountStore = new FileAccountSessionStore(workspace.RootPath, diagnostics);
        var credentialStore = new FileCredentialStore(workspace.RootPath, diagnostics);
        const string accessToken = "secret-access-token-9281";
        const string refreshToken = "refresh-secret-access-token-9281";
        await accountStore.SaveAsync(workspace.CreateAccount(key, Now, "Redaction User"));
        await credentialStore.SaveAsync(new AccountCredentialSnapshot(
            key,
            "desktop-api",
            accessToken,
            "Bearer",
            refreshToken,
            Now.AddMinutes(30)));

        var accountText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(workspace.RootPath, "accounts"), "*.json", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var credentialText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(workspace.RootPath, "credentials"), "*.json", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var diagnosticText = string.Join(
            '|',
            diagnostics.Records.SelectMany(static record => record.Context.Values).Where(static value => value is not null));

        Assert.DoesNotContain(accessToken, accountText, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshToken, accountText, StringComparison.Ordinal);
        Assert.Contains(accessToken, credentialText, StringComparison.Ordinal);
        Assert.Contains(refreshToken, credentialText, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, diagnosticText, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshToken, diagnosticText, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, workspace.CreateCredential(key, Now, accessToken).ToString(), StringComparison.Ordinal);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.AccountPersistenceCompleted &&
                record.Context["operation"] == "save" &&
                record.Context["storeKind"] == "account");
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.CredentialPersistenceCompleted &&
                record.Context["operation"] == "save" &&
                record.Context["storeKind"] == "credential");
    }
}
