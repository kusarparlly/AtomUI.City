using AtomUI.City.Generators.Data;
using AtomUI.City.Generators.DependencyInjection;
using AtomUI.City.Generators.Localization;
using AtomUI.City.Generators.Modularity;
using AtomUI.City.Generators.Presentation;
using AtomUI.City.Generators.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AtomUI.City.Generators.Tests;

public sealed class GeneratedSourceVisibilityTests
{
    [Fact]
    public void SourceBuildersExposeOnlyReviewedPublicDeclarations()
    {
        const string assemblyName = "Sample.App";
        var localizationManifest = new LocalizationManifest(
            [new LanguagePackageManifestEntry("sample", "en-US", ResourceScopeMetadata.Host, null, null, null, null)],
            [new LocalizedResourceManifestEntry(
                "Sample.Title",
                "sample",
                "en-US",
                LocalizedResourceMetadataKind.String,
                ResourceScopeMetadata.Host,
                null,
                critical: false)],
            ["en-US"],
            []);
        var route = new RouteDefinitionMetadata(
            "Sample.App.SampleRoutes",
            "Home",
            "home",
            RouteDefinitionMetadataKind.Route,
            "/home",
            viewModelTypeName: null,
            parentMethodName: null,
            outletName: "main",
            extensionPoint: null,
            redirectTargetMethodName: null);
        var sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Data"] = DataClientRegistrarSourceBuilder.Build(assemblyName, []),
            ["DependencyInjection"] = ServiceRegistrarSourceBuilder.Build(assemblyName, null, [], []),
            ["Localization"] = LocalizationRegistrarSourceBuilder.Build(localizationManifest),
            ["Modularity"] = ModuleRegistrarSourceBuilder.Build(assemblyName, [], []),
            ["Presentation"] = PresentationViewRegistrarSourceBuilder.Build(
                new PresentationViewManifest([])),
            ["Routing"] = RouteSourceBuilder.Build(
                [new RouteMapMetadata("Sample.App.SampleRoutes", [route])],
                new RouteManifest([])),
        };

        var actual = sources.ToDictionary(
            static pair => pair.Key,
            static pair => ReadPublicDeclarations(pair.Value),
            StringComparer.Ordinal);
        var dataRegistrar = GetSimpleTypeName(DataClientRegistrarSourceBuilder.GetRegistrarTypeName(assemblyName));
        var serviceRegistrar = GetSimpleTypeName(ServiceRegistrarSourceBuilder.GetRegistrarTypeName(assemblyName));
        var moduleRegistrar = GetSimpleTypeName(ModuleRegistrarSourceBuilder.GetRegistrarTypeName(assemblyName));

        Assert.Equal(
            [$"method:{dataRegistrar}.Register", $"type:{dataRegistrar}"],
            actual["Data"]);
        Assert.Equal(
            [$"method:{serviceRegistrar}.Register", $"type:{serviceRegistrar}"],
            actual["DependencyInjection"]);
        Assert.Equal(
            [
                "field:GeneratedLocalizationManifest.Keys.Sample_Title",
                "method:GeneratedLocalizationManifest.RegisterPackages",
                "property:GeneratedLocalizationManifest.ResourceKeys",
                "property:GeneratedLocalizationManifest.SupportedCultures",
                "type:GeneratedLocalizationManifest",
                "type:GeneratedLocalizationManifest.Keys",
            ],
            actual["Localization"]);
        Assert.Equal(
            [$"method:{moduleRegistrar}.Register", $"type:{moduleRegistrar}"],
            actual["Modularity"]);
        Assert.Equal(
            [
                "method:GeneratedPresentationViewRegistrar.RegisterViews",
                "type:GeneratedPresentationViewRegistrar",
            ],
            actual["Presentation"]);
        Assert.Equal(
            [
                "method:GeneratedRoutingRouteManifest.CreateDescriptors",
                "method:GeneratedRoutingRouteManifest.CreateSnapshot",
                "method:SampleRoutes.Home",
                "type:GeneratedRoutingRouteManifest",
                "type:SampleRoutes",
            ],
            actual["Routing"]);
    }

    private static string[] ReadPublicDeclarations(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        return root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(HasPublicModifier)
            .SelectMany(Describe)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasPublicModifier(MemberDeclarationSyntax declaration) =>
        declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword));

    private static IEnumerable<string> Describe(MemberDeclarationSyntax declaration)
    {
        var containingType = GetContainingTypeName(declaration);
        switch (declaration)
        {
            case BaseTypeDeclarationSyntax type:
                yield return $"type:{GetTypeName(type)}";
                break;
            case MethodDeclarationSyntax method:
                yield return $"method:{containingType}.{method.Identifier.ValueText}";
                break;
            case PropertyDeclarationSyntax property:
                yield return $"property:{containingType}.{property.Identifier.ValueText}";
                break;
            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                {
                    yield return $"field:{containingType}.{variable.Identifier.ValueText}";
                }

                break;
        }
    }

    private static string GetContainingTypeName(MemberDeclarationSyntax declaration)
    {
        var containingTypes = declaration.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(static type => type.Identifier.ValueText);
        return string.Join('.', containingTypes);
    }

    private static string GetTypeName(BaseTypeDeclarationSyntax declaration)
    {
        var containingType = GetContainingTypeName(declaration);
        return string.IsNullOrEmpty(containingType)
            ? declaration.Identifier.ValueText
            : $"{containingType}.{declaration.Identifier.ValueText}";
    }

    private static string GetSimpleTypeName(string typeName) =>
        typeName[(typeName.LastIndexOf('.') + 1)..];
}
