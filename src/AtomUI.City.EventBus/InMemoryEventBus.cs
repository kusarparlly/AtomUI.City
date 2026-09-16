using System.Diagnostics;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents in memory event bus.
/// </summary>
public sealed class InMemoryEventBus :
    IEventBus,
    IEventChannelMonitor,
    IEventBusMonitor,
    IEventBusLifecycleController,
    IEventBusContributionController
{
    private static readonly AsyncLocal<AmbientPublicationContext?> CurrentPublication = new();
    private const string ContractIdContextKey = "contractId";
    private const string EventIdContextKey = "eventId";
    private const string EventTypeContextKey = "eventType";
    private const string SubscriptionIdContextKey = "subscriptionId";
    private const string DispatchPolicyContextKey = "dispatchPolicy";
    private const string ErrorPolicyContextKey = "errorPolicy";
    private const string ChannelContextKey = "channel";
    private const string PartitionHashContextKey = "partitionHash";
    private const string DeliveryExceptionContractIdDataKey = "AtomUI.City.EventBus.ContractId";
    private const string DeliveryExceptionEventIdDataKey = "AtomUI.City.EventBus.EventId";
    private const string DeliveryExceptionSubscriptionIdDataKey = "AtomUI.City.EventBus.SubscriptionId";

    private readonly IEventContractRegistry _contractRegistry;
    private readonly IHostDiagnostics? _hostDiagnostics;
    private readonly EventDiagnosticWriter _diagnostics;
    private readonly EventBusDiagnosticsOptions _diagnosticsOptions;
    private readonly IReadOnlyDictionary<Type, EventPayloadDiagnosticProjectorDescriptor> _payloadProjectors;
    private readonly IEventBackgroundScheduler _backgroundScheduler;
    private readonly EventChannelOptions _channelOptions;
    private readonly EventBusDispatchOptions _dispatchOptions;
    private readonly EventBusRuntimeOptions _runtimeOptions;
    private readonly IReadOnlyList<GeneratedEventHandlerDescriptor> _generatedHandlers;
    private readonly IServiceProvider? _generatedHandlerServices;
    private readonly Dictionary<ChannelKey, List<EventSubscription>> _subscriptions = [];
    private readonly Dictionary<ChannelKey, EventChannelRuntime> _channelRuntimes = [];
    private readonly Dictionary<ChannelKey, EventChannelOptions> _configuredChannels = [];
    private readonly Dictionary<string, EventBusContributionLease> _pluginContributions = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private readonly object _syncRoot = new();
    private Task? _disposeTask;
    private bool _disposed;
    private bool _hostLifecycleRequired;
    private bool _started = true;
    private LifecycleScope? _applicationScope;
    private long _publicationCount;
    private long _deliverySucceededCount;
    private long _deliveryFailedCount;
    private long _deliveryCanceledCount;
    private long _deliveryTimedOutCount;
    private long _deliverySkippedCount;
    private long _totalHandlerDurationTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryEventBus"/> type.
    /// </summary>
    public InMemoryEventBus(
        IEventContractRegistry? contractRegistry = null,
        IHostDiagnostics? diagnostics = null,
        EventChannelOptions? channelOptions = null,
        IEnumerable<EventChannelDescriptor>? channelDescriptors = null,
        IEventBackgroundScheduler? backgroundScheduler = null,
        EventBusDispatchOptions? dispatchOptions = null,
        EventBusDiagnosticsOptions? diagnosticsOptions = null,
        IEnumerable<EventPayloadDiagnosticProjectorDescriptor>? payloadProjectors = null,
        EventBusRuntimeOptions? runtimeOptions = null,
        IEnumerable<GeneratedEventHandlerDescriptor>? generatedHandlers = null,
        IServiceProvider? generatedHandlerServices = null)
    {
        _contractRegistry = contractRegistry ?? new InMemoryEventContractRegistry();
        _hostDiagnostics = diagnostics;
        _diagnosticsOptions = diagnosticsOptions ?? EventBusDiagnosticsOptions.Default;
        _diagnostics = new EventDiagnosticWriter(diagnostics, _diagnosticsOptions);
        _payloadProjectors = (payloadProjectors ?? [])
            .ToDictionary(
                descriptor => descriptor?.EventType ??
                              throw new ArgumentException("Payload projector descriptors cannot contain null.", nameof(payloadProjectors)),
                descriptor => descriptor);
        _backgroundScheduler = backgroundScheduler ?? new ThreadPoolEventBackgroundScheduler();
        _channelOptions = channelOptions ?? EventChannelOptions.Default;
        _channelOptions.Validate();
        _dispatchOptions = dispatchOptions ?? EventBusDispatchOptions.Default;
        _dispatchOptions.Validate();
        _runtimeOptions = runtimeOptions ?? EventBusRuntimeOptions.Default;
        _runtimeOptions.Validate();
        _generatedHandlers = Array.AsReadOnly((generatedHandlers ?? [])
            .Select(descriptor => descriptor ??
                throw new ArgumentException("Generated event handler descriptors cannot contain null.", nameof(generatedHandlers)))
            .ToArray());
        _generatedHandlerServices = generatedHandlerServices;

        if (_generatedHandlers.Count > 0 && _generatedHandlerServices is null)
        {
            throw new ArgumentException(
                "A service provider is required when generated event handlers are configured.",
                nameof(generatedHandlerServices));
        }

        foreach (var descriptor in channelDescriptors ?? [])
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            var key = new ChannelKey(descriptor.EventType, descriptor.ChannelName);
            if (!_configuredChannels.TryAdd(key, descriptor.Options))
            {
                throw new InvalidOperationException(
                    $"Event channel '{descriptor.ChannelName}' for type '{descriptor.EventType.FullName}' is configured more than once.");
            }
        }

        if (_configuredChannels.Count > _runtimeOptions.MaximumChannelRuntimes)
        {
            throw new InvalidOperationException(
                $"The {_configuredChannels.Count} configured event channels exceed the EventBus runtime limit of {_runtimeOptions.MaximumChannelRuntimes}.");
        }
    }

    /// <summary>
    /// Executes the subscribe&lt;tevent&gt; operation.
    /// </summary>
    public IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        return Subscribe(owner, EventChannel<TEvent>.Default, handler, options);
    }

    /// <summary>
    /// Executes the subscribe&lt;tevent&gt; operation.
    /// </summary>
    public IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        return SubscribeCore(owner, channel.Name, handler, options ?? EventSubscriptionOptions.Serialized);
    }

    /// <summary>
    /// Executes the subscribe&lt;tevent&gt; operation.
    /// </summary>
    public IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe(owner, EventChannel<TEvent>.Default, handler, options);
    }

    /// <summary>
    /// Executes the subscribe&lt;tevent&gt; operation.
    /// </summary>
    public IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        return SubscribeCore<TEvent>(
            owner,
            channel.Name,
            context => handler.HandleAsync(context),
            options ?? EventSubscriptionOptions.Serialized,
            handler.GetType().FullName);
    }

    /// <summary>
    /// Executes the publish async&lt;tevent&gt; operation.
    /// </summary>
    public ValueTask<EventPublishResult> PublishAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return PublishAsync(EventChannel<TEvent>.Default, eventData, options, cancellationToken);
    }

    /// <summary>
    /// Executes the publish async&lt;tevent&gt; operation.
    /// </summary>
    public async ValueTask<EventPublishResult> PublishAsync<TEvent>(
        EventChannel<TEvent> channel,
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfUnavailable();
        var publishOptions = NormalizeAndValidatePublishOptions(options);
        var eventId = Guid.NewGuid();
        var descriptor = ResolveContract<TEvent>("publish", channel.Name, eventId);
        var snapshot = GetSnapshot(typeof(TEvent), channel.Name);
        var runtime = GetOrCreateChannelRuntime(descriptor, channel.Name);
        if (runtime is null)
        {
            var reason = $"The maximum number of event channel runtimes ({_runtimeOptions.MaximumChannelRuntimes}) has been reached.";
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Event '{descriptor.ContractId.Value}' with id '{eventId:D}' was rejected: {reason}",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(descriptor, eventId, channel.Name, publishOptions));
            throw new EventPublicationRejectedException(eventId, descriptor.ContractId, reason);
        }

        if (runtime.IsExecutingOnCurrentContext)
        {
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Event '{descriptor.ContractId.Value}' with id '{eventId:D}' was rejected because its channel cannot synchronously await itself.",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(descriptor, eventId, channel.Name, publishOptions));
            throw new EventPublicationRejectedException(
                eventId,
                descriptor.ContractId,
                "An event channel cannot synchronously await a nested publication to itself. Use PostAsync or another channel.");
        }

        var request = CreatePublicationRequest(
            eventData,
            publishOptions,
            descriptor,
            channel.Name,
            eventId,
            cancellationToken,
            snapshot,
            waitForCompletion: true);

        EventChannelRuntime.EnqueueResult enqueueResult;
        try
        {
            enqueueResult = await runtime.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Event '{descriptor.ContractId.Value}' with id '{eventId:D}' was canceled before channel acceptance.",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(descriptor, eventId, channel.Name, publishOptions));
            throw;
        }

        if (enqueueResult != EventChannelRuntime.EnqueueResult.Accepted)
        {
            var reason = GetRejectionReason(enqueueResult, canceled: false);
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Event '{descriptor.ContractId.Value}' with id '{eventId:D}' was rejected: {reason}",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(
                    descriptor,
                    eventId,
                    channel.Name,
                    publishOptions,
                    runtime.BackpressurePolicy));
            throw CreateRejectedPublicationException(eventId, descriptor.ContractId, enqueueResult);
        }

        return await request.Completion!.ConfigureAwait(false);
    }

    private async ValueTask<EventPublishResult> PublishCoreAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options,
        EventContractDescriptor descriptor,
        string channelName,
        Guid eventId,
        TimeSpan queueWaitDuration,
        CancellationToken cancellationToken,
        EventSubscription[] snapshot)
    {
        var publishOptions = NormalizeAndValidatePublishOptions(options);
        var publishedAt = DateTimeOffset.UtcNow;
        var publicationStartedAt = Stopwatch.GetTimestamp();
        var correlationId = string.IsNullOrWhiteSpace(publishOptions.CorrelationId)
            ? eventId.ToString("D")
            : publishOptions.CorrelationId;
        var deliveries = new EventDeliveryResult?[snapshot.Length];
        var maximumConcurrentDeliveries = _dispatchOptions.MaximumConcurrentDeliveriesPerPublication;
        var pendingDeliveries = new List<PendingDelivery>(maximumConcurrentDeliveries);
        var publicationCanceled = false;
        Interlocked.Increment(ref _publicationCount);

        if (_diagnostics.IsSampledTraceEnabled(eventId))
        {
            WriteDiagnostic(
                EventDiagnosticIds.EventPublished,
                $"Event '{descriptor.ContractId.Value}' with id '{eventId:D}' was published.",
                HostDiagnosticSeverity.Trace,
                CreatePublishedEventDiagnosticContext(
                    descriptor,
                    eventId,
                    channelName,
                    publishOptions,
                    eventData!,
                    queueWaitDuration),
                sampledTrace: true,
                eventId);
        }

        for (var index = 0; index < snapshot.Length; index++)
        {
            var subscription = snapshot[index];
            if (cancellationToken.IsCancellationRequested)
            {
                publicationCanceled = true;
                break;
            }

            if (subscription.Options.ErrorPolicy is EventErrorPolicy.StopPublication or EventErrorPolicy.FailPublisher)
            {
                await CompletePendingDeliveriesAsync(pendingDeliveries, deliveries).ConfigureAwait(false);

                var controlledDelivery = await DeliverAsync(subscription).ConfigureAwait(false);
                deliveries[index] = controlledDelivery ?? CreateSkippedDelivery(subscription);

                if (controlledDelivery is { Canceled: true } ||
                    controlledDelivery is { Succeeded: false } &&
                    subscription.Options.ErrorPolicy == EventErrorPolicy.StopPublication)
                {
                    break;
                }
            }
            else
            {
                pendingDeliveries.Add(new PendingDelivery(index, subscription, DeliverAsync(subscription).AsTask()));
                if (pendingDeliveries.Count == maximumConcurrentDeliveries)
                {
                    await CompletePendingDeliveriesAsync(pendingDeliveries, deliveries).ConfigureAwait(false);
                }
            }
        }

        await CompletePendingDeliveriesAsync(pendingDeliveries, deliveries).ConfigureAwait(false);

        for (var index = 0; index < snapshot.Length; index++)
        {
            deliveries[index] ??= publicationCanceled
                ? CreateCanceledDelivery(snapshot[index])
                : CreateSkippedDelivery(snapshot[index]);

            if (deliveries[index]!.Status == EventDeliveryStatus.Skipped)
            {
                RecordDelivery(EventDeliveryStatus.Skipped, TimeSpan.Zero);
            }
        }

        return new EventPublishResult(
            eventId,
            descriptor.ContractId,
            deliveries!,
            Stopwatch.GetElapsedTime(publicationStartedAt));

        ValueTask<EventDeliveryResult?> DeliverAsync(EventSubscription subscription)
        {
            return subscription.DeliverAsync(
                eventData!,
                descriptor,
                eventId,
                correlationId,
                publishOptions.CausationId,
                publishedAt,
                publishOptions.PublishDepth,
                cancellationToken);
        }
    }

    private static async ValueTask CompletePendingDeliveriesAsync(
        List<PendingDelivery> pendingDeliveries,
        EventDeliveryResult?[] deliveries)
    {
        foreach (var pending in pendingDeliveries)
        {
            var delivery = await pending.Operation.ConfigureAwait(false);
            deliveries[pending.Index] = delivery ?? CreateSkippedDelivery(pending.Subscription);
        }

        pendingDeliveries.Clear();
    }

    private static EventDeliveryResult CreateSkippedDelivery(EventSubscription subscription)
    {
        return new EventDeliveryResult(
            subscription.Id,
            subscription.Options.DispatchPolicy,
            Succeeded: false)
        {
            Skipped = true
        };
    }

    private static EventDeliveryResult CreateCanceledDelivery(EventSubscription subscription)
    {
        return new EventDeliveryResult(
            subscription.Id,
            subscription.Options.DispatchPolicy,
            Succeeded: false,
            "Publication was canceled before this delivery started.",
            Canceled: true)
        {
            Skipped = true
        };
    }

    /// <summary>
    /// Executes the post async&lt;tevent&gt; operation.
    /// </summary>
    public ValueTask<EventPostResult> PostAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(EventChannel<TEvent>.Default, eventData, options, cancellationToken);
    }

    /// <summary>
    /// Executes the post async&lt;tevent&gt; operation.
    /// </summary>
    public async ValueTask<EventPostResult> PostAsync<TEvent>(
        EventChannel<TEvent> channel,
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        ThrowIfUnavailable();
        var publishOptions = NormalizeAndValidatePublishOptions(options);

        var eventId = Guid.NewGuid();
        var descriptor = ResolveContract<TEvent>("post", channel.Name, eventId);

        if (cancellationToken.IsCancellationRequested)
        {
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}' was rejected because publication was canceled before acceptance.",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(descriptor, eventId, channel.Name, publishOptions));

            return new EventPostResult(
                eventId,
                descriptor.ContractId,
                Accepted: false,
                "Publication was canceled before it was accepted.");
        }

        var snapshot = GetSnapshot(typeof(TEvent), channel.Name);
        var runtime = GetOrCreateChannelRuntime(descriptor, channel.Name);
        if (runtime is null)
        {
            var reason = $"The maximum number of event channel runtimes ({_runtimeOptions.MaximumChannelRuntimes}) has been reached.";
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}' was rejected: {reason}",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(descriptor, eventId, channel.Name, publishOptions));
            return new EventPostResult(
                eventId,
                descriptor.ContractId,
                Accepted: false,
                reason);
        }

        var request = CreatePublicationRequest(
            eventData,
            publishOptions,
            descriptor,
            channel.Name,
            eventId,
            cancellationToken,
            snapshot,
            waitForCompletion: false);
        EventChannelRuntime.EnqueueResult enqueueResult;

        try
        {
            enqueueResult = await runtime.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            enqueueResult = EventChannelRuntime.EnqueueResult.Rejected;
        }

        if (enqueueResult != EventChannelRuntime.EnqueueResult.Accepted)
        {
            var reason = GetRejectionReason(enqueueResult, cancellationToken.IsCancellationRequested);
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}' was rejected: {reason}",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(
                    descriptor,
                    eventId,
                    channel.Name,
                    publishOptions,
                    runtime.BackpressurePolicy));

            return new EventPostResult(
                eventId,
                descriptor.ContractId,
                Accepted: false,
                reason);
        }

        WriteDiagnostic(
            EventDiagnosticIds.EventAccepted,
            $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}' was accepted.",
            HostDiagnosticSeverity.Trace,
            CreateEventDiagnosticContext(descriptor, eventId, channel.Name, publishOptions),
            sampledTrace: true,
            eventId);

        return new EventPostResult(
            eventId,
            descriptor.ContractId,
            Accepted: true);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        _ = RequestDisposal();
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return new ValueTask(RequestDisposal());
    }

    internal void RequireHostLifecycle()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (_subscriptions.Count != 0 || _channelRuntimes.Count != 0)
            {
                throw new InvalidOperationException(
                    "The EventBus cannot become Host-managed after it has accepted runtime work.");
            }

            _hostLifecycleRequired = true;
            _started = false;
        }
    }

    ValueTask IEventBusLifecycleController.StartAsync(
        LifecycleScope applicationScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applicationScope);
        cancellationToken.ThrowIfCancellationRequested();

        if (applicationScope.State != LifecycleScopeState.Running ||
            applicationScope.CancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "The EventBus can only start with a running ApplicationScope.");
        }

        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (!_hostLifecycleRequired)
            {
                throw new InvalidOperationException(
                    "The EventBus was not registered for City Host lifecycle management.");
            }

            if (_started)
            {
                if (!ReferenceEquals(_applicationScope, applicationScope))
                {
                    throw new InvalidOperationException(
                        "The EventBus has already started with a different ApplicationScope.");
                }

                return ValueTask.CompletedTask;
            }

            _applicationScope = applicationScope;
            _started = true;
        }

        var activated = new List<IEventSubscription>(_generatedHandlers.Count);
        try
        {
            foreach (var descriptor in _generatedHandlers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                activated.Add(descriptor.Activate(
                    _generatedHandlerServices!,
                    applicationScope,
                    this));
            }
        }
        catch
        {
            foreach (var subscription in activated)
            {
                subscription.Dispose();
            }

            lock (_syncRoot)
            {
                _applicationScope = null;
                _started = false;
            }

            throw;
        }

        return ValueTask.CompletedTask;
    }

    ValueTask IEventBusLifecycleController.StopAsync(CancellationToken cancellationToken)
    {
        var termination = RequestDisposal();
        return termination.IsCompletedSuccessfully || !cancellationToken.CanBeCanceled
            ? new ValueTask(termination)
            : new ValueTask(termination.WaitAsync(cancellationToken));
    }

    ValueTask<IEventBusContributionLease> IEventBusContributionController.CreateAsync(
        EventBusContributionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var rule in request.SharedAccess)
        {
            if (!_contractRegistry.TryGet(rule.ContractId, out var descriptor) || descriptor is null ||
                descriptor.Plane != EventContractPlane.Shared)
                throw RejectPluginContribution(request.PluginId,
                    $"Shared event contract '{rule.ContractId.Value}' is not registered.", rule.ContractId.Value, rule.ChannelName);
            if (!descriptor.IsGeneratedObjectGraphValidated)
                throw RejectPluginContribution(request.PluginId,
                    $"Shared event contract '{rule.ContractId.Value}' was not generated from a validated closed object graph.",
                    rule.ContractId.Value, rule.ChannelName);
            if (descriptor.SchemaVersion < rule.MinimumSchemaVersion || descriptor.SchemaVersion > rule.MaximumSchemaVersion)
                throw RejectPluginContribution(request.PluginId,
                    $"Shared event contract '{rule.ContractId.Value}' schema version {descriptor.SchemaVersion} is outside the requested range {rule.MinimumSchemaVersion}..{rule.MaximumSchemaVersion}.",
                    rule.ContractId.Value, rule.ChannelName);
        }

        LifecycleScope applicationScope;
        lock (_syncRoot)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!_hostLifecycleRequired || !_started || _applicationScope is null)
                throw new InvalidOperationException("Plugin EventBus contributions require a running Host-managed EventBus.");
            if (_pluginContributions.ContainsKey(request.PluginId))
                throw new InvalidOperationException($"Plugin EventBus contribution '{request.PluginId}' already exists.");
            applicationScope = _applicationScope;
        }

        var scope = applicationScope.CreateChild(LifecycleScopeKind.Operation, $"eventbus-plugin:{request.PluginId}");
        var privateRegistry = new PluginPrivateEventContractRegistry(request.PrivateContracts);
        var privateBus = new InMemoryEventBus(
            privateRegistry,
            _hostDiagnostics,
            runtimeOptions: new EventBusRuntimeOptions
            {
                MaximumChannelRuntimes = request.Quotas.MaximumPrivateChannelRuntimes,
            });
        var lease = new EventBusContributionLease(
            request, this, _contractRegistry, privateBus, privateRegistry, scope, _diagnostics, RemovePluginContribution);

        var committed = false;
        lock (_syncRoot)
        {
            if (!_disposed && _started && ReferenceEquals(_applicationScope, applicationScope))
            {
                committed = _pluginContributions.TryAdd(request.PluginId, lease);
            }
        }

        if (!committed)
        {
            lease.Dispose();
            throw new InvalidOperationException(
                $"Plugin EventBus contribution '{request.PluginId}' could not be committed because the Host lifecycle changed or the id is already active.");
        }

        if (!lease.TryActivate())
        {
            lease.Dispose();
            throw new InvalidOperationException(
                $"Plugin EventBus contribution '{request.PluginId}' was stopped before activation completed.");
        }

        lock (_syncRoot)
        {
            committed = !_disposed && _started && ReferenceEquals(_applicationScope, applicationScope) &&
                        _pluginContributions.TryGetValue(request.PluginId, out var current) &&
                        ReferenceEquals(current, lease) && lease.State == EventBusContributionState.Active;
        }

        if (!committed)
        {
            lease.Dispose();
            throw new InvalidOperationException(
                $"Plugin EventBus contribution '{request.PluginId}' was stopped while activation was completing.");
        }

        return ValueTask.FromResult<IEventBusContributionLease>(lease);
    }

    private InvalidOperationException RejectPluginContribution(
        string pluginId,
        string message,
        string? contractId = null,
        string? channel = null)
    {
        WriteDiagnostic(
            EventDiagnosticIds.PluginContributionRejected,
            message,
            HostDiagnosticSeverity.Warning,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["pluginId"] = pluginId,
                ["contractId"] = contractId,
                ["channel"] = channel,
            });
        return new InvalidOperationException(message);
    }

    private void RemovePluginContribution(string pluginId, EventBusContributionLease lease)
    {
        lock (_syncRoot)
        {
            if (_pluginContributions.TryGetValue(pluginId, out var current) && ReferenceEquals(current, lease))
                _pluginContributions.Remove(pluginId);
        }
    }

    private Task RequestDisposal()
    {
        TaskCompletionSource? completion = null;

        lock (_syncRoot)
        {
            if (_disposeTask is not null)
            {
                return _disposeTask;
            }

            _disposed = true;
            _started = false;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
        }

        _ = DisposeCoreAsync(completion);
        return completion.Task;
    }

    private async Task DisposeCoreAsync(TaskCompletionSource completion)
    {
        EventSubscription[] subscriptions;
        EventChannelRuntime[] runtimes;
        EventBusContributionLease[] pluginContributions;

        lock (_syncRoot)
        {
            subscriptions = _subscriptions.Values.SelectMany(group => group).ToArray();
            runtimes = _channelRuntimes.Values.ToArray();
            pluginContributions = _pluginContributions.Values.ToArray();
            _subscriptions.Clear();
            _channelRuntimes.Clear();
            _pluginContributions.Clear();
            _applicationScope = null;
        }

        var failures = new List<Exception>();

        foreach (var contribution in pluginContributions)
        {
            try { await contribution.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { AddFailure(failures, exception); }
        }

        try
        {
            _shutdownCancellation.Cancel(throwOnFirstException: false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        foreach (var runtime in runtimes)
        {
            EventChannelRuntime.PublicationRequest[] pending;
            try
            {
                pending = runtime.Complete();
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
                continue;
            }

            foreach (var publication in pending)
            {
                try
                {
                    publication.Reject("The publication was canceled because the EventBus is shutting down.");
                }
                catch (Exception exception)
                {
                    AddFailure(failures, exception);
                }
            }
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        foreach (var runtime in runtimes)
        {
            try
            {
                await runtime.Completion.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                await subscription.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        foreach (var runtime in runtimes)
        {
            try
            {
                runtime.Dispose();
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        try
        {
            _shutdownCancellation.Dispose();
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        if (failures.Count == 0)
        {
            completion.TrySetResult();
        }
        else if (failures.Count == 1)
        {
            completion.TrySetException(failures[0]);
        }
        else
        {
            completion.TrySetException(new AggregateException(failures));
        }
    }

    /// <summary>
    /// Executes the get channel snapshots operation.
    /// </summary>
    public IReadOnlyList<EventChannelMetricsSnapshot> GetChannelSnapshots()
    {
        lock (_syncRoot)
        {
            return Array.AsReadOnly(
                _channelRuntimes.Values
                    .Select(runtime => runtime.GetSnapshot())
                    .OrderBy(snapshot => snapshot.ContractId.Value, StringComparer.Ordinal)
                    .ThenBy(snapshot => snapshot.ChannelName, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    /// <summary>
    /// Executes the get snapshot operation.
    /// </summary>
    public EventBusMetricsSnapshot GetSnapshot()
    {
        int activeSubscriptionCount;
        lock (_syncRoot)
        {
            activeSubscriptionCount = _subscriptions.Values.Sum(group =>
                group.Count(subscription => subscription.State == EventSubscriptionState.Active));
        }

        return new EventBusMetricsSnapshot(
            activeSubscriptionCount,
            Interlocked.Read(ref _publicationCount),
            Interlocked.Read(ref _deliverySucceededCount),
            Interlocked.Read(ref _deliveryFailedCount),
            Interlocked.Read(ref _deliveryCanceledCount),
            Interlocked.Read(ref _deliveryTimedOutCount),
            Interlocked.Read(ref _deliverySkippedCount),
            TimeSpan.FromTicks(Interlocked.Read(ref _totalHandlerDurationTicks)),
            _diagnostics.WriteFailureCount);
    }

    private void RecordDelivery(EventDeliveryStatus status, TimeSpan duration)
    {
        switch (status)
        {
            case EventDeliveryStatus.Succeeded:
                Interlocked.Increment(ref _deliverySucceededCount);
                break;
            case EventDeliveryStatus.Failed:
                Interlocked.Increment(ref _deliveryFailedCount);
                break;
            case EventDeliveryStatus.Canceled:
                Interlocked.Increment(ref _deliveryCanceledCount);
                break;
            case EventDeliveryStatus.TimedOut:
                Interlocked.Increment(ref _deliveryTimedOutCount);
                break;
            case EventDeliveryStatus.Skipped:
                Interlocked.Increment(ref _deliverySkippedCount);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Event delivery status is not supported.");
        }

        Interlocked.Add(ref _totalHandlerDurationTicks, duration.Ticks);
    }

    private EventChannelRuntime? GetOrCreateChannelRuntime(
        EventContractDescriptor descriptor,
        string channelName)
    {
        var key = new ChannelKey(descriptor.EventType, channelName);
        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (_channelRuntimes.TryGetValue(key, out var runtime))
            {
                return runtime;
            }

            if (_channelRuntimes.Count >= _runtimeOptions.MaximumChannelRuntimes)
            {
                return null;
            }

            runtime = new EventChannelRuntime(
                descriptor,
                channelName,
                _configuredChannels.GetValueOrDefault(key, _channelOptions),
                _shutdownCancellation.Token,
                _diagnostics);
            _channelRuntimes.Add(key, runtime);
            return runtime;
        }
    }

    private EventChannelRuntime.PublicationRequest CreatePublicationRequest<TEvent>(
        TEvent eventData,
        EventPublishOptions publishOptions,
        EventContractDescriptor descriptor,
        string channelName,
        Guid eventId,
        CancellationToken publisherToken,
        EventSubscription[] snapshot,
        bool waitForCompletion)
    {
        return new EventChannelRuntime.PublicationRequest(
            eventId,
            descriptor.ContractId,
            publishOptions.PartitionKey,
            publishOptions.CorrelationId ?? eventId.ToString("D"),
            publishOptions.CausationId,
            publishOptions.PublishDepth,
            async (shutdownToken, queueWaitDuration) =>
            {
                using var deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    publisherToken,
                    shutdownToken);
                return await PublishCoreAsync(
                        eventData,
                        publishOptions,
                        descriptor,
                        channelName,
                        eventId,
                        queueWaitDuration,
                        deliveryCancellation.Token,
                        snapshot)
                    .ConfigureAwait(false);
            },
            waitForCompletion,
            exception => ObservePostedFailure(
                descriptor,
                channelName,
                publishOptions,
                eventId,
                exception));
    }

    private void ObservePostedFailure(
        EventContractDescriptor descriptor,
        string channelName,
        EventPublishOptions publishOptions,
        Guid eventId,
        Exception exception)
    {
        var context = CreatePostedFailureDiagnosticContext(
            descriptor,
            channelName,
            publishOptions,
            eventId,
            exception);
        var subscriptionMessage = context.TryGetValue(SubscriptionIdContextKey, out var subscriptionId) &&
                                  !string.IsNullOrWhiteSpace(subscriptionId)
            ? $" subscription '{subscriptionId}'"
            : string.Empty;

        WriteDiagnostic(
            EventDiagnosticIds.EventDeliveryFailed,
            $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}'{subscriptionMessage} failed: {GetSafeExceptionMessage(exception)}",
            HostDiagnosticSeverity.Error,
            context);
    }

    private static EventPublicationRejectedException CreateRejectedPublicationException(
        Guid eventId,
        EventContractId contractId,
        EventChannelRuntime.EnqueueResult enqueueResult)
    {
        return new EventPublicationRejectedException(
            eventId,
            contractId,
            GetRejectionReason(enqueueResult, canceled: false));
    }

    private static string GetRejectionReason(
        EventChannelRuntime.EnqueueResult enqueueResult,
        bool canceled)
    {
        if (canceled)
        {
            return "Publication was canceled before it was accepted.";
        }

        return enqueueResult switch
        {
            EventChannelRuntime.EnqueueResult.Rejected =>
                "The event channel is full and its backpressure policy rejected the publication.",
            EventChannelRuntime.EnqueueResult.TimedOut =>
                "The event channel did not acquire capacity before its queue wait timeout elapsed.",
            EventChannelRuntime.EnqueueResult.Closed =>
                "The event channel is closed because the EventBus is shutting down.",
            _ => "The publication was not accepted."
        };
    }

    private static void AddFailure(List<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            failures.AddRange(aggregateException.Flatten().InnerExceptions);
            return;
        }

        failures.Add(exception);
    }

    private IEventSubscription SubscribeCore<TEvent>(
        LifecycleScope owner,
        string channelName,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions options,
        string? handlerTypeId = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfUnavailable();

        _ = ResolveContract<TEvent>("subscribe", channelName, eventId: null);

        var subscription = new EventSubscription(
            this,
            typeof(TEvent),
            channelName,
            owner.Id,
            handlerTypeId ?? handler.Target?.GetType().FullName ?? handler.Method.DeclaringType?.FullName ?? "<static-handler>",
            options,
            (eventData, delivery) =>
            {
                var context = new EventContext<TEvent>(
                    (TEvent)eventData,
                    delivery.ContractId,
                    delivery.EventId,
                    delivery.CorrelationId,
                    delivery.CausationId,
                    delivery.PublishedAt,
                    delivery.PublishDepth,
                    delivery.SubscriptionId,
                    delivery.DispatchPolicy,
                    delivery.CancellationToken);

                return handler(context);
            },
            _diagnostics,
            _backgroundScheduler);

        subscription.BindOwner(owner.CancellationToken);

        try
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                if (owner.State != LifecycleScopeState.Running || owner.CancellationToken.IsCancellationRequested)
                {
                    throw new InvalidOperationException("Event subscription owner scope must be running.");
                }

                if (!subscription.TryMarkActive())
                {
                    throw new InvalidOperationException("Event subscription owner scope stopped during registration.");
                }

                var key = new ChannelKey(subscription.EventType, subscription.ChannelName);
                if (!_subscriptions.TryGetValue(key, out var subscriptions))
                {
                    subscriptions = [];
                    _subscriptions[key] = subscriptions;
                }

                subscriptions.Add(subscription);
            }
        }
        catch
        {
            subscription.Dispose();
            throw;
        }

        WriteDiagnostic(
            EventDiagnosticIds.EventSubscriptionAdded,
            $"Event subscription '{subscription.Id}' was added.",
            HostDiagnosticSeverity.Trace,
            CreateSubscriptionDiagnosticContext(subscription));

        return subscription;
    }

    private static EventPublishOptions NormalizeAndValidatePublishOptions(EventPublishOptions? options)
    {
        var publishOptions = options ?? EventPublishOptions.Default;
        if (publishOptions.PublishDepth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EventPublishOptions.PublishDepth),
                publishOptions.PublishDepth,
                "Event publish depth cannot be negative.");
        }

        var ambient = CurrentPublication.Value;
        if (ambient is null)
        {
            return publishOptions;
        }

        return new EventPublishOptions
        {
            CorrelationId = publishOptions.CorrelationId ?? ambient.CorrelationId,
            CausationId = publishOptions.CausationId ?? ambient.EventId.ToString("D"),
            PublishDepth = publishOptions.PublishDepth == 0
                ? checked(ambient.PublishDepth + 1)
                : publishOptions.PublishDepth,
            PartitionKey = publishOptions.PartitionKey
        };
    }

    private EventSubscription[] GetSnapshot(Type eventType, string channelName)
    {
        var key = new ChannelKey(eventType, channelName);
        lock (_syncRoot)
        {
            return _subscriptions.TryGetValue(key, out var subscriptions)
                ? subscriptions
                    .Where(subscription => subscription.State == EventSubscriptionState.Active)
                    .ToArray()
                : [];
        }
    }

    private void Remove(EventSubscription subscription)
    {
        var key = new ChannelKey(subscription.EventType, subscription.ChannelName);
        lock (_syncRoot)
        {
            if (!_subscriptions.TryGetValue(key, out var subscriptions))
            {
                return;
            }

            subscriptions.Remove(subscription);
            if (subscriptions.Count == 0)
            {
                _subscriptions.Remove(key);
            }
        }
    }

    private void WriteDiagnostic(
        string code,
        string message,
        HostDiagnosticSeverity severity,
        IReadOnlyDictionary<string, string?>? context = null,
        bool sampledTrace = false,
        Guid eventId = default)
    {
        var record = new HostDiagnosticRecord(code, message, severity);
        if (context is not null)
        {
            record = record with { Context = context };
        }

        _diagnostics.Write(record, sampledTrace, eventId);
    }

    private static IReadOnlyDictionary<string, string?> CreateEventDiagnosticContext(
        EventContractDescriptor descriptor,
        Guid eventId,
        string channelName,
        EventPublishOptions publishOptions,
        EventChannelBackpressurePolicy? backpressurePolicy = null)
    {
        var context = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ContractIdContextKey] = descriptor.ContractId.Value,
            [EventIdContextKey] = eventId.ToString("D"),
            [EventTypeContextKey] = descriptor.EventType.FullName,
            [ChannelContextKey] = channelName,
            [PartitionHashContextKey] = EventCorrelationIds.ToDiagnosticHash(publishOptions.PartitionKey),
            ["correlationId"] = publishOptions.CorrelationId ?? eventId.ToString("D"),
            ["causationId"] = publishOptions.CausationId,
            ["publishDepth"] = publishOptions.PublishDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (backpressurePolicy is not null)
        {
            context["backpressurePolicy"] = backpressurePolicy.Value.ToString();
        }

        return context;
    }

    private IReadOnlyDictionary<string, string?> CreatePublishedEventDiagnosticContext(
        EventContractDescriptor descriptor,
        Guid eventId,
        string channelName,
        EventPublishOptions publishOptions,
        object eventData,
        TimeSpan queueWaitDuration)
    {
        var baseContext = CreateEventDiagnosticContext(descriptor, eventId, channelName, publishOptions);
        var context = new Dictionary<string, string?>(baseContext, StringComparer.Ordinal)
        {
            ["queueWaitDurationMs"] = queueWaitDuration.TotalMilliseconds.ToString(
                "F3",
                System.Globalization.CultureInfo.InvariantCulture)
        };
        if (!_diagnosticsOptions.EnablePayloadProjection ||
            !_payloadProjectors.TryGetValue(descriptor.EventType, out var projector))
        {
            return context;
        }

        EventPayloadDiagnosticSnapshot snapshot;
        try
        {
            snapshot = projector.Project(eventData);
        }
        catch (Exception exception)
        {
            _diagnostics.Write(
                new HostDiagnosticRecord(
                    EventDiagnosticIds.EventPayloadProjectionFailed,
                    $"The safe payload projector failed for contract '{descriptor.ContractId.Value}' event '{eventId:D}': {GetSafeExceptionMessage(exception)}",
                    HostDiagnosticSeverity.Warning)
                {
                    Context = CreateExceptionDiagnosticContext(context, exception)
                });
            return context;
        }

        context["payloadSchemaVersion"] = snapshot.SchemaVersion;
        context["payloadSizeEstimate"] = snapshot.SizeEstimate?.ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (var field in snapshot.Fields
                     .OrderBy(field => field.Key, StringComparer.Ordinal)
                     .Take(_diagnosticsOptions.MaximumPayloadFieldCount))
        {
            context[$"payload.{field.Key}"] = TruncateDiagnosticValue(
                field.Value,
                _diagnosticsOptions.MaximumPayloadValueLength);
        }

        return context;
    }

    private static IReadOnlyDictionary<string, string?> CreateSubscriptionDiagnosticContext(
        EventSubscription subscription)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SubscriptionIdContextKey] = subscription.Id.ToString(),
            [EventTypeContextKey] = subscription.EventType.FullName,
            ["channel"] = subscription.ChannelName,
            ["ownerScopeId"] = subscription.OwnerScopeId,
            ["handlerTypeId"] = subscription.HandlerTypeId,
            ["dispatchTarget"] = subscription.Options.DispatchPolicy.ToString(),
            [DispatchPolicyContextKey] = subscription.Options.DispatchPolicy.ToString(),
            [ErrorPolicyContextKey] = subscription.Options.ErrorPolicy.ToString()
        };
    }

    private static IReadOnlyDictionary<string, string?> CreateDeliveryDiagnosticContext(
        EventContractDescriptor descriptor,
        Guid eventId,
        EventSubscription subscription,
        string correlationId,
        string? causationId,
        int publishDepth,
        TimeSpan? handlerDuration = null,
        EventDeliveryStatus? deliveryResult = null,
        string? cancellationSource = null)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ContractIdContextKey] = descriptor.ContractId.Value,
            [EventIdContextKey] = eventId.ToString("D"),
            [SubscriptionIdContextKey] = subscription.Id.ToString(),
            [EventTypeContextKey] = descriptor.EventType.FullName,
            [DispatchPolicyContextKey] = subscription.Options.DispatchPolicy.ToString(),
            [ErrorPolicyContextKey] = subscription.Options.ErrorPolicy.ToString(),
            ["correlationId"] = correlationId,
            ["causationId"] = causationId,
            ["publishDepth"] = publishDepth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ownerScopeId"] = subscription.OwnerScopeId,
            ["handlerTypeId"] = subscription.HandlerTypeId,
            ["dispatchTarget"] = subscription.Options.DispatchPolicy.ToString(),
            ["handlerDurationMs"] = handlerDuration?.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            ["deliveryResult"] = deliveryResult?.ToString(),
            ["cancellationSource"] = cancellationSource
        };
    }

    private static IReadOnlyDictionary<string, string?> CreateExceptionDiagnosticContext(
        IReadOnlyDictionary<string, string?> context,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var result = new Dictionary<string, string?>(context, StringComparer.Ordinal)
        {
            ["exceptionType"] = GetSafeExceptionType(exception),
            ["exceptionMessage"] = GetSafeExceptionMessage(exception),
            ["exceptionStack"] = GetSafeExceptionStack(exception)
        };
        return result;
    }

    private static string? TruncateDiagnosticValue(string? value, int maximumLength)
    {
        return value is null || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
    }

    private static string GetSafeExceptionType(Exception exception)
    {
        try
        {
            return exception.GetType().FullName ?? exception.GetType().Name;
        }
        catch
        {
            return "<unavailable-exception-type>";
        }
    }

    private static string GetSafeExceptionMessage(Exception exception)
    {
        try
        {
            return TruncateDiagnosticValue(exception.Message, 1024) ?? "<no-message>";
        }
        catch
        {
            return "<unavailable-exception-message>";
        }
    }

    private static string? GetSafeExceptionStack(Exception exception)
    {
        try
        {
            return TruncateDiagnosticValue(exception.StackTrace, 4096);
        }
        catch
        {
            return "<unavailable-exception-stack>";
        }
    }

    private static IReadOnlyDictionary<string, string?> CreatePostedFailureDiagnosticContext(
        EventContractDescriptor descriptor,
        string channelName,
        EventPublishOptions publishOptions,
        Guid eventId,
        Exception exception)
    {
        var context = new Dictionary<string, string?>(
            CreateEventDiagnosticContext(descriptor, eventId, channelName, publishOptions),
            StringComparer.Ordinal);

        if (exception.Data[DeliveryExceptionSubscriptionIdDataKey] is string subscriptionId)
        {
            context[SubscriptionIdContextKey] = subscriptionId;
        }

        if (exception.Data[DeliveryExceptionContractIdDataKey] is string contractId)
        {
            context[ContractIdContextKey] = contractId;
        }

        if (exception.Data[DeliveryExceptionEventIdDataKey] is string deliveredEventId)
        {
            context[EventIdContextKey] = deliveredEventId;
        }

        return context;
    }

    private static void AttachDeliveryContextToException(
        Exception exception,
        EventContractId contractId,
        Guid eventId,
        EventSubscriptionId subscriptionId)
    {
        exception.Data[DeliveryExceptionContractIdDataKey] = contractId.Value;
        exception.Data[DeliveryExceptionEventIdDataKey] = eventId.ToString("D");
        exception.Data[DeliveryExceptionSubscriptionIdDataKey] = subscriptionId.ToString();
    }

    private void ThrowIfUnavailable()
    {
        if (Volatile.Read(ref _disposed))
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (Volatile.Read(ref _hostLifecycleRequired) && !Volatile.Read(ref _started))
        {
            throw new InvalidOperationException(
                "The EventBus is managed by City Host and has not entered its running lifecycle.");
        }
    }

    private EventContractDescriptor ResolveContract<TEvent>(
        string operation,
        string channelName,
        Guid? eventId)
    {
        try
        {
            return _contractRegistry.GetOrCreate<TEvent>();
        }
        catch (InvalidOperationException exception)
        {
            var context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [EventTypeContextKey] = typeof(TEvent).FullName,
                [ChannelContextKey] = channelName,
                ["operation"] = operation
            };

            if (eventId is not null)
            {
                context[EventIdContextKey] = eventId.Value.ToString("D");
            }

            WriteDiagnostic(
                EventDiagnosticIds.EventContractRejected,
                $"Event contract for type '{typeof(TEvent).FullName}' was rejected during {operation}: {GetSafeExceptionMessage(exception)}",
                HostDiagnosticSeverity.Warning,
                context);
            throw;
        }
    }

    private readonly record struct DeliveryContext(
        EventContractId ContractId,
        Guid EventId,
        string CorrelationId,
        string? CausationId,
        DateTimeOffset PublishedAt,
        int PublishDepth,
        EventSubscriptionId SubscriptionId,
        EventDispatchPolicy DispatchPolicy,
        CancellationToken CancellationToken);

    private sealed record AmbientPublicationContext(
        Guid EventId,
        string CorrelationId,
        int PublishDepth);

    private readonly record struct PendingDelivery(
        int Index,
        EventSubscription Subscription,
        Task<EventDeliveryResult?> Operation);

    private readonly record struct ChannelKey(Type EventType, string ChannelName);

    private sealed class EventSubscription : IEventSubscription
    {
        private InMemoryEventBus? _eventBus;
        private Func<object, DeliveryContext, ValueTask>? _handler;
        private EventDiagnosticWriter? _diagnostics;
        private readonly IEventBackgroundScheduler _backgroundScheduler;
        private readonly SemaphoreSlim _serialGate = new(1, 1);
        private readonly object _stateGate = new();
        private readonly CancellationTokenSource _subscriptionCancellation = new();
        private CancellationTokenRegistration _ownerCancellation;
        private TaskCompletionSource? _drainCompletion;
        private int _inFlightCount;
        private bool _ownerBound;
        private EventSubscriptionState _state = EventSubscriptionState.Created;
        private Task? _terminationTask;
        private int _consecutiveFailures;

        public EventSubscription(
            InMemoryEventBus eventBus,
            Type eventType,
            string channelName,
            string ownerScopeId,
            string handlerTypeId,
            EventSubscriptionOptions options,
            Func<object, DeliveryContext, ValueTask> handler,
            EventDiagnosticWriter diagnostics,
            IEventBackgroundScheduler backgroundScheduler)
        {
            _eventBus = eventBus;
            EventType = eventType;
            ChannelName = channelName;
            OwnerScopeId = ownerScopeId;
            HandlerTypeId = handlerTypeId;
            Options = options;
            _handler = handler;
            _diagnostics = diagnostics;
            _backgroundScheduler = backgroundScheduler;
            Id = EventSubscriptionId.New();
        }

        public EventSubscriptionId Id { get; }

        public Type EventType { get; }

        public string ChannelName { get; }

        public string OwnerScopeId { get; }

        public string HandlerTypeId { get; }

        public EventSubscriptionOptions Options { get; }

        public EventSubscriptionState State
        {
            get
            {
                lock (_stateGate)
                {
                    return _state;
                }
            }
        }

        public void BindOwner(CancellationToken ownerCancellationToken)
        {
            var registration = ownerCancellationToken.Register(
                static state => ((EventSubscription)state!).RequestTermination(),
                this);
            var disposeRegistration = false;

            lock (_stateGate)
            {
                if (_ownerBound)
                {
                    registration.Dispose();
                    throw new InvalidOperationException("Event subscription already has an owner.");
                }

                _ownerBound = true;
                _ownerCancellation = registration;
                disposeRegistration = _terminationTask is not null;
            }

            if (disposeRegistration)
            {
                registration.Dispose();
            }
        }

        public bool TryMarkActive()
        {
            lock (_stateGate)
            {
                if (_state != EventSubscriptionState.Created || _terminationTask is not null)
                {
                    return false;
                }

                _state = EventSubscriptionState.Active;
                return true;
            }
        }

        public async ValueTask<EventDeliveryResult?> DeliverAsync(
            object eventData,
            EventContractDescriptor descriptor,
            Guid eventId,
            string correlationId,
            string? causationId,
            DateTimeOffset publishedAt,
            int publishDepth,
            CancellationToken cancellationToken)
        {
            if (!TryBeginDelivery())
            {
                return null;
            }

            var serialGateAcquired = false;
            var completionDeferred = false;
            try
            {
                _diagnostics!.Write(
                    new HostDiagnosticRecord(
                        EventDiagnosticIds.EventDeliveryStarted,
                        $"Event handler '{Id}' started delivery for contract '{descriptor.ContractId.Value}' event '{eventId:D}'.",
                        HostDiagnosticSeverity.Trace)
                    {
                        Context = CreateDeliveryDiagnosticContext(
                            descriptor,
                            eventId,
                            this,
                            correlationId,
                            causationId,
                            publishDepth)
                    },
                    sampledTrace: true,
                    eventId);

                using var deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _subscriptionCancellation.Token);
                using var timeoutCancellation = new CancellationTokenSource();
                if (Options.HandlerTimeout is { } handlerTimeout)
                {
                    timeoutCancellation.CancelAfter(handlerTimeout);
                }

                using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    deliveryCancellation.Token,
                    timeoutCancellation.Token);
                var deliveryCancellationToken = combinedCancellation.Token;

                if (Options.DispatchPolicy == EventDispatchPolicy.Serialized)
                {
                    try
                    {
                        await _serialGate.WaitAsync(deliveryCancellationToken).ConfigureAwait(false);
                        serialGateAcquired = true;
                    }
                    catch (OperationCanceledException exception) when (deliveryCancellationToken.IsCancellationRequested)
                    {
                        return ReportCanceledDelivery(
                            descriptor,
                            eventId,
                            correlationId,
                            causationId,
                            publishDepth,
                            timeoutCancellation.IsCancellationRequested &&
                            !deliveryCancellation.IsCancellationRequested,
                            GetSafeExceptionMessage(exception),
                            TimeSpan.Zero);
                    }
                }

                var outcome = await DispatchAsync(
                        eventData,
                        descriptor,
                        eventId,
                        correlationId,
                        causationId,
                        publishedAt,
                        publishDepth,
                        deliveryCancellationToken,
                        deliveryCancellation.Token,
                        timeoutCancellation.Token)
                    .ConfigureAwait(false);

                if (outcome.LingeringOperation is not null)
                {
                    completionDeferred = true;
                    _ = ObserveTimedOutOperationAsync(
                        outcome.LingeringOperation,
                        descriptor,
                        eventId,
                        correlationId,
                        causationId,
                        publishDepth,
                        serialGateAcquired);
                    serialGateAcquired = false;
                }

                return outcome.Result;
            }
            finally
            {
                if (!completionDeferred && serialGateAcquired)
                {
                    _serialGate.Release();
                }

                if (!completionDeferred)
                {
                    EndDelivery();
                }
            }
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateGate)
            {
                if (_state == EventSubscriptionState.Disposed)
                {
                    return;
                }
            }

            var terminationTask = RequestTermination();
            await terminationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _ = RequestTermination();
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(RequestTermination());
        }

        private Task RequestTermination()
        {
            TaskCompletionSource? completion = null;
            Task terminationTask;

            lock (_stateGate)
            {
                if (_terminationTask is not null)
                {
                    return _terminationTask;
                }

                if (_state == EventSubscriptionState.Disposed)
                {
                    _terminationTask = Task.CompletedTask;
                    return _terminationTask;
                }

                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _terminationTask = completion.Task;
                _state = EventSubscriptionState.Quiescing;
                terminationTask = _terminationTask;
            }

            try
            {
                _ = Task.Run(() => TerminateCoreAsync(completion));
            }
            catch (Exception exception)
            {
                lock (_stateGate)
                {
                    _state = EventSubscriptionState.Faulted;
                }

                completion.TrySetException(exception);
            }

            return terminationTask;
        }

        private async Task TerminateCoreAsync(TaskCompletionSource completion)
        {
            var failures = new List<Exception>();
            Task? drainTask;
            var eventBus = _eventBus;
            var diagnostics = _diagnostics;

            try
            {
                eventBus?.Remove(this);
                diagnostics?.Write(
                    new HostDiagnosticRecord(
                        EventDiagnosticIds.EventSubscriptionQuiescing,
                        $"Event subscription '{Id}' stopped accepting new deliveries.",
                        HostDiagnosticSeverity.Trace)
                    {
                        Context = CreateSubscriptionDiagnosticContext(this)
                    });
            }
            catch (Exception exception)
            {
                AddTerminationFailure(failures, exception);
            }

            try
            {
                if (!_subscriptionCancellation.IsCancellationRequested)
                {
                    _subscriptionCancellation.Cancel(throwOnFirstException: false);
                }
            }
            catch (Exception exception)
            {
                AddTerminationFailure(failures, exception);
            }

            lock (_stateGate)
            {
                if (_inFlightCount > 0)
                {
                    _state = EventSubscriptionState.Draining;
                    _drainCompletion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    drainTask = _drainCompletion.Task;
                }
                else
                {
                    drainTask = null;
                }
            }

            if (drainTask is not null)
            {
                await drainTask.ConfigureAwait(false);
            }

            try
            {
                _ownerCancellation.Dispose();
            }
            catch (Exception exception)
            {
                AddTerminationFailure(failures, exception);
            }

            try
            {
                _subscriptionCancellation.Dispose();
            }
            catch (Exception exception)
            {
                AddTerminationFailure(failures, exception);
            }

            try
            {
                _serialGate.Dispose();
            }
            catch (Exception exception)
            {
                AddTerminationFailure(failures, exception);
            }

            _handler = null;
            _eventBus = null;
            _diagnostics = null;

            if (failures.Count == 0)
            {
                try
                {
                    diagnostics?.Write(
                        new HostDiagnosticRecord(
                            EventDiagnosticIds.EventSubscriptionDisposed,
                            $"Event subscription '{Id}' was disposed.",
                            HostDiagnosticSeverity.Trace)
                        {
                            Context = CreateSubscriptionDiagnosticContext(this)
                        });
                }
                catch (Exception exception)
                {
                    AddTerminationFailure(failures, exception);
                }
            }
            else
            {
                try
                {
                    diagnostics?.Write(
                        new HostDiagnosticRecord(
                            EventDiagnosticIds.EventSubscriptionTerminationFailed,
                            $"Event subscription '{Id}' termination failed: {GetSafeExceptionMessage(failures[0])}",
                            HostDiagnosticSeverity.Error)
                        {
                            Context = CreateSubscriptionDiagnosticContext(this)
                        });
                }
                catch (Exception exception)
                {
                    AddTerminationFailure(failures, exception);
                }
            }

            lock (_stateGate)
            {
                _state = failures.Count == 0
                    ? EventSubscriptionState.Disposed
                    : EventSubscriptionState.Faulted;
            }

            if (failures.Count == 0)
            {
                completion.TrySetResult();
            }
            else if (failures.Count == 1)
            {
                completion.TrySetException(failures[0]);
            }
            else
            {
                completion.TrySetException(new AggregateException(failures));
            }
        }

        private static void AddTerminationFailure(List<Exception> failures, Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                failures.AddRange(aggregateException.Flatten().InnerExceptions);
                return;
            }

            failures.Add(exception);
        }

        private async ValueTask<DispatchOutcome> DispatchAsync(
            object eventData,
            EventContractDescriptor descriptor,
            Guid eventId,
            string correlationId,
            string? causationId,
            DateTimeOffset publishedAt,
            int publishDepth,
            CancellationToken cancellationToken,
            CancellationToken nonTimeoutCancellationToken,
            CancellationToken timeoutToken)
        {
            Task? dispatchOperation = null;
            var handlerStartedAt = Stopwatch.GetTimestamp();
            try
            {
                var delivery = new DeliveryContext(
                    descriptor.ContractId,
                    eventId,
                    correlationId,
                    causationId,
                    publishedAt,
                    publishDepth,
                    Id,
                    Options.DispatchPolicy,
                    cancellationToken);

                dispatchOperation = DispatchCoreAsync(eventData, delivery, cancellationToken).AsTask();
                if (Options.HandlerTimeout is not null)
                {
                    await dispatchOperation.WaitAsync(timeoutToken).ConfigureAwait(false);
                }
                else
                {
                    await dispatchOperation.ConfigureAwait(false);
                }

                Interlocked.Exchange(ref _consecutiveFailures, 0);

                var duration = Stopwatch.GetElapsedTime(handlerStartedAt);
                var result = new EventDeliveryResult(
                    Id,
                    Options.DispatchPolicy,
                    Succeeded: true)
                {
                    Duration = duration
                };
                _eventBus?.RecordDelivery(result.Status, duration);
                _diagnostics!.Write(
                    new HostDiagnosticRecord(
                        EventDiagnosticIds.EventDeliveryCompleted,
                        $"Event handler '{Id}' completed delivery for contract '{descriptor.ContractId.Value}' event '{eventId:D}'.",
                        HostDiagnosticSeverity.Trace)
                    {
                        Context = CreateDeliveryDiagnosticContext(
                            descriptor,
                            eventId,
                            this,
                            correlationId,
                            causationId,
                            publishDepth,
                            duration,
                            result.Status)
                    },
                    sampledTrace: true,
                    eventId);

                return new DispatchOutcome(
                    result,
                    LingeringOperation: null);
            }
            catch (OperationCanceledException exception) when (
                cancellationToken.IsCancellationRequested || timeoutToken.IsCancellationRequested)
            {
                var timedOut = timeoutToken.IsCancellationRequested &&
                               !nonTimeoutCancellationToken.IsCancellationRequested;
                var duration = Stopwatch.GetElapsedTime(handlerStartedAt);

                return new DispatchOutcome(
                    ReportCanceledDelivery(
                        descriptor,
                        eventId,
                        correlationId,
                        causationId,
                        publishDepth,
                        timedOut,
                        GetSafeExceptionMessage(exception),
                        duration),
                    timedOut && dispatchOperation is { IsCompleted: false }
                        ? dispatchOperation
                        : null);
            }
            catch (Exception exception)
            {
                var duration = Stopwatch.GetElapsedTime(handlerStartedAt);
                _diagnostics!.Write(
                    new HostDiagnosticRecord(
                        EventDiagnosticIds.EventDeliveryFailed,
                        $"Event handler '{Id}' failed for contract '{descriptor.ContractId.Value}' event '{eventId:D}' subscription '{Id}': {GetSafeExceptionMessage(exception)}",
                        HostDiagnosticSeverity.Error)
                    {
                        Context = CreateExceptionDiagnosticContext(
                            CreateDeliveryDiagnosticContext(
                                descriptor,
                                eventId,
                                this,
                                correlationId,
                                causationId,
                                publishDepth,
                                duration,
                                EventDeliveryStatus.Failed),
                            exception)
                    });

                _eventBus?.RecordDelivery(EventDeliveryStatus.Failed, duration);

                if (Options.ErrorPolicy == EventErrorPolicy.FailPublisher)
                {
                    AttachDeliveryContextToException(exception, descriptor.ContractId, eventId, Id);

                    throw;
                }

                TryDisableAfterFailure(descriptor, eventId, correlationId, causationId, publishDepth);

                return new DispatchOutcome(
                    new EventDeliveryResult(
                        Id,
                        Options.DispatchPolicy,
                        Succeeded: false,
                        GetSafeExceptionMessage(exception))
                    {
                        Duration = duration
                    },
                    LingeringOperation: null);
            }
        }

        private async Task ObserveTimedOutOperationAsync(
            Task operation,
            EventContractDescriptor descriptor,
            Guid eventId,
            string correlationId,
            string? causationId,
            int publishDepth,
            bool serialGateAcquired)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The timeout has already been reported as the delivery result.
            }
            catch (Exception exception)
            {
                try
                {
                    _diagnostics!.Write(
                        new HostDiagnosticRecord(
                            EventDiagnosticIds.EventDeliveryFailed,
                            $"Timed-out event handler '{Id}' later failed for contract '{descriptor.ContractId.Value}' event '{eventId:D}': {GetSafeExceptionMessage(exception)}",
                            HostDiagnosticSeverity.Error)
                        {
                            Context = CreateExceptionDiagnosticContext(
                                CreateDeliveryDiagnosticContext(
                                    descriptor,
                                    eventId,
                                    this,
                                    correlationId,
                                    causationId,
                                    publishDepth,
                                    deliveryResult: EventDeliveryStatus.TimedOut),
                                exception)
                        });
                }
                catch
                {
                    // AUC-EVENTBUS-005 owns diagnostics sink failure isolation.
                }
            }
            finally
            {
                if (serialGateAcquired)
                {
                    _serialGate.Release();
                }

                EndDelivery();
            }
        }

        private void TryDisableAfterFailure(
            EventContractDescriptor descriptor,
            Guid eventId,
            string correlationId,
            string? causationId,
            int publishDepth)
        {
            if (Options.ErrorPolicy != EventErrorPolicy.DisableSubscription ||
                Interlocked.Increment(ref _consecutiveFailures) != Options.DisableSubscriptionAfterFailures)
            {
                return;
            }

            _diagnostics!.Write(
                new HostDiagnosticRecord(
                    EventDiagnosticIds.EventSubscriptionDisabled,
                    $"Event subscription '{Id}' was disabled after repeated handler failures.",
                    HostDiagnosticSeverity.Warning)
                {
                    Context = CreateDeliveryDiagnosticContext(
                        descriptor,
                        eventId,
                        this,
                        correlationId,
                        causationId,
                        publishDepth,
                        deliveryResult: EventDeliveryStatus.Failed)
                });
            _ = RequestTermination();
        }

        private async ValueTask DispatchCoreAsync(
            object eventData,
            DeliveryContext delivery,
            CancellationToken cancellationToken)
        {
            var handler = _handler ?? throw new ObjectDisposedException(nameof(EventSubscription));

            async ValueTask InvokeHandlerAsync(
                object dispatchedEvent,
                DeliveryContext dispatchedDelivery)
            {
                var previousPublication = CurrentPublication.Value;
                CurrentPublication.Value = new AmbientPublicationContext(
                    dispatchedDelivery.EventId,
                    dispatchedDelivery.CorrelationId,
                    dispatchedDelivery.PublishDepth);
                try
                {
                    await handler(dispatchedEvent, dispatchedDelivery).ConfigureAwait(false);
                }
                finally
                {
                    CurrentPublication.Value = previousPublication;
                }
            }

            switch (Options.DispatchPolicy)
            {
                case EventDispatchPolicy.UiThread:
                    if (Options.UiDispatcher is null)
                    {
                        throw new InvalidOperationException("UI dispatcher subscription requires a dispatcher.");
                    }

                    await DispatchToUiAsync(InvokeHandlerAsync, eventData, delivery, cancellationToken).ConfigureAwait(false);
                    break;
                case EventDispatchPolicy.Background:
                    await _backgroundScheduler.RunAsync(
                            token => InvokeHandlerAsync(eventData, delivery with { CancellationToken = token }),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    await InvokeHandlerAsync(eventData, delivery).ConfigureAwait(false);
                    break;
            }
        }

        private async ValueTask DispatchToUiAsync(
            Func<object, DeliveryContext, ValueTask> handler,
            object eventData,
            DeliveryContext delivery,
            CancellationToken cancellationToken)
        {
            var dispatcher = Options.UiDispatcher!;
            if (Options.DispatchMode == EventDispatchMode.InlineIfAllowed && dispatcher.CheckAccess())
            {
                await handler(eventData, delivery).ConfigureAwait(false);
                return;
            }

            if (Options.DispatchMode == EventDispatchMode.Post)
            {
                await Task.Yield();
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(
                static state => ((TaskCompletionSource)state!).TrySetCanceled(),
                completion);

            try
            {
                await dispatcher.PostAsync(
                        async token =>
                        {
                            try
                            {
                                token.ThrowIfCancellationRequested();
                                await handler(eventData, delivery with { CancellationToken = token }).ConfigureAwait(false);
                                completion.TrySetResult();
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested)
                            {
                                completion.TrySetCanceled(token);
                                throw;
                            }
                            catch (Exception exception)
                            {
                                completion.TrySetException(exception);
                                throw;
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                completion.TrySetCanceled();
                if (completion.Task.IsFaulted)
                {
                    _ = completion.Task.Exception;
                }

                throw;
            }

            await completion.Task.ConfigureAwait(false);
        }

        private EventDeliveryResult CreateCanceledDeliveryResult(
            bool timedOut,
            string? message = null)
        {
            return new EventDeliveryResult(
                Id,
                Options.DispatchPolicy,
                Succeeded: false,
                message ?? (timedOut ? "Event handler execution timed out." : "Event handler delivery was canceled."),
                Canceled: true)
            {
                TimedOut = timedOut
            };
        }

        private EventDeliveryResult ReportCanceledDelivery(
            EventContractDescriptor descriptor,
            Guid eventId,
            string correlationId,
            string? causationId,
            int publishDepth,
            bool timedOut,
            string? message,
            TimeSpan duration)
        {
            _diagnostics!.Write(
                new HostDiagnosticRecord(
                    timedOut
                        ? EventDiagnosticIds.EventDeliveryTimedOut
                        : EventDiagnosticIds.EventDeliveryCancelled,
                    timedOut
                        ? $"Event handler '{Id}' timed out for contract '{descriptor.ContractId.Value}' event '{eventId:D}' subscription '{Id}'."
                        : $"Event handler '{Id}' was cancelled for contract '{descriptor.ContractId.Value}' event '{eventId:D}' subscription '{Id}': {message}",
                    timedOut ? HostDiagnosticSeverity.Warning : HostDiagnosticSeverity.Trace)
                {
                    Context = CreateDeliveryDiagnosticContext(
                        descriptor,
                        eventId,
                        this,
                        correlationId,
                        causationId,
                        publishDepth,
                        duration,
                        timedOut ? EventDeliveryStatus.TimedOut : EventDeliveryStatus.Canceled,
                        timedOut
                            ? "handlerTimeout"
                            : _subscriptionCancellation.IsCancellationRequested
                                ? "subscription"
                                : "publication")
                });

            _eventBus?.RecordDelivery(
                timedOut ? EventDeliveryStatus.TimedOut : EventDeliveryStatus.Canceled,
                duration);

            if (timedOut)
            {
                TryDisableAfterFailure(descriptor, eventId, correlationId, causationId, publishDepth);
            }

            return CreateCanceledDeliveryResult(timedOut, message) with { Duration = duration };
        }

        private readonly record struct DispatchOutcome(
            EventDeliveryResult Result,
            Task? LingeringOperation);

        private bool TryBeginDelivery()
        {
            lock (_stateGate)
            {
                if (_state != EventSubscriptionState.Active)
                {
                    return false;
                }

                _inFlightCount++;

                return true;
            }
        }

        private void EndDelivery()
        {
            TaskCompletionSource? drainCompletion = null;

            lock (_stateGate)
            {
                _inFlightCount--;

                if (_inFlightCount == 0 &&
                    (_state == EventSubscriptionState.Quiescing ||
                     _state == EventSubscriptionState.Draining))
                {
                    drainCompletion = _drainCompletion;
                }
            }

            drainCompletion?.TrySetResult();
        }
    }
}
