using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

public static class PresentationRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(
            serviceProvider =>
            {
                var diagnostics = serviceProvider.GetService<IHostDiagnostics>();
                return new PresentationRuntime(
                    () => serviceProvider.GetService<AtomUI.City.Core.Threading.IUiDispatcher>(),
                    diagnostics,
                    serviceProvider.GetService<PresentationQueueOptions>(),
                    serviceProvider.GetService<IPresentationFailurePresenter>(),
                    serviceProvider.GetService<VisualLifecycleHub>());
            });
        services.TryAddSingleton<IPresentationRuntime>(
            serviceProvider => serviceProvider.GetRequiredService<PresentationRuntime>());
        services.TryAddSingleton<IViewModelFactory, DefaultViewModelFactory>();
        services.TryAddSingleton<IPresentationFailurePresenter, NullPresentationFailurePresenter>();

        return services;
    }
}
