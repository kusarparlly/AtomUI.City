using Microsoft.CodeAnalysis;
using AtomUI.City.Generators.Diagnostics;

namespace AtomUI.City.Generators.Localization;

internal static class LocalizationMetadataReader
{
    private const string LanguagePackageAttributeName = "AtomUI.City.Localization.LanguagePackageAttribute";
    private const string LocalizedResourceAttributeName = "AtomUI.City.Localization.LocalizedResourceAttribute";

    public static LocalizationMetadata Read(Compilation compilation)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        var attributes = compilation.Assembly.GetAttributes();
        var diagnostics = new List<GeneratorDiagnostic>();
        var packages = attributes
            .Where(attribute => string.Equals(GetAttributeTypeName(attribute), LanguagePackageAttributeName, StringComparison.Ordinal))
            .Select(attribute => ReadLanguagePackage(attribute, diagnostics))
            .Where(package => package is not null)
            .Cast<LanguagePackageMetadata>()
            .ToArray();
        var resources = attributes
            .Where(attribute => string.Equals(GetAttributeTypeName(attribute), LocalizedResourceAttributeName, StringComparison.Ordinal))
            .Select(attribute => ReadLocalizedResource(attribute, diagnostics))
            .Where(resource => resource is not null)
            .Cast<LocalizedResourceMetadata>()
            .ToArray();

        return new LocalizationMetadata(packages, resources, diagnostics);
    }

    private static LanguagePackageMetadata? ReadLanguagePackage(
        AttributeData attribute,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (attribute.ConstructorArguments.Length < 2)
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                "LanguagePackage requires package id and culture constructor arguments.",
                "LanguagePackage"));
            return null;
        }

        var packageId = attribute.ConstructorArguments[0].Value as string;
        var culture = attribute.ConstructorArguments[1].Value as string;

        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(culture))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                "LanguagePackage package id and culture cannot be empty.",
                packageId ?? "LanguagePackage"));
            return null;
        }

        return new LanguagePackageMetadata(
            packageId!,
            culture!,
            ReadScope(attribute),
            ReadNamedString(attribute, "ResourceBaseName"),
            ReadNamedString(attribute, "FallbackCulture"),
            ReadNamedString(attribute, "Version"),
            ReadNamedString(attribute, "Checksum"),
            ReadNamedString(attribute, "ScopeId"),
            ReadNamedString(attribute, "ContributionId"));
    }

    private static LocalizedResourceMetadata? ReadLocalizedResource(
        AttributeData attribute,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (attribute.ConstructorArguments.Length < 2)
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                "LocalizedResource requires key and package id constructor arguments.",
                "LocalizedResource"));
            return null;
        }

        var key = attribute.ConstructorArguments[0].Value as string;
        var packageId = attribute.ConstructorArguments[1].Value as string;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(packageId))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                "LocalizedResource key and package id cannot be empty.",
                key ?? "LocalizedResource"));
            return null;
        }

        return new LocalizedResourceMetadata(
            key!,
            packageId!,
            ReadKind(attribute),
            ReadScope(attribute),
            ReadNamedString(attribute, "Version"),
            ReadNamedBoolean(attribute, "Critical"),
            ReadNamedString(attribute, "ScopeId"),
            ReadNamedString(attribute, "Culture"));
    }

    private static LocalizedResourceMetadataKind ReadKind(AttributeData attribute)
    {
        var value = ReadNamedEnumValue(attribute, "Kind");

        return value.HasValue
            ? (LocalizedResourceMetadataKind)value.Value
            : LocalizedResourceMetadataKind.String;
    }

    private static ResourceScopeMetadata ReadScope(AttributeData attribute)
    {
        var value = ReadNamedEnumValue(attribute, "Scope");

        return value.HasValue
            ? (ResourceScopeMetadata)value.Value
            : ResourceScopeMetadata.Module;
    }

    private static int? ReadNamedEnumValue(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal) &&
                argument.Value.Value is int value)
            {
                return value;
            }
        }

        return null;
    }

    private static bool ReadNamedBoolean(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
            {
                return argument.Value.Value is true;
            }
        }

        return false;
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
        return attribute.AttributeClass is null
            ? null
            : attribute.AttributeClass.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }
}
