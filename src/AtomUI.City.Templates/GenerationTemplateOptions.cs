using System.Globalization;

namespace AtomUI.City.Templates;

public sealed class GenerationTemplateOptions
{
    private static readonly HashSet<string> ReservedIdentifiers = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
    };

    private IReadOnlyList<string> _cultures = Array.AsReadOnly(["en-US", "zh-CN"]);
    private IReadOnlyList<string> _moduleDependencies = Array.Empty<string>();

    public required GenerationTemplateKind Kind { get; init; }

    public required string Name { get; init; }

    public required string ProjectName { get; init; }

    public required string RootNamespace { get; init; }

    public required string OutputPath { get; init; }

    public string? RoutePath { get; init; }

    public IReadOnlyList<string> Cultures
    {
        get => _cultures;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _cultures = Array.AsReadOnly(value.ToArray());
        }
    }

    public IReadOnlyList<string> ModuleDependencies
    {
        get => _moduleDependencies;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _moduleDependencies = Array.AsReadOnly(value.ToArray());
        }
    }

    public bool IncludeTests { get; init; } = true;

    public bool ReloadableConfiguration { get; init; }

    public IReadOnlyList<TemplateDiagnostic> Validate()
    {
        var diagnostics = new List<TemplateDiagnostic>();
        if (!Enum.IsDefined(Kind))
        {
            diagnostics.Add(CreateDiagnostic(
                "AUCTPL2001",
                "Generation template kind is invalid.",
                "kind",
                Kind.ToString(),
                "known-generation-kind"));
        }

        if (!TryGetNameSegments(Name, out _))
        {
            diagnostics.Add(CreateDiagnostic(
                "AUCTPL2001",
                "Generation name must contain portable C# identifier segments.",
                "name",
                Name,
                "relative-csharp-identifier-path"));
        }

        if (!IsValidProjectName(ProjectName))
        {
            diagnostics.Add(CreateDiagnostic(
                "AUCTPL2002",
                "ProjectName must be a portable project identifier.",
                "projectName",
                ProjectName,
                "portable-project-name"));
        }

        if (!IsValidQualifiedIdentifier(RootNamespace))
        {
            diagnostics.Add(CreateDiagnostic(
                "AUCTPL2003",
                "RootNamespace must be a valid C# namespace.",
                "rootNamespace",
                RootNamespace,
                "csharp-namespace"));
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            diagnostics.Add(CreateDiagnostic(
                "AUCTPL0001",
                "OutputPath is required.",
                "outputPath",
                OutputPath,
                "non-empty-path"));
        }
        else
        {
            try
            {
                _ = Path.GetFullPath(OutputPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                diagnostics.Add(CreateDiagnostic(
                    "AUCTPL0001",
                    "OutputPath must be a valid file-system path.",
                    "outputPath",
                    OutputPath,
                    "valid-file-system-path"));
            }
        }

        if (Kind == GenerationTemplateKind.Page && !IsValidRoute(RoutePath))
        {
            diagnostics.Add(CreateDiagnostic(
                "AUCTPL2004",
                "Page generation requires an absolute route path without traversal or quotes.",
                "routePath",
                RoutePath,
                "absolute-route-path"));
        }

        if (Kind == GenerationTemplateKind.Localization)
        {
            ValidateCultures(diagnostics);
        }

        for (var index = 0; index < ModuleDependencies.Count; index++)
        {
            var dependency = ModuleDependencies[index];
            if (!IsValidQualifiedIdentifier(dependency))
            {
                diagnostics.Add(CreateDiagnostic(
                    "AUCTPL2001",
                    "Module dependency must be a fully qualified C# type name.",
                    $"moduleDependencies[{index}]",
                    dependency,
                    "qualified-csharp-type"));
            }
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    internal IReadOnlyList<string> GetNameSegments()
    {
        return TryGetNameSegments(Name, out var segments)
            ? segments
            : throw new InvalidOperationException("Generation name was not validated.");
    }

    internal string GetTemplateId()
    {
        return Kind switch
        {
            GenerationTemplateKind.Module => "atomui-city-module",
            GenerationTemplateKind.Page => "atomui-city-page",
            GenerationTemplateKind.Test => "atomui-city-test",
            GenerationTemplateKind.Configuration => "atomui-city-configuration",
            GenerationTemplateKind.Localization => "atomui-city-localization",
            _ => "atomui-city-generation",
        };
    }

    internal string GetNormalizedRoutePath()
    {
        return RoutePath!.Trim('/');
    }

    private void ValidateCultures(List<TemplateDiagnostic> diagnostics)
    {
        if (Cultures.Count == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                "AUCTPL2005",
                "Localization generation requires at least one culture.",
                "cultures",
                string.Empty,
                "non-empty-culture-list"));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < Cultures.Count; index++)
        {
            var culture = Cultures[index];
            try
            {
                var canonicalName = CultureInfo.GetCultureInfo(culture).Name;
                if (string.IsNullOrWhiteSpace(canonicalName) || !seen.Add(canonicalName))
                {
                    throw new CultureNotFoundException(nameof(culture), culture, "Culture must be unique and specific.");
                }
            }
            catch (CultureNotFoundException)
            {
                diagnostics.Add(CreateDiagnostic(
                    "AUCTPL2005",
                    "Culture must be a unique valid culture name.",
                    $"cultures[{index}]",
                    culture,
                    "unique-culture-name"));
            }
        }
    }

    private TemplateDiagnostic CreateDiagnostic(
        string code,
        string message,
        string variable,
        string? rawValue,
        string rule)
    {
        return new TemplateDiagnostic(
            code,
            message,
            new Dictionary<string, object?>
            {
                ["templateId"] = GetTemplateId(),
                ["targetPath"] = OutputPath,
                ["variable"] = variable,
                ["rawValue"] = rawValue,
                ["rule"] = rule,
            });
    }

    private static bool IsValidProjectName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !value.Contains('/') &&
            !value.Contains('\\') &&
            value.Split('.').All(IsValidIdentifier);
    }

    private static bool IsValidQualifiedIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Split('.').All(IsValidIdentifier);
    }

    private static bool TryGetNameSegments(string value, out IReadOnlyList<string> segments)
    {
        segments = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(value) ||
            value.StartsWith('/') ||
            value.StartsWith('\\') ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains("\\\\", StringComparison.Ordinal))
        {
            return false;
        }

        var resolved = value
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.None);
        if (resolved.Length == 0 || resolved.Any(segment => !IsValidIdentifier(segment)))
        {
            return false;
        }

        segments = Array.AsReadOnly(resolved);
        return true;
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            ReservedIdentifiers.Contains(value) ||
            !(value[0] == '_' || char.IsLetter(value[0])))
        {
            return false;
        }

        return value.Skip(1).All(character => character == '_' || char.IsLetterOrDigit(character));
    }

    private static bool IsValidRoute(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
            value.StartsWith('/') &&
            value.Length > 1 &&
            !value.StartsWith("//", StringComparison.Ordinal) &&
            !value.Contains("..", StringComparison.Ordinal) &&
            !value.Contains('\\') &&
            !value.Contains('"') &&
            !value.Any(char.IsWhiteSpace) &&
            !value.Any(char.IsControl);
    }
}
