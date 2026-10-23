using AtomUI.City.Testing;

namespace AtomUI.City.Testing.Tests;

public sealed class AotSourcePatternCheckTests
{
    [Fact]
    public void EvaluateReportsConfiguredOrdinalPattern()
    {
        var check = AotSourcePatternCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        var diagnostics = check.Evaluate(
            [
                new SourceFile("ModuleScanner.cs", "var types = assembly.Assembly.GetTypes();"),
                new SourceFile("StaticManifest.cs", "var manifest = StaticManifest.Instance;"),
            ]);

        var diagnostic = Assert.Single(diagnostics);

        Assert.Equal("AOT001", diagnostic.Id);
        Assert.Equal("ModuleScanner.cs", diagnostic.SourcePath);
        Assert.Contains("Assembly.GetTypes", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateReturnsNoDiagnosticsWhenSourcesAvoidConfiguredPattern()
    {
        var check = AotSourcePatternCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        var diagnostics = check.Evaluate(
            [new SourceFile("StaticManifest.cs", "var manifest = StaticManifest.Instance;")]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ForbidDefaultAotRiskPatternsReportsThreeHeuristicPatterns()
    {
        var check = AotSourcePatternCheck
            .Create()
            .ForbidDefaultAotRiskPatterns();

        var diagnostics = check.Evaluate(
            [
                new SourceFile("Reflection.cs", "var types = assembly.Assembly.GetTypes();"),
                new SourceFile("Activator.cs", "var instance = Activator.CreateInstance(type);"),
                new SourceFile("DynamicCode.cs", "var method = new DynamicMethod(\"m\", null, Type.EmptyTypes);"),
            ]);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "AOT001");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "AOT002");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "AOT003");
    }

    [Fact]
    public void DuplicateRuleRegistrationIsIdempotent()
    {
        var check = AotSourcePatternCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes")
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        var diagnostics = check.Evaluate(
            [new SourceFile("Reflection.cs", "var types = assembly.Assembly.GetTypes();")]);

        Assert.Single(diagnostics);
    }

    [Fact]
    public void TextLintHasExplicitFalsePositiveAndFalseNegativeBoundary()
    {
        var check = AotSourcePatternCheck
            .Create()
            .ForbidDefaultAotRiskPatterns();

        var diagnostics = check.Evaluate(
            [
                new SourceFile("Comment.cs", "// Assembly.GetTypes is intentionally documented here."),
                new SourceFile("AliasedReflection.cs", "var method = typeof(Assembly).GetMethod(\"GetTypes\");"),
            ]);

        Assert.Contains(diagnostics, diagnostic => diagnostic.SourcePath == "Comment.cs");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.SourcePath == "AliasedReflection.cs");
    }

    [Fact]
    public async Task EvaluateObservesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var check = AotSourcePatternCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        Assert.Throws<OperationCanceledException>(() => check.Evaluate(
            [new SourceFile("Reflection.cs", "var types = assembly.Assembly.GetTypes();")],
            cancellation.Token));
    }

    [Fact]
    public void EvaluateDiagnosticsRejectExternalListMutation()
    {
        var check = AotSourcePatternCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        var diagnostics = check.Evaluate(
            [new SourceFile("ModuleScanner.cs", "var types = assembly.Assembly.GetTypes();")]);
        var exposedDiagnostics = Assert.IsAssignableFrom<IList<AotSourcePatternDiagnostic>>(diagnostics);

        Assert.Throws<NotSupportedException>(() => exposedDiagnostics[0] = new AotSourcePatternDiagnostic(
            "AOT999",
            "Changed.cs",
            "Changed"));
        Assert.Equal("AOT001", diagnostics[0].Id);
    }
}
