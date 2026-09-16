using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents presentation runtime service collection extensions.
/// </summary>
public static class PresentationRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add presentation runtime operation.
    /// </summary>
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
