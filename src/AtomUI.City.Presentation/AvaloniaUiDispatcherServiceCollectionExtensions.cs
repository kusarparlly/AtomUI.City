using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

public static class AvaloniaUiDispatcherServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaUiDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == typeof(IUiDispatcher) &&
                descriptor.ImplementationType == typeof(UnavailableUiDispatcher))
            {
                services.RemoveAt(index);
            }
        }

        services.TryAddSingleton<IUiDispatcher>(
            serviceProvider => new AvaloniaUiDispatcher(
                Avalonia.Threading.Dispatcher.UIThread,
                serviceProvider.GetService<IPresentationRuntime>(),
                serviceProvider.GetService<IHostDiagnostics>()));

        return services;
    }
}
