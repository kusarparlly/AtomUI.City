using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

public static class PresentationInteractionServiceCollectionExtensions
{
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
