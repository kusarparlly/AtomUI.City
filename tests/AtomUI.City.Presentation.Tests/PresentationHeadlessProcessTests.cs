using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationHeadlessProcessTests
{
    [Fact]
    public async Task AvaloniaHeadlessLifecycleFixturePasses()
    {
        var assembly = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "AtomUI.City.Presentation.HeadlessApp",
            "net10.0",
            "AtomUI.City.Presentation.HeadlessApp.dll"));
        Assert.True(File.Exists(assembly), $"Headless fixture was not built: {assembly}");

        var result = await ProcessTestRunner.RunAsync(
            "dotnet",
            Path.GetDirectoryName(assembly),
            TimeSpan.FromSeconds(90),
            assembly);
        Assert.True(
            result.ExitCode == 0,
            $"Fixture failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}");
        Assert.Contains(
            "Presentation headless industrial scenario passed.",
            result.StandardOutput,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(20260911)]
    [InlineData(2147483629)]
    [Trait("Category", "HeadlessIntegration")]
    public async Task DesktopDogfoodHeadlessUserControlsDriveCrossModuleBusinessFlows(int seed)
    {
        var assembly = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "AtomUI.City.Fixtures.DesktopDogfood",
            "net10.0",
            "AtomUI.City.Fixtures.DesktopDogfood.dll"));
        Assert.True(File.Exists(assembly), $"Desktop Dogfood fixture was not built: {assembly}");
        var artifactRoot = Path.Combine(
            Path.GetTempPath(),
            "AtomUI.City.Tests",
            "DesktopDogfood",
            $"headless-controls-{seed}-{Guid.NewGuid():N}");

        try
        {
            var result = await ProcessTestRunner.RunAsync(
                "dotnet",
                Path.GetDirectoryName(assembly),
                TimeSpan.FromMinutes(5),
                assembly,
                "--profile",
                "headless",
                "--seed",
                seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--artifact-root",
                artifactRoot);

            Assert.True(
                result.ExitCode == 0,
                $"Headless control Dogfood failed with exit code {result.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{result.StandardError}");
            Assert.DoesNotContain("DESKTOP_DOGFOOD_FAILED", result.StandardError, StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_HEADLESS_UI controls=30",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "navigation=6 searches=3 localization=2 security=3",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains("scroll=1 renderFrames=3", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                "scenarios=2 scenarioEvidence=",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains("scenarioCancellations=1", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_READY profile=headless windows=4 outlets=12 modules=48 services=110",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_DATA_RESILIENCE faults=13",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "restarts=1 http=ready grpc=ready signalR=ready childReleased=true",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains("DESKTOP_DOGFOOD_CHECKPOINT host-stopped", result.StandardOutput, StringComparison.Ordinal);

            using var report = System.Text.Json.JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(artifactRoot, "run-report.json")));
            var root = report.RootElement;
            var categories = root.GetProperty("categoryCounts");
            Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
            Assert.Equal(seed, root.GetProperty("seed").GetInt32());
            Assert.True(root.GetProperty("actionCount").GetInt64() >= 20_000);
            Assert.True(root.GetProperty("expectedFailureCount").GetInt64() >= 9);
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.True(root.GetProperty("scenarioResults").GetArrayLength() >= 13);
            Assert.Equal(30, categories.GetProperty("ui-control").GetInt64());
            Assert.Equal(6, categories.GetProperty("ui-navigation").GetInt64());
            Assert.Equal(3, categories.GetProperty("ui-search").GetInt64());
            Assert.Equal(2, categories.GetProperty("ui-localization").GetInt64());
            Assert.Equal(3, categories.GetProperty("ui-security").GetInt64());
            Assert.Equal(2, categories.GetProperty("ui-scenario").GetInt64());
            Assert.Equal(1, categories.GetProperty("ui-scenario-cancel").GetInt64());
            Assert.True(categories.GetProperty("ui-pointer").GetInt64() >= 20);
            Assert.True(categories.GetProperty("ui-keyboard").GetInt64() >= 10);
            Assert.True(categories.GetProperty("ui-binding").GetInt64() >= 10);
            Assert.Equal(1, categories.GetProperty("ui-scroll").GetInt64());
            Assert.Equal(3, categories.GetProperty("ui-render").GetInt64());
            Assert.Equal(13, categories.GetProperty("data-resilience").GetInt64());
            Assert.True(root.GetProperty("resourcesReleased").GetBoolean());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, root.GetProperty("failure").ValueKind);
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }
}
