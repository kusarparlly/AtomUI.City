using AtomUI.City.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.Diagnostics;

internal static class GeneratorDiagnostics
{
    public static readonly GeneratorDiagnosticDefinition DuplicateModuleName = new GeneratorDiagnosticDefinition(
        GeneratorDiagnosticIds.DuplicateModuleName,
        "Duplicate module name",
        "Module names must be unique within the generated module manifest.",
        GeneratorDiagnosticSeverity.Error);

    public static readonly GeneratorDiagnosticDefinition CircularModuleDependency = new GeneratorDiagnosticDefinition(
        GeneratorDiagnosticIds.CircularModuleDependency,
        "Circular module dependency",
        "Module dependency graph contains a circular dependency.",
        GeneratorDiagnosticSeverity.Error);

    public static readonly GeneratorDiagnosticDefinition DuplicateRoute = new GeneratorDiagnosticDefinition(
        GeneratorDiagnosticIds.DuplicateRoute,
        "Duplicate route",
        "Route patterns and route names must be unique within the generated route manifest.",
        GeneratorDiagnosticSeverity.Error);

    public static readonly GeneratorDiagnosticDefinition InvalidManifestInput = new GeneratorDiagnosticDefinition(
        GeneratorDiagnosticIds.InvalidManifestInput,
        "Invalid manifest input",
        "Generator manifest input is invalid or incomplete.",
        GeneratorDiagnosticSeverity.Error);

    public static readonly GeneratorDiagnosticDefinition DuplicatePresentationView = new GeneratorDiagnosticDefinition(
        GeneratorDiagnosticIds.DuplicatePresentationView,
        "Duplicate presentation view",
        "A view model can only have one presentation view for each view key.",
        GeneratorDiagnosticSeverity.Error);

    public static readonly GeneratorDiagnosticDefinition MultipleApplicationModules = new GeneratorDiagnosticDefinition(
        GeneratorDiagnosticIds.MultipleApplicationModules,
        "Multiple application modules",
        "An application compilation can declare only one ApplicationModule.",
        GeneratorDiagnosticSeverity.Error);

    public static readonly GeneratorDiagnosticDefinition InvalidGeneratedModule = new GeneratorDiagnosticDefinition(
        GeneratorDiagnosticIds.InvalidGeneratedModule,
        "Module cannot be generated",
        "A generated module does not satisfy the module catalog or application-root contract.",
        GeneratorDiagnosticSeverity.Error);

    public static IReadOnlyList<GeneratorDiagnosticDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        DuplicateModuleName,
        CircularModuleDependency,
        DuplicateRoute,
        InvalidManifestInput,
        DuplicatePresentationView,
        MultipleApplicationModules,
        InvalidGeneratedModule,
    });

    public static Diagnostic CreateRoslynDiagnostic(
        GeneratorFeature feature,
        GeneratorDiagnostic diagnostic,
        Location? location = null,
        params object?[] messageArgs)
    {
        if (diagnostic is null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        var descriptor = new DiagnosticDescriptor(
            diagnostic.Id,
            diagnostic.Title,
            diagnostic.Message,
            $"AtomUI.City.Generators.{GeneratorFeatureNames.GetName(feature)}",
            ToDiagnosticSeverity(diagnostic.Severity),
            isEnabledByDefault: true);

        return Diagnostic.Create(
            descriptor,
            location ?? Location.None,
            messageArgs ?? Array.Empty<object?>());
    }

    private static DiagnosticSeverity ToDiagnosticSeverity(GeneratorDiagnosticSeverity severity)
    {
        switch (severity)
        {
            case GeneratorDiagnosticSeverity.Info:
                return DiagnosticSeverity.Info;
            case GeneratorDiagnosticSeverity.Warning:
                return DiagnosticSeverity.Warning;
            case GeneratorDiagnosticSeverity.Error:
                return DiagnosticSeverity.Error;
            default:
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown generator diagnostic severity.");
        }
    }
}
