using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Threading;
using AtomUI.City.EventBus;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodEventWorkload : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IEventBusMonitor _busMonitor;
    private readonly IEventChannelMonitor _channelMonitor;
    private readonly IEventContractRegistry _contracts;
    private readonly IApplicationStateWriter _stateWriter;
    private readonly IUiDispatcher _dispatcher;
    private readonly List<LifecycleScope> _owners = [];
    private int _initialized;

    public DogfoodEventWorkload(
        IEventBus eventBus,
        IEventBusMonitor busMonitor,
        IEventChannelMonitor channelMonitor,
        IEventContractRegistry contracts,
        IApplicationStateWriter stateWriter,
        IUiDispatcher dispatcher)
    {
        _eventBus = eventBus;
        _busMonitor = busMonitor;
        _channelMonitor = channelMonitor;
        _contracts = contracts;
        _stateWriter = stateWriter;
        _dispatcher = dispatcher;
    }

    public async Task InitializeAsync(LifecycleScope windowScope, Action<string> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(windowScope);
        ArgumentNullException.ThrowIfNull(updateStatus);
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            throw new InvalidOperationException("The EventBus workload can only be initialized once.");
        }

        var projectionOwner = windowScope.CreateChild(LifecycleScopeKind.Subscription, "dogfood-event-projections");
        var auditOwner = windowScope.CreateChild(LifecycleScopeKind.Subscription, "dogfood-event-audit");
        _owners.Add(projectionOwner);
        _owners.Add(auditOwner);

        var cases = CreateCases();
        if (cases.Count != 72 || cases.Select(static item => item.EventType).Distinct().Count() != 72)
        {
            throw new InvalidOperationException("The EventBus workload must contain 72 unique event types.");
        }

        var generatedContracts = _contracts.Descriptors
            .Count(descriptor => descriptor.ContractId.Value.StartsWith("dogfood.", StringComparison.Ordinal));
        if (generatedContracts != 72)
        {
            throw new InvalidOperationException($"Expected 72 generated Event contracts; observed {generatedContracts}.");
        }

        foreach (var item in cases)
        {
            await item.RunAsync(
                _eventBus,
                projectionOwner,
                auditOwner,
                _stateWriter,
                _dispatcher).ConfigureAwait(false);
        }

        var metrics = _busMonitor.GetSnapshot();
        var channels = _channelMonitor.GetChannelSnapshots();
        if (metrics.PublicationCount != 72 ||
            metrics.DeliverySucceededCount < 145 ||
            metrics.ActiveSubscriptionCount < 145 ||
            channels.Count != 72 ||
            ApplicationReadyAuditHandler.CallCount != 1)
        {
            throw new InvalidOperationException(
                $"EventBus metrics mismatch: publications={metrics.PublicationCount}, deliveries={metrics.DeliverySucceededCount}, subscriptions={metrics.ActiveSubscriptionCount}, channels={channels.Count}, static={ApplicationReadyAuditHandler.CallCount}.");
        }

        await _dispatcher.InvokeAsync(
            () => updateStatus("72 event contracts delivered through 12 channel profiles"));
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_EVENTBUS contracts=72 channels={channels.Count} publications={metrics.PublicationCount} deliveries={metrics.DeliverySucceededCount}");
    }

    public void Dispose()
    {
        for (var index = _owners.Count - 1; index >= 0; index--)
        {
            _owners[index].Dispose();
        }

        _owners.Clear();
    }

    private static IReadOnlyList<IDogfoodEventCase> CreateCases()
    {
        var operation = Guid.NewGuid();
        var revision = 0;
        return
        [
            Case(new ApplicationReady(operation, "host", ++revision), revision, "critical"),
            Case(new ApplicationStopping(operation, "host", ++revision), revision),
            Case(new ModuleHealthChanged(operation, "modules", ++revision), revision),
            Case(new ConfigurationReloadRequested(operation, "configuration", ++revision), revision),
            Case(new SettingsChanged(operation, "settings", ++revision), revision),
            Case(new DiagnosticRaised(operation, "diagnostics", ++revision), revision, "telemetry"),
            Case(new FeatureFlagChanged(operation, "feature", ++revision), revision),
            Case(new AutomationCheckpointReached(operation, "automation", ++revision), revision, "chaos-tiny"),
            Case(new SignInRequested(operation, "alice", ++revision), revision),
            Case(new UserSignedIn(operation, "alice", ++revision), revision),
            Case(new SignInFailed(operation, "bob", ++revision), revision),
            Case(new UserSignedOut(operation, "alice", ++revision), revision),
            Case(new TenantSwitchRequested(operation, "tenant-a", ++revision), revision),
            Case(new TenantSwitched(operation, "tenant-b", ++revision), revision),
            Case(new PermissionsChanged(operation, "alice", ++revision), revision),
            Case(new AccessTokenExpiring(operation, "alice", ++revision), revision),
            Case(new NetworkReachabilityChanged(operation, "online", ++revision), revision),
            Case(new DataRequestFailed(operation, "orders", ++revision), revision),
            Case(new CacheInvalidated(operation, "catalog", ++revision), revision),
            Case(new SyncStarted(operation, "tenant-b", ++revision), revision),
            Case(new SyncCompleted(operation, "tenant-b", ++revision), revision),
            Case(new SyncFailed(operation, "tenant-c", ++revision), revision),
            Case(new RealtimeConnected(operation, "tenant-b", ++revision), revision, "realtime", partitioned: true),
            Case(new RealtimeDisconnected(operation, "tenant-b", ++revision), revision),
            Case(new RemoteSequenceGapDetected(operation, "tenant-b", ++revision), revision),
            Case(new LargeTransferProgressed(operation, "export", ++revision), revision),
            Case(new ProductCreated(operation, "SKU-1001", ++revision), revision),
            Case(new ProductUpdated(operation, "SKU-1001", ++revision), revision),
            Case(new CatalogRebuilt(operation, "catalog", ++revision), revision),
            Case(new PriceChanged(operation, "SKU-1001", ++revision), revision),
            Case(new CurrencyChanged(operation, "USD", ++revision), revision),
            Case(new PromotionActivated(operation, "PROMO-10", ++revision), revision),
            Case(new InventoryAdjusted(operation, "SKU-1001", ++revision), revision, "inventory", partitioned: true),
            Case(new InventoryLow(operation, "SKU-1002", ++revision), revision),
            Case(new WarehouseCapacityChanged(operation, "WH-01", ++revision), revision),
            Case(new PurchaseOrderCreated(operation, "PO-1001", ++revision), revision),
            Case(new CustomerCreated(operation, "CUS-1001", ++revision), revision),
            Case(new CustomerUpdated(operation, "CUS-1001", ++revision), revision),
            Case(new OrderDraftSaved(operation, "ORDER-DRAFT", ++revision), revision),
            Case(new OrderSubmitted(operation, "ORDER-1001", ++revision), revision, "orders", partitioned: true),
            Case(new OrderConfirmed(operation, "ORDER-1001", ++revision), revision),
            Case(new TaxCalculated(operation, "ORDER-1001", ++revision), revision),
            Case(new InvoiceIssued(operation, "INV-1001", ++revision), revision),
            Case(new BillingSettled(operation, "INV-1001", ++revision), revision),
            Case(new CartAbandoned(operation, "CART-1001", ++revision), revision),
            Case(new SalesTargetChanged(operation, "day", ++revision), revision),
            Case(new FraudAssessmentCompleted(operation, "ORDER-1001", ++revision), revision),
            Case(new FraudFlagged(operation, "ORDER-1002", ++revision), revision),
            Case(new PaymentAuthorized(operation, "PAY-1001", ++revision), revision),
            Case(new PaymentCaptured(operation, "PAY-1001", ++revision), revision),
            Case(new PaymentFailed(operation, "PAY-1002", ++revision), revision),
            Case(new FulfillmentPlanned(operation, "ORDER-1001", ++revision), revision),
            Case(new PickWaveCreated(operation, "WAVE-1001", ++revision), revision),
            Case(new ShipmentQuoted(operation, "ORDER-1001", ++revision), revision),
            Case(new ShipmentDispatched(operation, "SHIP-1001", ++revision), revision),
            Case(new ShipmentProgressed(operation, "SHIP-1001", ++revision), revision),
            Case(new ReturnRequested(operation, "RET-1001", ++revision), revision),
            Case(new ReturnApproved(operation, "RET-1001", ++revision), revision),
            Case(new RefundCompleted(operation, "REF-1001", ++revision), revision),
            Case(new WorkflowCompensated(operation, "WF-1001", ++revision), revision, "workflow", partitioned: true),
            Case(new SearchExecuted(operation, "keyboard", ++revision), revision, "search"),
            Case(new RecommendationProduced(operation, "alice", ++revision), revision),
            Case(new NotificationRaised(operation, "inventory-low", ++revision), revision, "notifications"),
            Case(new SupportTicketOpened(operation, "TICKET-1001", ++revision), revision),
            Case(new SupportMessageReceived(operation, "TICKET-1001", ++revision), revision),
            Case(new ReportGenerated(operation, "sales-day", ++revision), revision, "background"),
            Case(new AnalyticsRefreshed(operation, "dashboard", ++revision), revision),
            Case(new AuditAppended(operation, "order-submitted", ++revision), revision, "audit"),
            Case(new NavigationCommitted(operation, "dashboard", ++revision), revision),
            Case(new OutletReconciled(operation, "primary", ++revision), revision, "ui-feedback"),
            Case(new WindowWorkspaceOpened(operation, "main", ++revision), revision),
            Case(new UserActionRejected(operation, "permission-denied", ++revision), revision),
        ];
    }

    private static IDogfoodEventCase Case<TEvent>(
        TEvent eventData,
        int index,
        string? channelName = null,
        bool partitioned = false)
        where TEvent : IDogfoodEvent =>
        new DogfoodEventCase<TEvent>(eventData, index, channelName, partitioned);

    private interface IDogfoodEventCase
    {
        Type EventType { get; }

        Task RunAsync(
            IEventBus eventBus,
            LifecycleScope projectionOwner,
            LifecycleScope auditOwner,
            IApplicationStateWriter stateWriter,
            IUiDispatcher dispatcher);
    }

    private sealed class DogfoodEventCase<TEvent> : IDogfoodEventCase
        where TEvent : IDogfoodEvent
    {
        private readonly TEvent _eventData;
        private readonly int _index;
        private readonly string? _channelName;
        private readonly bool _partitioned;

        public DogfoodEventCase(TEvent eventData, int index, string? channelName, bool partitioned)
        {
            _eventData = eventData;
            _index = index;
            _channelName = channelName;
            _partitioned = partitioned;
        }

        public Type EventType => typeof(TEvent);

        public async Task RunAsync(
            IEventBus eventBus,
            LifecycleScope projectionOwner,
            LifecycleScope auditOwner,
            IApplicationStateWriter stateWriter,
            IUiDispatcher dispatcher)
        {
            var deliveryCount = 0;
            var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var options = CreateSubscriptionOptions(_index, dispatcher);
            var projectionHandler = new ProjectionHandler<TEvent>(
                context =>
                {
                    if (options.DispatchPolicy == EventDispatchPolicy.UiThread && !dispatcher.CheckAccess())
                    {
                        throw new InvalidOperationException("A UiThread EventBus handler ran outside the Avalonia dispatcher.");
                    }

                    stateWriter.Update(DogfoodStateWorkload.AuditRevision, static value => value + 1);
                    SignalDelivery(ref deliveryCount, delivered);
                });

            EventChannel<TEvent>? channel = _channelName is null
                ? null
                : new EventChannel<TEvent>(_channelName);

            if (channel is null)
            {
                eventBus.Subscribe(projectionOwner, projectionHandler, options);
                eventBus.Subscribe<TEvent>(auditOwner, _ => SignalDelivery(ref deliveryCount, delivered), options);
            }
            else
            {
                eventBus.Subscribe(projectionOwner, channel.Value, projectionHandler, options);
                eventBus.Subscribe<TEvent>(auditOwner, channel.Value, _ => SignalDelivery(ref deliveryCount, delivered), options);
            }

            var publishOptions = new EventPublishOptions
            {
                CorrelationId = _eventData.OperationId.ToString("N"),
                CausationId = $"dogfood-{_index - 1}",
                PublishDepth = _index % 3,
                PartitionKey = _partitioned ? _eventData.Subject : null,
            };

            if (_index % 3 == 0)
            {
                var posted = channel is null
                    ? await eventBus.PostAsync(_eventData, publishOptions).ConfigureAwait(false)
                    : await eventBus.PostAsync(channel.Value, _eventData, publishOptions).ConfigureAwait(false);
                if (!posted.Accepted)
                {
                    throw new InvalidOperationException(posted.RejectionReason ?? "Event post was rejected.");
                }

                await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            else
            {
                var published = channel is null
                    ? await eventBus.PublishAsync(_eventData, publishOptions).ConfigureAwait(false)
                    : await eventBus.PublishAsync(channel.Value, _eventData, publishOptions).ConfigureAwait(false);
                if (!published.Succeeded)
                {
                    throw new InvalidOperationException($"Event publication failed for '{typeof(TEvent).Name}'.");
                }
            }
        }

        private static EventSubscriptionOptions CreateSubscriptionOptions(int index, IUiDispatcher dispatcher)
        {
            var options = (index % 4) switch
            {
                0 => EventSubscriptionOptions.Current,
                1 => EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.StopPublication),
                2 => EventSubscriptionOptions.Background().WithErrorPolicy(EventErrorPolicy.FailPublisher),
                _ => EventSubscriptionOptions
                    .UiThread(
                        dispatcher,
                        index % 2 == 0 ? EventDispatchMode.Post : EventDispatchMode.InlineIfAllowed)
                    .WithErrorPolicy(EventErrorPolicy.DisableSubscription),
            };

            return options
                .WithHandlerTimeout(TimeSpan.FromSeconds(5))
                .WithDisableSubscriptionAfterFailures(2);
        }

        private static void SignalDelivery(ref int count, TaskCompletionSource delivered)
        {
            if (Interlocked.Increment(ref count) == 2)
            {
                delivered.TrySetResult();
            }
        }
    }

    private sealed class ProjectionHandler<TEvent>(Action<EventContext<TEvent>> handler) : IEventHandler<TEvent>
    {
        public ValueTask HandleAsync(EventContext<TEvent> context)
        {
            handler(context);
            return ValueTask.CompletedTask;
        }
    }
}

[EventHandler(
    typeof(DiagnosticsModule),
    ChannelName = "critical",
    DispatchPolicy = EventDispatchPolicy.Serialized,
    ErrorPolicy = EventErrorPolicy.FailPublisher)]
public sealed class ApplicationReadyAuditHandler(AuditSink auditSink) : IEventHandler<ApplicationReady>
{
    private static int _callCount;

    public static int CallCount => Volatile.Read(ref _callCount);

    public ValueTask HandleAsync(EventContext<ApplicationReady> context)
    {
        auditSink.Execute($"application-ready:{context.Event.Revision}");
        Interlocked.Increment(ref _callCount);
        return ValueTask.CompletedTask;
    }
}
