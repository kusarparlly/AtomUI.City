using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Security;

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        return AddSecurity(services, new SecurityPersistenceOptions());
    }

    public static IServiceCollection AddSecurity(
        this IServiceCollection services,
        SecurityPersistenceOptions persistenceOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(persistenceOptions);

        services.TryAddSingleton(persistenceOptions);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuthenticationStateStore>();
        services.TryAddSingleton<IAuthenticationStateProvider>(
            serviceProvider => serviceProvider.GetRequiredService<AuthenticationStateStore>());
        services.TryAddSingleton<ICurrentPrincipalAccessor>(
            serviceProvider => serviceProvider.GetRequiredService<AuthenticationStateStore>());
        services.TryAddSingleton<PermissionRegistry>();
        services.TryAddSingleton<IPermissionRegistry>(
            serviceProvider => serviceProvider.GetRequiredService<PermissionRegistry>());
        services.TryAddSingleton<InMemoryAuthorizationPolicyProvider>();
        services.TryAddSingleton<IAuthorizationPolicyProvider>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryAuthorizationPolicyProvider>());
        services.TryAddSingleton<IAuthorizationEvaluator, AuthorizationEvaluator>();
        services.TryAddSingleton<IPermissionChecker>(
            serviceProvider => new PermissionChecker(
                serviceProvider.GetRequiredService<IAuthorizationEvaluator>(),
                serviceProvider.GetRequiredService<ICurrentPrincipalAccessor>()));
        services.TryAddSingleton<InMemoryCommandAuthorizationDescriptorProvider>();
        services.TryAddSingleton<ICommandAuthorizationDescriptorProvider>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryCommandAuthorizationDescriptorProvider>());
        services.TryAddSingleton<ICommandAuthorizationSource>(
            serviceProvider => new CommandAuthorizationSource(
                serviceProvider.GetRequiredService<IAuthorizationEvaluator>(),
                serviceProvider.GetRequiredService<ICurrentPrincipalAccessor>(),
                serviceProvider.GetRequiredService<ICommandAuthorizationDescriptorProvider>(),
                serviceProvider.GetRequiredService<IAuthenticationStateProvider>(),
                serviceProvider.GetRequiredService<IPermissionRegistry>(),
                serviceProvider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton<FileAccountSessionStore>(serviceProvider =>
        {
            var rootPath = SecurityFilePersistence.ResolveRootPath(
                serviceProvider.GetRequiredService<SecurityPersistenceOptions>(),
                serviceProvider.GetService<IApplicationContext>());
            return new FileAccountSessionStore(rootPath, serviceProvider.GetService<IHostDiagnostics>());
        });
        services.TryAddSingleton<IAccountSessionStore>(
            serviceProvider => serviceProvider.GetRequiredService<FileAccountSessionStore>());
        services.TryAddSingleton<FileCredentialStore>(serviceProvider =>
        {
            var rootPath = SecurityFilePersistence.ResolveRootPath(
                serviceProvider.GetRequiredService<SecurityPersistenceOptions>(),
                serviceProvider.GetService<IApplicationContext>());
            return new FileCredentialStore(rootPath, serviceProvider.GetService<IHostDiagnostics>());
        });
        services.TryAddSingleton<ICredentialStore>(
            serviceProvider => serviceProvider.GetRequiredService<FileCredentialStore>());
        services.TryAddSingleton<AccountSessionManager>(serviceProvider =>
            new AccountSessionManager(
                serviceProvider.GetRequiredService<IAccountSessionStore>(),
                serviceProvider.GetRequiredService<ICredentialStore>(),
                serviceProvider.GetRequiredService<AuthenticationStateStore>(),
                serviceProvider.GetRequiredService<SecurityPersistenceOptions>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                serviceProvider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton<IAccountSessionManager>(
            serviceProvider => serviceProvider.GetRequiredService<AccountSessionManager>());
        services.TryAddSingleton<IAccessTokenProvider>(
            serviceProvider => serviceProvider.GetRequiredService<AccountSessionManager>());
        services.TryAddSingleton<InMemoryRouteAuthorizationPolicyProvider>();
        services.TryAddSingleton<IRouteAuthorizationPolicyProvider>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryRouteAuthorizationPolicyProvider>());
        services.TryAddSingleton(new SecurityRouteGuardOptions());
        services.TryAddSingleton<SecurityRouteGuard>();

        return services;
    }
}
