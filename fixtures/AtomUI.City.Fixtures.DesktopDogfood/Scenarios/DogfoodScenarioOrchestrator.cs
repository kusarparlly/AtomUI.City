using System.Diagnostics;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Localization;
using AtomUI.City.Security;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class DogfoodAccountCatalog
{
    public static readonly IReadOnlyDictionary<string, SecurityAccountKey> Accounts =
        new Dictionary<string, SecurityAccountKey>(StringComparer.Ordinal)
        {
            ["Alice"] = new("oidc", "https://identity.dogfood.test", "alpha", "alice"),
            ["Bob"] = new("oidc", "https://identity.dogfood.test", "beta", "bob"),
            ["Administrator"] = new("oidc", "https://identity.dogfood.test", "alpha", "admin"),
        };
}

internal static class DogfoodScenarioCatalog
{
    public static IReadOnlyList<DogfoodScenarioDefinition> All { get; } = Array.AsReadOnly(
        new DogfoodScenarioDefinition[]
    {
        new("S01", "Account context rebind", "Administration", "Dashboard", 0, false, ["Security"]),
        new("S02", "Search request supersession", "Commerce", "Operations", 9, false, ["Mvvm"]),
        new("S03", "Order submit and cache invalidation", "Commerce", "Fulfillment", 18, false, ["Mvvm"]),
        new("S04", "Payment failure compensation", "Commerce", "Operations", 27, true, ["Mvvm"]),
        new("S05", "Inventory event burst", "Fulfillment", "Commerce", 36, false, []),
        new("S06", "Offline account recovery", "Administration", "Operations", 45, false, ["Security"]),
        new("S07", "Culture and navigation convergence", "Dashboard", "Customers", 54, false, ["Localization"]),
        new("S08", "Route and outlet replacement", "Operations", "Dashboard", 63, false, ["Mvvm"]),
        new("S09", "Account culture search composition", "Customers", "Commerce", 72, false, ["Security", "Localization", "Mvvm"]),
        new("S10", "Event fanout and audit", "Operations", "Administration", 81, false, []),
        new("S11", "Cancellation and recovery", "Commerce", "Dashboard", 90, true, ["Mvvm"]),
        new("S12", "Full domain reconciliation", "Administration", "Dashboard", 99, false, ["Security", "Localization", "Mvvm"]),
    });

    public static IReadOnlyList<string> DisplayNames { get; } =
        All.Select(static item => $"{item.Id} | {item.Name}").ToArray();

    public static DogfoodScenarioDefinition GetRequired(string idOrDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrDisplayName);
        var id = idOrDisplayName.Split('|', 2)[0].Trim();
        return All.SingleOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal)) ??
            throw new KeyNotFoundException($"Dogfood scenario '{idOrDisplayName}' is not registered.");
    }
}

internal sealed record DogfoodScenarioDefinition(
    string Id,
    string Name,
    string PrimarySection,
    string SecondarySection,
    int ServiceOffset,
    bool InjectServiceFailure,
    IReadOnlyList<string> AdditionalModules);

internal sealed record DogfoodScenarioEvidence(
    int BatchSequence,
    int ScenarioSequence,
    string ScenarioId,
    Guid OperationId,
    string CorrelationId,
    string CausationId,
    string Status,
    long ElapsedMilliseconds,
    int ServiceCalls,
    int ServiceStages,
    int CompensationCalls,
    int DataOperations,
    int EventPublications,
    int StateMutations,
    int Navigations,
    string Culture,
    string AccountMode,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Invariants);

internal sealed record DogfoodScenarioBatchResult(
    int BatchSequence,
    int CompletedCount,
    int ServiceCalls,
    int DataOperations,
    int EventPublications,
    int StateMutations,
    int Navigations,
    int CompensationCalls,
    IReadOnlyList<DogfoodScenarioEvidence> Results)
{
    public string Summary =>
        $"{CompletedCount} scenarios | {Navigations} routes | {ServiceCalls} service calls | " +
        $"{DataOperations} data | {EventPublications} events";
}

internal sealed class DogfoodScenarioOrchestrator
{
    private const int ServicesPerScenario = 24;

    private static readonly string[] CommonModules =
    [
        "Core", "Services", "EventBus", "State", "Data", "Routing", "Presentation",
    ];

