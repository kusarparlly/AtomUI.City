using System.Globalization;
using AtomUI.City.Generators.Diagnostics;

namespace AtomUI.City.Generators.Localization;

internal static class LocalizationManifestBuilder
{
    public static LocalizationManifestResult Build(
        IReadOnlyList<LanguagePackageMetadata> packages,
        IReadOnlyList<LocalizedResourceMetadata> resources)
    {
        if (packages is null)
        {
            throw new ArgumentNullException(nameof(packages));
        }

        if (resources is null)
        {
            throw new ArgumentNullException(nameof(resources));
        }

        var diagnostics = new List<GeneratorDiagnostic>();
        AddInvalidCultureDiagnostics(packages, resources, diagnostics);
        AddInvalidScopeDiagnostics(packages, resources, diagnostics);
        if (diagnostics.Count > 0)
        {
            return new LocalizationManifestResult(CreateEmptyManifest(), diagnostics);
        }

        var packagesByIdentity = new Dictionary<(string Culture, string PackageId), LanguagePackageMetadata>(
            PackageIdentityComparer.Instance);

        foreach (var package in packages)
        {
            var identity = (NormalizeCulture(package.Culture), package.PackageId);
            if (packagesByIdentity.ContainsKey(identity))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Language package '{package.PackageId}' for culture '{package.Culture}' is declared more than once.",
                    package.PackageId));
                continue;
            }

            packagesByIdentity.Add(identity, package);
        }

        var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var resolvedPackages = new Dictionary<LocalizedResourceMetadata, LanguagePackageMetadata>();

        foreach (var resource in resources)
        {
            var matchingPackages = packages
                .Where(package => string.Equals(package.PackageId, resource.PackageId, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(resource.Culture)
                        || string.Equals(
                            NormalizeCulture(package.Culture),
                            NormalizeCulture(resource.Culture!),
                            StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (matchingPackages.Length == 0)
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Localized resource '{resource.Key}' references missing package '{resource.PackageId}'.",
                    resource.Key));
                continue;
            }

            if (matchingPackages.Length > 1)
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Localized resource '{resource.Key}' must specify Culture because package id '{resource.PackageId}' exists for multiple cultures.",
                    resource.Key));
                continue;
            }

            var matchingPackage = matchingPackages[0];
            resolvedPackages.Add(resource, matchingPackage);
            if (resource.Scope != matchingPackage.Scope
                || !string.Equals(resource.ScopeId, matchingPackage.ScopeId, StringComparison.Ordinal))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Localized resource '{resource.Key}' scope must match package '{resource.PackageId}'.",
                    resource.Key));
                continue;
            }

            var resourceKey = string.Join(
                "|",
                NormalizeCulture(matchingPackage.Culture),
                resource.PackageId,
                resource.Scope,
                resource.Key);

            if (!resourceKeys.Add(resourceKey))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Localized resource '{resource.Key}' is declared more than once in package '{resource.PackageId}'.",
                    resource.Key));
            }
        }

        var supportedCultures = new HashSet<string>(
            packages.Select(package => NormalizeCulture(package.Culture)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var package in packages)
        {
            if (string.IsNullOrWhiteSpace(package.FallbackCulture) ||
                supportedCultures.Contains(NormalizeCulture(package.FallbackCulture!)))
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Language package '{package.PackageId}' declares fallback culture '{package.FallbackCulture}' without a matching language package.",
                package.PackageId));
        }

        AddFallbackCycleDiagnostics(packages, diagnostics);

        if (diagnostics.Count > 0)
        {
            return new LocalizationManifestResult(CreateEmptyManifest(), diagnostics);
        }

        var packageEntries = packages
            .Select(package => new LanguagePackageManifestEntry(
                package.PackageId,
                NormalizeCulture(package.Culture),
                package.Scope,
                package.ResourceBaseName,
                string.IsNullOrWhiteSpace(package.FallbackCulture)
                    ? null
                    : NormalizeCulture(package.FallbackCulture!),
                package.Version,
                package.Checksum,
                package.ScopeId,
                package.ContributionId))
            .OrderBy(package => package.PackageId, StringComparer.Ordinal)
            .ThenBy(package => package.Culture, StringComparer.Ordinal)
            .ToArray();
        var resourceEntries = resources
            .Select(resource =>
            {
                var package = resolvedPackages[resource];

                return new LocalizedResourceManifestEntry(
                    resource.Key,
                    resource.PackageId,
                    NormalizeCulture(package.Culture),
                    resource.Kind,
                    resource.Scope,
                    resource.Version,
                    resource.Critical,
                    resource.ScopeId);
            })
            .OrderBy(resource => resource.PackageId, StringComparer.Ordinal)
            .ThenBy(resource => resource.Key, StringComparer.Ordinal)
            .ToArray();
        var manifestSupportedCultures = packages
            .Select(package => package.Culture)
            .Select(NormalizeCulture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(culture => culture, StringComparer.Ordinal)
            .ToArray();
        var fallbacks = packages
            .Where(package => !string.IsNullOrWhiteSpace(package.FallbackCulture))
            .Select(package => new CultureFallbackManifestEntry(
                NormalizeCulture(package.Culture),
                NormalizeCulture(package.FallbackCulture!)))
            .OrderBy(fallback => fallback.Culture, StringComparer.Ordinal)
            .ThenBy(fallback => fallback.FallbackCulture, StringComparer.Ordinal)
            .ToArray();

        return new LocalizationManifestResult(
            new LocalizationManifest(packageEntries, resourceEntries, manifestSupportedCultures, fallbacks),
            diagnostics);
    }

    private static LocalizationManifest CreateEmptyManifest()
    {
        return new LocalizationManifest([], [], [], []);
    }

    private static void AddInvalidCultureDiagnostics(
        IEnumerable<LanguagePackageMetadata> packages,
        IEnumerable<LocalizedResourceMetadata> resources,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var package in packages)
        {
            if (!IsInvalidCulture(package.Culture))
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Language package '{package.PackageId}' declares invalid culture '{package.Culture}'.",
                package.PackageId));
        }

        foreach (var package in packages.Where(package =>
                     !string.IsNullOrWhiteSpace(package.FallbackCulture)
                     && IsInvalidCulture(package.FallbackCulture!)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Language package '{package.PackageId}' has invalid fallback culture '{package.FallbackCulture}'.",
                package.PackageId));
        }

        foreach (var resource in resources.Where(resource =>
                     !string.IsNullOrWhiteSpace(resource.Culture)
                     && IsInvalidCulture(resource.Culture!)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Localized resource '{resource.Key}' has invalid culture '{resource.Culture}'.",
                resource.Key));
        }
    }

    private static bool IsInvalidCulture(string culture)
    {
        try
        {
            CultureInfo.GetCultureInfo(culture);
            return false;
        }
        catch (CultureNotFoundException)
        {
            return true;
        }
    }

    private static void AddInvalidScopeDiagnostics(
        IEnumerable<LanguagePackageMetadata> packages,
        IEnumerable<LocalizedResourceMetadata> resources,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var package in packages.Where(package =>
                     !Enum.IsDefined(typeof(ResourceScopeMetadata), package.Scope)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Language package '{package.PackageId}' declares unknown resource scope '{package.Scope}'.",
                package.PackageId));
        }

        foreach (var resource in resources.Where(resource =>
                     !Enum.IsDefined(typeof(ResourceScopeMetadata), resource.Scope)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Localized resource '{resource.Key}' declares unknown resource scope '{resource.Scope}'.",
                resource.Key));
        }

        foreach (var resource in resources.Where(resource =>
                     !Enum.IsDefined(typeof(LocalizedResourceMetadataKind), resource.Kind)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Localized resource '{resource.Key}' declares unknown resource kind '{resource.Kind}'.",
                resource.Key));
        }

        foreach (var package in packages.Where(package => string.IsNullOrWhiteSpace(package.ResourceBaseName)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Language package '{package.PackageId}' requires an embedded resource base name.",
                package.PackageId));
        }

        foreach (var package in packages.Where(package =>
                     RequiresScopeId(package.Scope) && string.IsNullOrWhiteSpace(package.ScopeId)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Language package '{package.PackageId}' in scope '{package.Scope}' requires a scope id.",
                package.PackageId));
        }

        foreach (var resource in resources.Where(resource =>
                     RequiresScopeId(resource.Scope) && string.IsNullOrWhiteSpace(resource.ScopeId)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Localized resource '{resource.Key}' in scope '{resource.Scope}' requires a scope id.",
                resource.Key));
        }

        foreach (var resource in resources.Where(resource =>
                     Enum.IsDefined(typeof(LocalizedResourceMetadataKind), resource.Kind)
                     && !IsSupportedStringResource(resource.Kind)))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Localized resource '{resource.Key}' uses kind '{resource.Kind}', which has no runtime execution contract in Localization 1.0.",
                resource.Key));
        }
    }

    private static bool RequiresScopeId(ResourceScopeMetadata scope)
    {
        return scope is ResourceScopeMetadata.Module
            or ResourceScopeMetadata.Plugin
            or ResourceScopeMetadata.Route
            or ResourceScopeMetadata.Window;
    }

    private static bool IsSupportedStringResource(LocalizedResourceMetadataKind kind)
    {
        return kind is LocalizedResourceMetadataKind.String
            or LocalizedResourceMetadataKind.FormattedString
            or LocalizedResourceMetadataKind.ValidationMessage
            or LocalizedResourceMetadataKind.ErrorMessage
            or LocalizedResourceMetadataKind.CommandText
            or LocalizedResourceMetadataKind.RouteTitle;
    }

    private static void AddFallbackCycleDiagnostics(
        IEnumerable<LanguagePackageMetadata> packages,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var graph = packages
            .GroupBy(package => NormalizeCulture(package.Culture), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(package => package.FallbackCulture)
                    .Where(fallback => !string.IsNullOrWhiteSpace(fallback))
                    .Select(fallback => NormalizeCulture(fallback!))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in graph.Keys)
        {
            if (Visit(culture))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Localization fallback graph contains a cycle involving culture '{culture}'.",
                    culture));
                return;
            }
        }

        bool Visit(string culture)
        {
            if (active.Contains(culture))
            {
                return true;
            }

            if (!visited.Add(culture))
            {
                return false;
            }

            active.Add(culture);
            if (graph.TryGetValue(culture, out var fallbacks))
            {
                foreach (var fallback in fallbacks)
                {
                    if (Visit(fallback))
                    {
                        return true;
                    }
                }
            }

            active.Remove(culture);
            return false;
        }
    }

    private static string NormalizeCulture(string culture)
    {
        return CultureInfo.GetCultureInfo(culture).Name;
    }

    private sealed class PackageIdentityComparer : IEqualityComparer<(string Culture, string PackageId)>
    {
        public static PackageIdentityComparer Instance { get; } = new();

        public bool Equals(
            (string Culture, string PackageId) left,
            (string Culture, string PackageId) right)
        {
            return string.Equals(left.Culture, right.Culture, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.PackageId, right.PackageId, StringComparison.Ordinal);
        }

        public int GetHashCode((string Culture, string PackageId) value)
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(value.Culture) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(value.PackageId);
            }
        }
    }
}
