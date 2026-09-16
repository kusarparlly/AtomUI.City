using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation interaction service collection extensions.
/// </summary>
public static class PresentationInteractionServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add presentation interaction handlers operation.
    /// </summary>
    public static IServiceCollection AddPresentationInteractionHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(
            serviceProvider => new InteractionHandlerRegistry(
                serviceProvider.GetRequiredService<IUiDispatcher>(),
                serviceProvider.GetService<IHostDiagnostics>(),
                serviceProvider.GetService<PresentationQueueOptions>()));
        services.TryAddSingleton<IInteractionHandlerRegistry>(
            serviceProvider => serviceProvider.GetRequiredService<InteractionHandlerRegistry>());

        return services;
    }
}
