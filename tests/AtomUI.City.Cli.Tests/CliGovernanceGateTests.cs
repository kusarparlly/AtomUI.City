using AtomUI.City.Cli;

namespace AtomUI.City.Cli.Tests;

public sealed class CliGovernanceGateTests
{
    [Theory]
    [InlineData("docs", 408)]
    [InlineData("tests", 268)]
    public async Task CurrentFrameworkWorkspacePassesGovernanceGate(
        string gate,
        int minimumCheckedFiles)
    {
        var repositoryRoot = FindRepositoryRoot();
        var output = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["city", gate, "check", "--json"],
            output,
            TextWriter.Null,
            new CliExecutionEnvironment(repositoryRoot));

        Assert.Equal(0, exitCode);
        using var json = System.Text.Json.JsonDocument.Parse(output.ToString());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("data").GetProperty("errorCount").GetInt32());
        Assert.True(json.RootElement.GetProperty("data").GetProperty("checkedFiles").GetInt32() >= minimumCheckedFiles);
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("tests")]
    public async Task UnrecognizedWorkspaceFailsWithStableDiagnostic(string gate)
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", gate, "check", "--json");

        Assert.Equal(1, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0601", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("atomui-city-framework", json.RootElement.GetProperty("data").GetProperty("ruleSetId").GetString());
        Assert.Equal("1.0", json.RootElement.GetProperty("data").GetProperty("ruleSetVersion").GetString());
    }

    [Fact]
    public async Task CancellationReturnsGovernanceDiagnosticWithoutWriting()
    {
        var repositoryRoot = FindRepositoryRoot();
        var before = Directory.GetLastWriteTimeUtc(repositoryRoot);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var output = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["city", "docs", "check", "--json"],
            output,
            TextWriter.Null,
            new CliExecutionEnvironment(repositoryRoot),
            cancellation.Token);

        Assert.Equal(1, exitCode);
        Assert.Contains("AUCCLI0616", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, Directory.GetLastWriteTimeUtc(repositoryRoot));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AtomUICity.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
