using Avalonia.Controls;
using Avalonia.Threading;
using AtomUI.City.Presentation;
using AtomUI.City.Routing;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodDesktopCoordinator
{
    private readonly IPresentationRuntime _runtime;
    private readonly DogfoodWindowFactory _windowFactory;
    private readonly IServiceProvider _services;
    private readonly IViewRegistry _viewRegistry;
    private readonly IViewModelFactory _viewModelFactory;
    private readonly ViewFactory _viewFactory;
    private readonly ViewBinder _viewBinder;
    private readonly DogfoodStateWorkload _stateWorkload;
    private readonly DogfoodEventWorkload _eventWorkload;
    private readonly DogfoodRoutingWorkload _routingWorkload;
    private readonly DogfoodLocalizationWorkload _localizationWorkload;
    private readonly DogfoodSecurityWorkload _securityWorkload;
    private readonly DogfoodDataWorkload _dataWorkload;
    private readonly DogfoodDataResilienceWorkload _dataResilienceWorkload;
    private readonly DogfoodNetworkLabWorkload _networkLabWorkload;
    private readonly DogfoodBusinessWorkload _businessWorkload;
    private readonly DogfoodConcurrencyWorkload _concurrencyWorkload;
    private readonly DogfoodScenarioOrchestrator _scenarioOrchestrator;
    private readonly DogfoodContributionWorkload _contributionWorkload;
    private readonly DogfoodInteractionViewModel _interactionViewModel;
    private readonly DogfoodAutomationWorkload _automationWorkload;
    private readonly DogfoodApiCoverageWorkload _apiCoverageWorkload;
    private readonly DogfoodRunLedger _ledger;
    private readonly DogfoodResourceMonitor _resourceMonitor;
    private readonly List<WindowSession> _auxiliarySessions = [];
    private WindowSession? _session;
    private int _initialized;

    public DogfoodDesktopCoordinator(
        IPresentationRuntime runtime,
        DogfoodWindowFactory windowFactory,
        IServiceProvider services,
        IViewRegistry viewRegistry,
        IViewModelFactory viewModelFactory,
        ViewFactory viewFactory,
        ViewBinder viewBinder,
        DogfoodStateWorkload stateWorkload,
        DogfoodEventWorkload eventWorkload,
        DogfoodRoutingWorkload routingWorkload,
        DogfoodLocalizationWorkload localizationWorkload,
        DogfoodSecurityWorkload securityWorkload,
        DogfoodDataWorkload dataWorkload,
        DogfoodDataResilienceWorkload dataResilienceWorkload,
        DogfoodNetworkLabWorkload networkLabWorkload,
        DogfoodBusinessWorkload businessWorkload,
        DogfoodConcurrencyWorkload concurrencyWorkload,
        DogfoodScenarioOrchestrator scenarioOrchestrator,
        DogfoodContributionWorkload contributionWorkload,
        DogfoodInteractionViewModel interactionViewModel,
        DogfoodAutomationWorkload automationWorkload,
        DogfoodApiCoverageWorkload apiCoverageWorkload,
        DogfoodRunLedger ledger,
        DogfoodResourceMonitor resourceMonitor)
    {
        _runtime = runtime;
        _windowFactory = windowFactory;
        _services = services;
        _viewRegistry = viewRegistry;
        _viewModelFactory = viewModelFactory;
        _viewFactory = viewFactory;
        _viewBinder = viewBinder;
        _stateWorkload = stateWorkload;
        _eventWorkload = eventWorkload;
        _routingWorkload = routingWorkload;
        _localizationWorkload = localizationWorkload;
        _securityWorkload = securityWorkload;
        _dataWorkload = dataWorkload;
        _dataResilienceWorkload = dataResilienceWorkload;
        _networkLabWorkload = networkLabWorkload;
        _businessWorkload = businessWorkload;
        _concurrencyWorkload = concurrencyWorkload;
        _scenarioOrchestrator = scenarioOrchestrator;
        _contributionWorkload = contributionWorkload;
        _interactionViewModel = interactionViewModel;
        _automationWorkload = automationWorkload;
        _apiCoverageWorkload = apiCoverageWorkload;
        _ledger = ledger;
        _resourceMonitor = resourceMonitor;
    }

    public async Task InitializeAsync(DogfoodMainWindow window, WindowSession session)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(session);
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            throw new InvalidOperationException("The desktop coordinator can only initialize one main window.");
        }

        _session = session;
        _interactionViewModel.AttachScenarioRunner((scenarioId, runMatrix, cancellationToken) =>
            _scenarioOrchestrator.RunAsync(
                scenarioId,
                runMatrix,
                (section, token) => NavigateAndPresentOnUiThreadAsync(session, section, token),
                cancellationToken));
        await Dispatcher.UIThread.InvokeAsync(static () => { });
        EnsureOutlets(session);
        RegisterViews();

        await CommitExistingAsync(session, "navigation", window.CreateNavigationContent(), "navigation-shell");
        await _routingWorkload.InitializeAsync(
            result => CommitNavigationAsync(session, result),
            session.Scope.CancellationToken);
        await CommitExistingAsync(session, "inspector", DogfoodMainWindow.CreateInspectorContent(), "runtime-inspector");
        await CommitExistingAsync(session, "activity", DogfoodMainWindow.CreateActivityContent(), "activity-stream");
        await _stateWorkload.AttachUiAsync(
            value => window.SetStatus("State projection", value));
        await _eventWorkload.InitializeAsync(
            session.Scope,
            value => window.SetStatus("EventBus workload", value));
        await _localizationWorkload.InitializeAsync(
            window.ApplyLocalizedNavigation,
            value => window.SetStatus("Localization workload", value),
            session.Scope.CancellationToken);
        await _securityWorkload.InitializeAsync(
            value => window.SetStatus("Security workload", value),
            session.Scope.CancellationToken);
        await _dataWorkload.InitializeAsync(
            value => window.SetStatus("Data workload", value),
            session.Scope.CancellationToken);
        await _dataResilienceWorkload.RunAsync(session.Scope.CancellationToken);
        if (DogfoodBootstrap.Options.Profile is
            DogfoodRunProfile.Network or DogfoodRunProfile.Extreme or DogfoodRunProfile.Soak)
        {
            await _networkLabWorkload.RunAsync(session.Scope.CancellationToken);
        }
        await _businessWorkload.RunAsync(session.Scope.CancellationToken);
        await _stateWorkload.RunStressAsync(
            DogfoodBootstrap.Options,
            _ledger,
            session.Scope.CancellationToken);
        await _concurrencyWorkload.RunAsync(session.Scope.CancellationToken);
        if (DogfoodBootstrap.Options.Profile is
            DogfoodRunProfile.Contribution or DogfoodRunProfile.Extreme or DogfoodRunProfile.Soak)
        {
            await _contributionWorkload.RunAsync(session.Scope.CancellationToken);
        }
        await OpenAuxiliaryWindowsAsync(window, session.Scope.CancellationToken);
        if (DogfoodBootstrap.Options.IsAutomated)
        {
            await _routingWorkload.TraverseCatalogAsync(
                result => CommitNavigationAsync(session, result),
                _ledger,
                session.Scope.CancellationToken);
            await ExerciseViewModelCatalogAsync(session, session.Scope.CancellationToken);
            if (DogfoodBootstrap.Options.Profile != DogfoodRunProfile.Headless)
            {
                await _scenarioOrchestrator.RunAsync(
                    scenarioId: null,
                    runMatrix: true,
                    (section, token) => NavigateAndPresentOnUiThreadAsync(session, section, token),
                    session.Scope.CancellationToken);
            }
        }

        await _resourceMonitor.BeginMeasuredPhaseAsync(session.Scope.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(() => _automationWorkload.RunAsync(
            (section, token) => NavigateAndPresentAsync(session, section, token),
            value => window.SetStatus("Automation workload", value),
            session.Scope.CancellationToken));
        if (DogfoodBootstrap.Options.Profile == DogfoodRunProfile.Api)
        {
            await _apiCoverageWorkload.RunAsync(session.Scope.CancellationToken);
        }

        window.WireNavigation(section => NavigateSectionAsync(window, section));
        window.SetStatus("Runtime ready", "Four managed windows and twelve outlets are active.");
    }

    public async Task CloseAuxiliaryWindowsAsync()
    {
        for (var index = _auxiliarySessions.Count - 1; index >= 0; index--)
        {
            var session = _auxiliarySessions[index];
            var closeWaiters = Enumerable.Range(0, 4)
                .Select(_ => session.CloseAsync(WindowCloseOrigin.Application).AsTask())
                .ToArray();
            var results = await Task.WhenAll(closeWaiters).ConfigureAwait(true);
            if (results.Any(static result => !result) ||
                session.State != WindowSessionState.Closed ||
                session.Scope.State != AtomUI.City.Core.Lifecycle.LifecycleScopeState.Disposed)
            {
                throw new InvalidOperationException(
                    $"Auxiliary Window '{session.Id}' did not merge four application close waiters into a released session.");
            }

            foreach (var _ in results)
            {
                _ledger.Record("window-close", session.Id);
            }
        }

        _auxiliarySessions.Clear();
    }

    public async Task CloseMainWindowAsync(WindowSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var closeWaiters = Enumerable.Range(0, 8)
            .Select(_ => session.CloseAsync(WindowCloseOrigin.Application).AsTask())
            .ToArray();
        var results = await Task.WhenAll(closeWaiters).ConfigureAwait(true);
        if (results.Any(static result => !result) ||
            session.State != WindowSessionState.Closed ||
            session.Scope.State != AtomUI.City.Core.Lifecycle.LifecycleScopeState.Disposed)
        {
            throw new InvalidOperationException(
                $"Main Window '{session.Id}' did not merge eight application close waiters into a released session.");
        }

        foreach (var _ in results)
        {
            _ledger.Record("window-close", session.Id);
        }
    }

    private void RegisterViews()
    {
        _viewRegistry.RegisterManifest(DogfoodViewModelCatalog.Types.Select(type =>
            type == typeof(DashboardViewModel)
                ? new ViewDescriptor(type, typeof(DashboardView), viewKey: null, _ => new DashboardView())
                : new ViewDescriptor(type, typeof(DogfoodPageView), viewKey: null, _ => new DogfoodPageView())));
    }

    private async Task CommitDashboardAsync(WindowSession session)
    {
        var descriptor = _viewRegistry.Locate(typeof(DashboardViewModel));
        var lease = await _viewModelFactory.AcquireAsync(
            new ViewModelAcquisitionRequest(typeof(DashboardViewModel), _services));
        BoundViewHandle? handle = null;
        try
        {
            var view = await _viewFactory.CreateAsync(descriptor);
            handle = _viewBinder.Bind(descriptor, view, lease.Instance);
            var result = await session.GetOutlet("primary").CommitAsync(
                RouteOutletCommitPlan.Replace(
                    "primary",
                    handle,
                    lease,
                    routeId: "dashboard.home",
                    reuseKey: "dashboard"));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Message ?? "Dashboard outlet commit failed.");
            }

            handle = null;
        }
        finally
        {
            if (handle is not null)
            {
                handle.Dispose();
                await lease.DisposeAsync();
            }
        }
    }

    private async Task CommitNavigationAsync(WindowSession session, NavigationResult navigation)
    {
        var target = navigation.Route.ViewModelTarget ??
            throw new InvalidOperationException($"Route '{navigation.Route.RouteId}' has no ViewModel target.");
        var descriptor = _viewRegistry.Locate(target.ViewModelType);
        var lease = await _viewModelFactory.AcquireAsync(
            new ViewModelAcquisitionRequest(target.ViewModelType, _services),
            session.Scope.CancellationToken);
        BoundViewHandle? handle = null;
        try
        {
            var view = await _viewFactory.CreateAsync(descriptor, session.Scope.CancellationToken);
            handle = _viewBinder.Bind(descriptor, view, lease.Instance);
            var outletName = navigation.Route.OutletName;
            var targetSession = ResolveOutletSession(session, outletName);
            var result = await targetSession.GetOutlet(outletName).CommitAsync(
                RouteOutletCommitPlan.Replace(
                    outletName,
                    handle,
                    lease,
                    navigation.Route.RouteId,
                    target.ReuseKey,
                    targetSession.Scope.CancellationToken),
                targetSession.Scope.CancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Message ?? "Router to Presentation commit failed.");
            }

            handle = null;
        }
        finally
        {
            if (handle is not null)
            {
                handle.Dispose();
                await lease.DisposeAsync();
            }
        }
    }

    private async Task ExerciseViewModelCatalogAsync(
        WindowSession session,
        CancellationToken cancellationToken)
    {
        foreach (var viewModelType in DogfoodViewModelCatalog.Types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = _viewRegistry.Locate(viewModelType);
            var lease = await _viewModelFactory.AcquireAsync(
                new ViewModelAcquisitionRequest(viewModelType, _services),
                cancellationToken);
            BoundViewHandle? handle = null;
            try
            {
                var view = await _viewFactory.CreateAsync(descriptor, cancellationToken);
                var attachedToVisualTree = false;
                if (view is Control visualControl)
                {
                    visualControl.AttachedToVisualTree += (_, _) => attachedToVisualTree = true;
                }

                handle = _viewBinder.Bind(descriptor, view, lease.Instance);
                var result = await session.GetOutlet("primary").CommitAsync(
                    RouteOutletCommitPlan.Replace(
                        "primary",
                        handle,
                        lease,
                        routeId: $"dogfood.viewmodel.{viewModelType.Name}",
                        reuseKey: viewModelType.FullName,
                        cancellationToken),
                    cancellationToken);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        result.Message ?? $"ViewModel '{viewModelType.Name}' failed to commit.");
                }

                handle = null;
                await Dispatcher.UIThread.InvokeAsync(static () => { });
                if (view is Control && !attachedToVisualTree)
                {
                    throw new InvalidOperationException(
                        $"ViewModel '{viewModelType.Name}' was bound but its View never attached to a VisualRoot.");
                }

                _ledger.Record("viewmodel", viewModelType.Name);
                await ExecuteCommandsAsync(lease.Instance, viewModelType, cancellationToken);
            }
            finally
            {
                if (handle is not null)
                {
                    handle.Dispose();
                    await lease.DisposeAsync();
                }
            }
        }

        if (_ledger.Covered("viewmodel") != 64 || _ledger.Covered("command") != 96)
        {
            throw new InvalidOperationException(
                $"MVVM traversal was incomplete: viewModels={_ledger.Covered("viewmodel")}, commands={_ledger.Covered("command")}.");
        }

        Console.WriteLine("DESKTOP_DOGFOOD_MVVM viewModels=64 commands=96 visualAttachments=64");
    }

    private async Task ExecuteCommandsAsync(
        object viewModel,
        Type viewModelType,
        CancellationToken cancellationToken)
    {
        var properties = viewModelType
            .GetProperties()
            .Where(static property => typeof(ICommand).IsAssignableFrom(property.PropertyType))
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (var property in properties)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = (ICommand?)property.GetValue(viewModel) ??
                throw new InvalidOperationException(
                    $"Command '{viewModelType.Name}.{property.Name}' resolved to null.");
            if (!command.CanExecute(null))
            {
                throw new InvalidOperationException(
                    $"Command '{viewModelType.Name}.{property.Name}' was unexpectedly disabled.");
            }

            if (command is IAsyncRelayCommand asyncCommand)
            {
                await asyncCommand.ExecuteAsync(null);
            }
            else
            {
                command.Execute(null);
            }

            _ledger.Record("command", $"{viewModelType.Name}.{property.Name}");
        }
    }

    private WindowSession ResolveOutletSession(WindowSession mainSession, string outletName)
    {
        if (mainSession.Outlets.Any(outlet => string.Equals(outlet.Name, outletName, StringComparison.Ordinal)))
        {
            return mainSession;
        }

        var matches = _auxiliarySessions
            .Where(candidate => candidate.Outlets.Any(outlet =>
                string.Equals(outlet.Name, outletName, StringComparison.Ordinal)))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No Window owns route outlet '{outletName}'."),
            _ => throw new InvalidOperationException($"Route outlet '{outletName}' has multiple candidate Windows."),
        };
    }

    private static async Task CommitExistingAsync(
        WindowSession session,
        string outletName,
        Control view,
        string routeId)
    {
        var viewModel = new object();
        var handle = BoundViewHandle.FromExisting(view, viewModel);
        var result = await session.GetOutlet(outletName).CommitAsync(
            RouteOutletCommitPlan.Replace(
                outletName,
                handle,
                ViewModelLease.Borrowed(viewModel),
                routeId));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message ?? $"Outlet '{outletName}' commit failed.");
        }
    }

    private async Task NavigateSectionAsync(DogfoodMainWindow window, string section)
    {
        try
        {
            var session = _session ?? throw new InvalidOperationException("No active main window session.");
            var navigation = await _routingWorkload.NavigateSectionAsync(section, session.Scope.CancellationToken);
            await CommitNavigationAsync(session, navigation);

            window.SetStatus("Navigation committed", $"Primary outlet now displays {section}.");
            _ledger.Record("ui-navigation", section);
        }
        catch (Exception exception)
        {
            window.SetStatus("Navigation failed", exception.Message, isError: true);
        }
    }

    private async Task<object> NavigateAndPresentAsync(
        WindowSession session,
        string section,
        CancellationToken cancellationToken)
    {
        var navigation = await _routingWorkload.NavigateSectionAsync(section, cancellationToken);
        await CommitNavigationAsync(session, navigation);
        return session.GetOutlet(navigation.Route.OutletName).CurrentEntry?.ViewModel ??
            throw new InvalidOperationException(
                $"Route '{navigation.Route.RouteId}' committed without a current Presentation entry.");
    }

    private async Task<object> NavigateAndPresentOnUiThreadAsync(
        WindowSession session,
        string section,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return await NavigateAndPresentAsync(session, section, cancellationToken);
        }

        var pendingNavigation = await Dispatcher.UIThread.InvokeAsync(
            () => NavigateAndPresentAsync(session, section, cancellationToken));
        return pendingNavigation;
    }

    private async Task OpenAuxiliaryWindowsAsync(Window owner, CancellationToken cancellationToken)
    {
        var specifications = new[]
        {
            new AuxiliaryWindowSpecification(
                "order-workbench",
                "Order Workbench",
                "Parallel order inspection and fulfillment timeline",
                [
                    new("primary", "Order workspace", [("Orders", "3"), ("Pending", "1")]),
                    new("details", "Order details", [("Selection", "ORDER-1001"), ("State", "Confirmed")]),
                    new("timeline", "Fulfillment timeline", [("Events", "8"), ("Shipment", "In transit")]),
                ]),
            new AuxiliaryWindowSpecification(
                "support-workspace",
                "Support Workspace",
                "Customer context and live support conversation",
                [
                    new("primary", "Support queue", [("Open", "4"), ("Priority", "2")]),
                    new("customer", "Customer context", [("Customer", "CUS-1001"), ("Orders", "6")]),
                    new("conversation", "Conversation", [("Messages", "12"), ("Status", "Active")]),
                ]),
            new AuxiliaryWindowSpecification(
                "diagnostics",
                "Diagnostics",
                "Host, event, state, route and transport probes",
                [
                    new("primary", "Runtime diagnostics", [("Modules", "48"), ("Services", "110")]),
                    new("probe", "Live probes", [("Events", "102"), ("Transports", "3")]),
                ]),
        };

        foreach (var specification in specifications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var window = _windowFactory.CreateAuxiliaryWindow(
                specification.WindowId,
                specification.Title,
                specification.Description,
                specification.Outlets.Select(static outlet => outlet.Name).ToArray());
            var session = _runtime.RegisterWindow(window, specification.WindowId);
            _auxiliarySessions.Add(session);

            window.Show(owner);
            await Dispatcher.UIThread.InvokeAsync(static () => { });
            var outletNames = session.Outlets
                .Select(static outlet => outlet.Name)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();
            var expectedOutletNames = specification.Outlets
                .Select(static outlet => outlet.Name)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();
            if (!outletNames.SequenceEqual(expectedOutletNames, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Auxiliary Window '{specification.WindowId}' did not register all named outlets.");
            }

            foreach (var outlet in specification.Outlets)
            {
                await CommitExistingAsync(
                    session,
                    outlet.Name,
                    window.CreateWorkspaceContent(outlet.Name, outlet.Headline, outlet.Metrics),
                    $"{specification.WindowId}.{outlet.Name}");
            }
        }

        if (_runtime.Windows.Count != 4 || _auxiliarySessions.Count != 3)
        {
            throw new InvalidOperationException(
                $"Expected four managed Windows; runtime={_runtime.Windows.Count}, auxiliary={_auxiliarySessions.Count}.");
        }
    }

    private static void EnsureOutlets(WindowSession session)
    {
        var names = session.Outlets.Select(static outlet => outlet.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
        var expected = new[] { "activity", "inspector", "navigation", "primary" };
        if (!names.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Expected four named outlets; observed [{string.Join(", ", names)}].");
        }
    }

    private sealed record AuxiliaryWindowSpecification(
        string WindowId,
        string Title,
        string Description,
        AuxiliaryOutletSpecification[] Outlets);

    private sealed record AuxiliaryOutletSpecification(
        string Name,
        string Headline,
        (string Name, string Value)[] Metrics);
}
