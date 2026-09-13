using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationDesktopProcessTests
{
    [Fact]
    [Trait("Category", "DesktopIntegration")]
    public async Task WindowsAvaloniaDesktopFixturePasses()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var assembly = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "AtomUI.City.Presentation.DesktopApp",
            "net10.0",
            "AtomUI.City.Presentation.DesktopApp.dll"));
        Assert.True(File.Exists(assembly), $"Desktop fixture was not built: {assembly}");

        var result = await ProcessTestRunner.RunAsync(
            "dotnet",
            Path.GetDirectoryName(assembly),
            TimeSpan.FromSeconds(60),
            assembly);
        Assert.True(
            result.ExitCode == 0,
            $"Desktop fixture failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}");
        Assert.Contains(
            "Presentation Windows desktop self-test passed.",
            result.StandardOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "DesktopIntegration")]
    public Task WindowsDesktopDogfoodQuickProfilePasses() =>
        RunDesktopDogfoodProfileAsync("quick", expectedStateUpdates: 128, minimumActions: 500, TimeSpan.FromSeconds(120));

    [Fact]
    [Trait("Category", "DesktopIntegration")]
    public Task WindowsDesktopDogfoodStandardProfilePasses() =>
        RunDesktopDogfoodProfileAsync("standard", expectedStateUpdates: 1024, minimumActions: 10_000, TimeSpan.FromMinutes(5));

    private static async Task RunDesktopDogfoodProfileAsync(
        string profile,
        int expectedStateUpdates,
        int minimumActions,
        TimeSpan timeout)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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
            $"{profile}-{Guid.NewGuid():N}");

        try
        {
            var result = await ProcessTestRunner.RunAsync(
                "dotnet",
                Path.GetDirectoryName(assembly),
                timeout,
                assembly,
                "--profile",
                profile,
                "--seed",
                "20260911",
                "--artifact-root",
                artifactRoot);

            Assert.True(
                result.ExitCode == 0,
                $"Desktop Dogfood failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}");
            Assert.DoesNotContain("DESKTOP_DOGFOOD_FAILED", result.StandardError, StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_STATE application=72 computed=20 collections=16 scoped=20 persisted=30",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_ROUTING routes=96 layouts=6 groups=8 indexes=10 regular=60 redirects=6 extensions=6 viewModels=64 commands=96 entries=8",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_EVENTBUS contracts=72 channels=72 publications=72 deliveries=145",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_LOCALIZATION keys=480 packages=12 switches=100 tracked=16 notifications=1600 culture=en-US",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_SECURITY permissions=64 policies=24 accounts=3 accountSwitches=4 restored=1 states=8 tokenStatuses=7 revision=15",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains("DESKTOP_DOGFOOD_DATA httpHandlers=", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_DATA_RESILIENCE faults=13",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "restarts=1 http=ready grpc=ready signalR=ready childReleased=true",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains("DESKTOP_DOGFOOD_SERVICES services=110 workflows=8", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_ROUTE_TRAVERSAL definitions=96 navigated=76 indexes=10 redirects=6",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_MVVM viewModels=64 commands=96 visualAttachments=64",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_SCENARIOS batch=1 completed=12",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                $"DESKTOP_DOGFOOD_STATE_STRESS updates={expectedStateUpdates} final={expectedStateUpdates} boundedWrites=64",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "DESKTOP_DOGFOOD_CONCURRENCY queueTimeouts=1 maxEventConcurrency=3 cancellationBoundaries=2",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains($"DESKTOP_DOGFOOD_AUTOMATION profile={profile}", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                $"DESKTOP_DOGFOOD_READY profile={profile} windows=4 outlets=12 modules=48 services=110",
                result.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains($"DESKTOP_DOGFOOD_REPORT profile={profile}", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("DESKTOP_DOGFOOD_CHECKPOINT host-stopped", result.StandardOutput, StringComparison.Ordinal);

            using var report = System.Text.Json.JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(artifactRoot, "run-report.json")));
            var root = report.RootElement;
            Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
            Assert.True(root.GetProperty("actionCount").GetInt64() >= minimumActions);
            Assert.True(root.GetProperty("expectedFailureCount").GetInt64() >= 9);
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(12, root.GetProperty("scenarioResults").GetArrayLength());
            Assert.Equal(
                13,
                root.GetProperty("categoryCounts").GetProperty("data-resilience").GetInt64());
            Assert.Equal(
                20,
                root.GetProperty("categoryCounts").GetProperty("window-close").GetInt64());
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
