using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation resource registry service collection extensions.
/// </summary>
public static class PresentationResourceRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add presentation resource registry operation.
    /// </summary>
    public static IServiceCollection AddPresentationResourceRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(
            serviceProvider => new PresentationResourceRegistry(
                serviceProvider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton<IPresentationResourceRegistry>(
            serviceProvider => serviceProvider.GetRequiredService<PresentationResourceRegistry>());

        return services;
    }
}
