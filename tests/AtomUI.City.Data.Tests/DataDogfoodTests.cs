using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Data.Tests;

public sealed class DataDogfoodTests
{
    private static readonly IReadOnlyDictionary<string, string?> InvalidProxyEnvironment =
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["HTTP_PROXY"] = "http://127.0.0.1:1",
            ["HTTPS_PROXY"] = "http://127.0.0.1:1",
            ["ALL_PROXY"] = "http://127.0.0.1:1",
            ["NO_PROXY"] = null,
            ["no_proxy"] = null,
        };

    [Fact]
    public async Task RealLocalHttpGrpcAndSignalRFixturePasses()
    {
        var assembly = GetFixtureAssembly();
        Assert.True(File.Exists(assembly), $"Headless fixture was not built: {assembly}");

        var result = await ProcessTestRunner.RunAsync(
            "dotnet",
            Path.GetDirectoryName(assembly),
            TimeSpan.FromSeconds(45),
            InvalidProxyEnvironment,
            assembly);

        Assert.True(
            result.ExitCode == 0,
            $"Fixture failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}");
        Assert.Contains("DATA_HEADLESS_OK", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeadlessFixtureConvertsUnhandledFailureToStableProcessResult()
    {
        var assembly = GetFixtureAssembly();
        Assert.True(File.Exists(assembly), $"Headless fixture was not built: {assembly}");

        var result = await ProcessTestRunner.RunAsync(
            "dotnet",
            Path.GetDirectoryName(assembly),
            TimeSpan.FromSeconds(15),
            environment: null,
            assembly,
            "--expected-failure");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("DATA_HEADLESS_EXPECTED_FAILURE", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception.", result.StandardError, StringComparison.Ordinal);
    }

    private static string GetFixtureAssembly()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "AtomUI.City.Data.HeadlessApp",
            "net10.0",
            "AtomUI.City.Data.HeadlessApp.dll"));
    }
}
