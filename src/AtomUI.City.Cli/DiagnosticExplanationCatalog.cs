using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace AtomUI.City.Cli;

internal static partial class DiagnosticExplanationCatalog
{
    private static readonly IReadOnlyDictionary<string, DiagnosticExplanation> Entries = CreateEntries();

    public static bool TryGet(string code, out DiagnosticExplanation explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return Entries.TryGetValue(code.Trim(), out explanation!);
    }

    public static IReadOnlyCollection<DiagnosticExplanation> All =>
        Array.AsReadOnly(Entries.Values.ToArray());

    public static DiagnosticExplanation UnknownCodeFallback(string code) => new(
        "AUCCLI0802",
        "Error",
        "The diagnostic code is not present in the CLI explanation catalog.",
        $"'{code}' is not produced or explicitly forwarded by this CLI version.",
        ["Check the code for typing errors and use the CLI version that produced the original diagnostic."],
        ["docs/modules/cli/diagnostics.md"],
        ["atomui city doctor --json"],
        "CLI");

    private static IReadOnlyDictionary<string, DiagnosticExplanation> CreateEntries()
    {
        var entries = new List<DiagnosticExplanation>();
        AddCliEntries(entries);
        AddForwardedEntries(
            entries,
            "Templates",
            "docs/modules/templates/diagnostics.md",
            [
                "AUCTPL0001", "AUCTPL0002", "AUCTPL0301",
                "AUCTPL1001", "AUCTPL1002", "AUCTPL1003", "AUCTPL1004", "AUCTPL1005", "AUCTPL1006",
                "AUCTPL2001", "AUCTPL2002", "AUCTPL2003", "AUCTPL2004", "AUCTPL2005",
            ],
            "Templates rejected an input, target path, render operation, or rollback operation.",
            "Review the template diagnostic context, correct the generation input or target conflict, and retry.");
        AddForwardedEntries(
            entries,
            "PluginSystem",
            "docs/modules/plugins/diagnostics.md",
            Enumerable.Range(0, 24).Select(static value => $"AUCPLG{value:0000}"),
            "Plugin package or manifest validation reported an invalid or incompatible condition.",
            "Inspect the plugin manifest and package layout with 'atomui city plugin doctor <path> --json'.");

        var dictionary = new Dictionary<string, DiagnosticExplanation>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            Validate(entry);
            if (!dictionary.TryAdd(entry.Code, entry))
            {
                throw new InvalidOperationException($"Diagnostic explanation '{entry.Code}' is duplicated.");
            }
        }

