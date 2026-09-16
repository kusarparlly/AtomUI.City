using AtomUI.City.Generators;
using AtomUI.City.Generators.Analyzers;
using AtomUI.City.Generators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AtomUI.City.Generators.Tests;

public sealed class IncrementalGeneratorInfrastructureTests
{
    [Fact]
    public void AssemblyExportsOnlyRoslynToolingEntries()
    {
        var assembly = typeof(AtomUICityIncrementalGenerator).Assembly;
        var exportedTypes = assembly.GetExportedTypes()
            .Select(static type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                typeof(BuildServiceProviderUsageAnalyzer).FullName!,
                typeof(AtomUICityIncrementalGenerator).FullName!,
            ],
            exportedTypes);
        Assert.NotNull(Activator.CreateInstance(typeof(AtomUICityIncrementalGenerator)));
        Assert.NotNull(Activator.CreateInstance(typeof(BuildServiceProviderUsageAnalyzer)));
    }

    [Fact]
    public void BootstrapperUsesIncrementalGeneratorContract()
    {
        var generatorType = typeof(AtomUICityIncrementalGenerator);

        Assert.Contains(
            generatorType.GetInterfaces(),
            contract => string.Equals(contract.FullName, "Microsoft.CodeAnalysis.IIncrementalGenerator", StringComparison.Ordinal));
    }

    [Fact]
    public void BootstrapperDeclaresRoslynGeneratorAttribute()
    {
        var generatorType = typeof(AtomUICityIncrementalGenerator);

        Assert.Contains(
            generatorType.GetCustomAttributesData(),
            attribute => string.Equals(attribute.AttributeType.FullName, "Microsoft.CodeAnalysis.GeneratorAttribute", StringComparison.Ordinal));
    }

    [Fact]
    public void FeatureDiagnosticsIncludeFeatureCategoryAndSourceLocation()
    {
        var compilation = CreateCompilation(
            """
            namespace AtomUI.City.Presentation
            {
                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
                public sealed class ViewForAttribute : System.Attribute
                {
                    public ViewForAttribute(System.Type viewModelType)
                    {
                    }
                }
            }

            namespace Sample.App
            {
                public sealed class SettingsViewModel
                {
                }

                [AtomUI.City.Presentation.ViewFor(typeof(SettingsViewModel))]
                public sealed class SettingsView
                {
                }

                [AtomUI.City.Presentation.ViewFor(typeof(SettingsViewModel))]
                public sealed class AlternateSettingsView
                {
                }
            }
            """);
        var driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var diagnostic = Assert.Single(Assert.Single(runResult.Results).Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.DuplicatePresentationView, diagnostic.Id);
        Assert.Equal("AtomUI.City.Generators.Presentation", diagnostic.Descriptor.Category);
        Assert.True(diagnostic.Location.IsInSource);
    }

    [Fact]
    public void GeneratorAssemblyDoesNotReferenceRuntimePackages()
    {
        var referencedAssemblies = typeof(AtomUICityIncrementalGenerator)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null && name.StartsWith("AtomUI.City.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(referencedAssemblies);
    }

    [Fact]
    public void UnrelatedAttributedTypesDoNotEmitGeneratedSources()
    {
        var compilation = CreateCompilation(
            """
            namespace Sample.App
            {
                [System.Obsolete]
                public sealed class UnrelatedType
                {
                }
            }
            """);
        var driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var generatorResult = Assert.Single(runResult.Results);

        Assert.All(
            generatorResult.GeneratedSources,
            source => Assert.Contains("/Modularity/", source.HintName, StringComparison.Ordinal));
        Assert.Empty(generatorResult.Diagnostics);
    }

    [Fact]
    public void SourceGenerationModeOffSuppressesAllGeneratorFeatures()
    {
        var compilation = CreateCompilation(
            """
            namespace AtomUI.City.Presentation
            {
                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
                public sealed class ViewForAttribute : System.Attribute
                {
                    public ViewForAttribute(System.Type viewModelType) { }
                }
            }

            namespace Sample.App
            {
                public sealed class ViewModel { }
                [AtomUI.City.Presentation.ViewFor(typeof(ViewModel))]
                public sealed class FirstView { }
                [AtomUI.City.Presentation.ViewFor(typeof(ViewModel))]
                public sealed class SecondView { }
            }
            """);
        var driver = CSharpGeneratorDriver.Create(
            [new AtomUICityIncrementalGenerator().AsSourceGenerator()],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(
                "build_property.AtomUICitySourceGenerationMode",
                "Off"));

        var generatorResult = Assert.Single(driver.RunGenerators(compilation).GetRunResult().Results);

        Assert.Empty(generatorResult.GeneratedSources);
        Assert.Empty(generatorResult.Diagnostics);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var sourceTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .DistinctBy(reference => reference.Display)
            .ToArray();

        return CSharpCompilation.Create(
            "Sample.App",
            [sourceTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed class TestAnalyzerConfigOptionsProvider(string key, string value) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty = new TestAnalyzerConfigOptions(new Dictionary<string, string>());

        public override AnalyzerConfigOptions GlobalOptions { get; } =
            new TestAnalyzerConfigOptions(new Dictionary<string, string> { [key] = value });

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var configured))
            {
                value = configured;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
