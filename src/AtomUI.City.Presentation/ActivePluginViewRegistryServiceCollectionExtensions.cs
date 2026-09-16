using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents active plugin view registry service collection extensions.
/// </summary>
public static class ActivePluginViewRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add active plugin view registry operation.
    /// </summary>
    public static IServiceCollection AddActivePluginViewRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(
            serviceProvider => new ActivePluginViewRegistry(
                serviceProvider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton<IActivePluginViewRegistry>(
            serviceProvider => serviceProvider.GetRequiredService<ActivePluginViewRegistry>());

        return services;
    }
}