        return new ReadOnlyDictionary<string, DiagnosticExplanation>(dictionary);
    }

    private static void AddCliEntries(List<DiagnosticExplanation> entries)
    {
        Add(entries, "AUCCLI0001", "Command prefix is invalid.", "The first business argument was not 'city'.", "Start the command with 'atomui city'.");
        Add(entries, "AUCCLI0002", "The command is unknown.", "The requested top-level command is not supported by this CLI version.", "Review command usage and choose a supported command.");
        Add(entries, "AUCCLI0003", "A command is required.", "Only the 'city' prefix was supplied.", "Supply a command such as doctor, build, generate, docs, or tests.");
        Add(entries, "AUCCLI0004", "An option is unknown.", "The option is not in the command-line grammar.", "Remove the option or use the spelling documented for this CLI version.");
        Add(entries, "AUCCLI0005", "An option value is required.", "A value-bearing option was not followed by a value.", "Supply the required value immediately after the option.");
        Add(entries, "AUCCLI0101", "Application name is required.", "The new app command did not receive an application name.", "Run 'atomui city new app <AppName>'.");
        Add(entries, "AUCCLI0102", "The root namespace is reserved.", "Application namespaces cannot claim the AtomUI.City framework namespace.", "Choose an application-owned root namespace.");
        Add(entries, "AUCCLI0103", "AOT and dynamic plugins conflict.", "The requested application options combine modes that are not supported together.", "Choose NativeAOT with static capabilities or disable the dynamic plugin option.");
        Add(entries, "AUCCLI0104", "Application name is invalid.", "The supplied name is not a valid project and namespace identifier.", "Use letters, digits, and underscores with a valid identifier start.");
        Add(entries, "AUCCLI0105", "The generation target already exists.", "Create-only application generation would overwrite an existing path.", "Choose an empty output path or remove the conflicting target intentionally.");
        Add(entries, "AUCCLI0106", "Application generation was cancelled.", "Cancellation was observed before the generation transaction completed.", "Retry when the caller can allow the operation to complete.");
        Add(entries, "AUCCLI0201", "A dotnet command failed.", "The child dotnet process returned a non-zero exit code.", "Inspect the captured stderr summary and rerun the equivalent dotnet command.");
        Add(entries, "AUCCLI0202", "A dotnet command was cancelled.", "The caller cancelled while the child process was running.", "Retry or increase the caller's operation deadline.");
        Add(entries, "AUCCLI0203", "The dotnet working directory is missing.", "The requested process root does not exist.", "Correct --working-directory or create the intended workspace first.");
        Add(entries, "AUCCLI0301", "Plugin package path is required.", "A plugin command that needs a package did not receive its path.", "Supply the plugin package root or manifest path.");
        Add(entries, "AUCCLI0302", "Plugin inspection path is required.", "Plugin inspect or doctor did not receive a package path.", "Run the command with a package root or atomui-city/plugin.json path.");
        Add(entries, "AUCCLI0401", "Explicit confirmation is required.", "A mutating command is running non-interactively without --yes.", "Review the operation and rerun with --yes.");
        Add(entries, "AUCCLI0501", "Generation arguments are required.", "The generate command is missing kind or name.", "Supply a supported generation kind and identifier.");
        Add(entries, "AUCCLI0502", "Generation kind is unsupported.", "The requested generator is outside the current non-plugin feature set.", "Use module, page, test, config, or localization.");
        Add(entries, "AUCCLI0503", "The target project could not be resolved.", "The workspace contains no unique compatible project for generation.", "Pass --project with the intended project path.");
        Add(entries, "AUCCLI0504", "Generation was cancelled.", "Cancellation was observed during template planning or rendering.", "Retry after confirming the destination is unchanged.");

        var governance = new (string Code, string Summary)[]
        {
            ("AUCCLI0601", "The framework workspace could not be recognized."),
            ("AUCCLI0602", "The documentation root is missing."),
            ("AUCCLI0603", "A required module document is missing."),
            ("AUCCLI0604", "A Markdown link is invalid, missing, or escapes the workspace."),
            ("AUCCLI0605", "A Feature identity or reference is invalid."),
            ("AUCCLI0606", "A canonical Feature is defined more than once."),
            ("AUCCLI0607", "A Feature has no testing matrix row."),
            ("AUCCLI0608", "A forbidden documentation placeholder was found."),
            ("AUCCLI0609", "The test root or a required test project is missing."),
            ("AUCCLI0610", "Declared test evidence could not be found."),
            ("AUCCLI0611", "Feature and testing evidence are inconsistent."),
            ("AUCCLI0612", "A testing matrix Test Type is invalid."),
            ("AUCCLI0613", "Specialized Generator, Analyzer, Build, or Template evidence is missing."),
            ("AUCCLI0614", "An explicit TestLayer value is invalid."),
            ("AUCCLI0615", "A governance input file could not be read."),
            ("AUCCLI0616", "The governance check was cancelled."),
        };
        foreach (var item in governance)
        {
            Add(entries, item.Code, item.Summary, "The AtomUI.City framework governance rule set rejected the workspace snapshot.", "Use the diagnostic target and position to correct the repository evidence, then rerun the same check.");
        }

        var plan = new (string Code, string Summary)[]
        {
            ("AUCCLI0701", "Plan generation arguments are required."),
            ("AUCCLI0702", "The requested plan operation is unsupported."),
            ("AUCCLI0703", "The plan workspace is invalid."),
            ("AUCCLI0704", "A plan path is invalid or escapes the workspace."),
            ("AUCCLI0705", "The plan file could not be written atomically."),
            ("AUCCLI0706", "The plan file could not be read."),
            ("AUCCLI0707", "The plan schema is invalid or unsupported."),
            ("AUCCLI0708", "The plan belongs to a different workspace."),
            ("AUCCLI0709", "Plan content failed its integrity check."),
            ("AUCCLI0710", "An apply precondition conflicts with the workspace."),
            ("AUCCLI0711", "Another apply transaction owns the workspace lock."),
            ("AUCCLI0712", "The apply commit failed."),
            ("AUCCLI0713", "The apply rollback failed."),
            ("AUCCLI0714", "Plan generation or apply was cancelled."),
            ("AUCCLI0715", "The isolated plan preview could not be cleaned up."),
        };
        foreach (var item in plan)
        {
            Add(entries, item.Code, item.Summary, "The create-only plan/apply transaction could not prove or restore a safe workspace state.", "Inspect the plan, transaction state, path, and hash context before retrying.");
        }

        Add(entries, "AUCCLI0801", "A diagnostic code is required.", "The explain command did not receive a code.", "Run 'atomui city explain <diagnostic-code> --json'.");
        Add(entries, "AUCCLI0802", "The diagnostic code is unknown.", "The code is not present in this CLI version's immutable catalog.", "Verify the code and use the CLI version that produced it.");
        Add(entries, "AUCCLI0803", "The diagnostic catalog is invalid.", "Catalog entries are duplicated or contain invalid required fields.", "Treat the CLI package as invalid and use a verified package version.");
    }

    private static void Add(
        List<DiagnosticExplanation> entries,
        string code,
        string summary,
        string cause,
        string action)
    {
        entries.Add(new DiagnosticExplanation(
            code,
            "Error",
            summary,
            cause,
            [action],
            ["docs/modules/cli/diagnostics.md"],
            [$"atomui city explain {code} --json"],
            "CLI"));
    }

    private static void AddForwardedEntries(
        List<DiagnosticExplanation> entries,
        string owner,
        string documentationLink,
        IEnumerable<string> codes,
        string cause,
        string action)
    {
        foreach (var code in codes)
        {
            entries.Add(new DiagnosticExplanation(
                code,
                "Error",
                $"{owner} reported diagnostic {code}.",
                cause,
                [action],
                [documentationLink],
                [$"atomui city explain {code} --json"],
                owner));
        }
    }

    private static void Validate(DiagnosticExplanation entry)
    {
        if (!DiagnosticCodeRegex().IsMatch(entry.Code) ||
            entry.Severity is not ("Error" or "Warning" or "Info") ||
            string.IsNullOrWhiteSpace(entry.Summary) ||
            string.IsNullOrWhiteSpace(entry.Cause) ||
            string.IsNullOrWhiteSpace(entry.OwnerModule) ||
            entry.SuggestedActions.Count == 0 ||
            entry.SuggestedActions.Any(string.IsNullOrWhiteSpace) ||
            entry.DocumentationLinks.Count == 0 ||
            entry.DocumentationLinks.Any(link => !IsTrustedLink(link)))
        {
            throw new InvalidOperationException($"Diagnostic explanation '{entry.Code}' is invalid.");
        }
    }

    private static bool IsTrustedLink(string link)
    {
        return link.StartsWith("docs/", StringComparison.Ordinal) ||
               Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    [GeneratedRegex("^(?:AUCCLI|AUCTPL|AUCPLG)[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticCodeRegex();
}

internal sealed record DiagnosticExplanation(
    string Code,
    string Severity,
    string Summary,
    string Cause,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<string> DocumentationLinks,
    IReadOnlyList<string> RelatedCommands,
    string OwnerModule);
