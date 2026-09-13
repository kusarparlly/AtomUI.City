using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AtomUI.City.EventBus;
using AtomUI.City.Data;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using AtomUI.City.Security;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodHeadlessControlWorkload
{
    private static readonly string[] Sections =
    [
        "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration",
    ];

    private static readonly string[] RequiredControlIds =
    [
        DogfoodAutomationIds.StatusTitle,
        DogfoodAutomationIds.StatusDetail,
        DogfoodAutomationIds.InteractionPanel,
        DogfoodAutomationIds.SearchQuery,
        DogfoodAutomationIds.SearchSubmit,
        DogfoodAutomationIds.SearchCancel,
        DogfoodAutomationIds.SearchClear,
        DogfoodAutomationIds.SearchStatus,
        DogfoodAutomationIds.SearchResults,
        DogfoodAutomationIds.CultureSelector,
        DogfoodAutomationIds.CultureApply,
        DogfoodAutomationIds.CulturePreview,
        DogfoodAutomationIds.AccountSelector,
        DogfoodAutomationIds.AccountSwitch,
        DogfoodAutomationIds.AccountStatus,
        DogfoodAutomationIds.ScenarioSelector,
        DogfoodAutomationIds.ScenarioRun,
        DogfoodAutomationIds.ScenarioRunMatrix,
        DogfoodAutomationIds.ScenarioCancel,
        DogfoodAutomationIds.ScenarioStatus,
        DogfoodAutomationIds.ScenarioSummary,
        DogfoodAutomationIds.RealtimeToggle,
        DogfoodAutomationIds.PrioritySlider,
        DogfoodAutomationIds.HistoryScroll,
        "dogfood-navigation-dashboard",
        "dogfood-navigation-commerce",
        "dogfood-navigation-fulfillment",
        "dogfood-navigation-customers",
        "dogfood-navigation-operations",
        "dogfood-navigation-administration",
    ];

    private readonly DogfoodInteractionViewModel _viewModel;
    private readonly DogfoodRunLedger _ledger;
    private readonly DogfoodDataRequestProbe _dataProbe;
    private readonly IEventBusMonitor _eventBusMonitor;
    private readonly ApplicationStateRegistry _stateRegistry;
    private readonly IAccountSessionManager _accountSessions;
    private readonly DogfoodRemoteOperations _remoteOperations;

    public DogfoodHeadlessControlWorkload(
        DogfoodInteractionViewModel viewModel,
        DogfoodRunLedger ledger,
        DogfoodDataRequestProbe dataProbe,
        IEventBusMonitor eventBusMonitor,
        IStateRegistry stateRegistry,
        IAccountSessionManager accountSessions,
        DogfoodRemoteOperations remoteOperations)
    {
        _viewModel = viewModel;
        _ledger = ledger;
        _dataProbe = dataProbe;
        _eventBusMonitor = eventBusMonitor;
        _stateRegistry = stateRegistry as ApplicationStateRegistry ??
            throw new InvalidOperationException(
                $"Headless control verification requires {nameof(ApplicationStateRegistry)}.");
        _accountSessions = accountSessions;
        _remoteOperations = remoteOperations;
    }

    public async Task RunAsync(
        DogfoodMainWindow window,
        WindowSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(session);

        window.Activate();
        Dispatcher.UIThread.RunJobs();
        VerifyAutomationSurface(window);

        var statusDetail = Find<TextBlock>(window, DogfoodAutomationIds.StatusDetail);
        await ExerciseNavigationAsync(window, session, statusDetail, cancellationToken);

        var searchBox = Find<TextBox>(window, DogfoodAutomationIds.SearchQuery);
        var searchButton = Find<Button>(window, DogfoodAutomationIds.SearchSubmit);
        var cancelButton = Find<Button>(window, DogfoodAutomationIds.SearchCancel);
        var clearButton = Find<Button>(window, DogfoodAutomationIds.SearchClear);
        var searchStatus = Find<TextBlock>(window, DogfoodAutomationIds.SearchStatus);
        var results = Find<ListBox>(window, DogfoodAutomationIds.SearchResults);
        await ExerciseSearchAsync(
            window,
            searchBox,
            searchButton,
            cancelButton,
            searchStatus,
            results,
            cancellationToken);

        await ExerciseLocalizationAsync(window, cancellationToken);
        await ExerciseSecurityAsync(window, cancellationToken);
        await ExerciseScenariosAsync(window, cancellationToken);
        await ExerciseStateBindingsAsync(window, cancellationToken);
        ExerciseSelectionAndScrolling(window, results);

        Click(window, clearButton);
        await WaitForAsync(
            () => _viewModel.SearchResults.Count == 0 &&
                  string.IsNullOrEmpty(_viewModel.SearchQuery) &&
                  string.Equals(searchStatus.Text, "Cleared by operator", StringComparison.Ordinal),
            "The clear command did not update the bound controls.",
            cancellationToken);
        _ledger.Record("ui-binding", "clear-command-projection");

        VerifyRenderedFrames(window);

        if (_ledger.Covered("ui-control") != RequiredControlIds.Length ||
            _ledger.Covered("ui-navigation") != Sections.Length ||
            _ledger.Count("ui-search") != 3 ||
            _ledger.Count("ui-localization") != 2 ||
            _ledger.Count("ui-security") != 3 ||
            _ledger.Count("security-offline-guard") != 1 ||
            _ledger.Count("ui-scenario") != 2 ||
            _ledger.Count("ui-scenario-cancel") != 1 ||
            _ledger.ScenarioResults.Count < 13 ||
            _ledger.Count("ui-render") != 3)
        {
            throw new InvalidOperationException(
                "Headless user-control coverage did not reach its required interaction matrix.");
        }

        Console.WriteLine(
            $"DESKTOP_DOGFOOD_HEADLESS_UI controls={_ledger.Covered("ui-control")} " +
            $"pointer={_ledger.Count("ui-pointer")} keyboard={_ledger.Count("ui-keyboard")} " +
            $"navigation={_ledger.Covered("ui-navigation")} searches={_ledger.Count("ui-search")} " +
            $"localization={_ledger.Count("ui-localization")} security={_ledger.Count("ui-security")} " +
            $"scenarios={_ledger.Count("ui-scenario")} scenarioEvidence={_ledger.ScenarioResults.Count} " +
            $"scenarioCancellations={_ledger.Count("ui-scenario-cancel")} " +
            $"state={_ledger.Count("ui-state")} selection={_ledger.Count("ui-selection")} " +
            $"scroll={_ledger.Count("ui-scroll")} renderFrames={_ledger.Count("ui-render")}");
    }

    private async Task ExerciseNavigationAsync(
        DogfoodMainWindow window,
        WindowSession session,
        TextBlock statusDetail,
        CancellationToken cancellationToken)
    {
        var initialNavigations = _ledger.Count("ui-navigation");
        for (var index = 0; index < Sections.Length; index++)
        {
            var section = Sections[index];
            Click(window, Find<Button>(window, DogfoodAutomationIds.Navigation(section)));
            var expectedCount = initialNavigations + index + 1;
            await WaitForAsync(
                () => _ledger.Count("ui-navigation") >= expectedCount &&
                      statusDetail.Text?.Contains(section, StringComparison.Ordinal) == true,
                $"Navigation button '{section}' did not drive Router and Presentation.",
                cancellationToken);
            if (!string.Equals(
                    session.GetOutlet("primary").CurrentEntry?.ViewModel.GetType().Name,
                    ExpectedViewModel(section),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Navigation button '{section}' committed an unexpected primary ViewModel.");
            }

            _ledger.Record("ui-binding", $"navigation-{section}");
        }
    }

    private async Task ExerciseSearchAsync(
        DogfoodMainWindow window,
        TextBox searchBox,
        Button searchButton,
        Button cancelButton,
        TextBlock searchStatus,
        ListBox results,
        CancellationToken cancellationToken)
    {
        Click(window, searchButton);
        await WaitForAsync(
            () => string.Equals(searchStatus.Text, "A search term is required", StringComparison.Ordinal),
            "Empty search validation did not reach the UI.",
            cancellationToken);

        Click(window, searchBox);
        if (!searchBox.IsFocused)
        {
            throw new InvalidOperationException("Raw pointer input did not focus the search TextBox.");
        }

        _ledger.Record("ui-focus", DogfoodAutomationIds.SearchQuery);
        TextInput(window, "priority orders");
        await WaitForAsync(
            () => string.Equals(searchBox.Text, "priority orders", StringComparison.Ordinal) &&
                  string.Equals(_viewModel.SearchQuery, "priority orders", StringComparison.Ordinal),
            "Text input did not update both the TextBox and its ViewModel binding.",
            cancellationToken);
        _ledger.Record("ui-binding", "search-query-two-way");

        PressKey(window, Key.Tab, PhysicalKey.Tab);
        await WaitForAsync(
            () => searchButton.IsFocused,
            "Tab navigation did not move focus from the TextBox to the search Button.",
            cancellationToken);
        _ledger.Record("ui-focus", DogfoodAutomationIds.SearchSubmit);

        Click(window, searchBox);
        var initialDataInvocations = _dataProbe.Invocations;
        var initialPublications = _eventBusMonitor.GetSnapshot().PublicationCount;
        PressKey(window, Key.Enter, PhysicalKey.Enter);
        await WaitForAsync(
            () => !searchButton.IsEffectivelyEnabled,
            "The async command did not disable its Button while Data was in flight.",
            cancellationToken);
        await WaitForAsync(
            () => string.Equals(searchStatus.Text, "Search completed: priority orders", StringComparison.Ordinal),
            "Enter did not drive the bound MVVM search command.",
            cancellationToken);
        VerifySearchSideEffects("priority orders", initialDataInvocations, initialPublications, results, 1);

        ReplaceText(window, searchBox, "button orders");
        initialDataInvocations = _dataProbe.Invocations;
        initialPublications = _eventBusMonitor.GetSnapshot().PublicationCount;
        Click(window, searchButton);
        await WaitForAsync(
            () => !searchButton.IsEffectivelyEnabled,
            "A pointer-triggered async command did not expose its disabled state.",
            cancellationToken);
        await WaitForAsync(
            () => string.Equals(searchStatus.Text, "Search completed: button orders", StringComparison.Ordinal),
            "Button input did not complete the Data search.",
            cancellationToken);
        VerifySearchSideEffects("button orders", initialDataInvocations, initialPublications, results, 2);

        ReplaceText(window, searchBox, "cancel request");
        initialDataInvocations = _dataProbe.Invocations;
        Click(window, searchButton);
        await WaitForAsync(
            () => !searchButton.IsEffectivelyEnabled &&
                  searchStatus.Text?.StartsWith("Searching for", StringComparison.Ordinal) == true,
            "The cancellable command did not enter its running visual state.",
            cancellationToken);
        Click(window, cancelButton);
        await WaitForAsync(
            () => string.Equals(searchStatus.Text, "Search cancelled: cancel request", StringComparison.Ordinal) &&
                  _viewModel.SearchExecution.LastResult?.Status == OperationStatus.Canceled,
            "The cancel Button did not cancel the running MVVM/Data operation.",
            cancellationToken);
        if (_dataProbe.Invocations <= initialDataInvocations || results.ItemCount != 2)
        {
            throw new InvalidOperationException("Cancelled search side effects were not bounded.");
        }

        _ledger.Record("ui-binding", "search-cancellation-projection");
    }

    private async Task ExerciseLocalizationAsync(
        DogfoodMainWindow window,
        CancellationToken cancellationToken)
    {
        var selector = Find<ComboBox>(window, DogfoodAutomationIds.CultureSelector);
        var apply = Find<Button>(window, DogfoodAutomationIds.CultureApply);
        var preview = Find<TextBlock>(window, DogfoodAutomationIds.CulturePreview);
        var dashboard = Find<Button>(window, DogfoodAutomationIds.Navigation("Dashboard"));

        SelectComboBoxItem(window, selector, Key.End, PhysicalKey.End);
        await WaitForAsync(
            () => string.Equals(_viewModel.SelectedCulture, "zh-CN", StringComparison.Ordinal),
            "Keyboard selection did not choose zh-CN.",
            cancellationToken);
        Click(window, apply);
        await WaitForAsync(
            () => string.Equals(preview.Text, "zh-CN: 仪表盘", StringComparison.Ordinal) &&
                  string.Equals(dashboard.Content as string, "仪表盘", StringComparison.Ordinal),
            "Localization command did not refresh bound and existing navigation controls.",
            cancellationToken);

        SelectComboBoxItem(window, selector, Key.Home, PhysicalKey.Home);
        await WaitForAsync(
            () => string.Equals(_viewModel.SelectedCulture, "en-US", StringComparison.Ordinal),
            "Keyboard selection did not restore en-US.",
            cancellationToken);
        Click(window, apply);
        await WaitForAsync(
            () => string.Equals(preview.Text, "en-US: Dashboard", StringComparison.Ordinal) &&
                  string.Equals(dashboard.Content as string, "Dashboard", StringComparison.Ordinal),
            "Localization command did not restore the English controls.",
            cancellationToken);
        _ledger.Record("ui-binding", "localized-navigation-refresh");
    }

    private async Task ExerciseSecurityAsync(
        DogfoodMainWindow window,
        CancellationToken cancellationToken)
    {
        var selector = Find<ComboBox>(window, DogfoodAutomationIds.AccountSelector);
        var switchButton = Find<Button>(window, DogfoodAutomationIds.AccountSwitch);
        var status = Find<TextBlock>(window, DogfoodAutomationIds.AccountStatus);

        if (!switchButton.Focus())
        {
            throw new InvalidOperationException("The account switch Button rejected keyboard focus.");
        }

        _ledger.Record("ui-focus", DogfoodAutomationIds.AccountSwitch);
        PressKey(window, Key.Enter, PhysicalKey.Enter);
        await WaitForAsync(
            () => string.Equals(status.Text, "Administrator: Online", StringComparison.Ordinal) &&
                  _accountSessions.Current.AccountKey?.SubjectId == "admin",
            "Account switch controls did not confirm the persisted administrator session.",
            cancellationToken);

        SelectComboBoxItem(window, selector, Key.Home, PhysicalKey.Home);
        PressKey(window, Key.Down, PhysicalKey.ArrowDown);
        await WaitForAsync(
            () => string.Equals(_viewModel.SelectedAccount, "Bob", StringComparison.Ordinal),
            "Keyboard selection did not choose the offline account.",
            cancellationToken);
        if (!switchButton.Focus())
        {
            throw new InvalidOperationException("The account switch Button rejected offline-account keyboard focus.");
        }

        PressKey(window, Key.Enter, PhysicalKey.Enter);
        await WaitForAsync(
            () => string.Equals(status.Text, "Bob: OfflineRestricted", StringComparison.Ordinal) &&
                  _accountSessions.Current.AccountKey?.SubjectId == "bob",
            "Account switch controls did not activate Bob's persisted offline session.",
            cancellationToken);
        var deniedOperation = await _remoteOperations.GetProductAsync(
            "SKU-OFFLINE-GUARD",
            cancellationToken: cancellationToken);
        if (deniedOperation.Succeeded || deniedOperation.Error?.Kind != DataErrorKind.AuthenticationExpired)
        {
            throw new InvalidOperationException(
                $"An offline-restricted account reached an online Data operation: {deniedOperation.Status}/{deniedOperation.Error?.Kind}.");
        }

        _ledger.Record("security-offline-guard", "data-request", "expected-failure");

        SelectComboBoxItem(window, selector, Key.End, PhysicalKey.End);
        await WaitForAsync(
            () => string.Equals(_viewModel.SelectedAccount, "Administrator", StringComparison.Ordinal),
            "Keyboard selection did not restore the online administrator account.",
            cancellationToken);
        if (!switchButton.Focus())
        {
            throw new InvalidOperationException("The account switch Button rejected online-account keyboard focus.");
        }

        PressKey(window, Key.Enter, PhysicalKey.Enter);
        await WaitForAsync(
            () => string.Equals(status.Text, "Administrator: Online", StringComparison.Ordinal) &&
                  _accountSessions.Current.AccountKey?.SubjectId == "admin",
            "Account switch controls did not restore the online administrator session.",
            cancellationToken);
        _ledger.Record("ui-binding", "account-session-projection");
    }

    private async Task ExerciseStateBindingsAsync(
        DogfoodMainWindow window,
        CancellationToken cancellationToken)
    {
        var realtime = Find<CheckBox>(window, DogfoodAutomationIds.RealtimeToggle);
        var priority = Find<Slider>(window, DogfoodAutomationIds.PrioritySlider);

        Click(window, realtime);
        await WaitForAsync(
            () => realtime.IsChecked == false &&
                  !_viewModel.RealtimeEnabled &&
                  string.Equals(
                      _stateRegistry.Get(DogfoodStateWorkload.NetworkReachability).Value,
                      "Realtime paused",
                      StringComparison.Ordinal),
            "CheckBox input did not propagate through ViewModel to State.",
            cancellationToken);
        Click(window, realtime);
        await WaitForAsync(
            () => realtime.IsChecked == true && _viewModel.RealtimeEnabled,
            "CheckBox input did not restore realtime State.",
            cancellationToken);

        Click(window, priority);
        PressKey(window, Key.Right, PhysicalKey.ArrowRight);
        PressKey(window, Key.Right, PhysicalKey.ArrowRight);
        await WaitForAsync(
            () => Math.Abs(priority.Value - 5) < double.Epsilon &&
                  Math.Abs(_viewModel.Priority - 5) < double.Epsilon,
            "Slider keyboard input did not update its two-way binding.",
            cancellationToken);
        _ledger.Record("ui-binding", "state-controls-two-way");
    }

    private async Task ExerciseScenariosAsync(
        DogfoodMainWindow window,
        CancellationToken cancellationToken)
    {
        var selector = Find<ComboBox>(window, DogfoodAutomationIds.ScenarioSelector);
        var run = Find<Button>(window, DogfoodAutomationIds.ScenarioRun);
        var runMatrix = Find<Button>(window, DogfoodAutomationIds.ScenarioRunMatrix);
        var cancel = Find<Button>(window, DogfoodAutomationIds.ScenarioCancel);
        var status = Find<TextBlock>(window, DogfoodAutomationIds.ScenarioStatus);
        var summary = Find<TextBlock>(window, DogfoodAutomationIds.ScenarioSummary);

        SelectComboBoxItem(window, selector, Key.End, PhysicalKey.End);
        await WaitForAsync(
            () => _viewModel.SelectedScenario.StartsWith("S12 |", StringComparison.Ordinal),
            "Scenario selector did not choose S12.",
            cancellationToken);
        Click(window, run);
        await WaitForAsync(
            () => string.Equals(status.Text, "Scenario completed: 1/1", StringComparison.Ordinal) &&
                  summary.Text?.StartsWith("1 scenarios | 2 routes |", StringComparison.Ordinal) == true,
            "The selected scenario did not complete its cross-module transaction.",
            cancellationToken);

        Click(window, runMatrix);
        await WaitForAsync(
            () => status.Text?.StartsWith("Running scenario matrix:", StringComparison.Ordinal) == true &&
                  !runMatrix.IsEffectivelyEnabled,
            "The scenario matrix did not enter its cancellable running state.",
            cancellationToken);
        Click(window, cancel);
        await WaitForAsync(
            () => string.Equals(status.Text, "Scenario matrix cancelled", StringComparison.Ordinal) &&
                  runMatrix.IsEffectivelyEnabled,
            "The scenario matrix did not expose a terminal cancelled state.",
            cancellationToken);

        Click(window, runMatrix);
        await WaitForAsync(
            () => string.Equals(status.Text, "Scenario matrix completed: 12/12", StringComparison.Ordinal) &&
                  summary.Text?.StartsWith("12 scenarios | 24 routes |", StringComparison.Ordinal) == true &&
                  _ledger.ScenarioResults.Count >= 13,
            "The scenario matrix did not recover and complete all twelve scenarios.",
            cancellationToken);
        if (DogfoodScenarioCatalog.All.Any(definition =>
                !_ledger.ScenarioResults.Any(result =>
                    string.Equals(result.ScenarioId, definition.Id, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException("Headless scenario evidence omitted a catalog entry.");
        }

        _ledger.Record("ui-binding", "scenario-matrix-projection");
    }

    private void ExerciseSelectionAndScrolling(DogfoodMainWindow window, ListBox results)
    {
        Click(window, results);
        PressKey(window, Key.Home, PhysicalKey.Home);
        if (results.SelectedIndex < 0)
        {
            throw new InvalidOperationException("ListBox did not accept pointer and keyboard selection.");
        }

        _ledger.Record("ui-selection", "search-result");

        var history = Find<ScrollViewer>(window, DogfoodAutomationIds.HistoryScroll);
        var initialOffset = history.Offset.Y;
        Scroll(window, history, new Vector(0, -8));
        if (history.Offset.Y <= initialOffset)
        {
            throw new InvalidOperationException("Mouse wheel input did not scroll the history control.");
        }

        _ledger.Record("ui-scroll", "operator-history");
    }

    private void VerifySearchSideEffects(
        string query,
        int initialDataInvocations,
        long initialPublications,
        ListBox results,
        int expectedResultCount)
    {
        if (_dataProbe.Invocations <= initialDataInvocations ||
            _eventBusMonitor.GetSnapshot().PublicationCount <= initialPublications ||
            !string.Equals(
                _stateRegistry.Get(DogfoodStateWorkload.SearchQuery).Value,
                query,
                StringComparison.Ordinal) ||
            results.ItemCount != expectedResultCount ||
            _viewModel.SearchResults[0].StartsWith(query, StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException(
                $"Search '{query}' did not complete the Control -> MVVM -> Data/EventBus/State chain.");
        }

        _ledger.Record("ui-binding", $"search-result-{expectedResultCount}");
    }

    private void VerifyAutomationSurface(DogfoodMainWindow window)
    {
        var controlsById = window.GetVisualDescendants()
            .OfType<Control>()
            .Select(control => new
            {
                Control = control,
                Id = AutomationProperties.GetAutomationId(control),
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(static item => item.Id!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);

        foreach (var id in RequiredControlIds)
        {
            if (!controlsById.TryGetValue(id, out var matches) || matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"AutomationId '{id}' expected exactly one visible control; observed {matches?.Length ?? 0}.");
            }

            _ledger.Record("ui-control", id);
        }
    }

    private void VerifyRenderedFrames(DogfoodMainWindow window)
    {
        var previousWidth = 0;
        foreach (var scaling in new[] { 1d, 1.5d, 2d })
        {
            window.SetRenderScaling(scaling);
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame() ??
                throw new InvalidOperationException($"Headless renderer returned no frame at {scaling:0.0}x scaling.");
            if (frame.PixelSize.Width <= 0 || frame.PixelSize.Height <= 0 || frame.PixelSize.Width < previousWidth)
            {
                throw new InvalidOperationException(
                    $"Headless frame dimensions were invalid at {scaling:0.0}x scaling: {frame.PixelSize}.");
            }
            if (!HasRenderedContent(frame))
            {
                throw new InvalidOperationException(
                    $"Headless frame contained no distinguishable UI pixels at {scaling:0.0}x scaling.");
            }

            previousWidth = frame.PixelSize.Width;
            _ledger.Record("ui-render", $"scale-{scaling:0.0}");
        }

        window.SetRenderScaling(1);
        Dispatcher.UIThread.RunJobs();
    }

    private static bool HasRenderedContent(Bitmap frame)
    {
        using var writable = new WriteableBitmap(
            frame.PixelSize,
            frame.Dpi,
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);
        using var framebuffer = writable.Lock();
        frame.CopyPixels(framebuffer);

        var pixels = new byte[checked(framebuffer.RowBytes * framebuffer.Size.Height)];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);
        var colors = new HashSet<uint>();
        var xStep = Math.Max(1, framebuffer.Size.Width / 64);
        var yStep = Math.Max(1, framebuffer.Size.Height / 64);
        for (var y = 0; y < framebuffer.Size.Height; y += yStep)
        {
            for (var x = 0; x < framebuffer.Size.Width; x += xStep)
            {
                var offset = checked((y * framebuffer.RowBytes) + (x * 4));
                var color = (uint)(pixels[offset] |
                                   (pixels[offset + 1] << 8) |
                                   (pixels[offset + 2] << 16) |
                                   (pixels[offset + 3] << 24));
                colors.Add(color);
                if (colors.Count >= 8)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void SelectComboBoxItem(
        DogfoodMainWindow window,
        ComboBox comboBox,
        Key key,
        PhysicalKey physicalKey)
    {
        Click(window, comboBox);
        PressKey(window, key, physicalKey);
        PressKey(window, Key.Enter, PhysicalKey.Enter);
        PressKey(window, Key.Escape, PhysicalKey.Escape);
        comboBox.IsDropDownOpen = false;
        Dispatcher.UIThread.RunJobs();

        _ledger.Record("ui-selection", AutomationProperties.GetAutomationId(comboBox) ?? comboBox.GetType().Name);
    }

    private void ReplaceText(DogfoodMainWindow window, TextBox textBox, string value)
    {
        Click(window, textBox);
        PressKey(window, Key.A, PhysicalKey.A, RawInputModifiers.Control, "a");
        TextInput(window, value);
    }

    private void Click(DogfoodMainWindow window, Control control)
    {
        Dispatcher.UIThread.RunJobs();
        var point = CenterInWindow(window, control);
        var hit = window.InputHitTest(point) as Visual;
        if (string.Equals(hit?.GetType().Name, "LightDismissOverlayLayer", StringComparison.Ordinal))
        {
            SendPointerClick(window, point);
            Dispatcher.UIThread.RunJobs();
            _ledger.Record("ui-pointer", "light-dismiss-overlay");
            hit = window.InputHitTest(point) as Visual;
        }

        if (hit is null ||
            (!ReferenceEquals(hit, control) && !hit.GetVisualAncestors().Contains(control)))
        {
            var hitPath = hit is null
                ? "none"
                : string.Join(
                    " > ",
                    new[] { hit }
                        .Concat(hit.GetVisualAncestors())
                        .Take(10)
                        .Select(static visual => visual is Control candidate
                            ? $"{candidate.GetType().Name}[{AutomationProperties.GetAutomationId(candidate) ?? "-"}]"
                            : visual.GetType().Name));
            throw new InvalidOperationException(
                $"Raw pointer target '{AutomationProperties.GetAutomationId(control)}' was occluded; " +
                $"point={point}, hitPath={hitPath}, bounds={control.Bounds}.");
        }

        SendPointerClick(window, point);
        Dispatcher.UIThread.RunJobs();
        _ledger.Record("ui-pointer", AutomationProperties.GetAutomationId(control) ?? control.GetType().Name);
    }

    private static void SendPointerClick(DogfoodMainWindow window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
    }

    private void Scroll(DogfoodMainWindow window, Control control, Vector delta)
    {
        var point = CenterInWindow(window, control);
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseWheel(point, delta, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        _ledger.Record("ui-pointer", $"wheel:{AutomationProperties.GetAutomationId(control)}");
    }

    private void TextInput(DogfoodMainWindow window, string value)
    {
        window.KeyTextInput(value);
        Dispatcher.UIThread.RunJobs();
        _ledger.Record("ui-keyboard", $"text:{value}");
    }

    private void PressKey(
        DogfoodMainWindow window,
        Key key,
        PhysicalKey physicalKey,
        RawInputModifiers modifiers = RawInputModifiers.None,
        string? keySymbol = null)
    {
        window.KeyPress(key, modifiers, physicalKey, keySymbol);
        window.KeyRelease(key, modifiers, physicalKey, keySymbol);
        Dispatcher.UIThread.RunJobs();
        _ledger.Record("ui-keyboard", key.ToString());
    }

    private static Point CenterInWindow(DogfoodMainWindow window, Control control)
    {
        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            throw new InvalidOperationException(
                $"Control '{AutomationProperties.GetAutomationId(control)}' has no arranged bounds.");
        }

        var localPoint = control is CheckBox
            ? new Point(Math.Min(12, control.Bounds.Width / 2), control.Bounds.Height / 2)
            : new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        return control.TranslatePoint(
                   localPoint,
                   window) ??
               throw new InvalidOperationException(
                   $"Control '{AutomationProperties.GetAutomationId(control)}' is not attached to the main Window.");
    }

    private static TControl Find<TControl>(DogfoodMainWindow window, string automationId)
        where TControl : Control
    {
        var matches = window.GetVisualDescendants()
            .OfType<TControl>()
            .Where(control => string.Equals(
                AutomationProperties.GetAutomationId(control),
                automationId,
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Expected one {typeof(TControl).Name} with AutomationId '{automationId}'; observed {matches.Length}."),
        };
    }

    private static async Task WaitForAsync(
        Func<bool> predicate,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(static () => { });
            if (predicate())
            {
                return;
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(true);
        }

        throw new TimeoutException(failureMessage);
    }

    private static string ExpectedViewModel(string section) => section switch
    {
        "Dashboard" => nameof(DashboardViewModel),
        "Commerce" => nameof(ProductListViewModel),
        "Fulfillment" => nameof(ShipmentListViewModel),
        "Customers" => nameof(CustomerListViewModel),
        "Operations" => nameof(ReportCenterViewModel),
        "Administration" => nameof(UserAdminViewModel),
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };
}
