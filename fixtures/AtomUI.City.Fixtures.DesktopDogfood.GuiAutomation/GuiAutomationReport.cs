using System.Text.Json;

namespace AtomUI.City.Fixtures.DesktopDogfood.GuiAutomation;

internal sealed class GuiAutomationReportBuilder
{
    private readonly List<string> _actions = [];
    private readonly List<string> _controls = [];
    private readonly List<string> _screenshots = [];

    public int ActionCount => _actions.Count;

    public int ControlCount => _controls.Count;

    public int ScreenshotCount => _screenshots.Count;

    public void RecordAction(string action) => _actions.Add(action);

    public void RecordControl(string automationId)
    {
        if (!_controls.Contains(automationId, StringComparer.Ordinal))
        {
            _controls.Add(automationId);
        }
    }

    public void RecordScreenshot(string path) => _screenshots.Add(Path.GetFileName(path));

    public void Write(
        GuiAutomationOptions options,
        bool succeeded,
        int? applicationExitCode,
        bool cursorRestored,
        bool emergencyAborted,
        int discoveredWindowCount,
        int monitorCount,
        string? failure,
        long elapsedMilliseconds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.GuiReportPath)!);
        var report = new GuiAutomationReport(
            SchemaVersion: 1,
            options.Seed,
            Succeeded: succeeded,
            ApplicationExitCode: applicationExitCode,
            CursorRestored: cursorRestored,
            EmergencyAborted: emergencyAborted,
            DiscoveredWindowCount: discoveredWindowCount,
            MonitorCount: monitorCount,
            ElapsedMilliseconds: elapsedMilliseconds,
            Failure: failure,
            Actions: _actions.ToArray(),
            Controls: _controls.Order(StringComparer.Ordinal).ToArray(),
            Screenshots: _screenshots.ToArray());
        File.WriteAllText(
            options.GuiReportPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }));
    }
}

internal sealed record GuiAutomationReport(
    int SchemaVersion,
    int Seed,
    bool Succeeded,
    int? ApplicationExitCode,
    bool CursorRestored,
    bool EmergencyAborted,
    int DiscoveredWindowCount,
    int MonitorCount,
    long ElapsedMilliseconds,
    string? Failure,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Controls,
    IReadOnlyList<string> Screenshots);
