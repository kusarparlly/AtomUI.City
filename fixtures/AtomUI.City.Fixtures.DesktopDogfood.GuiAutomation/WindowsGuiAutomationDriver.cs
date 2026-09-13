using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;

namespace AtomUI.City.Fixtures.DesktopDogfood.GuiAutomation;

internal sealed class WindowsGuiAutomationDriver
{
    private const string MainWindowTitle = "AtomUI.City Operations Workbench";
    private static readonly string[] AuxiliaryWindowTitles =
    [
        "Order Workbench",
        "Support Workspace",
        "Diagnostics",
    ];

    private static readonly string[] RequiredControlIds =
    [
        "dogfood-main-window",
        "dogfood-status-title",
        "dogfood-status-detail",
        "dogfood-search-query",
        "dogfood-search-submit",
        "dogfood-search-cancel",
        "dogfood-search-clear",
        "dogfood-search-status",
        "dogfood-search-results",
        "dogfood-culture-selector",
        "dogfood-culture-apply",
        "dogfood-culture-preview",
        "dogfood-account-selector",
        "dogfood-account-switch",
        "dogfood-account-status",
        "dogfood-scenario-selector",
        "dogfood-scenario-run",
        "dogfood-scenario-run-matrix",
        "dogfood-scenario-cancel",
        "dogfood-scenario-status",
        "dogfood-scenario-summary",
        "dogfood-realtime-toggle",
        "dogfood-priority-slider",
        "dogfood-history-scroll",
        "dogfood-navigation-dashboard",
        "dogfood-navigation-commerce",
        "dogfood-navigation-fulfillment",
        "dogfood-navigation-customers",
        "dogfood-navigation-operations",
        "dogfood-navigation-administration",
    ];

    private readonly GuiAutomationOptions _options;
    private readonly GuiAutomationReportBuilder _report = new();
    private readonly StringBuilder _standardOutput = new();
    private readonly StringBuilder _standardError = new();
    private readonly object _outputSyncRoot = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private Process? _application;
    private int _screenshotSequence;
    private int _discoveredWindowCount;
    private int _monitorCount;
    private int? _applicationExitCode;
    private bool _emergencyAborted;

