using System.Text.Json;
using AtomUI.City.Cli;

namespace AtomUI.City.Cli.Tests;

public sealed class CliGenerationCommandTests
{
    [Theory]
    [InlineData("module", "src/SampleApp/Modules/Sales/SalesModule.cs")]
    [InlineData("page", "src/SampleApp/Routes/Sales/SalesRoute.cs")]
    [InlineData("test", "tests/SampleApp.Tests/Features/Sales/SalesTests.cs")]
    [InlineData("config", "src/SampleApp/Configuration/Sales/SalesOptions.cs")]
    [InlineData("localization", "src/SampleApp/Localization/Sales/en-US/Resources.resx")]
    public async Task GenerateCreatesEverySupportedArtifactFamily(
        string kind,
        string expectedPath)
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var args = new List<string> { "city", "generate", kind, "Sales", "--json" };
        if (kind == "page")
        {
            args.AddRange(["--route", "/sales"]);
        }

        var run = await host.RunAsync([.. args]);

        Assert.Equal(0, run.ExitCode);
        Assert.True(File.Exists(Path.Combine([host.WorkingDirectory, .. expectedPath.Split('/')])), run.Output);
        using var json = run.ReadJson();
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("changedFiles").EnumerateArray(),
            item => item.GetString() == expectedPath);
    }

    [Fact]
    public async Task GenerateDryRunEmitsPlanWithoutWriting()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);

        var run = await host.RunAsync(
            "city",
            "generate",
            "module",
            "Sales",
            "--dry-run",
            "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src", "SampleApp", "Modules")));
        using var json = run.ReadJson();
        Assert.True(json.RootElement.GetProperty("data").GetProperty("dryRun").GetBoolean());
        Assert.NotEmpty(json.RootElement.GetProperty("data").GetProperty("artifacts").EnumerateArray());
    }

    [Fact]
    public async Task GenerateInfersRootNamespaceFromSingleProject()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host, rootNamespace: "Company.Product");

        var run = await host.RunAsync("city", "generate", "module", "Sales", "--no-tests", "--json");

        Assert.Equal(0, run.ExitCode);
        var module = await File.ReadAllTextAsync(Path.Combine(
            host.WorkingDirectory,
            "src",
            "SampleApp",
            "Modules",
            "Sales",
            "SalesModule.cs"));
        Assert.Contains("namespace Company.Product.Modules.Sales;", module);
    }

    [Fact]
    public async Task GenerateUsesExplicitProjectAndNamespace()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host, "FirstApp");
        await CreateProjectAsync(host, "SecondApp");

        var run = await host.RunAsync(
            "city",
            "generate",
            "config",
            "Runtime",
            "--project",
            "SecondApp",
            "--namespace",
            "Company.Second",
            "--reloadable",
            "--json");

        Assert.Equal(0, run.ExitCode);
        var options = await File.ReadAllTextAsync(Path.Combine(
            host.WorkingDirectory,
            "src",
            "SecondApp",
            "Configuration",
            "Runtime",
            "RuntimeOptions.cs"));
        Assert.Contains("namespace Company.Second.Configuration.Runtime;", options);
        Assert.Contains("ReloadOnChange = true", options);
    }

    [Fact]
    public async Task GeneratePluginIsExplicitlyDeferredAndWritesNothing()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "generate", "plugin", "Payments", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0502", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("plugin", json.RootElement.GetProperty("data").GetProperty("kind").GetString());
        Assert.Equal("Payments", json.RootElement.GetProperty("data").GetProperty("name").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src")));
    }

    [Theory]
    [InlineData(new[] { "city", "generate", "--json" }, "AUCCLI0501")]
    [InlineData(new[] { "city", "generate", "unknown", "Sales", "--json" }, "AUCCLI0502")]
    public async Task GenerateRejectsMissingOrUnknownKind(string[] args, string expectedCode)
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(args);

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal(expectedCode, json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task GenerateRequiresExplicitProjectWhenWorkspaceIsAmbiguous()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host, "FirstApp");
        await CreateProjectAsync(host, "SecondApp");

        var run = await host.RunAsync("city", "generate", "module", "Sales", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0503", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal(host.WorkingDirectory, json.RootElement.GetProperty("data").GetProperty("workingDirectory").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("data").GetProperty("candidates").GetArrayLength());
    }

    [Fact]
    public async Task GenerateRejectsNonCanonicalProjectLayout()
    {
        using var host = new CliTestHost();
        var directory = Path.Combine(host.WorkingDirectory, "src", "Applications", "SampleApp");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "SampleApp.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var run = await host.RunAsync("city", "generate", "module", "Sales", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0503", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src", "SampleApp")));
    }

    [Fact]
    public async Task GeneratePageRequiresValidRoute()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);

        var run = await host.RunAsync("city", "generate", "page", "Sales", "--route", "relative", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCTPL2004", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src", "SampleApp", "Routes")));
    }

    [Fact]
    public async Task GenerateConflictDoesNotOverwriteExistingArtifact()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var first = await host.RunAsync("city", "generate", "module", "Sales", "--json");
        Assert.Equal(0, first.ExitCode);
        var modulePath = Path.Combine(
            host.WorkingDirectory,
            "src",
            "SampleApp",
            "Modules",
            "Sales",
            "SalesModule.cs");
        var original = await File.ReadAllTextAsync(modulePath);

        var second = await host.RunAsync("city", "generate", "module", "Sales", "--json");

        Assert.Equal(2, second.ExitCode);
        Assert.Equal(original, await File.ReadAllTextAsync(modulePath));
        using var json = second.ReadJson();
        Assert.Equal("AUCTPL1004", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task GenerateCancellationWritesNoArtifacts()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["city", "generate", "module", "Sales", "--json"],
            output,
            error,
            new CliExecutionEnvironment(host.WorkingDirectory),
            cancellation.Token);

        Assert.Equal(1, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("AUCCLI0504", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.StartsWith("generate-module-sales", json.RootElement.GetProperty("data").GetProperty("operationId").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "src", "SampleApp", "Modules")));
    }

    private static async Task CreateProjectAsync(
        CliTestHost host,
        string projectName = "SampleApp",
        string rootNamespace = "Company.Sample")
    {
        var directory = Path.Combine(host.WorkingDirectory, "src", projectName);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, projectName + ".csproj"),
            $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                  <TargetFramework>net10.0</TargetFramework>
                  <RootNamespace>{{rootNamespace}}</RootNamespace>
                </PropertyGroup>
              </Project>
              """);
    }
}
