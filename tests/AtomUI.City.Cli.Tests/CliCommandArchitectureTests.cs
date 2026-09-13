using System.Text.Json;
using AtomUI.City.Cli;

namespace AtomUI.City.Cli.Tests;

public sealed class CliCommandArchitectureTests
{
    [Fact]
    public async Task DoctorCommandWritesJsonEnvelope()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "doctor", "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var root = json.RootElement;
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("atomui city doctor", root.GetProperty("command").GetString());
        Assert.Equal("succeeded", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("diagnostics").ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("artifacts").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("suggestedCommands").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("changedFiles").ValueKind);
        Assert.False(root.GetProperty("retryable").GetBoolean());
        Assert.Equal(string.Empty, run.Error);
        Assert.StartsWith("{", run.Output.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("OK", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingCityRootReturnsStableDiagnostic()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("doctor", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        var root = json.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("AUCCLI0001", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnknownCommandReturnsStableDiagnostic()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "unknown", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        var diagnostic = json.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal("AUCCLI0002", diagnostic.GetProperty("code").GetString());
        Assert.Equal("unknown", diagnostic.GetProperty("target").GetString());
        Assert.Equal(1, diagnostic.GetProperty("position").GetInt32());
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.GetProperty("retryable").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("suggestedCommands").EnumerateArray(),
            command => command.GetString() == "atomui city explain AUCCLI0002 --json");
    }

    [Fact]
    public async Task RuntimeFailureEnvelopeIsRetryable()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "AtomUICityCliTests", Guid.NewGuid().ToString("N"));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            [
                "city",
                "build",
                "--json",
            ],
            output,
            error,
            new CliExecutionEnvironment(missingDirectory));

        Assert.Equal(1, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.True(json.RootElement.GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public async Task JsonEnvelopePromotesArtifactsForAgents()
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
        using var json = run.ReadJson();
        var artifacts = json.RootElement.GetProperty("artifacts");
        var changedFiles = json.RootElement.GetProperty("changedFiles");
        Assert.Contains(
            artifacts.EnumerateArray(),
            artifact => artifact.GetProperty("path").GetString() == "src/SalesClient/SalesClient.csproj");
        Assert.Contains(
            changedFiles.EnumerateArray(),
            path => path.GetString() == "src/SalesClient/SalesClient.csproj");
    }

    [Fact]
    public async Task NonInteractiveApplyRequiresExplicitYes()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "apply", "plan.json", "--non-interactive", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        var root = json.RootElement;
        Assert.Equal("AUCCLI0401", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.True(root.GetProperty("data").GetProperty("environment").GetProperty("nonInteractive").GetBoolean());
        Assert.Equal("--yes", root.GetProperty("data").GetProperty("requiredOption").GetString());
    }

    [Fact]
    public async Task NonInteractiveApplyWithYesDoesNotFailConfirmation()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "apply", "plan.json", "--non-interactive", "--yes", "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task CiOptionEnablesNonInteractiveMode()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "apply", "plan.json", "--ci", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        var environment = json.RootElement.GetProperty("data").GetProperty("environment");
        Assert.True(environment.GetProperty("ci").GetBoolean());
        Assert.True(environment.GetProperty("nonInteractive").GetBoolean());
    }

    [Fact]
    public async Task UnavailableStdinEnablesNonInteractiveMode()
    {
        using var host = new CliTestHost();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            [
                "city",
                "apply",
                "plan.json",
                "--json",
            ],
            output,
            error,
            new CliExecutionEnvironment(host.WorkingDirectory, isStdinAvailable: false));

        Assert.Equal(2, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        var environment = json.RootElement.GetProperty("data").GetProperty("environment");
        Assert.False(environment.GetProperty("stdinAvailable").GetBoolean());
        Assert.True(environment.GetProperty("nonInteractive").GetBoolean());
    }

    [Fact]
    public async Task EnvironmentCiModeFlowsToDotnetInvocation()
    {
        using var host = new CliTestHost();
        DotnetInvocation? captured = null;
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            [
                "city",
                "build",
                "--json",
            ],
            output,
            error,
            new CliExecutionEnvironment(host.WorkingDirectory, isCi: true),
            CancellationToken.None,
            (invocation, _) =>
            {
                captured = invocation;
                return ValueTask.FromResult(new ProcessRunResult(0, string.Empty, string.Empty));
            });

