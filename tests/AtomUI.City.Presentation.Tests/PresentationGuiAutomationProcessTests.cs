using System.Text.Json;
using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationGuiAutomationProcessTests
{
    [Fact]
    [Trait("Category", "InteractiveDesktop")]
    public async Task WindowsDesktopDogfoodAcceptsRealSystemMouseAndKeyboardInput()
    {
        if (!OperatingSystem.IsWindows() ||
            !string.Equals(
                Environment.GetEnvironmentVariable("ATOMUI_CITY_RUN_INTERACTIVE_GUI"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var driverAssembly = FindSiblingAssembly(
            "AtomUI.City.Fixtures.DesktopDogfood.GuiAutomation",
            "net10.0-windows");
        var applicationAssembly = FindSiblingAssembly(
            "AtomUI.City.Fixtures.DesktopDogfood",
            "net10.0");
        Assert.True(File.Exists(driverAssembly), $"GUI automation driver was not built: {driverAssembly}");
        Assert.True(File.Exists(applicationAssembly), $"DesktopDogfood fixture was not built: {applicationAssembly}");
        var configuredArtifactRoot = Environment.GetEnvironmentVariable("ATOMUI_CITY_GUI_ARTIFACT_ROOT");
        var preserveArtifacts = !string.IsNullOrWhiteSpace(configuredArtifactRoot);
        var artifactRoot = preserveArtifacts
            ? Path.GetFullPath(configuredArtifactRoot!)
            : Path.Combine(
                Path.GetTempPath(),
                "AtomUI.City.Tests",
                "DesktopDogfood",
                $"gui-{Guid.NewGuid():N}");
        Assert.False(
            Directory.Exists(artifactRoot),
            $"GUI artifact directory must not already exist: {artifactRoot}");

        try
        {
            var result = await ProcessTestRunner.RunAsync(
                "dotnet",
                Path.GetDirectoryName(driverAssembly),
                TimeSpan.FromMinutes(8),
                driverAssembly,
                "--app",
                applicationAssembly,
                "--artifact-root",
                artifactRoot,
                "--seed",
                "20260911",
                "--countdown",
                "5");

            Assert.True(
                result.ExitCode == 0,
                $"GUI automation failed with exit code {result.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{result.StandardError}");
            Assert.Contains("DESKTOP_DOGFOOD_GUI_AUTOMATION success=true", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("DESKTOP_DOGFOOD_GUI_AUTOMATION_FAILED", result.StandardError, StringComparison.Ordinal);

            using var guiReport = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(artifactRoot, "gui-report.json")));
            var gui = guiReport.RootElement;
            Assert.True(gui.GetProperty("succeeded").GetBoolean());
            Assert.Equal(0, gui.GetProperty("applicationExitCode").GetInt32());
            Assert.True(gui.GetProperty("cursorRestored").GetBoolean());
            Assert.False(gui.GetProperty("emergencyAborted").GetBoolean());
            Assert.Equal(4, gui.GetProperty("discoveredWindowCount").GetInt32());
            Assert.True(gui.GetProperty("monitorCount").GetInt32() >= 1);
            Assert.True(gui.GetProperty("actions").GetArrayLength() >= 31);
            Assert.Equal(30, gui.GetProperty("controls").GetArrayLength());
            Assert.True(gui.GetProperty("screenshots").GetArrayLength() >= 13);
            Assert.Equal(JsonValueKind.Null, gui.GetProperty("failure").ValueKind);

            using var applicationReport = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(artifactRoot, "run-report.json")));
            var application = applicationReport.RootElement;
            Assert.Equal("Gui", application.GetProperty("profile").GetString());
            Assert.Equal(20260911, application.GetProperty("seed").GetInt32());
            Assert.Equal(0, application.GetProperty("exitCode").GetInt32());
            Assert.Equal(2, application.GetProperty("schemaVersion").GetInt32());
            Assert.True(application.GetProperty("scenarioResults").GetArrayLength() >= 25);
            Assert.Equal(
                13,
                application.GetProperty("categoryCounts").GetProperty("data-resilience").GetInt64());
            Assert.True(application.GetProperty("resourcesReleased").GetBoolean());
            Assert.Equal(JsonValueKind.Null, application.GetProperty("failure").ValueKind);
        }
        finally
        {
            if (!preserveArtifacts && Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    private static string FindSiblingAssembly(string projectName, string targetFramework) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            projectName,
            targetFramework,
            projectName + ".dll"));
}