    private readonly DogfoodBusinessWorkload _businessWorkload;
    private readonly DogfoodRemoteOperations _remoteOperations;
    private readonly DogfoodDataRequestProbe _dataProbe;
    private readonly IEventBus _eventBus;
    private readonly IEventBusMonitor _eventBusMonitor;
    private readonly IApplicationStateWriter _stateWriter;
    private readonly ApplicationStateRegistry _stateRegistry;
    private readonly ILocalizationService _localization;
    private readonly IAccountSessionManager _accountSessions;
    private readonly DogfoodRefreshingAccessTokenProvider _refreshingTokens;
    private readonly DogfoodRunLedger _ledger;
    private readonly SemaphoreSlim _batchGate = new(1, 1);
    private int _batchSequence;

    public DogfoodScenarioOrchestrator(
        DogfoodBusinessWorkload businessWorkload,
        DogfoodRemoteOperations remoteOperations,
        DogfoodDataRequestProbe dataProbe,
        IEventBus eventBus,
        IEventBusMonitor eventBusMonitor,
        IApplicationStateWriter stateWriter,
        IStateRegistry stateRegistry,
        ILocalizationService localization,
        IAccountSessionManager accountSessions,
        DogfoodRefreshingAccessTokenProvider refreshingTokens,
        DogfoodRunLedger ledger)
    {
        _businessWorkload = businessWorkload;
        _remoteOperations = remoteOperations;
        _dataProbe = dataProbe;
        _eventBus = eventBus;
        _eventBusMonitor = eventBusMonitor;
        _stateWriter = stateWriter;
        _stateRegistry = stateRegistry as ApplicationStateRegistry ??
            throw new InvalidOperationException(
                $"Scenario orchestration requires {nameof(ApplicationStateRegistry)}.");
        _localization = localization;
        _accountSessions = accountSessions;
        _refreshingTokens = refreshingTokens;
        _ledger = ledger;
    }

