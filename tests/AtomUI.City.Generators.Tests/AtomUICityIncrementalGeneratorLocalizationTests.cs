using AtomUI.City.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AtomUI.City.Generators.Tests;

public sealed class AtomUICityIncrementalGeneratorLocalizationTests
{
    [Fact]
    public void GeneratorEmitsRuntimePackageRegistrarCulturesAndStronglyTypedKeys()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Localization;

            [assembly: LanguagePackage("Host.en-US", "en-US", Scope = ResourceScope.Host, ResourceBaseName = "Sample.Resources.Host")]
            [assembly: LanguagePackage("Settings.zh-CN", "zh-CN", Scope = ResourceScope.Module, ScopeId = "settings.module", ResourceBaseName = "Sample.Resources.Settings", ContributionId = "settings.localization")]
            [assembly: LocalizedResource("Settings.Title", "Settings.zh-CN", Scope = ResourceScope.Module, ScopeId = "settings.module", Critical = true)]
            [assembly: LocalizedResource("class", "Settings.zh-CN", Scope = ResourceScope.Module, ScopeId = "settings.module")]

            namespace Sample.App;
            public sealed class Marker;
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var result = Assert.Single(driver.GetRunResult().Results);
        var generated = Assert.Single(LocalizationSources(result));
        var source = generated.SourceText.ToString();

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("GeneratedLocalizationManifest", source, StringComparison.Ordinal);
        Assert.Contains("RegisterPackages", source, StringComparison.Ordinal);
        Assert.Contains("RegisterRange", source, StringComparison.Ordinal);
        Assert.Contains("Settings_Title", source, StringComparison.Ordinal);
        Assert.Contains("const string _class", source, StringComparison.Ordinal);
        Assert.Contains("ScopeId = @\"settings.module\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AssemblyLoadContext", source, StringComparison.Ordinal);
        Assert.Contains("SourceAssembly = typeof(GeneratedLocalizationManifest).Assembly", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Assembly.Location", source, StringComparison.Ordinal);
        Assert.Contains("ContributionId = @\"settings.localization\"", source, StringComparison.Ordinal);
        Assert.Contains("CriticalResourceKeys = new string[] { @\"Settings.Title\" }", source, StringComparison.Ordinal);
        Assert.Contains("@\"en-US\"", source, StringComparison.Ordinal);
        Assert.Contains("@\"zh-CN\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsScopedPackageWithoutScopeId()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Localization;
            [assembly: LanguagePackage("Settings.zh-CN", "zh-CN", Scope = ResourceScope.Module)]
            namespace Sample.App;
            public sealed class Marker;
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Empty(LocalizationSources(Assert.Single(result.Results)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorRejectsExplicitUnknownLocalizationEnums()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Localization;
            [assembly: LanguagePackage("Host.en-US", "en-US", Scope = (ResourceScope)999, ResourceBaseName = "Sample.Resources.Host")]
            [assembly: LocalizedResource("Host.Title", "Host.en-US", Scope = (ResourceScope)999, Kind = (LocalizedResourceKind)999)]
            namespace Sample.App;
            public sealed class Marker;
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Empty(LocalizationSources(Assert.Single(result.Results)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorRejectsEmptyLocalizationAttributeArguments()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Localization;
            [assembly: LanguagePackage("", "en-US", Scope = ResourceScope.Host, ResourceBaseName = "Sample.Resources.Host")]
            namespace Sample.App;
            public sealed class Marker;
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Empty(LocalizationSources(Assert.Single(result.Results)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    private static IEnumerable<GeneratedSourceResult> LocalizationSources(GeneratorRunResult result) =>
        result.GeneratedSources.Where(source => source.HintName.Contains("/Localization/", StringComparison.Ordinal));

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat([MetadataReference.CreateFromFile(typeof(AtomUI.City.Localization.LanguagePackageAttribute).Assembly.Location)])
            .DistinctBy(reference => reference.Display)
            .ToArray();

        return CSharpCompilation.Create(
            "Sample.App",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
