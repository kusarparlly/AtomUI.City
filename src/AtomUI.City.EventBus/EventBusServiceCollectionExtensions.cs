using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event bus service collection extensions.
/// </summary>
public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add event bus operation.
    /// </summary>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        EventChannelOptions defaultChannelOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(defaultChannelOptions);
        defaultChannelOptions.Validate();

        services.Replace(ServiceDescriptor.Singleton(defaultChannelOptions));
        return services.AddEventBus();
    }

    /// <summary>
    /// Executes the add event bus operation.
    /// </summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IHostDiagnostics>(
            _ => new InMemoryHostDiagnostics(EventBusDiagnosticsOptions.DefaultMemoryBufferCapacity));
        services.TryAddSingleton(EventChannelOptions.Default);
        services.TryAddSingleton(EventBusRuntimeOptions.Default);
        services.TryAddSingleton(EventBusDispatchOptions.Default);
        services.TryAddSingleton(EventBusDiagnosticsOptions.Default);
        services.TryAddSingleton<IEventBackgroundScheduler, ThreadPoolEventBackgroundScheduler>();
        services.TryAddSingleton<IEventContractRegistry>(serviceProvider =>
        {
            var registry = new InMemoryEventContractRegistry();

            foreach (var descriptor in serviceProvider.GetServices<EventContractDescriptor>())
            {
                registry.Register(descriptor);
            }

            registry.Freeze();

            return registry;
        });
        services.TryAddSingleton(serviceProvider =>
        {
            var contractRegistry = serviceProvider.GetRequiredService<IEventContractRegistry>();
            PrepareContractRegistry(
                contractRegistry,
                serviceProvider.GetServices<EventContractDescriptor>());

            var eventBus = new InMemoryEventBus(
                contractRegistry,
                serviceProvider.GetRequiredService<IHostDiagnostics>(),
                serviceProvider.GetRequiredService<EventChannelOptions>(),
                serviceProvider.GetServices<EventChannelDescriptor>(),
                serviceProvider.GetRequiredService<IEventBackgroundScheduler>(),
                serviceProvider.GetRequiredService<EventBusDispatchOptions>(),
                serviceProvider.GetRequiredService<EventBusDiagnosticsOptions>(),
                serviceProvider.GetServices<EventPayloadDiagnosticProjectorDescriptor>(),
                serviceProvider.GetRequiredService<EventBusRuntimeOptions>(),
                serviceProvider.GetServices<GeneratedEventHandlerDescriptor>(),
                serviceProvider);

            if (serviceProvider.GetService<EventBusHostManagedMarker>() is not null)
            {
                eventBus.RequireHostLifecycle();
            }

            return eventBus;
        });
        services.TryAddSingleton<IEventBus>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryEventBus>());
        services.TryAddSingleton<IEventPublisher>(
            serviceProvider => serviceProvider.GetRequiredService<IEventBus>());
        services.TryAddSingleton<IEventSubscriber>(
            serviceProvider => serviceProvider.GetRequiredService<IEventBus>());
        services.TryAddSingleton<IEventChannelMonitor>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryEventBus>());
        services.TryAddSingleton<IEventBusMonitor>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryEventBus>());
        services.TryAddSingleton<IEventBusContributionController>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryEventBus>());

        return services;
    }

    /// <summary>
    /// Executes the add event payload diagnostic projector&lt;tevent, tprojector&gt; operation.
    /// </summary>
    public static IServiceCollection AddEventPayloadDiagnosticProjector<TEvent, TProjector>(
        this IServiceCollection services)
        where TProjector : class, IEventPayloadDiagnosticProjector<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TProjector>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                EventPayloadDiagnosticProjectorDescriptor,
                EventPayloadDiagnosticProjectorRegistration<TEvent, TProjector>>());
        return services;
    }

    /// <summary>
    /// Executes the configure event bus diagnostics operation.
    /// </summary>
    public static IServiceCollection ConfigureEventBusDiagnostics(
        this IServiceCollection services,
        EventBusDiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.Replace(ServiceDescriptor.Singleton(options));
        return services;
    }

    /// <summary>
    /// Executes the configure event bus runtime operation.
    /// </summary>
    public static IServiceCollection ConfigureEventBusRuntime(
        this IServiceCollection services,
        EventBusRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        services.AddEventBus();
        services.Replace(ServiceDescriptor.Singleton(options));
        return services;
    }

    private static void PrepareContractRegistry(
        IEventContractRegistry contractRegistry,
        IEnumerable<EventContractDescriptor> descriptors)
    {
        var collectedDescriptors = descriptors.ToArray();
        if (!contractRegistry.IsFrozen)
        {
            foreach (var descriptor in collectedDescriptors)
            {
                contractRegistry.Register(descriptor);
            }

            contractRegistry.Freeze();
        }

        foreach (var descriptor in collectedDescriptors)
        {
            if (!contractRegistry.TryGet(descriptor.ContractId, out var byContractId) ||
                !contractRegistry.TryGet(descriptor.EventType, out var byEventType) ||
                byContractId is null ||
                byEventType is null ||
                !ReferenceEquals(byContractId, byEventType) ||
                byContractId.ContractId != descriptor.ContractId ||
                byContractId.EventType != descriptor.EventType ||
                byContractId.Plane != descriptor.Plane ||
                byContractId.SchemaVersion != descriptor.SchemaVersion ||
                !string.Equals(byContractId.SchemaFingerprint, descriptor.SchemaFingerprint, StringComparison.Ordinal) ||
                !ReferenceEquals(byContractId.Assembly, descriptor.Assembly))
            {
                throw new InvalidOperationException(
                    $"Frozen event contract registry does not contain the collected descriptor '{descriptor.ContractId.Value}' for '{descriptor.EventType.FullName}'.");
            }
        }
    }

    /// <summary>
    /// Executes the add event contract&lt;tevent&gt; operation.
    /// </summary>
    public static IServiceCollection AddEventContract<TEvent>(
        this IServiceCollection services,
        EventContractId contractId)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEventBus();
        services.AddSingleton(EventContractDescriptor.Shared<TEvent>(contractId, typeof(TEvent).Assembly));

        return services;
    }

    /// <summary>
    /// Executes the configure event channel&lt;tevent&gt; operation.
    /// </summary>
    public static IServiceCollection ConfigureEventChannel<TEvent>(
        this IServiceCollection services,
        EventChannelOptions options)
    {
        return services.ConfigureEventChannel(EventChannel<TEvent>.Default, options);
    }

    /// <summary>
    /// Executes the configure event channel&lt;tevent&gt; operation.
    /// </summary>
    public static IServiceCollection ConfigureEventChannel<TEvent>(
        this IServiceCollection services,
        EventChannel<TEvent> channel,
        EventChannelOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        options.Validate();

        services.AddEventBus();
        services.AddSingleton(EventChannelDescriptor.Create(channel, options));
        return services;
    }
}
