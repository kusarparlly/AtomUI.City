using AtomUI.City.Generators.Diagnostics;

namespace AtomUI.City.Generators.PluginSystem;

internal static class PluginManifestBuilder
{
    public static PluginManifestResult Build(PluginMetadata metadata)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var diagnostics = new List<GeneratorDiagnostic>();

        ValidateRequired(metadata.PluginId, "pluginId", diagnostics);
        ValidateRequired(metadata.PackageId, "packageId", diagnostics);
        ValidateRequired(metadata.Version, "version", diagnostics);
        ValidateRequired(metadata.DisplayNameKey, "displayNameKey", diagnostics);
        ValidateRequired(metadata.MainAssembly, "mainAssembly", diagnostics);
        ValidateRequired(metadata.TargetFramework, "targetFramework", diagnostics);
        ValidateRequired(metadata.PluginApiVersion, "pluginApiVersion", diagnostics);
        ValidateRequired(metadata.MinHostVersion, "minHostVersion", diagnostics);

        if (!string.IsNullOrWhiteSpace(metadata.MainAssembly) && IsInvalidMainAssembly(metadata.MainAssembly))
        {
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Plugin main assembly '{metadata.MainAssembly}' must be a file name.",
                "mainAssembly"));
        }

        AddDuplicateContributionDiagnostics(metadata.Contributions, diagnostics);
        AddDuplicateCapabilityDiagnostics(metadata.Capabilities, diagnostics);
        AddContributionPathDiagnostics(metadata.Contributions, diagnostics);
        AddDependencyVersionRangeDiagnostics(metadata.Dependencies, diagnostics);

        if (diagnostics.Count > 0)
        {
            return new PluginManifestResult(CreateManifest(metadata, [], [], []), diagnostics);
        }

        var capabilities = metadata
            .Capabilities
            .Select(capability => new PluginCapabilityManifestEntry(
                capability.Name,
                capability.Scope.OrderBy(scope => scope, StringComparer.Ordinal).ToArray()))
            .OrderBy(capability => capability.Name, StringComparer.Ordinal)
            .ToArray();
        var contributions = metadata
            .Contributions
            .Select(contribution => new PluginContributionManifestEntry(
                contribution.Type,
                contribution.Path,
                contribution.Required))
            .OrderBy(contribution => contribution.Type, StringComparer.Ordinal)
            .ToArray();
        var dependencies = metadata
            .Dependencies
            .Select(dependency => new PluginDependencyManifestEntry(
                dependency.PluginId,
                dependency.VersionRange))
            .OrderBy(dependency => dependency.PluginId, StringComparer.Ordinal)
            .ToArray();

        return new PluginManifestResult(
            CreateManifest(metadata, capabilities, contributions, dependencies),
            diagnostics);
    }

    private static PluginManifest CreateManifest(
        PluginMetadata metadata,
        IReadOnlyList<PluginCapabilityManifestEntry> capabilities,
        IReadOnlyList<PluginContributionManifestEntry> contributions,
        IReadOnlyList<PluginDependencyManifestEntry> dependencies)
    {
        return new PluginManifest(
            metadata.SchemaVersion,
            metadata.PluginId,
            metadata.PackageId,
            metadata.Version,
            metadata.DisplayNameKey,
            metadata.DescriptionKey,
            metadata.Publisher,
            metadata.MainAssembly,
            metadata.TargetFramework,
            metadata.PluginApiVersion,
            metadata.MinHostVersion,
            metadata.MaxHostVersion,
            metadata.Unloadable,
            metadata.AotCompatible,
            capabilities,
            contributions,
            dependencies);
    }

    private static void ValidateRequired(
        string value,
        string fieldName,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        diagnostics.Add(new GeneratorDiagnostic(
            GeneratorDiagnostics.InvalidManifestInput,
            $"Plugin manifest field '{fieldName}' is required.",
            fieldName));
    }

    private static void AddDuplicateContributionDiagnostics(
        IEnumerable<PluginContributionManifestMetadata> contributions,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var types = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contribution in contributions)
        {
            if (types.Add(contribution.Type))
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Plugin contribution manifest '{contribution.Type}' is declared more than once.",
                contribution.Type));
        }
    }

    private static void AddDuplicateCapabilityDiagnostics(
        IEnumerable<PluginCapabilityMetadata> capabilities,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var capability in capabilities)
        {
            if (names.Add(capability.Name))
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Plugin capability '{capability.Name}' is declared more than once.",
                capability.Name));
        }
    }

    private static void AddContributionPathDiagnostics(
        IEnumerable<PluginContributionManifestMetadata> contributions,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var contribution in contributions)
        {
            if (!IsInvalidContributionPath(contribution.Path))
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Plugin contribution manifest path '{contribution.Path}' must be a relative package path using '/'.",
                contribution.Type));
        }
    }

    private static bool IsInvalidMainAssembly(string mainAssembly)
    {
        return mainAssembly.Contains("/", StringComparison.Ordinal) ||
            mainAssembly.Contains("\\", StringComparison.Ordinal) ||
            Path.GetFileName(mainAssembly) != mainAssembly;
    }

    private static bool IsInvalidContributionPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("\\", StringComparison.Ordinal))
        {
            return true;
        }

        var firstSeparatorIndex = path.IndexOf("/", StringComparison.Ordinal);
        var firstSegment = firstSeparatorIndex < 0
            ? path
            : path.Substring(0, firstSeparatorIndex);

        if (firstSegment.Contains(":", StringComparison.Ordinal))
        {
            return true;
        }

        return path
            .Split('/')
            .Any(segment => string.Equals(segment, ".", StringComparison.Ordinal) ||
                string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static void AddDependencyVersionRangeDiagnostics(
        IEnumerable<PluginDependencyMetadata> dependencies,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var dependency in dependencies)
        {
            if (IsValidDependencyVersionRange(dependency.VersionRange))
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Plugin dependency '{dependency.PluginId}' has invalid version range '{dependency.VersionRange}'.",
                dependency.PluginId));
        }
    }

    private static bool IsValidDependencyVersionRange(string? versionRange)
    {
        if (string.IsNullOrWhiteSpace(versionRange))
        {
            return true;
        }

        var range = versionRange!.Trim();
        if (range.Length < 2)
        {
            return IsValidSemanticVersion(range);
        }

        var hasLowerBound = range[0] == '[' || range[0] == '(';
        var hasUpperBound = range[range.Length - 1] == ']' || range[range.Length - 1] == ')';
        if (!hasLowerBound || !hasUpperBound)
        {
            return IsValidSemanticVersion(range);
        }

        var body = range.Substring(1, range.Length - 2);
        var commaIndex = body.IndexOf(",", StringComparison.Ordinal);
        if (commaIndex < 0)
        {
            return false;
        }

        var lowerBound = body.Substring(0, commaIndex).Trim();
        var upperBound = body.Substring(commaIndex + 1).Trim();

        return (lowerBound.Length == 0 || IsValidSemanticVersion(lowerBound)) &&
            (upperBound.Length == 0 || IsValidSemanticVersion(upperBound));
    }

    private static bool IsValidSemanticVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buildSeparatorIndex = value.IndexOf('+');
        var valueWithoutBuild = buildSeparatorIndex < 0
            ? value
            : value.Substring(0, buildSeparatorIndex);
        var build = buildSeparatorIndex < 0
            ? null
            : value.Substring(buildSeparatorIndex + 1);

        if (build is not null && !IsValidIdentifierList(build, allowLeadingZeroNumbers: true))
        {
            return false;
        }

        var prereleaseSeparatorIndex = valueWithoutBuild.IndexOf('-');
        var core = prereleaseSeparatorIndex < 0
            ? valueWithoutBuild
            : valueWithoutBuild.Substring(0, prereleaseSeparatorIndex);
        var prerelease = prereleaseSeparatorIndex < 0
            ? null
            : valueWithoutBuild.Substring(prereleaseSeparatorIndex + 1);

        if (prerelease is not null && !IsValidIdentifierList(prerelease, allowLeadingZeroNumbers: false))
        {
            return false;
        }

        var coreParts = core.Split('.');
        return coreParts.Length == 3 &&
            IsValidCoreVersionIdentifier(coreParts[0]) &&
            IsValidCoreVersionIdentifier(coreParts[1]) &&
            IsValidCoreVersionIdentifier(coreParts[2]);
    }

    private static bool IsValidIdentifierList(string value, bool allowLeadingZeroNumbers)
    {
        return value.Length > 0 &&
            value
                .Split('.')
                .All(identifier => IsValidIdentifier(identifier, allowLeadingZeroNumbers));
    }

    private static bool IsValidIdentifier(string identifier, bool allowLeadingZeroNumbers)
    {
        if (identifier.Length == 0 ||
            identifier.Any(character => !IsAsciiLetterOrDigit(character) && character != '-'))
        {
            return false;
        }

        return allowLeadingZeroNumbers ||
            !IsNumericIdentifier(identifier) ||
            IsValidCoreVersionIdentifier(identifier);
    }

    private static bool IsValidCoreVersionIdentifier(string identifier)
    {
        return IsNumericIdentifier(identifier) &&
            (identifier.Length == 1 || identifier[0] != '0') &&
            int.TryParse(identifier, out _);
    }

    private static bool IsNumericIdentifier(string identifier)
    {
        return identifier.Length > 0 &&
            identifier.All(IsAsciiDigit);
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return IsAsciiDigit(character) ||
            character is >= 'A' and <= 'Z' ||
            character is >= 'a' and <= 'z';
    }

    private static bool IsAsciiDigit(char character)
    {
        return character is >= '0' and <= '9';
    }
}
