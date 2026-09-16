using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event bus module.
/// </summary>
[Module("AtomUI.City.EventBus", Version = "1.0.0", Description = "Provides the City application event bus.")]
public sealed class EventBusModule : ModuleBase
{
    /// <summary>
    /// Executes the configure services operation.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.TryAddSingleton<EventBusHostManagedMarker>();
        context.Services.AddEventBus();
        context.Services.TryAddSingleton<IEventBusLifecycleController>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryEventBus>());
    }

    /// <summary>
    /// Executes the post configure services operation.
    /// </summary>
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        GeneratedEventCatalogValidator.ValidateSelectedContributions(context.Services);
    }

    /// <summary>
    /// Executes the on pre application initialization async operation.
    /// </summary>
    public override ValueTask OnPreApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<IEventBusLifecycleController>()
            .StartAsync(context.ApplicationScope, cancellationToken);
    }

    /// <summary>
    /// Executes the on application shutdown async operation.
    /// </summary>
    public override ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<IEventBusLifecycleController>()
            .StopAsync(cancellationToken);
    }
}
