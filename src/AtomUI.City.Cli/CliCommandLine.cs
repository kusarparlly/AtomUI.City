namespace AtomUI.City.Cli;

internal sealed class CliCommandLine
{
    private static readonly HashSet<string> ValueOptions =
    [
        "--verbosity",
        "--working-directory",
        "--namespace",
        "--target-framework",
        "--output",
        "--configuration",
        "--framework",
        "--project",
        "--output-root",
        "--plugins-root",
        "--route",
        "--culture",
        "--depends-on",
    ];

    private static readonly HashSet<string> FlagOptions =
    [
        "--ci",
        "--dry-run",
        "--json",
        "--no-color",
        "--no-tests",
        "--non-interactive",
        "--pretty",
        "--sample",
        "--use-aot",
        "--use-dynamic-plugins",
        "--yes",
        "--reloadable",
    ];

    private readonly Dictionary<string, string?> _options;

    private CliCommandLine(
        IReadOnlyList<string> positionals,
        Dictionary<string, string?> options,
        IReadOnlyList<CliDiagnostic> diagnostics)
    {
        Positionals = positionals;
        _options = options;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<string> Positionals { get; }

    public IReadOnlyList<CliDiagnostic> Diagnostics { get; }

    public bool HasOption(string option)
    {
        return _options.ContainsKey(option);
    }

    public string? GetOptionValue(string option)
    {
        return _options.GetValueOrDefault(option);
    }

    public static CliCommandLine Parse(string[] args)
    {
        var positionals = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);
        var diagnostics = new List<CliDiagnostic>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(arg);
                continue;
            }

            if (ValueOptions.Contains(arg))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[arg] = null;
                    diagnostics.Add(CliDiagnostic.Error(
                        "AUCCLI0005",
                        $"Option '{arg}' requires a value.",
                        arg,
                        i));
                    continue;
                }

                options[arg] = args[++i];
            }
            else if (FlagOptions.Contains(arg))
            {
                options[arg] = "true";
            }
            else
            {
                options[arg] = "true";
                diagnostics.Add(CliDiagnostic.Error(
                    "AUCCLI0004",
                    $"Unknown option '{arg}'.",
                    arg,
                    i));
            }
        }

        return new CliCommandLine(positionals, options, Array.AsReadOnly(diagnostics.ToArray()));
    }
}