    public WindowsGuiAutomationDriver(GuiAutomationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public int Run()
    {
        Directory.CreateDirectory(_options.ArtifactRoot);
        NativeDesktop.EnsureInteractiveDesktop();
        NativeDesktop.DisableWindowsCrashDialogs();
        var originalCursor = NativeDesktop.GetCursorPosition();
        var cursorRestored = false;
        var succeeded = false;
        string? failure = null;

        try
        {
            Countdown();
            StartApplication();
            if (!_ready.Wait(TimeSpan.FromMinutes(3)))
            {
                throw new TimeoutException(
                    $"DesktopDogfood did not publish its GUI ready signal. STDOUT:{Environment.NewLine}{OutputTail(_standardOutput)}");
            }

            var process = _application ?? throw new InvalidOperationException("DesktopDogfood process was not started.");
            var windows = WaitForWindows(process.Id, expectedCount: 4, TimeSpan.FromSeconds(30));
            _discoveredWindowCount = windows.Count;
            VerifyWindowSet(windows);
            var mainWindow = windows.Single(window =>
                string.Equals(SafeName(window), MainWindowTitle, StringComparison.Ordinal));
            var mainHandle = (nint)mainWindow.Current.NativeWindowHandle;

            ExerciseWindowManagement(process.Id, mainWindow, windows);
            PositionMainWindow(process.Id, mainHandle);
            mainWindow = WaitForWindow(process.Id, MainWindowTitle, TimeSpan.FromSeconds(10));
            VerifyAutomationSurface(mainWindow);
            Capture(mainWindow, process.Id, "main-ready");

            ExerciseNavigation(mainWindow, mainHandle, process.Id);
            ExerciseSearch(mainWindow, mainHandle, process.Id);
            ExerciseLocalization(mainWindow, mainHandle, process.Id);
            ExerciseSecurity(mainWindow, mainHandle, process.Id);
            ExerciseStateAndCollectionControls(mainWindow, mainHandle, process.Id);
            ExerciseScenarios(mainWindow, mainHandle, process.Id);
            ExerciseWindowSizes(mainWindow, mainHandle, process.Id);
            Capture(mainWindow, process.Id, "main-final");

            NativeDesktop.ActivateWindow(mainHandle, process.Id);
            NativeDesktop.Key(NativeDesktop.VkF4, alt: true);
            _report.RecordAction("main-window:alt-f4");
            if (!process.WaitForExit((int)TimeSpan.FromMinutes(2).TotalMilliseconds))
            {
                throw new TimeoutException("DesktopDogfood did not exit after the system Alt+F4 close request.");
            }

            process.WaitForExit();
            _applicationExitCode = process.ExitCode;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"DesktopDogfood exited with code {process.ExitCode}. STDERR:{Environment.NewLine}{OutputTail(_standardError)}");
            }

            ValidateApplicationReport();
            succeeded = true;
            Console.WriteLine(
                $"DESKTOP_DOGFOOD_GUI_AUTOMATION success=true windows={_discoveredWindowCount} " +
                $"controls={_report.ControlCount} actions={_report.ActionCount} screenshots={_report.ScreenshotCount} " +
                $"monitors={_monitorCount}");
            return 0;
        }
        catch (GuiAutomationAbortedException exception)
        {
            _emergencyAborted = true;
            failure = $"{exception.GetType().FullName}: {exception.Message}";
            Console.Error.WriteLine($"DESKTOP_DOGFOOD_GUI_AUTOMATION_ABORTED {exception.Message}");
            return 2;
        }
        catch (Exception exception)
        {
            failure = $"{exception.GetType().FullName}: {exception.Message}";
            Console.Error.WriteLine($"DESKTOP_DOGFOOD_GUI_AUTOMATION_FAILED {exception}");
            return 1;
        }
        finally
        {
            if (_application is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                _applicationExitCode = process.ExitCode;
            }

            cursorRestored = NativeDesktop.RestoreCursor(originalCursor);
            _elapsed.Stop();
            _report.Write(
                _options,
                succeeded,
                _applicationExitCode,
                cursorRestored,
                _emergencyAborted,
                _discoveredWindowCount,
                _monitorCount,
                failure,
                _elapsed.ElapsedMilliseconds);
        }
    }

    private void Countdown()
    {
        Console.WriteLine(
            "GUI automation will control the system mouse and keyboard. " +
            "Press Ctrl+Shift+F12 to abort.");
        for (var remaining = _options.CountdownSeconds; remaining > 0; remaining--)
        {
            Console.WriteLine($"DESKTOP_DOGFOOD_GUI_COUNTDOWN {remaining}");
            Thread.Sleep(1000);
            NativeDesktop.ThrowIfEmergencyAbortRequested();
        }
    }

    private void StartApplication()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(_options.ApplicationAssembly)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ErrorDialog = false,
        };
        startInfo.ArgumentList.Add(_options.ApplicationAssembly);
        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add("gui");
        startInfo.ArgumentList.Add("--seed");
        startInfo.ArgumentList.Add(_options.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--artifact-root");
        startInfo.ArgumentList.Add(_options.ArtifactRoot);
        startInfo.Environment["AVALONIA_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        startInfo.Environment["COMPlus_DbgEnableMiniDump"] = "0";
        startInfo.Environment["DOTNET_DbgEnableMiniDump"] = "0";

        _application = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        _application.OutputDataReceived += (_, args) => CaptureOutput(args.Data, _standardOutput, isError: false);
        _application.ErrorDataReceived += (_, args) => CaptureOutput(args.Data, _standardError, isError: true);
        if (!_application.Start())
        {
            throw new InvalidOperationException("DesktopDogfood process could not be started.");
        }

        _application.BeginOutputReadLine();
        _application.BeginErrorReadLine();
        _report.RecordAction($"application:start:{_application.Id}");
    }

    private void CaptureOutput(string? line, StringBuilder destination, bool isError)
    {
        if (line is null)
        {
            return;
        }

        lock (_outputSyncRoot)
        {
            destination.AppendLine(line);
        }

        if (!isError && line.StartsWith("DESKTOP_DOGFOOD_READY profile=gui ", StringComparison.Ordinal))
        {
            _ready.Set();
        }
    }

    private void VerifyWindowSet(IReadOnlyList<AutomationElement> windows)
    {
        var names = windows.Select(SafeName).ToArray();
        foreach (var expected in AuxiliaryWindowTitles.Prepend(MainWindowTitle))
        {
            if (names.Count(name => string.Equals(name, expected, StringComparison.Ordinal)) != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one GUI window named '{expected}'; observed: {string.Join(", ", names)}.");
            }
        }

        _report.RecordAction("windows:verified-four");
    }

    private void ExerciseWindowManagement(
        int processId,
        AutomationElement mainWindow,
        IReadOnlyList<AutomationElement> windows)
    {
        foreach (var window in windows
                     .Where(window => !string.Equals(SafeName(window), MainWindowTitle, StringComparison.Ordinal))
                     .OrderBy(SafeName, StringComparer.Ordinal))
        {
            Capture(window, processId, $"window-{Sanitize(SafeName(window))}");
        }

        var diagnostics = windows.Single(window =>
            string.Equals(SafeName(window), "Diagnostics", StringComparison.Ordinal));
        var diagnosticsPattern = GetPattern<WindowPattern>(diagnostics, WindowPattern.Pattern, "Diagnostics WindowPattern");
        diagnosticsPattern.SetWindowVisualState(WindowVisualState.Minimized);
        WaitUntil(
            () => GetPattern<WindowPattern>(
                    WaitForWindow(processId, "Diagnostics", TimeSpan.FromSeconds(2)),
                    WindowPattern.Pattern,
                    "Diagnostics WindowPattern")
                .Current.WindowVisualState == WindowVisualState.Minimized,
            "Diagnostics window did not minimize.",
            TimeSpan.FromSeconds(5));
        diagnosticsPattern.SetWindowVisualState(WindowVisualState.Normal);
        _report.RecordAction("auxiliary-window:minimize-restore");

        foreach (var auxiliaryTitle in AuxiliaryWindowTitles)
        {
            var auxiliary = windows.Single(window =>
                string.Equals(SafeName(window), auxiliaryTitle, StringComparison.Ordinal));
            GetPattern<WindowPattern>(auxiliary, WindowPattern.Pattern, $"{auxiliaryTitle} WindowPattern").Close();
        }

        WaitUntil(
            () => FindTopLevelWindows(processId).Count == 1,
            "Auxiliary windows did not close before main-window input.",
            TimeSpan.FromSeconds(10));
        NativeDesktop.ActivateWindow((nint)mainWindow.Current.NativeWindowHandle, processId);
        _report.RecordAction("auxiliary-windows:closed");
    }

    private void PositionMainWindow(int processId, nint mainHandle)
    {
        var workingArea = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ??
            throw new InvalidOperationException("Windows did not report a primary display.");
        if (workingArea.Width < 1120 || workingArea.Height < 740)
        {
            throw new InvalidOperationException(
                $"GUI automation requires at least a 1120x740 primary working area; actual={workingArea.Width}x{workingArea.Height}.");
        }

        var width = Math.Min(1440, workingArea.Width - 40);
        var height = Math.Min(900, workingArea.Height - 40);
        var bounds = new System.Drawing.Rectangle(
            workingArea.Left + ((workingArea.Width - width) / 2),
            workingArea.Top + ((workingArea.Height - height) / 2),
            width,
            height);
        NativeDesktop.MoveWindow(mainHandle, bounds);
        NativeDesktop.ActivateWindow(mainHandle, processId);
        Thread.Sleep(300);
        _report.RecordAction($"main-window:positioned:{width}x{height}");
    }

    private void VerifyAutomationSurface(AutomationElement mainWindow)
    {
        foreach (var automationId in RequiredControlIds)
        {
            if (string.Equals(automationId, "dogfood-main-window", StringComparison.Ordinal))
            {
                if (!string.Equals(mainWindow.Current.AutomationId, automationId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Main window AutomationId was '{mainWindow.Current.AutomationId}', expected '{automationId}'.");
                }
            }
            else
            {
                _ = FindControl(mainWindow, automationId);
            }
            _report.RecordControl(automationId);
        }

        _report.RecordAction("automation-surface:30-controls");
    }

    private void ExerciseNavigation(AutomationElement mainWindow, nint mainHandle, int processId)
    {
        var sections = new[]
        {
            "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration",
        };
        foreach (var section in sections)
        {
            ClickControl(mainWindow, mainHandle, processId, $"dogfood-navigation-{section.ToLowerInvariant()}");
            WaitControlText(
                mainWindow,
                "dogfood-status-detail",
                text => text.Contains(section, StringComparison.Ordinal),
                $"Navigation status did not reach '{section}'.");
            _report.RecordAction($"navigation:{section}");
        }

        Capture(mainWindow, processId, "navigation-administration");
    }

    private void ExerciseSearch(AutomationElement mainWindow, nint mainHandle, int processId)
    {
        ClickControl(mainWindow, mainHandle, processId, "dogfood-search-query");
        NativeDesktop.Key(NativeDesktop.VkA, control: true);
        NativeDesktop.Key(NativeDesktop.VkEscape);
        ClickControl(mainWindow, mainHandle, processId, "dogfood-search-submit");
        WaitControlText(
            mainWindow,
            "dogfood-search-status",
            text => string.Equals(text, "A search term is required", StringComparison.Ordinal),
            "Empty search validation did not reach the native automation tree.");
        _report.RecordAction("search:validation");

        EnterText(mainWindow, mainHandle, processId, "gui orders");
        NativeDesktop.Key(NativeDesktop.VkReturn);
        WaitControlText(
            mainWindow,
            "dogfood-search-status",
            text => string.Equals(text, "Search completed: gui orders", StringComparison.Ordinal),
            "Enter-triggered GUI search did not complete.");
        _report.RecordAction("search:enter-success");

        EnterText(mainWindow, mainHandle, processId, "cancel request");
        ClickControl(mainWindow, mainHandle, processId, "dogfood-search-submit");
        WaitControlText(
            mainWindow,
            "dogfood-search-status",
            text => text.StartsWith("Searching for", StringComparison.Ordinal),
            "Cancellable GUI search did not enter its running state.");
        ClickControl(mainWindow, mainHandle, processId, "dogfood-search-cancel");
        WaitControlText(
            mainWindow,
            "dogfood-search-status",
            text => string.Equals(text, "Search cancelled: cancel request", StringComparison.Ordinal),
            "GUI cancel button did not cancel the running search.");
        _report.RecordAction("search:cancelled");
    }

    private void ExerciseLocalization(AutomationElement mainWindow, nint mainHandle, int processId)
    {
        SelectComboBox(mainWindow, mainHandle, processId, "dogfood-culture-selector", NativeDesktop.VkEnd);
        WaitComboBoxSelection(mainWindow, "dogfood-culture-selector", "zh-CN");
        ClickControl(mainWindow, mainHandle, processId, "dogfood-culture-apply");
        WaitControlText(
            mainWindow,
            "dogfood-culture-preview",
            text => string.Equals(text, "zh-CN: 仪表盘", StringComparison.Ordinal),
            "Native GUI culture switch did not project Chinese text.");
        Capture(mainWindow, processId, "culture-zh-CN");
        _report.RecordAction("localization:zh-CN");

        SelectComboBox(mainWindow, mainHandle, processId, "dogfood-culture-selector", NativeDesktop.VkHome);
        WaitComboBoxSelection(mainWindow, "dogfood-culture-selector", "en-US");
        ClickControl(mainWindow, mainHandle, processId, "dogfood-culture-apply");
        WaitControlText(
            mainWindow,
            "dogfood-culture-preview",
            text => string.Equals(text, "en-US: Dashboard", StringComparison.Ordinal),
            "Native GUI culture switch did not restore English text.");
        _report.RecordAction("localization:en-US");
    }

    private void ExerciseSecurity(AutomationElement mainWindow, nint mainHandle, int processId)
    {
        SelectComboBox(
            mainWindow,
            mainHandle,
            processId,
            "dogfood-account-selector",
            NativeDesktop.VkHome,
            NativeDesktop.VkDown);
        WaitComboBoxSelection(mainWindow, "dogfood-account-selector", "Bob");
        NativeDesktop.Key(NativeDesktop.VkTab);
        NativeDesktop.Key(NativeDesktop.VkReturn);
        WaitControlText(
            mainWindow,
            "dogfood-account-status",
            text => string.Equals(text, "Bob: OfflineRestricted", StringComparison.Ordinal),
            "Native GUI account switch did not activate Bob's offline session.");
        Capture(mainWindow, processId, "security-bob-offline");
        _report.RecordAction("security:bob-offline");

        SelectComboBox(mainWindow, mainHandle, processId, "dogfood-account-selector", NativeDesktop.VkEnd);
        WaitComboBoxSelection(mainWindow, "dogfood-account-selector", "Administrator");
        NativeDesktop.Key(NativeDesktop.VkTab);
        NativeDesktop.Key(NativeDesktop.VkReturn);
        WaitControlText(
            mainWindow,
            "dogfood-account-status",
            text => string.Equals(text, "Administrator: Online", StringComparison.Ordinal),
            "Native GUI account switch did not restore the administrator session.");
        _report.RecordAction("security:administrator-online");
    }

    private void ExerciseStateAndCollectionControls(
        AutomationElement mainWindow,
        nint mainHandle,
        int processId)
    {
        var realtime = FindControl(mainWindow, "dogfood-realtime-toggle");
        var initialToggle = GetPattern<TogglePattern>(realtime, TogglePattern.Pattern, "Realtime TogglePattern")
            .Current.ToggleState;
        ClickElement(realtime, mainHandle, processId);
        WaitUntil(
            () => GetPattern<TogglePattern>(
                    FindControl(mainWindow, "dogfood-realtime-toggle"),
                    TogglePattern.Pattern,
                    "Realtime TogglePattern")
                .Current.ToggleState != initialToggle,
            "Realtime checkbox did not change ToggleState.",
            TimeSpan.FromSeconds(5));
        ClickControl(mainWindow, mainHandle, processId, "dogfood-realtime-toggle");
        WaitUntil(
            () => GetPattern<TogglePattern>(
                    FindControl(mainWindow, "dogfood-realtime-toggle"),
                    TogglePattern.Pattern,
                    "Realtime TogglePattern")
                .Current.ToggleState == initialToggle,
            "Realtime checkbox did not restore ToggleState.",
            TimeSpan.FromSeconds(5));
        _report.RecordAction("state:checkbox-two-way");

        ClickControl(mainWindow, mainHandle, processId, "dogfood-priority-slider");
        NativeDesktop.Key(NativeDesktop.VkHome);
        for (var index = 0; index < 4; index++)
        {
            NativeDesktop.Key(NativeDesktop.VkRight);
        }
        WaitUntil(
            () => Math.Abs(GetPattern<RangeValuePattern>(
                    FindControl(mainWindow, "dogfood-priority-slider"),
                    RangeValuePattern.Pattern,
                    "Priority RangeValuePattern").Current.Value - 5) < double.Epsilon,
            "Priority slider did not reach value 5 through keyboard input.",
            TimeSpan.FromSeconds(5));
        _report.RecordAction("state:slider-five");

        var results = FindControl(mainWindow, "dogfood-search-results");
        ClickElement(results, mainHandle, processId);
        NativeDesktop.Key(NativeDesktop.VkHome);
        WaitUntil(
            () => HasSelectedListItem(FindControl(mainWindow, "dogfood-search-results")),
            "Search result list did not accept keyboard selection.",
            TimeSpan.FromSeconds(5));
        _report.RecordAction("collection:list-selection");

        var history = FindControl(mainWindow, "dogfood-history-scroll");
        var scrollPattern = GetPattern<ScrollPattern>(history, ScrollPattern.Pattern, "History ScrollPattern");
        var initialScroll = scrollPattern.Current.VerticalScrollPercent;
        var center = Center(history);
        NativeDesktop.ActivateWindow(mainHandle, processId);
        NativeDesktop.Wheel(center.X, center.Y, -6);
        WaitUntil(
            () => GetPattern<ScrollPattern>(
                    FindControl(mainWindow, "dogfood-history-scroll"),
                    ScrollPattern.Pattern,
                    "History ScrollPattern").Current.VerticalScrollPercent > initialScroll,
            "History ScrollViewer did not move after system mouse-wheel input.",
            TimeSpan.FromSeconds(5));
        _report.RecordAction("collection:mouse-wheel");

        ClickControl(mainWindow, mainHandle, processId, "dogfood-search-clear");
        WaitControlText(
            mainWindow,
            "dogfood-search-status",
            text => string.Equals(text, "Cleared by operator", StringComparison.Ordinal),
            "Clear results button did not update UI state.");
        _report.RecordAction("search:clear");
    }

    private void ExerciseWindowSizes(AutomationElement mainWindow, nint mainHandle, int processId)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen ??
            throw new InvalidOperationException("Windows did not report a primary display.");
        _monitorCount = System.Windows.Forms.Screen.AllScreens.Length;
        var minimum = new System.Drawing.Rectangle(
            screen.WorkingArea.Left + 20,
            screen.WorkingArea.Top + 20,
            1080,
            700);
        NativeDesktop.MoveWindow(mainHandle, minimum);
        Thread.Sleep(500);
        Capture(WaitForWindow(processId, MainWindowTitle, TimeSpan.FromSeconds(5)), processId, "main-1080x700");
        _report.RecordAction("main-window:size-1080x700");

        if (_monitorCount > 1)
        {
            var secondary = System.Windows.Forms.Screen.AllScreens.First(display => !display.Primary);
            var width = Math.Min(1440, secondary.WorkingArea.Width - 40);
            var height = Math.Min(900, secondary.WorkingArea.Height - 40);
            if (width >= 1080 && height >= 700)
            {
                NativeDesktop.MoveWindow(
                    mainHandle,
                    new System.Drawing.Rectangle(
                        secondary.WorkingArea.Left + 20,
                        secondary.WorkingArea.Top + 20,
                        width,
                        height));
                NativeDesktop.ActivateWindow(mainHandle, processId);
                Thread.Sleep(500);
                Capture(
                    WaitForWindow(processId, MainWindowTitle, TimeSpan.FromSeconds(5)),
                    processId,
                    "main-secondary-monitor");
                _report.RecordAction("main-window:secondary-monitor");
            }
            else
            {
                _report.RecordAction("main-window:secondary-monitor-insufficient-size");
            }
        }
        else
        {
            _report.RecordAction("main-window:single-monitor-environment");
        }

        var restoredBounds = screen.WorkingArea;
        var restoredWidth = Math.Min(1440, restoredBounds.Width - 40);
        var restoredHeight = Math.Min(900, restoredBounds.Height - 40);
        NativeDesktop.MoveWindow(
            mainHandle,
            new System.Drawing.Rectangle(
                restoredBounds.Left + ((restoredBounds.Width - restoredWidth) / 2),
                restoredBounds.Top + ((restoredBounds.Height - restoredHeight) / 2),
                restoredWidth,
                restoredHeight));

        var windowPattern = GetPattern<WindowPattern>(mainWindow, WindowPattern.Pattern, "Main WindowPattern");
        windowPattern.SetWindowVisualState(WindowVisualState.Minimized);
        Thread.Sleep(400);
        windowPattern.SetWindowVisualState(WindowVisualState.Normal);
        NativeDesktop.ActivateWindow(mainHandle, processId);
        Thread.Sleep(500);
        Capture(WaitForWindow(processId, MainWindowTitle, TimeSpan.FromSeconds(5)), processId, "main-restored");
        _report.RecordAction("main-window:minimize-restore");
    }

    private void ExerciseScenarios(AutomationElement mainWindow, nint mainHandle, int processId)
    {
        SelectComboBox(
            mainWindow,
            mainHandle,
            processId,
            "dogfood-scenario-selector",
            NativeDesktop.VkEnd);
        WaitComboBoxSelection(mainWindow, "dogfood-scenario-selector", "S12 | Full domain reconciliation");
        ClickControl(mainWindow, mainHandle, processId, "dogfood-scenario-run");
        WaitControlText(
            mainWindow,
            "dogfood-scenario-status",
            text => string.Equals(text, "Scenario completed: 1/1", StringComparison.Ordinal),
            "The native GUI selected scenario did not complete.");
        WaitControlText(
            mainWindow,
            "dogfood-scenario-summary",
            text => text.StartsWith("1 scenarios | 2 routes |", StringComparison.Ordinal),
            "The native GUI selected scenario did not publish its summary.");
        Capture(mainWindow, processId, "scenario-selected-s12");
        _report.RecordAction("scenario:selected-s12");

        for (var pass = 1; pass <= 2; pass++)
        {
            ClickControl(mainWindow, mainHandle, processId, "dogfood-scenario-run-matrix");
            WaitControlText(
                mainWindow,
                "dogfood-scenario-status",
                text => text.StartsWith("Running scenario matrix:", StringComparison.Ordinal),
                $"Scenario matrix pass {pass} did not enter its running state.");
            WaitControlText(
                mainWindow,
                "dogfood-scenario-status",
                text => string.Equals(text, "Scenario matrix completed: 12/12", StringComparison.Ordinal),
                $"Scenario matrix pass {pass} did not complete all twelve scenarios.");
            WaitControlText(
                mainWindow,
                "dogfood-scenario-summary",
                text => text.StartsWith("12 scenarios | 24 routes |", StringComparison.Ordinal),
                $"Scenario matrix pass {pass} did not publish its cross-module summary.");
            Capture(mainWindow, processId, $"scenario-matrix-pass-{pass}");
            _report.RecordAction($"scenario:matrix-pass-{pass}");
        }
    }

    private void EnterText(AutomationElement mainWindow, nint mainHandle, int processId, string text)
    {
        ClickControl(mainWindow, mainHandle, processId, "dogfood-search-query");
        NativeDesktop.Key(NativeDesktop.VkA, control: true);
        NativeDesktop.Text(text);
        WaitControlText(
            mainWindow,
            "dogfood-search-query",
            value => string.Equals(value, text, StringComparison.Ordinal),
            $"System keyboard input did not produce '{text}'.");
    }

    private void SelectComboBox(
        AutomationElement mainWindow,
        nint mainHandle,
        int processId,
        string automationId,
        params ushort[] selectionKeys)
    {
        ClickControl(mainWindow, mainHandle, processId, automationId);
        foreach (var selectionKey in selectionKeys)
        {
            NativeDesktop.Key(selectionKey);
        }
        NativeDesktop.Key(NativeDesktop.VkReturn);
        Thread.Sleep(250);
        _report.RecordAction($"combobox:{automationId}:{string.Join('-', selectionKeys)}");
    }

    private void WaitComboBoxSelection(
        AutomationElement mainWindow,
        string automationId,
        string expectedSelection) =>
        WaitUntil(
            () => string.Equals(
                ReadSelection(FindControl(mainWindow, automationId)),
                expectedSelection,
                StringComparison.Ordinal),
            $"ComboBox '{automationId}' did not select '{expectedSelection}'.",
            TimeSpan.FromSeconds(5));

    private void ClickControl(
        AutomationElement mainWindow,
        nint mainHandle,
        int processId,
        string automationId) =>
        ClickElement(FindControl(mainWindow, automationId), mainHandle, processId);

    private void ClickElement(AutomationElement element, nint mainHandle, int processId)
    {
        if (element.Current.IsOffscreen)
        {
            throw new InvalidOperationException(
                $"Automation element '{element.Current.AutomationId}' is offscreen and cannot receive system input.");
        }

        var center = Center(element);
        NativeDesktop.ActivateWindow(mainHandle, processId);
        NativeDesktop.Click(center.X, center.Y);
        Thread.Sleep(150);
    }

    private void WaitControlText(
        AutomationElement mainWindow,
        string automationId,
        Func<string, bool> predicate,
        string failureMessage) =>
        WaitUntil(
            () => predicate(ReadText(FindControl(mainWindow, automationId))),
            failureMessage,
            TimeSpan.FromSeconds(15));

    private static string ReadText(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
        {
            return ((ValuePattern)valuePattern).Current.Value ?? string.Empty;
        }

        return element.Current.Name ?? string.Empty;
    }

    private static string ReadSelection(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(SelectionPattern.Pattern, out var selectionPattern))
        {
            var selection = ((SelectionPattern)selectionPattern).Current.GetSelection();
            if (selection.Length == 1)
            {
                return selection[0].Current.Name ?? string.Empty;
            }
        }

        return ReadText(element);
    }

    private static bool HasSelectedListItem(AutomationElement list)
    {
        var items = list.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
        return items.Cast<AutomationElement>().Any(item =>
            item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionItemPattern) &&
            ((SelectionItemPattern)selectionItemPattern).Current.IsSelected);
    }

    private static (double X, double Y) Center(AutomationElement element)
    {
        var bounds = element.Current.BoundingRectangle;
        if (bounds.IsEmpty || bounds.Width <= 1 || bounds.Height <= 1 ||
            double.IsNaN(bounds.X) || double.IsNaN(bounds.Y))
        {
            throw new InvalidOperationException(
                $"Automation element '{element.Current.AutomationId}' has invalid screen bounds '{bounds}'.");
        }

        var x = element.Current.ControlType == ControlType.CheckBox
            ? bounds.Left + Math.Min(12, bounds.Width / 2)
            : bounds.Left + (bounds.Width / 2);
        return (x, bounds.Top + (bounds.Height / 2));
    }

    private void Capture(AutomationElement window, int processId, string name)
    {
        NativeDesktop.ActivateWindow((nint)window.Current.NativeWindowHandle, processId);
        Thread.Sleep(250);
        var bounds = window.Current.BoundingRectangle;
        var rectangle = System.Drawing.Rectangle.FromLTRB(
            (int)Math.Floor(bounds.Left),
            (int)Math.Floor(bounds.Top),
            (int)Math.Ceiling(bounds.Right),
            (int)Math.Ceiling(bounds.Bottom));
        var path = Path.Combine(
            _options.ArtifactRoot,
            "screenshots",
            $"{++_screenshotSequence:00}-{name}.png");
        NativeDesktop.CaptureScreen(rectangle, path);
        _report.RecordScreenshot(path);
    }

    private void ValidateApplicationReport()
    {
        var path = Path.Combine(_options.ArtifactRoot, "run-report.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("DesktopDogfood did not produce run-report.json.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 2 ||
            root.GetProperty("exitCode").GetInt32() != 0 ||
            root.GetProperty("seed").GetInt32() != _options.Seed ||
            !root.GetProperty("resourcesReleased").GetBoolean() ||
            root.GetProperty("failure").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException("DesktopDogfood run report did not confirm a clean GUI shutdown.");
        }

        var categories = root.GetProperty("categoryCounts");
        RequireCategory(categories, "ui-navigation", 6);
        RequireCategory(categories, "ui-search", 2);
        RequireCategory(categories, "ui-localization", 2);
        RequireCategory(categories, "ui-security", 2);
        RequireCategory(categories, "ui-state", 4);
        RequireCategory(categories, "ui-scenario", 3);
        var scenarios = root.GetProperty("scenarioResults").EnumerateArray().ToArray();
        if (scenarios.Length < 25 ||
            scenarios.Select(item => item.GetProperty("scenarioId").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count() != 12 ||
            scenarios.Any(item =>
                item.GetProperty("modules").GetArrayLength() < 6 ||
                item.GetProperty("invariants").GetArrayLength() < 7 ||
                item.GetProperty("navigations").GetInt32() != 2 ||
                item.GetProperty("eventPublications").GetInt32() != 2 ||
                item.GetProperty("dataOperations").GetInt32() != 2))
        {
            throw new InvalidOperationException(
                $"DesktopDogfood scenario evidence was incomplete; observed {scenarios.Length} results.");
        }

        _report.RecordAction("application-report:validated");
    }

    private static void RequireCategory(JsonElement categories, string name, long minimum)
    {
        if (!categories.TryGetProperty(name, out var count) || count.GetInt64() < minimum)
        {
            throw new InvalidOperationException(
                $"DesktopDogfood report category '{name}' did not reach {minimum}.");
        }
    }

    private static AutomationElement FindControl(AutomationElement root, string automationId)
    {
        var matches = root.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
        return matches.Count switch
        {
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Expected one native automation element '{automationId}'; observed {matches.Count}."),
        };
    }

    private static AutomationElement WaitForWindow(int processId, string title, TimeSpan timeout)
    {
        AutomationElement? found = null;
        WaitUntil(
            () =>
            {
                found = FindTopLevelWindows(processId).FirstOrDefault(window =>
                    string.Equals(SafeName(window), title, StringComparison.Ordinal));
                return found is not null;
            },
            $"Window '{title}' was not found for process {processId}.",
            timeout);
        return found!;
    }

    private static IReadOnlyList<AutomationElement> WaitForWindows(
        int processId,
        int expectedCount,
        TimeSpan timeout)
    {
        IReadOnlyList<AutomationElement> windows = [];
        WaitUntil(
            () =>
            {
                windows = FindTopLevelWindows(processId);
                return windows.Count == expectedCount;
            },
            $"Expected {expectedCount} top-level windows for process {processId}; observed {windows.Count}.",
            timeout);
        return windows;
    }

    private static IReadOnlyList<AutomationElement> FindTopLevelWindows(int processId)
    {
        return NativeDesktop.EnumerateTopLevelWindows(processId)
            .Select(windowHandle =>
            {
                try
                {
                    return AutomationElement.FromHandle(windowHandle);
                }
                catch (ElementNotAvailableException)
                {
                    return null;
                }
            })
            .Where(static element => element is not null)
            .Select(static element => element!)
            .ToArray();
    }

    private static TPattern GetPattern<TPattern>(
        AutomationElement element,
        AutomationPattern pattern,
        string description)
        where TPattern : class
    {
        if (!element.TryGetCurrentPattern(pattern, out var value) || value is not TPattern typed)
        {
            throw new InvalidOperationException($"Native automation element does not support {description}.");
        }

        return typed;
    }

    private static void WaitUntil(
        Func<bool> predicate,
        string failureMessage,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastException = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            NativeDesktop.ThrowIfEmergencyAbortRequested();
            try
            {
                if (predicate())
                {
                    return;
                }
            }
            catch (ElementNotAvailableException exception)
            {
                lastException = exception;
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException(
            lastException is null ? failureMessage : $"{failureMessage} Last UIA error: {lastException.Message}");
    }

    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Current.Name ?? string.Empty;
        }
        catch (ElementNotAvailableException)
        {
            return string.Empty;
        }
    }

    private string OutputTail(StringBuilder output)
    {
        lock (_outputSyncRoot)
        {
            var value = output.ToString();
            return value.Length <= 4000 ? value : value[^4000..];
        }
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-'));
}
