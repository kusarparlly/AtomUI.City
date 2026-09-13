using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class FileCredentialStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ResourceNamesCannotEscapeCredentialRoot()
    {
        using var workspace = new SecurityTestWorkspace();
        var store = new FileCredentialStore(workspace.RootPath);
        var key = workspace.CreateKey("path-user");
        var credential = workspace.CreateCredential(
            key,
            Now,
            "path-token",
            "../../outside/credential");

        var result = await store.SaveAsync(credential);

        Assert.True(result.Succeeded);
        Assert.Single(Directory.EnumerateFiles(
            Path.Combine(workspace.RootPath, "credentials", key.ToStorageKey()),
            "*.json"));
        Assert.False(Directory.Exists(Path.Combine(workspace.RootPath, "outside")));
    }

    [Fact]
    public async Task CorruptAndFutureCredentialSchemasReturnStableFailures()
    {
        using var workspace = new SecurityTestWorkspace();
        var store = new FileCredentialStore(workspace.RootPath);
        var key = workspace.CreateKey("schema-user");
        await store.SaveAsync(workspace.CreateCredential(key, Now, "schema-token"));
        var path = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(workspace.RootPath, "credentials", key.ToStorageKey()),
            "*.json"));

        await File.WriteAllTextAsync(path, "{ invalid-json");
        var corrupt = await store.GetAsync(key, "desktop-api");
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":999}");
        var future = await store.GetAsync(key, "desktop-api");

        Assert.Equal(SecurityStoreResultStatus.InvalidData, corrupt.Status);
        Assert.Equal(SecurityStoreResultStatus.UnsupportedSchema, future.Status);
    }

    [Fact]
    public async Task ConcurrentAtomicReplacementNeverLeavesPartialJsonOrTemporaryFiles()
    {
        using var workspace = new SecurityTestWorkspace();
        var store = new FileCredentialStore(workspace.RootPath);
        var key = workspace.CreateKey("atomic-user");
        var tokens = Enumerable.Range(0, 80).Select(index => $"token-{index:D3}").ToArray();

        await Task.WhenAll(tokens.Select(token =>
            store.SaveAsync(workspace.CreateCredential(key, Now, token)).AsTask()));
        var result = await store.GetAsync(key, "desktop-api");

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!.AccessToken, tokens);
        Assert.Empty(Directory.EnumerateFiles(workspace.RootPath, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RemoveAllOnlyRemovesRequestedAccountAndCanBeRetried()
    {
        using var workspace = new SecurityTestWorkspace();
        var store = new FileCredentialStore(workspace.RootPath);
        var first = workspace.CreateKey("first-user");
        var second = workspace.CreateKey("second-user");
        await store.SaveAsync(workspace.CreateCredential(first, Now, "first-default"));
        await store.SaveAsync(workspace.CreateCredential(first, Now, "first-files", "file-api"));
        await store.SaveAsync(workspace.CreateCredential(second, Now, "second-default"));

        var removed = await store.RemoveAllAsync(first);
        var retried = await store.RemoveAllAsync(first);
        var secondCredential = await store.GetAsync(second, "desktop-api");

        Assert.True(removed.Succeeded);
        Assert.True(removed.Value);
        Assert.True(retried.Succeeded);
        Assert.False(retried.Value);
        Assert.Equal("second-default", secondCredential.Value!.AccessToken);
    }

    [Fact]
    public async Task UnusableRootReturnsIoFailureWithoutThrowing()
    {
        using var workspace = new SecurityTestWorkspace();
        Directory.CreateDirectory(Path.GetDirectoryName(workspace.RootPath)!);
        await File.WriteAllTextAsync(workspace.RootPath, "root-is-a-file");
        var store = new FileCredentialStore(workspace.RootPath);

        var result = await store.SaveAsync(
            workspace.CreateCredential(workspace.CreateKey("io-user"), Now, "io-token"));

        Assert.Equal(SecurityStoreResultStatus.IoFailed, result.Status);
        Assert.NotNull(result.Exception);
    }
}
