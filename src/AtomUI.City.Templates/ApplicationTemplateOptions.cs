namespace AtomUI.City.Templates;

/// <summary>
/// Represents application template options.
/// </summary>
public sealed class ApplicationTemplateOptions
{
    private static readonly HashSet<string> ReservedIdentifiers = new(StringComparer.Ordinal)
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "case",
        "catch",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };

    /// <summary>
    /// Gets or sets app name.
    /// </summary>
    public required string AppName { get; init; }

    /// <summary>
    /// Gets or sets root namespace.
    /// </summary>
    public required string RootNamespace { get; init; }

    /// <summary>
    /// Gets or sets output path.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// Gets or sets target framework.
    /// </summary>
    public string TargetFramework { get; init; } = "net10.0";

    /// <summary>
    /// Gets or sets include tests.
    /// </summary>
    public bool IncludeTests { get; init; } = true;

    /// <summary>
    /// Gets or sets use aot.
    /// </summary>
    public bool UseAot { get; init; }

    /// <summary>
    /// Gets or sets use dynamic plugins.
    /// </summary>
    public bool UseDynamicPlugins { get; init; }

    /// <summary>
    /// Gets or sets include sample.
    /// </summary>
    public bool IncludeSample { get; init; }

    /// <summary>
    /// Gets effective root namespace.
    /// </summary>
    public string EffectiveRootNamespace => string.IsNullOrWhiteSpace(RootNamespace)
        ? AppName
        : RootNamespace;

    /// <summary>
    /// Executes the validate operation.
    /// </summary>
    public IReadOnlyList<TemplateDiagnostic> Validate()
    {
        var diagnostics = new List<TemplateDiagnostic>();
        if (!IsValidIdentifier(AppName))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0001",
                "AppName must be a valid C# identifier.",
                "appName",
                AppName,
                "csharp-identifier"));
        }

        var rootNamespace = EffectiveRootNamespace;
        if (rootNamespace.Equals("AtomUI.City", StringComparison.Ordinal) ||
            rootNamespace.StartsWith("AtomUI.City.", StringComparison.Ordinal))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0002",
                "RootNamespace must not start with 'AtomUI.City'.",
                "rootNamespace",
                rootNamespace,
                "reserved-framework-namespace"));
        }
        else if (!IsValidNamespace(rootNamespace))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0001",
                "RootNamespace must be a valid C# namespace.",
                "rootNamespace",
                rootNamespace,
                "csharp-namespace"));
        }

        if (!IsValidTargetFramework(TargetFramework))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0001",
                "TargetFramework must be a framework moniker, not a path segment.",
                "targetFramework",
                TargetFramework,
                "target-framework-moniker"));
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            diagnostics.Add(CreateVariableDiagnostic(
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
                diagnostics.Add(CreateVariableDiagnostic(
                    "AUCTPL0001",
                    "OutputPath must be a valid file-system path.",
                    "outputPath",
                    OutputPath,
                    "valid-file-system-path"));
            }
        }

        if (UseAot && UseDynamicPlugins)
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0301",
                "UseAot cannot be combined with UseDynamicPlugins by default.",
                "useDynamicPlugins",
                UseDynamicPlugins.ToString().ToLowerInvariant(),
                "aot-dynamic-plugin-conflict"));
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private TemplateDiagnostic CreateVariableDiagnostic(
        string code,
        string message,
        string variable,
        string rawValue,
        string rule)
    {
        return new TemplateDiagnostic(
            code,
            message,
            new Dictionary<string, object?>
            {
                ["templateId"] = "atomui-city-app",
                ["targetPath"] = OutputPath,
                ["variable"] = variable,
                ["rawValue"] = rawValue,
                ["rule"] = rule,
            });
    }

    private static bool IsValidNamespace(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Split('.').All(IsValidIdentifier);
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            ReservedIdentifiers.Contains(value) ||
            !(value[0] == '_' || char.IsLetter(value[0])))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '_' && !char.IsLetterOrDigit(current))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidTargetFramework(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("net", StringComparison.Ordinal) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains('/', StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal) ||
            !value.All(static current => char.IsLetterOrDigit(current) || current is '.' or '-'))
        {
            return false;
        }

        var platformSeparator = value.IndexOf('-');
        if (platformSeparator != value.LastIndexOf('-'))
        {
            return false;
        }

        var framework = platformSeparator < 0 ? value : value[..platformSeparator];
        var version = framework.AsSpan(3);
        var decimalSeparator = version.IndexOf('.');
        if (decimalSeparator <= 0 ||
            decimalSeparator >= version.Length - 1 ||
            !version[..decimalSeparator].ToString().All(char.IsDigit) ||
            !version[(decimalSeparator + 1)..].ToString().All(char.IsDigit))
        {
            return false;
        }

        if (platformSeparator < 0)
        {
            return true;
        }

        var platform = value.AsSpan(platformSeparator + 1);
        return platform.Length > 0 &&
            char.IsLetter(platform[0]) &&
            platform[^1] != '.' &&
            !platform.Contains("..", StringComparison.Ordinal) &&
            platform.ToString().All(static character => char.IsLetterOrDigit(character) || character == '.');
    }
}
