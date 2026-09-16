using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Routing;

/// <summary>
/// Represents routing service collection extensions.
/// </summary>
public static class RoutingServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add routing operation.
    /// </summary>
    public static IServiceCollection AddRouting(
        this IServiceCollection services,
        IReadOnlyList<RouteDescriptor>? routes = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var initialRoutes = routes?.ToArray() ?? [];
        if (initialRoutes.Length > 0)
        {
            services.AddSingleton(new StaticRouteRegistration(initialRoutes));
        }

        services.TryAddSingleton(
            provider => new RouteRegistry(
                RouteGraphSnapshot.Create(
                    provider
                        .GetServices<StaticRouteRegistration>()
                        .SelectMany(registration => registration.Routes)
                        .ToArray()),
                provider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton<IRouteRegistry>(provider => provider.GetRequiredService<RouteRegistry>());
        services.TryAddSingleton<IRouteGraphProvider>(provider => provider.GetRequiredService<RouteRegistry>());
        services.TryAddScoped(
            provider => new NavigationScope(
                provider.GetRequiredService<IRouteGraphProvider>(),
                type => provider.GetService(type),
                provider.GetService<IHostDiagnostics>()));
        services.TryAddScoped<IRouter>(provider => provider.GetRequiredService<NavigationScope>());

        return services;
    }

    private sealed class StaticRouteRegistration
    {
        public StaticRouteRegistration(IReadOnlyList<RouteDescriptor> routes)
        {
            Routes = routes;
        }

        public IReadOnlyList<RouteDescriptor> Routes { get; }
    }
}
