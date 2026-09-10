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
}
