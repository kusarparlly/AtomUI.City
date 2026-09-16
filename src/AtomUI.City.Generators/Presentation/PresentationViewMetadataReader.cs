using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.Presentation;

internal static class PresentationViewMetadataReader
{
    private const string ViewForAttributeName = "AtomUI.City.Presentation.ViewForAttribute";

    public static IReadOnlyList<PresentationViewMetadata> Read(INamedTypeSymbol type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var viewTypeName = GetTypeName(type);
        var constructorSelection = ReadConstructorSelection(type);

        return Array.AsReadOnly(
            type
                .GetAttributes()
                .Where(attribute => string.Equals(GetAttributeTypeName(attribute), ViewForAttributeName, StringComparison.Ordinal))
                .Select(attribute => ReadView(viewTypeName, constructorSelection, attribute))
                .Where(view => view is not null)
                .Cast<PresentationViewMetadata>()
                .ToArray());
    }

    private static PresentationViewMetadata? ReadView(
        string viewTypeName,
        ConstructorSelection constructorSelection,
        AttributeData attribute)
    {
        var viewModelTypeName = ReadConstructorTypeName(attribute, 0);
        if (string.IsNullOrWhiteSpace(viewModelTypeName))
        {
            return null;
        }

        return new PresentationViewMetadata(
            viewTypeName,
            viewModelTypeName!,
            ReadNamedString(attribute, "Key"),
            ReadNamedString(attribute, "PluginId"),
            ReadNamedString(attribute, "ContributionId"),
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
            constructorSelection.Parameters,
            constructorSelection.HasAmbiguousConstructors);
    }

    private static ConstructorSelection ReadConstructorSelection(INamedTypeSymbol type)
    {
        var constructors = type.InstanceConstructors
            .Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(candidate => candidate.Parameters.Length)
            .ThenBy(GetConstructorSignature, StringComparer.Ordinal)
            .ToArray();

        if (constructors.Length == 0 || constructors[0].Parameters.Length == 0)
        {
            return new ConstructorSelection([], hasAmbiguousConstructors: false);
        }

        var maxParameterCount = constructors[0].Parameters.Length;
        var hasAmbiguousConstructors = constructors.Count(constructor => constructor.Parameters.Length == maxParameterCount) > 1;
        var parameters = constructors[0].Parameters
            .Select(parameter => new PresentationViewConstructorParameter(GetTypeName(parameter.Type)))
            .ToArray();

        return new ConstructorSelection(parameters, hasAmbiguousConstructors);
    }

    private static string GetConstructorSignature(IMethodSymbol constructor)
    {
        return string.Join(",", constructor.Parameters.Select(parameter => GetTypeName(parameter.Type)));
    }

    private static string? ReadConstructorTypeName(AttributeData attribute, int index)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value is INamedTypeSymbol type ? GetTypeName(type) : null;
    }

    private static string? ReadNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
            {
                return argument.Value.Value as string;
            }
        }

        return null;
    }

    private static string? GetAttributeTypeName(AttributeData attribute)
    {
        return attribute.AttributeClass is null ? null : GetTypeName(attribute.AttributeClass);
    }

    private static string GetTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private sealed class ConstructorSelection
    {
        public ConstructorSelection(
            IReadOnlyList<PresentationViewConstructorParameter> parameters,
            bool hasAmbiguousConstructors)
        {
            Parameters = parameters;
            HasAmbiguousConstructors = hasAmbiguousConstructors;
        }

        public IReadOnlyList<PresentationViewConstructorParameter> Parameters { get; }

        public bool HasAmbiguousConstructors { get; }
    }
}
