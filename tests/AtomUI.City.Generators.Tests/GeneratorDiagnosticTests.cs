using AtomUI.City.Generators.Common;
using AtomUI.City.Generators.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace AtomUI.City.Generators.Tests;

public sealed class GeneratorDiagnosticTests
{
    [Fact]
    public void DiagnosticIdsUseAucgenPrefixAndThreeDigits()
    {
        Assert.Equal("AUCGEN002", GeneratorDiagnosticIds.DuplicateModuleName);
        Assert.Equal("AUCGEN003", GeneratorDiagnosticIds.CircularModuleDependency);
        Assert.Equal("AUCGEN004", GeneratorDiagnosticIds.DuplicateRoute);
        Assert.Equal("AUCGEN005", GeneratorDiagnosticIds.InvalidManifestInput);
        Assert.Equal("AUCGEN006", GeneratorDiagnosticIds.DuplicatePresentationView);
        Assert.Equal("AUCGEN007", GeneratorDiagnosticIds.MultipleApplicationModules);
        Assert.Equal("AUCGEN008", GeneratorDiagnosticIds.InvalidGeneratedModule);
        Assert.Equal("AUCGEN009", GeneratorDiagnosticIds.InvalidViewModelValidation);
    }

    [Fact]
    public void DiagnosticDefinitionsExposeStableMetadata()
    {
        var definition = GeneratorDiagnostics.DuplicateModuleName;

        Assert.Equal("AUCGEN002", definition.Id);
        Assert.Equal(GeneratorDiagnosticSeverity.Error, definition.Severity);
        Assert.Contains("unique", definition.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllDiagnosticDefinitionsHaveUniqueIds()
    {
        var ids = GeneratorDiagnostics.All.Select(diagnostic => diagnostic.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllDiagnosticDefinitionsRejectExternalMutation()
    {
        var diagnostics = Assert.IsAssignableFrom<IList<GeneratorDiagnosticDefinition>>(GeneratorDiagnostics.All);

        Assert.Throws<NotSupportedException>(() => diagnostics[0] = GeneratorDiagnostics.DuplicateModuleName);
        Assert.Equal(GeneratorDiagnostics.DuplicateModuleName, GeneratorDiagnostics.All[0]);
    }

    [Fact]
    public void CreateRoslynDiagnosticPreservesMessageArgsSeverityCategoryAndLocation()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("public sealed class SettingsView { }", path: "SettingsView.cs");
        var location = syntaxTree.GetRoot().GetLocation();

        var diagnostic = GeneratorDiagnostics.CreateRoslynDiagnostic(
            GeneratorFeature.Presentation,
            new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                "View '{0}' has invalid metadata."),
            location,
            "SettingsView");

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("AtomUI.City.Generators.Presentation", diagnostic.Descriptor.Category);
        Assert.Equal("View 'SettingsView' has invalid metadata.", diagnostic.GetMessage());
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal("SettingsView.cs", diagnostic.Location.SourceTree?.FilePath);
    }
}