    public async Task<DogfoodScenarioBatchResult> RunAsync(
        string? scenarioId,
        bool runMatrix,
        Func<string, CancellationToken, Task<object>> navigateAndPresentAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(navigateAndPresentAsync);
        var definitions = runMatrix
            ? DogfoodScenarioCatalog.All
            : [DogfoodScenarioCatalog.GetRequired(scenarioId ?? string.Empty)];

        await _batchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var batchSequence = Interlocked.Increment(ref _batchSequence);
            var results = new List<DogfoodScenarioEvidence>(definitions.Count);
            for (var index = 0; index < definitions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var evidence = await RunScenarioAsync(
                    definitions[index],
                    batchSequence,
                    index + 1,
                    navigateAndPresentAsync,
                    cancellationToken).ConfigureAwait(false);
                results.Add(evidence);
                _ledger.RecordScenario(evidence);
            }

            var result = new DogfoodScenarioBatchResult(
                batchSequence,
                results.Count,
                results.Sum(static item => item.ServiceCalls),
                results.Sum(static item => item.DataOperations),
                results.Sum(static item => item.EventPublications),
                results.Sum(static item => item.StateMutations),
                results.Sum(static item => item.Navigations),
                results.Sum(static item => item.CompensationCalls),
                results);
            _ledger.Record("scenario-batch", $"batch-{batchSequence:000}");
            Console.WriteLine(
                $"DESKTOP_DOGFOOD_SCENARIOS batch={batchSequence} completed={result.CompletedCount} " +
                $"services={result.ServiceCalls} data={result.DataOperations} events={result.EventPublications} " +
                $"state={result.StateMutations} navigations={result.Navigations} compensations={result.CompensationCalls}");
            return result;
        }
        finally
        {
            _batchGate.Release();
        }
    }

    private async Task<DogfoodScenarioEvidence> RunScenarioAsync(
        DogfoodScenarioDefinition definition,
        int batchSequence,
        int scenarioSequence,
        Func<string, CancellationToken, Task<object>> navigateAndPresentAsync,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = CreateOperationId(_ledger.Options.Seed, batchSequence, scenarioSequence);
        var correlationId = $"{definition.Id.ToLowerInvariant()}-{operationId:N}";
        var causationId = $"{_ledger.Options.Profile.ToString().ToLowerInvariant()}-scenario-batch-{batchSequence:000}";
        var beforeData = _dataProbe.Invocations;
        var beforeEvents = _eventBusMonitor.GetSnapshot().PublicationCount;
        var beforeStateVersion = _stateRegistry.Get(DogfoodStateWorkload.AuditRevision).Version;
        var invariants = new List<string>();

        _ledger.Record("scenario-start", definition.Id);
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        var services = await _businessWorkload.RunScenarioAsync(
            operationId,
            definition.Id,
            definition.ServiceOffset,
            ServicesPerScenario,
            definition.InjectServiceFailure,
            cancellationToken).ConfigureAwait(false);
        if (services.StageCount < 3 || services.DependencyEdgeCount < 10)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Id}' did not form a multi-stage Service graph.");
        }
        if (definition.InjectServiceFailure && services.CompensationCount != services.CompletedCount)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Id}' did not fully compensate its completed Service slice.");
        }
        invariants.Add("service-graph-completed-or-compensated");

        await ApplyContextTransitionsAsync(definition, cancellationToken).ConfigureAwait(false);
        invariants.Add("security-localization-context-valid");

        var dataOperations = await ExecuteDataOperationsAsync(
            definition,
            operationId,
            cancellationToken).ConfigureAwait(false);
        if (_dataProbe.Invocations - beforeData < dataOperations)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Id}' Data pipeline did not observe all requested operations.");
        }
        invariants.Add("data-results-terminal-and-current");

        _stateWriter.Update(DogfoodStateWorkload.AuditRevision, static revision => revision + 1);
        _stateWriter.Set(DogfoodStateWorkload.SearchQuery, correlationId);
        _stateWriter.Set(DogfoodStateWorkload.HostHealth, $"Scenario {definition.Id} committed");
        const int stateMutations = 3;
        if (_stateRegistry.Get(DogfoodStateWorkload.AuditRevision).Version <= beforeStateVersion)
        {
            throw new InvalidOperationException($"Scenario '{definition.Id}' did not advance State revision.");
        }
        invariants.Add("state-revision-advanced");

        var eventPublications = await PublishScenarioEventsAsync(
            definition,
            operationId,
            correlationId,
            causationId,
            cancellationToken).ConfigureAwait(false);
        if (_eventBusMonitor.GetSnapshot().PublicationCount - beforeEvents < eventPublications)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Id}' EventBus publication count did not converge.");
        }
        invariants.Add("event-publications-delivered");

        var firstViewModel = await navigateAndPresentAsync(
            definition.PrimarySection,
            cancellationToken).ConfigureAwait(false);
        var secondViewModel = await navigateAndPresentAsync(
            definition.SecondarySection,
            cancellationToken).ConfigureAwait(false);
        if (firstViewModel is null || secondViewModel is null)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Id}' produced an empty Presentation entry.");
        }
        const int navigations = 2;
        invariants.Add("router-and-outlet-current-entry-converged");

        var localized = await _localization.GetStringAsync(
            "Shell.000",
            DogfoodLocalizationCatalog.LookupContext,
            cancellationToken).ConfigureAwait(false);
        if (localized.IsMissing)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Id}' could not resolve its active-culture UI text.");
        }
        invariants.Add("active-culture-text-resolved");

        var modules = CommonModules
            .Concat(definition.AdditionalModules)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (modules.Length < 6)
        {
            throw new InvalidOperationException($"Scenario '{definition.Id}' covered fewer than six modules.");
        }
        invariants.Add("module-coverage-minimum-met");

        stopwatch.Stop();
        return new DogfoodScenarioEvidence(
            batchSequence,
            scenarioSequence,
            definition.Id,
            operationId,
            correlationId,
            causationId,
            services.ExpectedFailureInjected ? "compensated" : "completed",
            stopwatch.ElapsedMilliseconds,
            services.InvocationCount,
            services.StageCount,
            services.CompensationCount,
            dataOperations,
            eventPublications,
            stateMutations,
            navigations,
            _localization.CurrentCulture.Name,
            _accountSessions.Current.Mode.ToString(),
            modules,
            invariants);
    }

    private async Task ApplyContextTransitionsAsync(
        DogfoodScenarioDefinition definition,
        CancellationToken cancellationToken)
    {
        switch (definition.Id)
        {
            case "S01":
                await SwitchAccountAsync("Alice", AccountSessionMode.Online, cancellationToken).ConfigureAwait(false);
                await SwitchAccountAsync("Administrator", AccountSessionMode.Online, cancellationToken).ConfigureAwait(false);
                break;
            case "S06":
                await SwitchAccountAsync("Bob", AccountSessionMode.OfflineRestricted, cancellationToken).ConfigureAwait(false);
                await SwitchAccountAsync("Administrator", AccountSessionMode.Online, cancellationToken).ConfigureAwait(false);
                break;
            case "S09":
                await SwitchAccountAsync("Alice", AccountSessionMode.Online, cancellationToken).ConfigureAwait(false);
                await SetCultureAsync("zh-CN", cancellationToken).ConfigureAwait(false);
                break;
            case "S07":
                await SetCultureAsync("zh-CN", cancellationToken).ConfigureAwait(false);
                await SetCultureAsync("en-US", cancellationToken).ConfigureAwait(false);
                break;
            case "S12":
                await SwitchAccountAsync("Administrator", AccountSessionMode.Online, cancellationToken).ConfigureAwait(false);
                await SetCultureAsync("en-US", cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task SwitchAccountAsync(
        string accountName,
        AccountSessionMode expectedMode,
        CancellationToken cancellationToken)
    {
        var accountKey = DogfoodAccountCatalog.Accounts[accountName];
        var refreshed = false;
        if (expectedMode == AccountSessionMode.Online)
        {
            refreshed = await _refreshingTokens.PrepareAccountForOnlineSwitchAsync(
                accountKey,
                DogfoodRemoteOperations.ClientId,
                cancellationToken).ConfigureAwait(false);
        }

        var options = new AccountSwitchOptions(
            allowOffline: true,
            credentialResourceName: DogfoodRemoteOperations.ClientId);
        var result = refreshed && _accountSessions.Current.AccountKey?.Equals(accountKey) == true
            ? await _accountSessions.RefreshAccountAsync(accountKey, options, cancellationToken).ConfigureAwait(false)
            : await _accountSessions.SwitchAccountAsync(accountKey, options, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Session.Mode != expectedMode)
        {
            throw new InvalidOperationException(
                $"Scenario account switch '{accountName}' failed: status={result.Status}, mode={result.Session.Mode}.");
        }

        _ledger.Record("scenario-security", accountName);
    }

    private async Task SetCultureAsync(string culture, CancellationToken cancellationToken)
    {
        var result = await _localization.SetCultureAsync(culture, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || !string.Equals(_localization.CurrentCulture.Name, culture, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Scenario culture switch '{culture}' failed.");
        }

        _ledger.Record("scenario-localization", culture);
    }

    private async Task<int> ExecuteDataOperationsAsync(
        DogfoodScenarioDefinition definition,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var sku = $"SKU-{definition.ServiceOffset:0000}-{operationId:N}";
        if (definition.Id == "S03")
        {
            var order = await _remoteOperations.SubmitOrderAsync(
                new StressSubmitOrderRequest(
                    sku,
                    2,
                    operationId.ToString("N")),
                cancellationToken).ConfigureAwait(false);
            var refreshed = await _remoteOperations.GetProductAsync(
                sku,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            EnsureDataSuccess(definition.Id, order, refreshed);
            return 2;
        }

        if (definition.Id is "S02" or "S11")
        {
            var obsolete = _remoteOperations.SearchAsync(
                $"{definition.Id.ToLowerInvariant()}-obsolete",
                180,
                DataConcurrencyPolicy.LatestWins,
                cancellationToken).AsTask();
            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
            var current = _remoteOperations.SearchAsync(
                $"{definition.Id.ToLowerInvariant()}-current",
                1,
                DataConcurrencyPolicy.LatestWins,
                cancellationToken).AsTask();
            var results = await Task.WhenAll(obsolete, current).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (results[0].Status is not (DataResultStatus.Cancelled or DataResultStatus.StaleSuppressed) ||
                !results[1].Succeeded)
            {
                throw new InvalidOperationException(
                    $"Scenario '{definition.Id}' request supersession failed: old={results[0].Status}, current={results[1].Status}.");
            }

            return 2;
        }

        var product = await _remoteOperations.GetProductAsync(
            sku,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var search = await _remoteOperations.SearchAsync(
            $"scenario-{definition.Id.ToLowerInvariant()}-{operationId:N}",
            1,
            DataConcurrencyPolicy.Queue,
            cancellationToken).ConfigureAwait(false);
        EnsureDataSuccess(definition.Id, product, search);
        return 2;
    }

    private static void EnsureDataSuccess<TFirst, TSecond>(
        string scenarioId,
        DataResult<TFirst> first,
        DataResult<TSecond> second)
    {
        if (!first.Succeeded || !second.Succeeded)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenarioId}' Data operations failed: first={first.Status}, second={second.Status}.");
        }
    }

    private async Task<int> PublishScenarioEventsAsync(
        DogfoodScenarioDefinition definition,
        Guid operationId,
        string correlationId,
        string causationId,
        CancellationToken cancellationToken)
    {
        var options = definition.Id is "S03" or "S04" or "S05"
            ? new EventPublishOptions
            {
                CorrelationId = correlationId,
                CausationId = causationId,
                PartitionKey = definition.Id,
            }
            : new EventPublishOptions
            {
                CorrelationId = correlationId,
                CausationId = causationId,
            };
        var auditOptions = new EventPublishOptions
        {
            CorrelationId = correlationId,
            CausationId = causationId,
        };
        var primary = definition.Id switch
        {
            "S01" => await _eventBus.PublishAsync(new TenantSwitched(operationId, definition.Name, 1), options, cancellationToken),
            "S02" => await _eventBus.PublishAsync(new EventChannel<SearchExecuted>("search"), new SearchExecuted(operationId, definition.Name, 2), options, cancellationToken),
            "S03" => await _eventBus.PublishAsync(new EventChannel<OrderSubmitted>("orders"), new OrderSubmitted(operationId, definition.Name, 3), options, cancellationToken),
            "S04" => await _eventBus.PublishAsync(new EventChannel<WorkflowCompensated>("workflow"), new WorkflowCompensated(operationId, definition.Name, 4), options, cancellationToken),
            "S05" => await _eventBus.PublishAsync(new EventChannel<InventoryAdjusted>("inventory"), new InventoryAdjusted(operationId, definition.Name, 5), options, cancellationToken),
            "S06" => await _eventBus.PublishAsync(new NetworkReachabilityChanged(operationId, definition.Name, 6), options, cancellationToken),
            "S07" => await _eventBus.PublishAsync(new SettingsChanged(operationId, definition.Name, 7), options, cancellationToken),
            "S08" => await _eventBus.PublishAsync(new NavigationCommitted(operationId, definition.Name, 8), options, cancellationToken),
            "S09" => await _eventBus.PublishAsync(new RecommendationProduced(operationId, definition.Name, 9), options, cancellationToken),
            "S10" => await _eventBus.PublishAsync(new EventChannel<DiagnosticRaised>("telemetry"), new DiagnosticRaised(operationId, definition.Name, 10), options, cancellationToken),
            "S11" => await _eventBus.PublishAsync(new DataRequestFailed(operationId, definition.Name, 11), options, cancellationToken),
            "S12" => await _eventBus.PublishAsync(new EventChannel<OutletReconciled>("ui-feedback"), new OutletReconciled(operationId, definition.Name, 12), options, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.Id, "Unknown scenario id."),
        };
        var audit = await _eventBus.PublishAsync(
            new EventChannel<AuditAppended>("audit"),
            new AuditAppended(operationId, definition.Id, 100 + definition.ServiceOffset),
            auditOptions,
            cancellationToken);
        if (!primary.Succeeded || !audit.Succeeded)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Id}' EventBus delivery failed: primary={primary.Succeeded}, audit={audit.Succeeded}.");
        }

        return 2;
    }

    private static Guid CreateOperationId(int seed, int batchSequence, int scenarioSequence)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, seed);
        BitConverter.TryWriteBytes(bytes[4..], batchSequence);
        BitConverter.TryWriteBytes(bytes[8..], scenarioSequence);
        BitConverter.TryWriteBytes(bytes[12..], 0x32564f44);
        return new Guid(bytes);
    }
}
