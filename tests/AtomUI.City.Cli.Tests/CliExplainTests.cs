using AtomUI.City.Cli;

namespace AtomUI.City.Cli.Tests;

public sealed class CliExplainTests
{
    [Fact]
    public async Task ExplainReturnsCanonicalStructuredEntryCaseInsensitively()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync("city", "explain", "auccli0709", "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("AUCCLI0709", data.GetProperty("code").GetString());
        Assert.Equal("Error", data.GetProperty("severity").GetString());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("summary").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("cause").GetString()));
        Assert.NotEmpty(data.GetProperty("suggestedActions").EnumerateArray());
        Assert.NotEmpty(data.GetProperty("documentationLinks").EnumerateArray());
        Assert.Equal("CLI", data.GetProperty("ownerModule").GetString());
    }

    [Fact]
    public async Task ExplainRejectsMissingAndUnknownCodes()
    {
        using var host = new CliTestHost();

        var missing = await host.RunAsync("city", "explain", "--json");
        var unknown = await host.RunAsync("city", "explain", "AUCXXX9999", "--json");

        Assert.Equal(2, missing.ExitCode);
        Assert.Equal(2, unknown.ExitCode);
        using var missingJson = missing.ReadJson();
        using var unknownJson = unknown.ReadJson();
        Assert.Equal("AUCCLI0801", missingJson.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("AUCCLI0802", unknownJson.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("AUCCLI0802", unknownJson.RootElement.GetProperty("data").GetProperty("code").GetString());
    }

    [Fact]
    public void CatalogIsUniqueCompleteAndImmutableForConcurrentReaders()
    {
        var entries = DiagnosticExplanationCatalog.All;

        Assert.NotEmpty(entries);
        Assert.Equal(entries.Count, entries.Select(static item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(entries, static item => item.Code == "AUCCLI0803");
        Assert.Contains(entries, static item => item.Code == "AUCTPL1004");
        Assert.Contains(entries, static item => item.Code == "AUCPLG0023");

        Parallel.For(0, 100, _ =>
        {
            Assert.True(DiagnosticExplanationCatalog.TryGet("auccli0601", out var entry));
            Assert.Equal("AUCCLI0601", entry.Code);
        });
    }
}
