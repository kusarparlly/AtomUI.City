using AtomUI.City.Core.Threading;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodStateWorkload : IDisposable
{
    private static readonly string[] StateNames =
    [
        "AppRunMode", "HostHealth", "NetworkReachability", "DiagnosticsLevel", "FeatureRevision",
        "SettingsRevision", "TelemetryRevision", "ClockSkew", "AutomationProfile", "LastCheckpoint",
        "UserCurrentAccount", "UserDisplayName", "TenantCurrent", "TenantRevision", "PermissionRevision",
        "SessionRevision", "TokenExpiryHint", "AccountSwitchStatus", "OfflineIdentityMode", "PrincipalFingerprint",
        "HttpHealth", "GrpcHealth", "SignalRHealth", "RealtimeConnectionState", "ReconnectAttempt",
        "RequestInFlight", "RequestFailureCount", "CacheHitRate", "CacheRevision", "SyncRevision",
        "SyncBacklog", "LastServerSequence", "CatalogRevision", "ProductCount", "ProductSelection",
        "PricingCurrency", "PricingRevision", "PromotionRevision", "InventoryRevision", "InventoryLowCount",
        "WarehouseCurrent", "WarehouseCapacity", "ProcurementPending", "CustomerSelection", "CustomerRevision",
        "CartDraftId", "OrderSelection", "OrderCount", "OrderPending", "OrderFailed", "TaxRegion",
        "TaxRevision", "BillingBalance", "InvoicePending", "RefundPending", "SalesDayTotal",
        "FraudThreshold", "FraudFlaggedCount", "PaymentPending", "PaymentExposure", "FulfillmentPending",
        "PickWaveCurrent", "ShipmentInTransit", "ShipmentDelayed", "ReturnsOpen", "SearchQuery",
        "SearchRevision", "RecommendationRevision", "NotificationsUnread", "SupportOpen", "WorkflowRunning",
        "AuditRevision",
    ];

    private static readonly HashSet<string> TextStateNames = new(StringComparer.Ordinal)
    {
        "AppRunMode", "HostHealth", "NetworkReachability", "AutomationProfile", "LastCheckpoint",
        "UserCurrentAccount", "UserDisplayName", "TenantCurrent", "AccountSwitchStatus", "PrincipalFingerprint",
        "HttpHealth", "GrpcHealth", "SignalRHealth", "RealtimeConnectionState", "ProductSelection",
        "PricingCurrency", "WarehouseCurrent", "CustomerSelection", "CartDraftId", "OrderSelection",
        "TaxRegion", "PickWaveCurrent", "SearchQuery",
    };

    private static readonly string[] ComputedNames =
    [
        "ApplicationReadyToOperate", "EffectiveOnlineMode", "EffectiveCultureLabel", "CurrentIdentityLabel",
        "CanMutateCommerce", "NavigationTitle", "CatalogHealth", "InventoryPressure", "PricingHealth",
        "OrderThroughput", "RevenueExposure", "PaymentRisk", "FulfillmentPressure", "ShippingHealth",
        "ReturnRate", "CustomerCareLoad", "SearchYield", "NotificationPressure", "OperationsScore", "OverallHealth",
    ];

    private static readonly string[] CollectionNames =
    [
        "Products", "Categories", "Prices", "Promotions", "InventoryItems", "Warehouses",
        "PurchaseOrders", "Customers", "Orders", "Invoices", "Payments", "Shipments", "Returns",
        "SupportTickets", "Notifications", "NavigationJournal",
    ];

    private static readonly string[] ScopedNames =
    [
        "WindowFocus", "WindowBusy", "WindowBanner", "WindowSelection", "RouteParameters",
        "RouteResolvedData", "RouteDraftDirty", "RouteValidation", "RouteCommandState", "RouteError",
        "OrderEditorDraft", "ProductEditorDraft", "CustomerEditorDraft", "PaymentWizardStep",
        "ReturnWizardStep", "SearchFilters", "ReportFilters", "SupportConversationDraft",
        "InspectorSelection", "ActivityFilter",
    ];

    public static readonly StateKey<string> HostHealth = Key<string>("HostHealth");
    public static readonly StateKey<string> NetworkReachability = Key<string>("NetworkReachability");
    public static readonly StateKey<string> HttpHealth = Key<string>("HttpHealth");
    public static readonly StateKey<string> GrpcHealth = Key<string>("GrpcHealth");
    public static readonly StateKey<string> SignalRHealth = Key<string>("SignalRHealth");
    public static readonly StateKey<string> RealtimeConnectionState = Key<string>("RealtimeConnectionState");
    public static readonly StateKey<string> LastCheckpoint = Key<string>("LastCheckpoint");
    public static readonly StateKey<string> SearchQuery = Key<string>("SearchQuery");
    public static readonly StateKey<int> FeatureRevision = Key<int>("FeatureRevision");
    public static readonly StateKey<int> OrderCount = Key<int>("OrderCount");
    public static readonly StateKey<int> PermissionRevision = Key<int>("PermissionRevision");
    public static readonly StateKey<int> AuditRevision = Key<int>("AuditRevision");

    private readonly ApplicationStateRegistry _registry;
    private readonly IApplicationStateWriter _hostWriter;
    private readonly IStateFactory _factory;
    private readonly IStateScopeAccessor _scopeAccessor;
    private readonly IUiDispatcher _dispatcher;
    private readonly List<IDisposable> _owned = [];
    private int _initialized;
    private int _uiAttached;

    public DogfoodStateWorkload(
        IStateRegistry registry,
        IApplicationStateWriter hostWriter,
        IStateFactory factory,
        IStateScopeAccessor scopeAccessor,
        IUiDispatcher dispatcher)
    {
        _registry = (ApplicationStateRegistry)registry;
        _hostWriter = hostWriter;
        _factory = factory;
        _scopeAccessor = scopeAccessor;
        _dispatcher = dispatcher;
    }

    public async Task InitializeAsync()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            throw new InvalidOperationException("The State workload can only be initialized once.");
        }

        RegisterApplicationStates();
        VerifyAuthoritiesAndSnapshot();
        await VerifyDispatchPoliciesAsync().ConfigureAwait(false);
        CreateComputedStates();
        CreateCollectionStates();
        VerifyScopedStates();

        Console.WriteLine("DESKTOP_DOGFOOD_STATE application=72 computed=20 collections=16 scoped=20 persisted=30");
    }

    public async Task AttachUiAsync(Action<string> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(updateStatus);
        if (Interlocked.Exchange(ref _uiAttached, 1) != 0)
        {
            throw new InvalidOperationException("The State UI projection can only be attached once.");
        }

        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _owned.Add(_registry.Get(HostHealth).OnChange(
            args =>
            {
                updateStatus(args.NewValue);
                delivered.TrySetResult();
            },
            StateSubscriptionOptions.Dispatcher(_dispatcher, maxPendingNotifications: 32)));

        _hostWriter.Set(HostHealth, "State dispatcher projection is live");
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    public async Task RunStressAsync(
        DogfoodRunOptions options,
        DogfoodRunLedger ledger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ledger);

        var updateCount = options.Profile switch
        {
            DogfoodRunProfile.Standard => 1_024,
            DogfoodRunProfile.Soak or DogfoodRunProfile.Extreme or DogfoodRunProfile.Headless => 4_096,
            _ => 128,
        };
        using var concurrent = _factory.CreateWritable(0, stateName: "dogfood.stress.concurrent");
        var workers = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                for (var index = 0; index < updateCount / 8; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    concurrent.Update(static value => value + 1);
                    ledger.Record("state-concurrent", "dogfood.stress.concurrent");
                }
            }, cancellationToken))
            .ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
        if (concurrent.Value != updateCount || concurrent.Version != updateCount)
        {
            throw new InvalidOperationException(
                $"Concurrent State updates diverged: value={concurrent.Value}, version={concurrent.Version}, expected={updateCount}.");
        }

        using var bounded = _factory.CreateWritable(0, stateName: "dogfood.stress.bounded");
        using var release = new ManualResetEventSlim(initialState: false);
        var firstDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = 0;
        using var subscription = bounded.OnChange(
            args =>
            {
                firstDelivery.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(5));
                Interlocked.Increment(ref delivered);
                if (args.NewValue == 64)
                {
                    finalDelivery.TrySetResult();
                }
            },
            StateSubscriptionOptions.Queued(maxPendingNotifications: 2));
        bounded.SetValue(1);
        await firstDelivery.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        for (var value = 2; value <= 64; value++)
        {
            bounded.SetValue(value);
        }

        release.Set();
        await finalDelivery.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        if (delivered >= 64 || bounded.Value != 64)
        {
            throw new InvalidOperationException(
                $"Bounded State queue did not coalesce pressure: delivered={delivered}, final={bounded.Value}.");
        }

        ledger.Record("backpressure", "state-drop-oldest", "expected-failure");
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_STATE_STRESS updates={updateCount} final={concurrent.Value} boundedWrites=64 boundedDeliveries={delivered}");
    }

    public void Dispose()
    {
        for (var index = _owned.Count - 1; index >= 0; index--)
        {
            _owned[index].Dispose();
        }

        _owned.Clear();
    }

    private void RegisterApplicationStates()
    {
        for (var index = 0; index < StateNames.Length; index++)
        {
            var name = StateNames[index];
            if (TextStateNames.Contains(name))
            {
                Register(Key<string>(name), string.Empty, name, index);
            }
            else
            {
                Register(Key<int>(name), 0, name, index);
            }
        }

        if (StateNames.Length != 72 || StateNames.Distinct(StringComparer.Ordinal).Count() != 72)
        {
            throw new InvalidOperationException("The application State catalog must contain 72 unique entries.");
        }
    }

    private void Register<T>(StateKey<T> key, T defaultValue, string name, int index)
    {
        var access = name switch
        {
            "LastCheckpoint" => StateAccessPolicy.ReadOnly,
            "OrderCount" => StateAccessPolicy.OwnerWrite,
            "PermissionRevision" => StateAccessPolicy.AuthorizedWrite,
            "FeatureRevision" => StateAccessPolicy.PluginIsolated,
            _ => StateAccessPolicy.HostWrite,
        };
        var lifetime = (StateLifetime)(index % Enum.GetValues<StateLifetime>().Length);
        var snapshot = index < 30 ? StateSnapshotPolicy.Persisted : StateSnapshotPolicy.Transient;

        _registry.Add(StateDefinition.Create(
            key,
            defaultValue,
            lifetime,
            access,
            snapshot,
            schemaVersion: index % 3 + 1,
            ownerModule: access == StateAccessPolicy.OwnerWrite ? "Orders" : null,
            pluginId: access == StateAccessPolicy.PluginIsolated ? "seasonal-operations" : null,
            writeCapability: access == StateAccessPolicy.AuthorizedWrite ? "dogfood.state.write" : null));
    }

    private void VerifyAuthoritiesAndSnapshot()
    {
        _hostWriter.Set(HostHealth, "Host started");
        _hostWriter.Set(NetworkReachability, "Online");

        var ownerWriter = _registry.CreateWriter(StateWriteAuthority.Module("Orders"));
        ownerWriter.Set(OrderCount, 3);

        var capabilityWriter = _registry.CreateWriter(
            StateWriteAuthority.Module("Automation", ["dogfood.state.write"]));
        capabilityWriter.Set(PermissionRevision, 7);

        var pluginWriter = _registry.CreateWriter(StateWriteAuthority.Plugin("seasonal-operations"));
        pluginWriter.Set(FeatureRevision, 11);

        try
        {
            _hostWriter.Set(LastCheckpoint, "must-fail");
            throw new InvalidOperationException("ReadOnly State unexpectedly accepted a Host write.");
        }
        catch (StateAccessDeniedException)
        {
        }

        var snapshot = _registry.CreateSnapshot();
        if (snapshot.Entries.Count != 30)
        {
            throw new InvalidOperationException($"Expected 30 persisted State entries; observed {snapshot.Entries.Count}.");
        }

        _registry.Restore(snapshot);
    }

    private async Task VerifyDispatchPoliciesAsync()
    {
        var state = _registry.Get(PermissionRevision);
        var immediate = 0;
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var background = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _owned.Add(state.OnChange(_ => Interlocked.Increment(ref immediate)));
        _owned.Add(state.OnChange(
            _ => queued.TrySetResult(),
            StateSubscriptionOptions.Queued(maxPendingNotifications: 32)));
        _owned.Add(state.OnChange(
            _ => background.TrySetResult(),
            StateSubscriptionOptions.Background(maxPendingNotifications: 32)));

        var capabilityWriter = _registry.CreateWriter(
            StateWriteAuthority.Module("Automation", ["dogfood.state.write"]));
        capabilityWriter.Update(PermissionRevision, static value => value + 1);

        await Task.WhenAll(queued.Task, background.Task)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        if (immediate != 1)
        {
            throw new InvalidOperationException($"Immediate State dispatch expected one call; observed {immediate}.");
        }
    }

    private void CreateComputedStates()
    {
        var root = _factory.CreateWritable(1, stateName: "dogfood.computed.root");
        var increment = _factory.CreateWritable(2, stateName: "dogfood.computed.increment");
        _owned.Add(root);
        _owned.Add(increment);

        IReadOnlyState<int> previous = root;
        for (var index = 0; index < ComputedNames.Length; index++)
        {
            var dependency = previous;
            var computed = _factory.CreateComputed(
                () => dependency.Value + increment.Value,
                dependency,
                increment);
            _owned.Add(computed);
            previous = computed;
        }

        var initial = previous.Value;
        root.SetValue(5);
        var updated = previous.Value;
        if (ComputedNames.Length != 20 || initial != 41 || updated != 45)
        {
            throw new InvalidOperationException(
                $"Computed State chain mismatch: count={ComputedNames.Length}, initial={initial}, updated={updated}.");
        }
    }

    private void CreateCollectionStates()
    {
        foreach (var name in CollectionNames)
        {
            var collection = new StateCollection<string, int>();
            collection.AddOrUpdate("first", 1);
            collection.AddOrUpdateRange([new("second", 2), new("third", 3)]);
            collection.AddOrUpdate("first", 4);
            var snapshot = collection.CreateSnapshot();
            collection.Remove("second");
            collection.RestoreSnapshot(snapshot);
            if (collection.Items.Count != 3 || collection.Version != snapshot.CollectionVersion)
            {
                throw new InvalidOperationException($"Collection State '{name}' failed its mutation/restore probe.");
            }

            _owned.Add(collection);
        }

        if (CollectionNames.Length != 16)
        {
            throw new InvalidOperationException("The collection State catalog must contain 16 entries.");
        }
    }

    private void VerifyScopedStates()
    {
        var scope = _factory.CreateScope("dogfood.route-window-scope");
        var states = new List<WritableState<string>>(ScopedNames.Length);
        using (_scopeAccessor.Push(scope))
        {
            foreach (var name in ScopedNames)
            {
                var state = _factory.CreateWritable(string.Empty, stateName: $"dogfood.scoped.{name}");
                state.SetValue(name);
                states.Add(state);
            }
        }

        scope.Dispose();
        if (ScopedNames.Length != 20 || scope.State != StateScopeState.Disposed)
        {
            throw new InvalidOperationException("The scoped State workload did not dispose all 20 entries.");
        }

        try
        {
            states[0].SetValue("must-fail");
            throw new InvalidOperationException("A scoped State remained writable after scope disposal.");
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static StateKey<T> Key<T>(string suffix) => new($"dogfood.state.{suffix}");
}
