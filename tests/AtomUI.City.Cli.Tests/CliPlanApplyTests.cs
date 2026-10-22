using System.Text;
using System.Text.Json;
using AtomUI.City.Cli;

namespace AtomUI.City.Cli.Tests;

public sealed class CliPlanApplyTests
{
    [Theory]
    [InlineData("module", "src/SampleApp/Modules/Sales/SalesModule.cs")]
    [InlineData("page", "src/SampleApp/Routes/Sales/SalesRoute.cs")]
    [InlineData("test", "tests/SampleApp.Tests/Features/Sales/SalesTests.cs")]
    [InlineData("config", "src/SampleApp/Configuration/Sales/SalesOptions.cs")]
    [InlineData("localization", "src/SampleApp/Localization/Sales/en-US/Resources.resx")]
    public async Task PlanUsesRealTemplatesWithoutWritingBusinessFiles(
        string kind,
        string expectedPath)
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var args = new List<string> { "city", "plan", "generate", kind, "Sales", "--json" };
        if (kind == "page")
        {
            args.AddRange(["--route", "/sales"]);
        }

        var run = await host.RunAsync([.. args]);

        Assert.Equal(0, run.ExitCode);
        Assert.False(File.Exists(ToPath(host.WorkingDirectory, expectedPath)));
        using var json = run.ReadJson();
        var data = json.RootElement.GetProperty("data");
        var planFile = data.GetProperty("planFile").GetString()!;
        Assert.StartsWith(".city/plans/generate-", planFile, StringComparison.Ordinal);
        Assert.True(File.Exists(ToPath(host.WorkingDirectory, planFile)));
        Assert.StartsWith("sha256:", data.GetProperty("planDigest").GetString(), StringComparison.Ordinal);
        Assert.Contains(
            data.GetProperty("plan").GetProperty("changes").EnumerateArray(),
            change => change.GetProperty("path").GetString() == expectedPath);
        Assert.Empty(json.RootElement.GetProperty("changedFiles").EnumerateArray());
    }

    [Fact]
    public async Task ApplyCommitsPlanAndReplayIsSuccessfulNoOp()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);

        var first = await host.RunAsync("city", "apply", planFile, "--yes", "--json");
        var second = await host.RunAsync("city", "apply", planFile, "--yes", "--json");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.True(File.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesModule.cs")));
        using var firstJson = first.ReadJson();
        using var secondJson = second.ReadJson();
        Assert.Equal("completed", firstJson.RootElement.GetProperty("data").GetProperty("transactionState").GetString());
        Assert.NotEmpty(firstJson.RootElement.GetProperty("changedFiles").EnumerateArray());
        Assert.Equal("already-completed", secondJson.RootElement.GetProperty("data").GetProperty("transactionState").GetString());
        Assert.Empty(secondJson.RootElement.GetProperty("changedFiles").EnumerateArray());
    }

    [Fact]
    public async Task InteractiveApplyRequiresAnAffirmativeConfirmation()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);

        var declined = await RunInteractiveApplyAsync(host, planFile, "no");
        Assert.Equal(2, declined.ExitCode);
        using (var declinedJson = declined.ReadJson())
        {
            Assert.Equal("AUCCLI0401", declinedJson.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        }

        Assert.False(Directory.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales")));

        var confirmed = await RunInteractiveApplyAsync(host, planFile, "yes");
        Assert.Equal(0, confirmed.ExitCode);
        Assert.Contains("Type 'yes'", confirmed.Error, StringComparison.Ordinal);
        Assert.True(File.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesModule.cs")));
    }

    [Fact]
    public async Task ApplyRejectsCompletedJournalWhenCommittedContentWasChanged()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        var first = await host.RunAsync("city", "apply", planFile, "--yes", "--json");
        Assert.Equal(0, first.ExitCode);

        var target = ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesModule.cs");
        await File.WriteAllTextAsync(target, "changed-after-completion");

        var replay = await host.RunAsync("city", "apply", planFile, "--yes", "--json");

        Assert.Equal(2, replay.ExitCode);
        using var json = replay.ReadJson();
        Assert.Equal("AUCCLI0709", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("changed-after-completion", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task ApplyRejectsCompletedJournalWithIncompleteCommittedPaths()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        var first = await host.RunAsync("city", "apply", planFile, "--yes", "--json");
        Assert.Equal(0, first.ExitCode);

        using var plannedJson = planned.ReadJson();
        var operationId = plannedJson.RootElement.GetProperty("data").GetProperty("operationId").GetString()!;
        var journalPath = ToPath(host.WorkingDirectory, $".city/transactions/{operationId}/journal.json");
        var journal = await File.ReadAllTextAsync(journalPath);
        using var journalJson = JsonDocument.Parse(journal);
        var tampered = journal.Replace(
            journalJson.RootElement.GetProperty("committedPaths").GetRawText(),
            "[]",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(journalPath, tampered, new UTF8Encoding(false));

        var replay = await host.RunAsync("city", "apply", planFile, "--yes", "--json");

        Assert.Equal(2, replay.ExitCode);
        using var json = replay.ReadJson();
        Assert.Equal("AUCCLI0709", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApplyRejectsTamperedContentBeforeWriting()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        var fullPlanPath = ToPath(host.WorkingDirectory, planFile);
        var text = await File.ReadAllTextAsync(fullPlanPath, Encoding.UTF8);
        await File.WriteAllTextAsync(fullPlanPath, text.Replace("public sealed class SalesService", "public sealed class StolenService", StringComparison.Ordinal), new UTF8Encoding(false));

        var applied = await host.RunAsync("city", "apply", planFile, "--yes", "--json");

        Assert.Equal(2, applied.ExitCode);
        using var json = applied.ReadJson();
        Assert.Contains(
            json.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "AUCCLI0709");
        Assert.False(Directory.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales")));
    }

    [Fact]
    public async Task ApplyRejectsUnsafeDeclaredTargetsBeforeWriting()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        var fullPlanPath = ToPath(host.WorkingDirectory, planFile);
        var text = await File.ReadAllTextAsync(fullPlanPath, Encoding.UTF8);
        var tampered = text.Replace(
            "src/SampleApp/SampleApp.csproj",
            "../outside.csproj",
            StringComparison.Ordinal);
        Assert.NotEqual(text, tampered);
        await File.WriteAllTextAsync(fullPlanPath, tampered, new UTF8Encoding(false));

        var applied = await host.RunAsync("city", "apply", planFile, "--yes", "--json");

        Assert.Equal(2, applied.ExitCode);
        using var json = applied.ReadJson();
        Assert.Contains(
            json.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "AUCCLI0704");
        Assert.False(File.Exists(Path.Combine(host.WorkingDirectory, "..", "outside.csproj")));
        Assert.False(Directory.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales")));
    }

    [Fact]
    public async Task ApplyRejectsTargetConflictWithoutPartialWrites()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        var conflict = ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesModule.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(conflict)!);
        await File.WriteAllTextAsync(conflict, "owned-by-user");

        var applied = await host.RunAsync("city", "apply", planFile, "--yes", "--json");

        Assert.Equal(1, applied.ExitCode);
        Assert.Equal("owned-by-user", await File.ReadAllTextAsync(conflict));
        Assert.False(File.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesOptions.cs")));
        using var json = applied.ReadJson();
        Assert.Equal("AUCCLI0710", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApplyRejectsUnknownSchemaFieldsAndWorkspaceMismatch()
    {
        using var source = new CliTestHost();
        using var target = new CliTestHost();
        await CreateProjectAsync(source);
        await CreateProjectAsync(target);
        var planned = await source.RunAsync("city", "plan", "generate", "config", "Runtime", "--json");
        var relativePlan = ReadPlanFile(planned);
        var sourcePlan = ToPath(source.WorkingDirectory, relativePlan);

        var original = await File.ReadAllTextAsync(sourcePlan);
        var unknownField = original.Insert(original.IndexOf('{') + 1, "\n  \"unknown\": true,");
        await File.WriteAllTextAsync(sourcePlan, unknownField, new UTF8Encoding(false));
        var unknown = await source.RunAsync("city", "apply", relativePlan, "--yes", "--json");

        var targetPlan = ToPath(target.WorkingDirectory, relativePlan);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPlan)!);
        await File.WriteAllTextAsync(targetPlan, original, new UTF8Encoding(false));
        var mismatch = await target.RunAsync("city", "apply", relativePlan, "--yes", "--json");

        Assert.Equal(2, unknown.ExitCode);
        Assert.Equal(2, mismatch.ExitCode);
        using var unknownJson = unknown.ReadJson();
        using var mismatchJson = mismatch.ReadJson();
        Assert.Equal("AUCCLI0707", unknownJson.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("AUCCLI0708", mismatchJson.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task PlanCancellationLeavesNoPlanOrBusinessArtifacts()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var run = await host.RunAsync(
            cancellation.Token,
            "city", "plan", "generate", "module", "Sales", "--json");

        Assert.Equal(1, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCCLI0714", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(ToPath(host.WorkingDirectory, ".city/plans")));
        Assert.False(Directory.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules")));
    }

    [Fact]
    public async Task IdenticalPlanInputReusesByteIdenticalPlan()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);

        var first = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var firstPlan = ReadPlanFile(first);
        var firstBytes = await File.ReadAllBytesAsync(ToPath(host.WorkingDirectory, firstPlan));
        var second = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var secondPlan = ReadPlanFile(second);
        var secondBytes = await File.ReadAllBytesAsync(ToPath(host.WorkingDirectory, secondPlan));

        Assert.Equal(firstPlan, secondPlan);
        Assert.Equal(firstBytes, secondBytes);
        using var firstJson = first.ReadJson();
        using var secondJson = second.ReadJson();
        Assert.Equal(
            firstJson.RootElement.GetProperty("data").GetProperty("planDigest").GetString(),
            secondJson.RootElement.GetProperty("data").GetProperty("planDigest").GetString());
    }

    [Fact]
    public async Task ApplyFailsFastWhenWorkspaceLockIsOwned()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        var lockPath = ToPath(host.WorkingDirectory, ".city/transactions/apply.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        await using var ownedLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var applied = await host.RunAsync("city", "apply", planFile, "--yes", "--json");

        Assert.Equal(1, applied.ExitCode);
        using var json = applied.ReadJson();
        Assert.Equal("AUCCLI0711", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales")));
    }

    [Fact]
    public async Task CommitFailureRollsBackEveryCreatedBusinessFile()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);

        var result = await PlanApplyTransaction.ApplyAsync(
            planFile,
            host.WorkingDirectory,
            CancellationToken.None,
            static (_, index) =>
            {
                if (index == 1)
                {
                    throw new IOException("injected commit failure");
                }
            });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "AUCCLI0712");
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Code == "AUCCLI0713");
        Assert.False(File.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesModule.cs")));
        Assert.False(File.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesOptions.cs")));
    }

    [Fact]
    public async Task RollbackFailureIsAggregatedWithoutDeletingChangedContent()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        var firstTarget = ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales/SalesModule.cs");

        var result = await PlanApplyTransaction.ApplyAsync(
            planFile,
            host.WorkingDirectory,
            CancellationToken.None,
            (_, index) =>
            {
                if (index == 1)
                {
                    File.WriteAllText(firstTarget, "user-changed-after-commit");
                    throw new IOException("injected commit failure");
                }
            });

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "AUCCLI0712");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "AUCCLI0713");
        Assert.Equal("user-changed-after-commit", await File.ReadAllTextAsync(firstTarget));
    }

    [Fact]
    public async Task ApplyCancellationBeforeCommitCreatesNoBusinessFiles()
    {
        using var host = new CliTestHost();
        await CreateProjectAsync(host);
        var planned = await host.RunAsync("city", "plan", "generate", "module", "Sales", "--json");
        var planFile = ReadPlanFile(planned);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var applied = await host.RunAsync(
            cancellation.Token,
            "city", "apply", planFile, "--yes", "--json");

        Assert.Equal(1, applied.ExitCode);
        using var json = applied.ReadJson();
        Assert.Equal("AUCCLI0714", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(ToPath(host.WorkingDirectory, "src/SampleApp/Modules/Sales")));
    }

    private static string ReadPlanFile(CliTestRun run)
    {
        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        return json.RootElement.GetProperty("data").GetProperty("planFile").GetString()!;
    }

    private static async ValueTask<CliTestRun> RunInteractiveApplyAsync(
        CliTestHost host,
        string planFile,
        string confirmation)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await CliApplication.RunAsync(
            ["city", "apply", planFile, "--json"],
            new StringReader(confirmation + Environment.NewLine),
            output,
            error,
            new CliExecutionEnvironment(host.WorkingDirectory),
            CancellationToken.None,
            ProcessRunner.RunAsync);
        return new CliTestRun(exitCode, output.ToString(), error.ToString());
    }

    private static string ToPath(string root, string relativePath) =>
        Path.Combine([root, .. relativePath.Split('/')]);

    private static async Task CreateProjectAsync(CliTestHost host)
    {
        var directory = Path.Combine(host.WorkingDirectory, "src", "SampleApp");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "SampleApp.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Company.Sample</RootNamespace>
              </PropertyGroup>
            </Project>
            """);
    }
}
