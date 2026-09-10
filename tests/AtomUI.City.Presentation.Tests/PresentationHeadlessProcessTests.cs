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
}
