using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Security;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Security.Tests;

public sealed class AccountSwitchIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DependencyInjectionUsesConfiguredRootAndOneSharedManager()
    {
        using var workspace = new SecurityTestWorkspace();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddSecurity(new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"));
        using var provider = services.BuildServiceProvider();
        var accountStore = provider.GetRequiredService<IAccountSessionStore>();
        var credentialStore = provider.GetRequiredService<ICredentialStore>();
        var manager = provider.GetRequiredService<IAccountSessionManager>();
        var key = workspace.CreateKey("di-user");
        await accountStore.SaveAsync(workspace.CreateAccount(key, Now, "DI User"));
        await credentialStore.SaveAsync(workspace.CreateCredential(key, Now, "di-token"));

        var switched = await manager.SwitchAccountAsync(key);
        var token = await provider.GetRequiredService<IAccessTokenProvider>()
            .GetTokenAsync(new AccessTokenRequest("desktop-api"));

        Assert.True(switched.Succeeded);
        Assert.Equal("di-token", token.Token);
        Assert.Same(manager, provider.GetRequiredService<IAccessTokenProvider>());
        Assert.Equal(workspace.RootPath, Assert.IsType<FileAccountSessionStore>(accountStore).RootPath);
        Assert.Equal(workspace.RootPath, Assert.IsType<FileCredentialStore>(credentialStore).RootPath);
    }

    [Fact]
    public async Task RepeatedSwitchIsIdempotentAndListAccountsReturnsAllSavedProfiles()
    {
        using var workspace = new SecurityTestWorkspace();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddSecurity(new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"));
        using var provider = services.BuildServiceProvider();
        var accountStore = provider.GetRequiredService<IAccountSessionStore>();
        var credentialStore = provider.GetRequiredService<ICredentialStore>();
        var manager = provider.GetRequiredService<IAccountSessionManager>();
        var first = workspace.CreateKey("first-user");
        var second = workspace.CreateKey("second-user");
        foreach (var (key, name, token) in new[]
                 {
                     (first, "First User", "first-token"),
                     (second, "Second User", "second-token"),
                 })
        {
            await accountStore.SaveAsync(workspace.CreateAccount(key, Now, name));
            await credentialStore.SaveAsync(workspace.CreateCredential(key, Now, token));
        }

        var notifications = 0;
        manager.SessionChanged += (_, _) => notifications++;
        var initial = await manager.SwitchAccountAsync(first);
        var repeated = await manager.SwitchAccountAsync(first);
        var accounts = await manager.ListAccountsAsync();

        Assert.True(initial.Succeeded);
        Assert.True(repeated.Succeeded);
        Assert.Equal(initial.Session.Revision, repeated.Session.Revision);
        Assert.Equal(1, notifications);
        Assert.Equal(2, accounts.Value!.Count);
    }

    [Fact]
    public async Task MissingCredentialCanOnlyBeActivatedInExplicitOfflineMode()
    {
        using var workspace = new SecurityTestWorkspace();
        var accountStore = new FileAccountSessionStore(workspace.RootPath);
        var credentialStore = new FileCredentialStore(workspace.RootPath);
        var authentication = new AuthenticationStateStore();
        var manager = new AccountSessionManager(
            accountStore,
            credentialStore,
            authentication,
            new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"),
            new FixedTimeProvider(Now));
        var key = workspace.CreateKey("offline-without-token");
        await accountStore.SaveAsync(workspace.CreateAccount(key, Now, "Offline Without Token"));

        var denied = await manager.SwitchAccountAsync(key);
        var accepted = await manager.SwitchAccountAsync(key, new AccountSwitchOptions(allowOffline: true));

        Assert.Equal(AccountSwitchResultStatus.CredentialUnavailable, denied.Status);
        Assert.True(accepted.Succeeded);
        Assert.Equal(AccountSessionMode.OfflineRestricted, accepted.Session.Mode);
        Assert.False(accepted.Session.Credential!.HasCredential);
        Assert.Equal(AuthenticationState.Expired, authentication.Current.State);
    }

    [Fact]
    public async Task SessionObserverFailureIsIsolatedAndDiagnosed()
    {
        using var workspace = new SecurityTestWorkspace();
        var diagnostics = new InMemoryHostDiagnostics();
        var accountStore = new FileAccountSessionStore(workspace.RootPath, diagnostics);
        var credentialStore = new FileCredentialStore(workspace.RootPath, diagnostics);
        var manager = new AccountSessionManager(
            accountStore,
            credentialStore,
            new AuthenticationStateStore(diagnostics),
            new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"),
            new FixedTimeProvider(Now),
            diagnostics);
        var key = workspace.CreateKey("observer-user");
        await accountStore.SaveAsync(workspace.CreateAccount(key, Now, "Observer User"));
        await credentialStore.SaveAsync(workspace.CreateCredential(key, Now, "observer-token"));
        var secondObserverCalled = false;
        manager.SessionChanged += (_, _) => throw new InvalidOperationException("observer failed");
        manager.SessionChanged += (_, _) => secondObserverCalled = true;

        var result = await manager.SwitchAccountAsync(key);

        Assert.True(result.Succeeded);
        Assert.True(secondObserverCalled);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.AccountSessionObserverFailed);
    }

    [Fact]
    public async Task FailedSwitchDiagnosticIdentifiesTargetAndFailureStageWithoutCredentials()
    {
        using var workspace = new SecurityTestWorkspace();
        var diagnostics = new InMemoryHostDiagnostics();
        var accountStore = new FileAccountSessionStore(workspace.RootPath, diagnostics);
        var credentialStore = new FileCredentialStore(workspace.RootPath, diagnostics);
        var manager = new AccountSessionManager(
            accountStore,
            credentialStore,
            new AuthenticationStateStore(diagnostics),
            new SecurityPersistenceOptions(workspace.RootPath, "desktop-api"),
            new FixedTimeProvider(Now),
            diagnostics);
        var key = workspace.CreateKey("diagnostic-user");
        await accountStore.SaveAsync(workspace.CreateAccount(key, Now, "Diagnostic User"));

        var result = await manager.SwitchAccountAsync(key);
        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == SecurityDiagnosticIds.AccountSessionOperationFailed);

        Assert.Equal(AccountSwitchResultStatus.CredentialUnavailable, result.Status);
        Assert.Equal("switch", record.Context["operation"]);
        Assert.Equal("credential", record.Context["failureStage"]);
        Assert.Equal(key.ToStorageKey()[..16], record.Context["targetAccountIdHash"]);
        Assert.Equal("Anonymous", record.Context["mode"]);
        Assert.Null(record.Context["previousAccountIdHash"]);
        Assert.DoesNotContain("diagnostic-user", string.Join('|', record.Context.Values), StringComparison.Ordinal);
    }
}
