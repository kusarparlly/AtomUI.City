using System.Text.Json;

namespace AtomUI.City.Cli.Tests;

public sealed class CliNewAppTests
{
    [Fact]
    public async Task NewAppDryRunEmitsPlanWithoutWritingFiles()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--output",
            host.WorkingDirectory,
            "--dry-run",
            "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src", "SalesClient")));
        using var json = run.ReadJson();
        var changes = json.RootElement.GetProperty("data").GetProperty("plan").GetProperty("changes");
        Assert.Contains(
            changes.EnumerateArray(),
            change => change.GetProperty("path").GetString() == "src/SalesClient/SalesClient.csproj");
    }

    [Fact]
    public async Task NewAppCreatesDesktopApplicationAndTestProject()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--namespace",
            "Company.SalesClient",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.True(File.Exists(Path.Combine(host.WorkingDirectory, "src", "SalesClient", "SalesClient.csproj")));
        Assert.True(File.Exists(Path.Combine(host.WorkingDirectory, "src", "SalesClient", "Program.cs")));
        Assert.True(File.Exists(Path.Combine(host.WorkingDirectory, "src", "SalesClient", "App.axaml")));
        Assert.True(File.Exists(Path.Combine(host.WorkingDirectory, "src", "SalesClient", "DesktopBootstrap.cs")));
        Assert.True(File.Exists(Path.Combine(host.WorkingDirectory, "src", "SalesClient", "MainWindow.axaml")));
        Assert.True(File.Exists(Path.Combine(host.WorkingDirectory, "tests", "SalesClient.Tests", "FeatureTestMatrix.md")));
        Assert.True(File.Exists(Path.Combine(host.WorkingDirectory, "tests", "SalesClient.Tests", "ApplicationSmokeTests.cs")));
    }

    [Fact]
    public async Task NewAppJsonIncludesRenderedArtifacts()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var artifacts = json.RootElement.GetProperty("data").GetProperty("artifacts");

        Assert.Contains(
            artifacts.EnumerateArray(),
            artifact => artifact.GetProperty("path").GetString() == "src/SalesClient/SalesClient.csproj");
    }

    [Fact]
    public async Task NewAppSampleOptionGeneratesExplicitSampleViewModel()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--sample",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.True(File.Exists(Path.Combine(
            host.WorkingDirectory,
            "src",
            "SalesClient",
            "Samples",
            "WelcomeViewModel.cs")));
    }

    [Fact]
    public async Task NewAppRejectsInvalidAppName()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "Sales Client",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0104", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src")));
    }

    [Fact]
    public async Task NewAppRejectsCSharpKeywordWithoutReportingArtifacts()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "class",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("AUCCLI0104", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src")));
    }

    [Fact]
    public async Task NewAppRejectsMalformedTargetFramework()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--target-framework",
            "abc",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCTPL0001", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src")));
    }

    [Fact]
    public async Task NewAppRejectsExistingTargetWithoutOverwriting()
    {
        using var host = new CliTestHost();
        var existingProject = Path.Combine(host.WorkingDirectory, "src", "SalesClient", "SalesClient.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(existingProject)!);
        await File.WriteAllTextAsync(existingProject, "existing");

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal("existing", await File.ReadAllTextAsync(existingProject));
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0105", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task NewAppCancellationDoesNotWriteFiles()
    {
        using var host = new CliTestHost();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await CliApplication.RunAsync(
            [
                "city",
                "new",
                "app",
                "SalesClient",
                "--output",
                host.WorkingDirectory,
                "--json",
            ],
            output,
            error,
            new CliExecutionEnvironment(host.WorkingDirectory),
            cancellation.Token);

        Assert.Equal(1, exitCode);
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src", "SalesClient")));
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("AUCCLI0106", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task NewAppRejectsFrameworkNamespace()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--namespace",
            "AtomUI.City.SalesClient",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0102", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task NewAppRejectsAotWithDynamicPlugins()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "new",
            "app",
            "SalesClient",
            "--use-aot",
            "--use-dynamic-plugins",
            "--output",
            host.WorkingDirectory,
            "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0103", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }
}
