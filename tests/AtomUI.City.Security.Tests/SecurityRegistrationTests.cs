using AtomUI.City.Security;
using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Security.Tests;

public sealed class SecurityRegistrationTests
{
    [Fact]
    public void AddSecurityRegistersCoreServices()
    {
        var services = new ServiceCollection();

        services.AddSecurity();

        using var serviceProvider = services.BuildServiceProvider();
        var stateProvider = serviceProvider.GetRequiredService<IAuthenticationStateProvider>();
        var principalAccessor = serviceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
        var registry = serviceProvider.GetRequiredService<IPermissionRegistry>();
        var policyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var permissionChecker = serviceProvider.GetRequiredService<IPermissionChecker>();
        var evaluator = serviceProvider.GetRequiredService<IAuthorizationEvaluator>();
        var commandDescriptorProvider = serviceProvider.GetRequiredService<ICommandAuthorizationDescriptorProvider>();
        var commandAuthorizationSource = serviceProvider.GetRequiredService<ICommandAuthorizationSource>();
        var tokenProvider = serviceProvider.GetRequiredService<IAccessTokenProvider>();
        var accountSessionManager = serviceProvider.GetRequiredService<IAccountSessionManager>();
        var accountStore = serviceProvider.GetRequiredService<IAccountSessionStore>();
        var credentialStore = serviceProvider.GetRequiredService<ICredentialStore>();

        Assert.Same(stateProvider, principalAccessor);
        Assert.IsType<PermissionRegistry>(registry);
        Assert.IsType<InMemoryAuthorizationPolicyProvider>(policyProvider);
        Assert.IsType<PermissionChecker>(permissionChecker);
        Assert.IsType<AuthorizationEvaluator>(evaluator);
        Assert.IsType<InMemoryCommandAuthorizationDescriptorProvider>(commandDescriptorProvider);
        Assert.IsType<CommandAuthorizationSource>(commandAuthorizationSource);
        Assert.Same(accountSessionManager, tokenProvider);
        Assert.IsType<AccountSessionManager>(accountSessionManager);
        Assert.IsType<FileAccountSessionStore>(accountStore);
        Assert.IsType<FileCredentialStore>(credentialStore);
    }

    [Fact]
    public async Task AddSecurityDefaultAccessTokenProviderReturnsUnavailableResult()
    {
        var services = new ServiceCollection();
        services.AddSecurity();

        using var serviceProvider = services.BuildServiceProvider();
        var tokenProvider = serviceProvider.GetRequiredService<IAccessTokenProvider>();

        var result = await tokenProvider.GetTokenAsync(new AccessTokenRequest("catalog"));

        Assert.Equal(AccessTokenResultStatus.Unavailable, result.Status);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderReturnsSuccessResult()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var provider = new DelegateAccessTokenProvider((request, _) =>
        {
            Assert.Equal("catalog", request.ResourceName);
            Assert.Equal("Bearer", request.Scheme);
            Assert.Equal("secure-items", request.OperationName);
            return ValueTask.FromResult(AccessTokenResult.Success("access-token", "Bearer", expiresAt));
        });

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog", "Bearer", "secure-items"));

        Assert.Equal(AccessTokenResultStatus.Success, result.Status);
        Assert.Equal("access-token", result.Token);
        Assert.Equal("Bearer", result.Scheme);
        Assert.Equal(expiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderMapsExceptionToFailedResult()
    {
        var exception = new InvalidOperationException("Token refresh failed.");
        var provider = new DelegateAccessTokenProvider((_, _) => throw exception);

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"));

        Assert.Equal(AccessTokenResultStatus.Failed, result.Status);
        Assert.False(result.Succeeded);
        Assert.Equal("Token refresh failed.", result.Message);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderReturnsCancelledWithoutCallingDelegate()
    {
        var called = false;
        var provider = new DelegateAccessTokenProvider((_, _) =>
        {
            called = true;
            return ValueTask.FromResult(AccessTokenResult.Success("access-token", "Bearer"));
        });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"), cancellation.Token);

        Assert.Equal(AccessTokenResultStatus.Cancelled, result.Status);
        Assert.False(called);
    }

    [Fact]
    public async Task UnavailableAccessTokenProviderObservesCancellation()
    {
        var provider = new UnavailableAccessTokenProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"), cancellation.Token);

        Assert.Equal(AccessTokenResultStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderMapsUnexpectedCancellationExceptionToFailure()
    {
        var exception = new OperationCanceledException("provider failed without caller cancellation");
        var provider = new DelegateAccessTokenProvider((_, _) => throw exception);

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"));

        Assert.Equal(AccessTokenResultStatus.Failed, result.Status);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderRejectsNullResult()
    {
        var provider = new DelegateAccessTokenProvider(
            (_, _) => ValueTask.FromResult<AccessTokenResult>(null!));

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"));

        Assert.Equal(AccessTokenResultStatus.Failed, result.Status);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderObservesCancellationAfterDelegate()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new DelegateAccessTokenProvider((_, _) =>
        {
            cancellation.Cancel();
            return ValueTask.FromResult(AccessTokenResult.Success("access-token", "Bearer"));
        });

        var result = await provider.GetTokenAsync(
            new AccessTokenRequest("catalog"),
            cancellation.Token);

        Assert.Equal(AccessTokenResultStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task AccessTokenDiagnosticDoesNotContainTokenValue()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var provider = new DelegateAccessTokenProvider(
            (_, _) => ValueTask.FromResult(AccessTokenResult.Success(
                "secret-access-token",
                "Bearer",
                DateTimeOffset.UtcNow.AddMinutes(5))),
            diagnostics);

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"));

        Assert.True(result.Succeeded);
        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == SecurityDiagnosticIds.AccessTokenResolved);
        var diagnosticText = string.Join('|', record.Context.Values);
        Assert.DoesNotContain("secret-access-token", diagnosticText, StringComparison.Ordinal);
        Assert.Equal("Success", record.Context["status"]);
    }

    [Fact]
    public void AddSecurityInjectsCoreDiagnosticsIntoSecurityServices()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var services = new ServiceCollection();
        services.AddSingleton<IHostDiagnostics>(diagnostics);
        services.AddSecurity();

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<AuthenticationStateStore>().SetAnonymous();

        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.AuthenticationStateChanged);
    }
}