        Assert.Equal(0, exitCode);
        Assert.NotNull(captured);
        Assert.True(captured.CiMode);
    }

    [Fact]
    public async Task MissingCommandReturnsUsageDiagnostic()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        var root = json.RootElement;
        var diagnostic = root.GetProperty("diagnostics")[0];
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("AUCCLI0003", diagnostic.GetProperty("code").GetString());
        Assert.Equal("city", diagnostic.GetProperty("target").GetString());
        Assert.Equal(1, diagnostic.GetProperty("position").GetInt32());
        Assert.Contains(
            root.GetProperty("data").GetProperty("usage").EnumerateArray(),
            item => item.GetString() == "atomui city doctor");
    }

    [Fact]
    public async Task UnknownOptionReturnsStableDiagnosticBeforeHandlerRuns()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "doctor", "--unknown", "--json");

        Assert.Equal(2, run.ExitCode);
        using var json = run.ReadJson();
        var diagnostic = json.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal("AUCCLI0004", diagnostic.GetProperty("code").GetString());
        Assert.Equal("--unknown", diagnostic.GetProperty("target").GetString());
        Assert.Equal(2, diagnostic.GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task MissingValueOptionDoesNotConsumeJsonFlag()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "doctor", "--working-directory", "--json");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        using var json = run.ReadJson();
        var diagnostic = json.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal("AUCCLI0005", diagnostic.GetProperty("code").GetString());
        Assert.Equal("--working-directory", diagnostic.GetProperty("target").GetString());
        Assert.Equal(2, diagnostic.GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task UnknownCommandWithoutJsonWritesUsageText()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "unknown");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("atomui city unknown: failed", run.Output, StringComparison.Ordinal);
        Assert.Contains("Usage:", run.Output, StringComparison.Ordinal);
        Assert.Contains("atomui city new app <AppName>", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvelopeDiagnosticsRejectExternalListMutation()
    {
        var envelope = CliEnvelope.Failed(
            "atomui city doctor",
            CliExitCodes.ArgumentError,
            CliDiagnostic.Error("AUCCLI0001", "Missing city root"),
            CliDiagnostic.Error("AUCCLI0002", "Unknown command"));
        var diagnostics = Assert.IsAssignableFrom<IList<CliDiagnostic>>(envelope.Diagnostics);

        Assert.Throws<NotSupportedException>(() => diagnostics[0] = CliDiagnostic.Error("AUCCLI9999", "Changed"));
        Assert.Equal("AUCCLI0001", envelope.Diagnostics[0].Code);
        Assert.Equal("AUCCLI0002", envelope.Diagnostics[1].Code);
    }

    [Fact]
    public void EnvelopeCopiesDictionaryDataSnapshot()
    {
        var data = new Dictionary<string, object?> { ["path"] = "source" };
        var envelope = CliEnvelope.Succeeded("atomui city inspect", data);

        data["path"] = "changed";
        data["extra"] = true;

        var envelopeData = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(envelope.Data);
        Assert.Equal("source", envelopeData["path"]);
        Assert.False(envelopeData.ContainsKey("extra"));
    }

    [Fact]
    public void EnvelopeDictionaryDataRejectsExternalMutation()
    {
        var envelope = CliEnvelope.Succeeded(
            "atomui city inspect",
            new Dictionary<string, object?> { ["path"] = "source" });

        var envelopeData = Assert.IsAssignableFrom<IDictionary<string, object?>>(envelope.Data);

        Assert.Throws<NotSupportedException>(() => envelopeData["path"] = "changed");
    }

    [Fact]
    public void EnvelopeCopiesValueTypeDictionaryDataSnapshot()
    {
        var data = new Dictionary<string, int> { ["count"] = 1 };
        var envelope = CliEnvelope.Succeeded("atomui city inspect", data);

        data["count"] = 99;
        data["extra"] = 2;

        var envelopeData = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(envelope.Data);

        Assert.Equal(1, envelopeData["count"]);
        Assert.False(envelopeData.ContainsKey("extra"));
    }

    [Fact]
    public void EnvelopeCopiesNestedCollectionDataSnapshot()
    {
        object?[] projects = ["src/App/App.csproj"];
        var envelope = CliEnvelope.Succeeded(
            "atomui city inspect",
            new Dictionary<string, object?> { ["projects"] = projects });

        projects[0] = "changed";

        var envelopeData = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(envelope.Data);
        var envelopeProjects = Assert.IsAssignableFrom<IList<object?>>(envelopeData["projects"]);

        Assert.Throws<NotSupportedException>(() => envelopeProjects[0] = "changed");
        Assert.Equal("src/App/App.csproj", envelopeProjects[0]);
    }

    [Fact]
    public void EnvelopeCopiesNestedValueTypeCollectionDataSnapshot()
    {
        int[] counts = [1, 2];
        var envelope = CliEnvelope.Succeeded(
            "atomui city inspect",
            new Dictionary<string, object?> { ["counts"] = counts });

        counts[0] = 99;

        var envelopeData = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(envelope.Data);
        var envelopeCounts = Assert.IsAssignableFrom<IList<object?>>(envelopeData["counts"]);

        Assert.Throws<NotSupportedException>(() => envelopeCounts[0] = 99);
        Assert.Equal(1, envelopeCounts[0]);
        Assert.Equal(2, envelopeCounts[1]);
    }
}
