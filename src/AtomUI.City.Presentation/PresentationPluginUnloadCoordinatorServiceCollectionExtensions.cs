using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation plugin unload coordinator service collection extensions.
/// </summary>
public static class PresentationPluginUnloadCoordinatorServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add presentation plugin unload coordinator operation.
    /// </summary>
    public static IServiceCollection AddPresentationPluginUnloadCoordinator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(
            serviceProvider => new PresentationPluginUnloadCoordinator(
                serviceProvider.GetRequiredService<IActivePluginViewRegistry>(),
                serviceProvider.GetRequiredService<IInteractionHandlerRegistry>(),
                serviceProvider.GetRequiredService<IViewRegistry>(),
                serviceProvider.GetRequiredService<IPresentationResourceRegistry>(),
                serviceProvider.GetRequiredService<IPresentationResourceDictionaryRevoker>(),
                serviceProvider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton<IPresentationPluginUnloadCoordinator>(
            serviceProvider => serviceProvider.GetRequiredService<PresentationPluginUnloadCoordinator>());

        return services;
    }
}
