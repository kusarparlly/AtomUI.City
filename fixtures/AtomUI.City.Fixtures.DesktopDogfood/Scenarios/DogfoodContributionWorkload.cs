using System.Globalization;
using System.Text.Json;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Localization;
using AtomUI.City.Presentation;
using AtomUI.City.Routing;
using AtomUI.City.Security;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodContributionWorkload
{
    private const int StageCount = 7;
    private readonly IEventBusContributionController _eventContributions;
    private readonly IEventContractRegistry _eventContracts;
    private readonly IRouteRegistry _routes;
    private readonly LanguagePackageRegistry _languagePackages;
    private readonly ILocalizationService _localization;
    private readonly DataContributionRegistry _data;
    private readonly PermissionRegistry _permissions;
    private readonly InMemoryAuthorizationPolicyProvider _policies;
    private readonly IViewRegistry _views;
    private readonly IPresentationResourceRegistry _resources;
    private readonly IStateFactory _states;
    private readonly DogfoodRunOptions _options;
    private readonly DogfoodRunLedger _ledger;
    private readonly List<DogfoodContributionCycleResult> _results = [];
    private int _executed;

    public DogfoodContributionWorkload(
        IEventBusContributionController eventContributions,
        IEventContractRegistry eventContracts,
        IRouteRegistry routes,
        LanguagePackageRegistry languagePackages,
        ILocalizationService localization,
        DataContributionRegistry data,
        PermissionRegistry permissions,
        InMemoryAuthorizationPolicyProvider policies,
        IViewRegistry views,
        IPresentationResourceRegistry resources,
        IStateFactory states,
        DogfoodRunOptions options,
        DogfoodRunLedger ledger)
    {
        _eventContributions = eventContributions;
        _eventContracts = eventContracts;
        _routes = routes;
        _languagePackages = languagePackages;
        _localization = localization;
        _data = data;
        _permissions = permissions;
        _policies = policies;
        _views = views;
        _resources = resources;
        _states = states;
        _options = options;
        _ledger = ledger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _executed, 1) != 0)
        {
            throw new InvalidOperationException("The contribution workload can only run once.");
        }

        var references = new List<WeakReference>(_options.ContributionCycles);
        for (var cycle = 0; cycle < _options.ContributionCycles; cycle++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var failureStage = cycle < StageCount ? cycle : -1;
            references.Add(await RunCycleAsync(cycle, failureStage, cancellationToken).ConfigureAwait(false));
        }

        for (var index = 0; index < 3; index++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        var retained = references.Count(static reference => reference.IsAlive);
        if (retained != 0)
        {
            throw new InvalidOperationException(
                $"Dynamic contribution revocation retained {retained} of {references.Count} resource sentinels.");
        }

        var path = Path.Combine(_options.ArtifactRoot, "contribution-lifecycle-report.json");
        Directory.CreateDirectory(_options.ArtifactRoot);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                new DogfoodContributionReport(1, _results.Count, StageCount, retained, _results),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }),
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_CONTRIBUTIONS cycles={_results.Count} injectedFailures={StageCount} " +
            $"retained={retained} passed=true path={path}");
    }

    private async Task<WeakReference> RunCycleAsync(
        int cycle,
        int failureStage,
        CancellationToken cancellationToken)
    {
        var id = $"seasonal-operations-{cycle:000}";
        var rollback = new Stack<Func<ValueTask>>();
        var sentinel = new ContributionResourceSentinel();
        var reference = new WeakReference(sentinel);
        var stage = 0;
        try
        {
            if (!_eventContracts.TryGet(typeof(NavigationCommitted), out var eventDescriptor) || eventDescriptor is null)
            {
                throw new InvalidOperationException("The NavigationCommitted event contract is unavailable.");
            }

            var eventLease = await _eventContributions.CreateAsync(
                new EventBusContributionRequest(
                    id,
                    [
                        new EventPluginAccessRule(
                            eventDescriptor.ContractId,
                            EventChannel<NavigationCommitted>.DefaultName,
                            EventPluginAccess.Publish | EventPluginAccess.Subscribe),
                    ]),
                cancellationToken).ConfigureAwait(false);
            rollback.Push(() => eventLease.StopAsync());
            await using var eventSubscription = eventLease.Subscriber.Subscribe<NavigationCommitted>(
                EventPluginPlane.Shared,
                _ => ValueTask.CompletedTask);
            Inject(failureStage, stage++);

            var routeId = $"seasonal.route.{cycle:000}";
            var routeLease = _routes.AddContribution(
                id,
                [
                    new RouteDescriptor(
                        routeId,
                        RouteDefinitionKind.Route,
                        $"seasonal/{cycle:000}",
                        new ViewModelTargetDescriptor(typeof(SeasonalContributionViewModel))),
                ]);
            rollback.Push(() =>
            {
                routeLease.Dispose();
                return ValueTask.CompletedTask;
            });
            Inject(failureStage, stage++);

            var package = new LanguagePackageDescriptor(
                $"seasonal.package.{cycle:000}",
                CultureInfo.GetCultureInfo("en-US"),
                ResourceScope.Plugin)
            {
                ScopeId = id,
                ContributionId = id,
                InMemoryResources = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [$"Seasonal.{cycle:000}.Title"] = $"Seasonal operation {cycle:000}",
                },
            };
            var packageResult = _languagePackages.Register(package, id);
            Require(packageResult.Succeeded, packageResult.Error?.Message ?? "Localization contribution failed.");
            rollback.Push(async () =>
            {
                _ = await _localization.RevokePackagesByContributionIdAsync(id).ConfigureAwait(false);
            });
            Inject(failureStage, stage++);

            var dataResult = _data.BeginContribution(
                id,
                id,
                DataCapability.UseDataClient | DataCapability.UseHttpClient);
            Require(dataResult.Succeeded && dataResult.Value is not null,
                dataResult.Error?.Message ?? "Data contribution failed.");
            var dataLease = dataResult.Value!;
            rollback.Push(() => dataLease.RevokeAsync());
            Inject(failureStage, stage++);

            var permissionName = $"seasonal.permission.{cycle:000}";
            var policyName = $"seasonal.policy.{cycle:000}";
            Require(_permissions.Add(new PermissionDescriptor(
                permissionName,
                contributionId: id)),
                "Security permission contribution failed.");
            Require(_policies.Add(AuthorizationPolicy.RequirePermission(policyName, permissionName, id)),
                "Security policy contribution failed.");
            rollback.Push(() =>
            {
                _policies.RemoveByContribution(id);
                _permissions.RemoveByContribution(id);
                return ValueTask.CompletedTask;
            });
            Inject(failureStage, stage++);

            _views.Register(new ViewDescriptor(
                typeof(SeasonalContributionViewModel),
                typeof(DogfoodPageView),
                viewKey: id,
                viewFactory: _ => new DogfoodPageView(),
                pluginId: id,
                contributionId: id));
            rollback.Push(() =>
            {
                _views.RevokeContribution(id);
                return ValueTask.CompletedTask;
            });
            var resourceLease = _resources.Register(new PresentationResourceContribution(
                "dogfood-sentinel",
                sentinel,
                pluginId: id,
                contributionId: id));
            rollback.Push(() =>
            {
                resourceLease.Dispose();
                return ValueTask.CompletedTask;
            });
            Inject(failureStage, stage++);

            using var stateScope = _states.CreateScope(id);
            using var state = _states.CreateWritable(0, stateName: $"{id}.state");
            var stateNotifications = 0;
            stateScope.Add(state.OnChange(_ => stateNotifications++));
            state.Set(1);
            Require(stateNotifications == 1, "Scoped contribution state did not notify exactly once.");
            rollback.Push(() =>
            {
                stateScope.Dispose();
                return ValueTask.CompletedTask;
            });
            Inject(failureStage, stage++);

            var delivered = await eventLease.Publisher.PublishAsync(
                EventPluginPlane.Shared,
                new NavigationCommitted(Guid.NewGuid(), id, cycle),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Require(delivered.Succeeded, "Contribution EventBus publication failed.");
            Require(_routes.CurrentSnapshot.Routes.Any(route => route.RouteId == routeId),
                "Contribution route was not visible in the current graph.");
            Require(_permissions.Contains(permissionName) && _policies.Contains(policyName),
                "Contribution security entries were not visible.");
            _ledger.Record("contribution-active", id);
        }
        catch (InjectedContributionFailure) when (failureStage >= 0)
        {
            _ledger.Record("contribution-rollback", id, "expected-failure");
        }
        finally
        {
            await RollbackAsync(rollback).ConfigureAwait(false);
        }

        Require(!_routes.CurrentSnapshot.Routes.Any(route => route.ContributionId == id),
            "Contribution routes survived revocation.");
        Require(!_permissions.Permissions.Any(permission => permission.ContributionId == id),
            "Contribution permissions survived revocation.");
        Require(!_policies.Policies.Any(policy => policy.ContributionId == id),
            "Contribution policies survived revocation.");
        Require(!_resources.Contributions.Any(resource => resource.ContributionId == id),
            "Contribution Presentation resources survived revocation.");
        Require(!_languagePackages.Descriptors.Any(descriptor => descriptor.ContributionId == id),
            "Contribution language packages survived revocation.");
        _results.Add(new DogfoodContributionCycleResult(cycle, failureStage, stage, true));
        sentinel = null!;
        return reference;
    }

    private static async ValueTask RollbackAsync(Stack<Func<ValueTask>> rollback)
    {
        while (true)
        {
            Func<ValueTask>? action;
            lock (rollback)
            {
                action = rollback.Count == 0 ? null : rollback.Pop();
            }

            if (action is null)
            {
                return;
            }

            var concurrentRevocations = Enumerable.Range(0, 4)
                .Select(_ => action().AsTask())
                .ToArray();
            await Task.WhenAll(concurrentRevocations).ConfigureAwait(false);
        }
    }

    private static void Inject(int failureStage, int currentStage)
    {
        if (failureStage == currentStage)
        {
            throw new InjectedContributionFailure(currentStage);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class InjectedContributionFailure(int stage)
        : Exception($"Injected contribution activation failure at stage {stage}.");

    private sealed class ContributionResourceSentinel : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class SeasonalContributionViewModel;
}

internal sealed record DogfoodContributionReport(
    int SchemaVersion,
    int CycleCount,
    int InjectedFailureCount,
    int RetainedResourceCount,
    IReadOnlyList<DogfoodContributionCycleResult> Cycles);

internal sealed record DogfoodContributionCycleResult(
    int Cycle,
    int FailureStage,
    int CompletedStages,
    bool Passed);
