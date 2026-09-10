using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new PresentationQueueOptions());
        services.TryAddSingleton<VisualLifecycleHub>(serviceProvider =>
        {
            var diagnostics = serviceProvider.GetService<AtomUI.City.Core.Diagnostics.IHostDiagnostics>();
            return diagnostics is null
                ? new VisualLifecycleHub()
                : new VisualLifecycleHub(diagnostics);
        });
        services.AddPresentationRuntime();
        services.AddAvaloniaUiDispatcher();
        services.AddViewRegistry();
        services.TryAddSingleton(serviceProvider => new ViewFactory(
            serviceProvider.GetRequiredService<IUiDispatcher>(),
            serviceProvider,
            serviceProvider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton(serviceProvider => new ViewBinder(
            serviceProvider.GetService<IHostDiagnostics>(),
            serviceProvider.GetService<VisualLifecycleHub>()));
        services.TryAddSingleton<CommandBinding>();
        services.TryAddSingleton<ValidationVisualStateBinding>();
        services.AddPresentationInteractionHandlers();
        services.AddPresentationResourceRegistry();
        services.AddActivePluginViewRegistry();
        services.TryAddSingleton<PresentationResourceDictionaryRevoker>();
        services.TryAddSingleton<IPresentationResourceDictionaryRevoker>(serviceProvider =>
            serviceProvider.GetRequiredService<PresentationResourceDictionaryRevoker>());
        services.AddPresentationPluginUnloadCoordinator();

        return services;
    }

    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        PresentationQueueOptions queueOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(queueOptions);
        queueOptions.Validate();

        services.AddPresentation();
        services.Replace(ServiceDescriptor.Singleton(queueOptions));
        return services;
    }
}
