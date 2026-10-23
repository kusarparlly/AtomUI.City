using System.Text;
using System.Text.RegularExpressions;

namespace AtomUI.City.Cli;

internal static partial class FrameworkGovernanceGate
{
    internal const string RuleSetId = "atomui-city-framework";
    internal const string RuleSetVersion = "1.0";

    private static readonly string[] RequiredDocuments =
    [
        "overview.md",
        "architecture.md",
        "features.md",
        "api-contracts.md",
        "lifecycle.md",
        "threading.md",
        "diagnostics.md",
        "testing.md",
        "compatibility.md",
        "integration.md",
    ];

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "artifacts", "TestResults",
    };

    private static readonly HashSet<string> ValidTestTypes = new(StringComparer.Ordinal)
    {
        "Unit", "Contract", "FrameworkIntegration", "RuntimeLifecycle", "PluginLifecycle",
        "PlatformIntegration", "Generator", "Analyzer", "Build", "TemplateSmoke", "Dogfood",
        "NativeAOT", "RealProtocol",
    };

    private static readonly HashSet<string> ValidTestLayers = new(StringComparer.Ordinal)
    {
        "Unit", "Contract", "FrameworkIntegration", "RuntimeLifecycle", "PluginLifecycle",
        "PlatformIntegration", "TemplateSmoke", "Generator", "Analyzer", "Build",
    };

    private static readonly FrameworkModule[] Modules =
    [
        new("build", "BUILD", "docs/modules/build", ["tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj", "tests/AtomUI.City.Build.Tasks.Tests/AtomUI.City.Build.Tasks.Tests.csproj"]),
        new("cli", "CLI", "docs/modules/cli", ["tests/AtomUI.City.Cli.Tests/AtomUI.City.Cli.Tests.csproj"]),
        new("core", "CORE", "docs/modules/core", ["tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj"]),
        new("data", "DATA", "docs/modules/data", ["tests/AtomUI.City.Data.Tests/AtomUI.City.Data.Tests.csproj"]),
        new("eventbus", "EVENTBUS", "docs/modules/eventbus", ["tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj"]),
        new("generators", "GENERATORS", "docs/modules/generators", ["tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj"]),
        new("localization", "LOCALIZATION", "docs/modules/localization", ["tests/AtomUI.City.Localization.Tests/AtomUI.City.Localization.Tests.csproj"]),
        new("mvvm", "MVVM", "docs/modules/mvvm", ["tests/AtomUI.City.Mvvm.Tests/AtomUI.City.Mvvm.Tests.csproj"]),
        new("plugins", "PLUGIN", "docs/modules/plugins", ["tests/AtomUI.City.PluginSystem.Tests/AtomUI.City.PluginSystem.Tests.csproj"]),
        new("presentation", "PRESENTATION", "docs/modules/presentation", ["tests/AtomUI.City.Presentation.Tests/AtomUI.City.Presentation.Tests.csproj"]),
        new("routing", "ROUTING", "docs/modules/routing", ["tests/AtomUI.City.Routing.Tests/AtomUI.City.Routing.Tests.csproj"]),
        new("security", "SECURITY", "docs/modules/security", ["tests/AtomUI.City.Security.Tests/AtomUI.City.Security.Tests.csproj"]),
        new("state", "STATE", "docs/modules/state", ["tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj"]),
        new("templates", "TEMPLATES", "docs/modules/templates", ["tests/AtomUI.City.TemplateSmokeTests/AtomUI.City.TemplateSmokeTests.csproj"]),
        new("testing", "TESTING", "docs/modules/testing", ["tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj"]),
    ];

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static async ValueTask<GovernanceGateResult> EvaluateAsync(
        string gate,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gate);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var diagnostics = new List<CliDiagnostic>();
        var checkedFiles = new HashSet<string>(StringComparer.Ordinal);
        var root = FindWorkspaceRoot(workingDirectory);
        if (root is null)
        {
            diagnostics.Add(Error(
                "AUCCLI0601",
                "The working directory is not inside an AtomUI.City framework workspace.",
                "."));
            return CreateResult(gate, null, workingDirectory, checkedFiles.Count, diagnostics);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var catalog = await ReadCatalogAsync(root, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);
            if (gate.Equals("docs", StringComparison.Ordinal))
            {
                await EvaluateDocumentsAsync(root, catalog, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);
            }
            else if (gate.Equals("tests", StringComparison.Ordinal))
            {
                await EvaluateTestsAsync(root, catalog, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(gate), gate, "Governance gate must be docs or tests.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            diagnostics.Add(Error("AUCCLI0616", "The governance check was cancelled.", "."));
        }
        catch (Exception exception)
        {
            diagnostics.Add(Error(
                "AUCCLI0615",
                $"The governance check could not read the workspace: {exception.Message}",
                "."));
        }

        return CreateResult(gate, root, workingDirectory, checkedFiles.Count, diagnostics);
    }

    private static async ValueTask<FrameworkCatalog> ReadCatalogAsync(
        string root,
        HashSet<string> checkedFiles,
        List<CliDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var definitions = new Dictionary<string, FeatureDefinition>(StringComparer.Ordinal);
        var matrices = new Dictionary<string, TestMatrixRow>(StringComparer.Ordinal);

        foreach (var module in Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var featurePath = CombineRoot(root, $"{module.DocumentsDirectory}/features.md");
            var testingPath = CombineRoot(root, $"{module.DocumentsDirectory}/testing.md");
            var featureDocument = await TryReadAsync(root, featurePath, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);
            var testingDocument = await TryReadAsync(root, testingPath, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);

            if (featureDocument is not null)
            {
                foreach (var definition in ParseFeatureDefinitions(module, featureDocument, diagnostics))
                {
                    if (!definitions.TryAdd(definition.Id, definition))
                    {
                        diagnostics.Add(Error(
                            "AUCCLI0606",
                            $"Feature '{definition.Id}' has more than one canonical definition.",
                            definition.Path,
                            definition.Line,
                            module.Key,
                            definition.Id));
                    }
                }
            }

            if (testingDocument is not null)
            {
                foreach (var row in ParseTestMatrix(module, testingDocument, diagnostics))
                {
                    if (!matrices.TryAdd(row.FeatureId, row))
                    {
                        diagnostics.Add(Error(
                            "AUCCLI0611",
                            $"Feature '{row.FeatureId}' has more than one testing matrix row.",
                            row.Path,
                            row.Line,
                            module.Key,
                            row.FeatureId));
                    }
                }
            }
        }

        foreach (var definition in definitions.Values.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            if (!matrices.ContainsKey(definition.Id))
            {
                diagnostics.Add(Error(
                    "AUCCLI0607",
                    $"Feature '{definition.Id}' does not have a testing matrix row.",
                    definition.Path,
                    definition.Line,
                    definition.Module.Key,
                    definition.Id));
            }
        }

        foreach (var row in matrices.Values.OrderBy(static value => value.FeatureId, StringComparer.Ordinal))
        {
            if (!definitions.ContainsKey(row.FeatureId))
            {
                diagnostics.Add(Error(
                    "AUCCLI0611",
                    $"Testing matrix references unknown feature '{row.FeatureId}'.",
                    row.Path,
                    row.Line,
                    row.Module.Key,
                    row.FeatureId));
            }
        }

        return new FrameworkCatalog(definitions, matrices);
    }

    private static async ValueTask EvaluateDocumentsAsync(
        string root,
        FrameworkCatalog catalog,
        HashSet<string> checkedFiles,
        List<CliDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var docsRoot = CombineRoot(root, "docs");
        if (!DirectoryExistsExact(root, "docs"))
        {
            diagnostics.Add(Error("AUCCLI0602", "The documentation root is missing.", "docs"));
            return;
        }

        foreach (var module in Modules)
        {
            foreach (var documentName in RequiredDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = $"{module.DocumentsDirectory}/{documentName}";
                if (!FileExistsExact(root, relativePath))
                {
                    diagnostics.Add(Error(
                        "AUCCLI0603",
                        $"Required module document '{documentName}' is missing.",
                        relativePath,
                        module: module.Key));
                }
            }
        }

        foreach (var path in EnumerateFilesSafely(docsRoot, "*.md"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await TryReadAsync(root, path, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                continue;
            }

            EvaluateForbiddenTokens(document, diagnostics);
            EvaluateLinks(root, document, diagnostics);
            EvaluateFeatureReferences(document, catalog.Definitions, diagnostics);
        }
    }

    private static async ValueTask EvaluateTestsAsync(
        string root,
        FrameworkCatalog catalog,
        HashSet<string> checkedFiles,
        List<CliDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (!DirectoryExistsExact(root, "tests"))
        {
            diagnostics.Add(Error("AUCCLI0609", "The test root is missing.", "tests"));
            return;
        }

        foreach (var module in Modules)
        {
            foreach (var testProject in module.RequiredTestProjects)
            {
                if (!FileExistsExact(root, testProject))
                {
                    diagnostics.Add(Error(
                        "AUCCLI0609",
                        "A required test project is missing.",
                        testProject,
                        module: module.Key));
                }
            }
        }

        var typeLocations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var testsRoot = CombineRoot(root, "tests");
        foreach (var sourcePath in EnumerateFilesSafely(testsRoot, "*.cs"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await TryReadAsync(root, sourcePath, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                continue;
            }

            foreach (Match match in TopLevelTestTypeRegex().Matches(document.Content))
            {
                var typeName = match.Groups["name"].Value;
                if (!typeLocations.TryGetValue(typeName, out var locations))
                {
                    locations = [];
                    typeLocations.Add(typeName, locations);
                }

                locations.Add(document.RelativePath);
            }

            EvaluateTestLayers(document, diagnostics);
        }

        foreach (var projectPath in EnumerateFilesSafely(testsRoot, "*.csproj"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TryReadAsync(root, projectPath, checkedFiles, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        foreach (var definition in catalog.Definitions.Values.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            if (!catalog.Matrices.TryGetValue(definition.Id, out var matrix))
            {
                continue;
            }

            foreach (var testType in matrix.TestTypes)
            {
                if (!ValidTestTypes.Contains(testType))
                {
                    diagnostics.Add(Error(
                        "AUCCLI0612",
                        $"Test Type '{testType}' is not part of the framework rule set.",
                        matrix.Path,
                        matrix.Line,
                        matrix.Module.Key,
                        matrix.FeatureId));
                }
            }

            foreach (var expected in definition.EvidenceTypes)
            {
                if (!matrix.EvidenceTypes.Contains(expected, StringComparer.Ordinal))
                {
                    diagnostics.Add(Error(
                        "AUCCLI0611",
                        $"Feature evidence '{expected}' is missing from its testing matrix row.",
                        matrix.Path,
                        matrix.Line,
                        matrix.Module.Key,
                        matrix.FeatureId));
                }
            }

            if (matrix.EvidenceTypes.Count == 0)
            {
                diagnostics.Add(Error(
                    "AUCCLI0610",
                    "The testing matrix row does not name a test type ending in 'Tests'.",
                    matrix.Path,
                    matrix.Line,
                    matrix.Module.Key,
                    matrix.FeatureId));
            }

            foreach (var evidence in matrix.EvidenceTypes)
            {
                if (!typeLocations.TryGetValue(evidence, out var locations))
                {
                    diagnostics.Add(Error(
                        "AUCCLI0610",
                        $"Declared test type '{evidence}' was not found in test source.",
                        matrix.Path,
                        matrix.Line,
                        matrix.Module.Key,
                        matrix.FeatureId));
                    continue;
                }

            }

            EvaluateSpecializedEvidence(matrix, typeLocations, diagnostics);
        }
    }

    private static void EvaluateSpecializedEvidence(
        TestMatrixRow row,
        IReadOnlyDictionary<string, List<string>> typeLocations,
        List<CliDiagnostic> diagnostics)
    {
        foreach (var testType in row.TestTypes)
        {
            string? requiredPrefix = testType switch
            {
                "Generator" or "Analyzer" => "tests/AtomUI.City.Generators.Tests/",
                "Build" => "tests/AtomUI.City.Build.",
                "TemplateSmoke" => "tests/AtomUI.City.TemplateSmokeTests/",
                _ => null,
            };

            if (requiredPrefix is not null &&
                !row.EvidenceTypes.Any(evidence =>
                    typeLocations.TryGetValue(evidence, out var locations) &&
                    locations.Any(path => path.StartsWith(requiredPrefix, StringComparison.Ordinal))))
            {
                diagnostics.Add(Error(
                    "AUCCLI0613",
                    $"The row has no test evidence in the required {testType} test project.",
                    row.Path,
                    row.Line,
                    row.Module.Key,
                    row.FeatureId));
            }
        }
    }

    private static IEnumerable<FeatureDefinition> ParseFeatureDefinitions(
        FrameworkModule module,
        TextDocument document,
        List<CliDiagnostic> diagnostics)
    {
        var table = FindTable(document, "## Feature 索引");
        if (table is null || table.Headers.Count == 0 || table.Headers[0] != "Feature ID")
        {
            diagnostics.Add(Error(
                "AUCCLI0605",
                "features.md does not contain the canonical Feature index table.",
                document.RelativePath,
                module: module.Key));
            return [];
        }

        var statusIndex = FindHeader(table.Headers, "状态", "Status");
        var evidenceIndex = FindHeader(table.Headers, "主测试", "主测试族", "主要证据", "Primary evidence");
        if (statusIndex < 0 || evidenceIndex < 0)
        {
            diagnostics.Add(Error(
                "AUCCLI0605",
                "Feature index must contain a status and primary evidence column.",
                document.RelativePath,
                table.HeaderLine,
                module.Key));
            return [];
        }

        var definitions = new List<FeatureDefinition>();
        var expectedPattern = $"AUC-{module.FeaturePrefix}-";
        foreach (var row in table.Rows)
        {
            var id = row.Cells[0];
            if (!FeatureIdRegex().IsMatch(id) || !id.StartsWith(expectedPattern, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "AUCCLI0605",
                    $"Feature ID '{id}' is invalid for module '{module.Key}'.",
                    document.RelativePath,
                    row.Line,
                    module.Key,
                    id));
                continue;
            }

            definitions.Add(new FeatureDefinition(
                id,
                module,
                document.RelativePath,
                row.Line,
                ExtractTestTypeNames(row.Cells[evidenceIndex])));
        }

        return definitions;
    }

    private static IEnumerable<TestMatrixRow> ParseTestMatrix(
        FrameworkModule module,
        TextDocument document,
        List<CliDiagnostic> diagnostics)
    {
        var table = FindTable(document, "## 测试矩阵");
        string[] expectedHeaders =
        [
            "Feature ID", "Test Type", "Test File", "Required Assertions", "Failure Paths", "Status",
        ];
        if (table is null || !table.Headers.SequenceEqual(expectedHeaders, StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "AUCCLI0611",
                "testing.md does not contain the canonical testing matrix table.",
                document.RelativePath,
                module: module.Key));
            return [];
        }

        var rows = new List<TestMatrixRow>();
        foreach (var row in table.Rows)
        {
            var tokens = row.Cells[1].Split('/', StringSplitOptions.None).Select(static token => token.Trim()).ToArray();
            if (tokens.Any(string.IsNullOrEmpty) || tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Length)
            {
                diagnostics.Add(Error(
                    "AUCCLI0612",
                    $"Test Type '{row.Cells[1]}' contains an empty or duplicate token.",
                    document.RelativePath,
                    row.Line,
                    module.Key,
                    row.Cells[0]));
            }

            rows.Add(new TestMatrixRow(
                row.Cells[0],
                module,
                document.RelativePath,
                row.Line,
                tokens,
                ExtractTestTypeNames(row.Cells[2])));
        }

        return rows;
    }

    private static MarkdownTable? FindTable(TextDocument document, string sectionHeading)
    {
        var lines = document.Lines;
        var section = Array.FindIndex(lines, line => line.Equals(sectionHeading, StringComparison.Ordinal));
        if (section < 0)
        {
            return null;
        }

        for (var index = section + 1; index + 1 < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith('|') || !IsSeparatorRow(lines[index + 1]))
            {
                continue;
            }

            var headers = SplitMarkdownRow(lines[index]);
            var rows = new List<MarkdownRow>();
            for (var rowIndex = index + 2; rowIndex < lines.Length; rowIndex++)
            {
                if (!lines[rowIndex].TrimStart().StartsWith('|'))
                {
                    break;
                }

                var cells = SplitMarkdownRow(lines[rowIndex]);
                if (cells.Count == headers.Count)
                {
                    rows.Add(new MarkdownRow(cells, rowIndex + 1));
                }
            }

            return new MarkdownTable(headers, index + 1, rows);
        }

        return null;
    }

    private static IReadOnlyList<string> SplitMarkdownRow(string line)
    {
        var cells = new List<string>();
        var builder = new StringBuilder();
        var escaped = false;
        var trimmed = line.Trim();
        var start = trimmed.StartsWith('|') ? 1 : 0;
        var end = trimmed.EndsWith('|') ? trimmed.Length - 1 : trimmed.Length;

        for (var index = start; index < end; index++)
        {
            var value = trimmed[index];
            if (escaped)
            {
                builder.Append(value);
                escaped = false;
            }
            else if (value == '\\')
            {
                escaped = true;
                builder.Append(value);
            }
            else if (value == '|')
            {
                cells.Add(builder.ToString().Trim());
                builder.Clear();
            }
            else
            {
                builder.Append(value);
            }
        }

        cells.Add(builder.ToString().Trim());
        return cells;
    }

    private static bool IsSeparatorRow(string line)
    {
        var cells = SplitMarkdownRow(line);
        return cells.Count > 0 && cells.All(static cell => Regex.IsMatch(cell, "^:?-{3,}:?$"));
    }

    private static int FindHeader(IReadOnlyList<string> headers, params string[] accepted)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (accepted.Contains(headers[index], StringComparer.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> ExtractTestTypeNames(string value)
    {
        return TestTypeNameRegex()
            .Matches(value)
            .Cast<Match>()
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void EvaluateForbiddenTokens(TextDocument document, List<CliDiagnostic> diagnostics)
    {
        foreach (Match match in ForbiddenTokenRegex().Matches(document.Content))
        {
            diagnostics.Add(Error(
                "AUCCLI0608",
                $"Forbidden documentation placeholder '{match.Value}' was found.",
                document.RelativePath,
                GetLineNumber(document.Content, match.Index)));
        }
    }

    private static void EvaluateLinks(string root, TextDocument document, List<CliDiagnostic> diagnostics)
    {
        foreach (Match match in MarkdownLinkRegex().Matches(document.Content))
        {
            var destination = match.Groups["angle"].Success
                ? match.Groups["angle"].Value
                : match.Groups["plain"].Value;
            EvaluateLink(root, document, destination, match.Index, diagnostics);
        }

        foreach (Match match in ReferenceLinkRegex().Matches(document.Content))
        {
            EvaluateLink(root, document, match.Groups["target"].Value, match.Index, diagnostics);
        }
    }

    private static void EvaluateLink(
        string root,
        TextDocument document,
        string destination,
        int position,
        List<CliDiagnostic> diagnostics)
    {
        var normalized = destination.Trim();
        if (normalized.Length == 0 ||
            normalized.StartsWith('#') ||
            Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri) &&
            absoluteUri.Scheme is "http" or "https" or "mailto")
        {
            return;
        }

        var queryOrFragment = normalized.IndexOfAny(['?', '#']);
        if (queryOrFragment >= 0)
        {
            normalized = normalized[..queryOrFragment];
        }

        try
        {
            normalized = Uri.UnescapeDataString(normalized);
        }
        catch (UriFormatException)
        {
            AddInvalidLink();
            return;
        }

        if (Path.IsPathRooted(normalized))
        {
            AddInvalidLink();
            return;
        }

        var sourceDirectory = Path.GetDirectoryName(CombineRoot(root, document.RelativePath))!;
        var target = Path.GetFullPath(normalized.Replace('/', Path.DirectorySeparatorChar), sourceDirectory);
        if (!IsWithinRoot(root, target))
        {
            AddInvalidLink();
            return;
        }

        var relativeTarget = NormalizeRelativePath(root, target);
        if (!PathExistsExact(root, relativeTarget))
        {
            AddInvalidLink();
        }

        return;

        void AddInvalidLink()
        {
            diagnostics.Add(Error(
                "AUCCLI0604",
                $"Markdown link '{destination}' is missing, invalid, or escapes the workspace.",
                document.RelativePath,
                GetLineNumber(document.Content, position)));
        }
    }

    private static void EvaluateFeatureReferences(
        TextDocument document,
        IReadOnlyDictionary<string, FeatureDefinition> definitions,
        List<CliDiagnostic> diagnostics)
    {
        foreach (Match match in FeatureReferenceRegex().Matches(document.Content))
        {
            var featureId = match.Value;
            if (!Modules.Any(module =>
                    featureId.StartsWith($"AUC-{module.FeaturePrefix}-", StringComparison.Ordinal)))
            {
                continue;
            }

            if (!definitions.ContainsKey(featureId))
            {
                diagnostics.Add(Error(
                    "AUCCLI0605",
                    $"Feature reference '{featureId}' has no canonical definition.",
                    document.RelativePath,
                    GetLineNumber(document.Content, match.Index),
                    featureId: featureId));
            }
        }
    }

    private static void EvaluateTestLayers(TextDocument document, List<CliDiagnostic> diagnostics)
    {
        foreach (Match match in TestLayerRegex().Matches(document.Content))
        {
            var value = match.Groups["enum"].Success
                ? match.Groups["enum"].Value
                : match.Groups["string"].Value;
            if (!ValidTestLayers.Contains(value))
            {
                diagnostics.Add(Error(
                    "AUCCLI0614",
                    $"Test layer '{value}' is not part of the framework rule set.",
                    document.RelativePath,
                    GetLineNumber(document.Content, match.Index)));
            }
        }
    }

    private static async ValueTask<TextDocument?> TryReadAsync(
        string root,
        string path,
        HashSet<string> checkedFiles,
        List<CliDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = NormalizeRelativePath(root, path);
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var content = StrictUtf8.GetString(bytes);
            checkedFiles.Add(relativePath);
            return new TextDocument(relativePath, MaskFencedCode(content));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(Error(
                "AUCCLI0615",
                $"File could not be read as UTF-8: {exception.Message}",
                relativePath));
            return null;
        }
    }

    private static string MaskFencedCode(string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var insideFence = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideFence = !insideFence;
                lines[index] = string.Empty;
            }
            else if (insideFence)
            {
                lines[index] = string.Empty;
            }
        }

        return string.Join('\n', lines);
    }

    private static GovernanceGateResult CreateResult(
        string gate,
        string? root,
        string workingDirectory,
        int checkedFiles,
        IEnumerable<CliDiagnostic> diagnostics)
    {
        var ordered = diagnostics
            .OrderBy(static diagnostic => diagnostic.Target ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Position ?? 0)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToArray();
        var relativeWorkingDirectory = root is null
            ? "."
            : NormalizeRelativePath(root, Path.GetFullPath(workingDirectory));

        return new GovernanceGateResult(
            gate,
            root,
            relativeWorkingDirectory,
            checkedFiles,
            ordered);
    }

    private static string? FindWorkspaceRoot(string workingDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(workingDirectory));
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }

            if (HasExactChild(current.FullName, "AtomUICity.slnx", directory: false) &&
                HasExactChild(current.FullName, "global.json", directory: false) &&
                HasExactChild(current.FullName, "Directory.Build.props", directory: false) &&
                HasExactChild(current.FullName, "src", directory: true))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool FileExistsExact(string root, string relativePath) =>
        PathExistsExact(root, relativePath, expectedDirectory: false);

    private static bool DirectoryExistsExact(string root, string relativePath) =>
        PathExistsExact(root, relativePath, expectedDirectory: true);

    private static bool PathExistsExact(string root, string relativePath, bool? expectedDirectory = null)
    {
        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (var index = 0; index < segments.Length; index++)
        {
            var isIntermediate = index < segments.Length - 1;
            var expectDirectory = isIntermediate ? true : expectedDirectory;
            var exists = expectDirectory is null
                ? HasExactChild(current, segments[index], directory: true) ||
                  HasExactChild(current, segments[index], directory: false)
                : HasExactChild(current, segments[index], expectDirectory.Value);
            if (!exists)
            {
                return false;
            }

            current = Path.Combine(current, segments[index]);
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }

        if (expectedDirectory is null)
        {
            return File.Exists(current) || Directory.Exists(current);
        }

        return expectedDirectory.Value ? Directory.Exists(current) : File.Exists(current);
    }

    private static bool HasExactChild(string parent, string name, bool directory)
    {
        try
        {
            return directory
                ? Directory.EnumerateDirectories(parent).Any(path => Path.GetFileName(path).Equals(name, StringComparison.Ordinal))
                : Directory.EnumerateFiles(parent).Any(path => Path.GetFileName(path).Equals(name, StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, string pattern)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory, pattern).OrderBy(static path => path, StringComparer.Ordinal))
            {
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory).OrderByDescending(static path => path, StringComparer.Ordinal))
            {
                var info = new DirectoryInfo(child);
                if (ExcludedDirectories.Contains(info.Name) ||
                    (info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                pending.Push(child);
            }
        }
    }

    private static bool IsWithinRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string CombineRoot(string root, string relativePath) =>
        Path.GetFullPath(relativePath.Replace('/', Path.DirectorySeparatorChar), root);

    private static string NormalizeRelativePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative.Length == 0 ? "." : relative;
    }

    private static int GetLineNumber(string content, int position)
    {
        var line = 1;
        for (var index = 0; index < position && index < content.Length; index++)
        {
            if (content[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static CliDiagnostic Error(
        string code,
        string message,
        string? target = null,
        int? position = null,
        string? module = null,
        string? featureId = null) =>
        CliDiagnostic.Error(code, message, target, position, module, featureId);

    [GeneratedRegex(@"^AUC-[A-Z][A-Z0-9]*-[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex FeatureIdRegex();

    [GeneratedRegex(@"AUC-[A-Z][A-Z0-9]*-[0-9]{3}", RegexOptions.CultureInvariant)]
    private static partial Regex FeatureReferenceRegex();

    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*Tests\b", RegexOptions.CultureInvariant)]
    private static partial Regex TestTypeNameRegex();

    [GeneratedRegex(@"(?m)^(?:(?:public|internal)\s+)?(?:(?:sealed|abstract|partial|static|readonly)\s+)*(?:class|record(?:\s+(?:class|struct))?|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TopLevelTestTypeRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9_])(?:TODO|TBD|FIXME)(?![A-Za-z0-9_])|待定|：text|:text", RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenTokenRegex();

    [GeneratedRegex(@"!?\[[^\]]*\]\(\s*(?:<(?<angle>[^>]+)>|(?<plain>[^)\s]+))", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"(?m)^\s*\[[^\]]+\]:\s*(?<target>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceLinkRegex();

    [GeneratedRegex("""(?m)^\s*\[TestLayer\(\s*(?:TestLayer\.(?<enum>[A-Za-z_][A-Za-z0-9_]*)|TestLayerNames\.(?<enum>[A-Za-z_][A-Za-z0-9_]*)|"(?<string>[^"]+)")""", RegexOptions.CultureInvariant)]
    private static partial Regex TestLayerRegex();

    private sealed record FrameworkModule(
        string Key,
        string FeaturePrefix,
        string DocumentsDirectory,
        IReadOnlyList<string> RequiredTestProjects);

    private sealed record FeatureDefinition(
        string Id,
        FrameworkModule Module,
        string Path,
        int Line,
        IReadOnlyList<string> EvidenceTypes);

    private sealed record TestMatrixRow(
        string FeatureId,
        FrameworkModule Module,
        string Path,
        int Line,
        IReadOnlyList<string> TestTypes,
        IReadOnlyList<string> EvidenceTypes);

    private sealed record FrameworkCatalog(
        IReadOnlyDictionary<string, FeatureDefinition> Definitions,
        IReadOnlyDictionary<string, TestMatrixRow> Matrices);

    private sealed record MarkdownTable(
        IReadOnlyList<string> Headers,
        int HeaderLine,
        IReadOnlyList<MarkdownRow> Rows);

    private sealed record MarkdownRow(IReadOnlyList<string> Cells, int Line);

    private sealed record TextDocument(string RelativePath, string Content)
    {
        public string[] Lines { get; } = Content.Split('\n');
    }
}

internal sealed record GovernanceGateResult(
    string Gate,
    string? Root,
    string WorkingDirectory,
    int CheckedFiles,
    IReadOnlyList<CliDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.All(static diagnostic =>
        !diagnostic.Severity.Equals("Error", StringComparison.Ordinal));

    public int ErrorCount => Diagnostics.Count(static diagnostic =>
        diagnostic.Severity.Equals("Error", StringComparison.Ordinal));

    public int WarningCount => Diagnostics.Count(static diagnostic =>
        diagnostic.Severity.Equals("Warning", StringComparison.Ordinal));
}
