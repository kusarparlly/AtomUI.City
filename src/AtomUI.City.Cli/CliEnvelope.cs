namespace AtomUI.City.Cli;

/// <summary>
/// Represents cli envelope.
/// </summary>
public sealed class CliEnvelope
{
    private CliEnvelope(
        string command,
        bool success,
        int exitCode,
        IReadOnlyList<CliDiagnostic> diagnostics,
        object? data)
    {
        Command = command;
        Success = success;
        ExitCode = exitCode;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Data = NormalizeData(data);
        Status = success ? "succeeded" : "failed";
        Artifacts = ExtractObjectList(Data, "artifacts");
        ChangedFiles = ExtractChangedFiles(Data, Artifacts);
        SuggestedCommands = CreateSuggestedCommands(Diagnostics);
        SuggestedActions = SuggestedCommands;
        DocumentationLinks = CreateDocumentationLinks(Diagnostics);
        Retryable = !success && exitCode == CliExitCodes.Failure;
    }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public string SchemaVersion { get; } = "1.0";

    /// <summary>
    /// Gets command.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets status.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets success.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets exit code.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public IReadOnlyList<CliDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets data.
    /// </summary>
    public object Data { get; }

    /// <summary>
    /// Gets artifacts.
    /// </summary>
    public IReadOnlyList<object?> Artifacts { get; }

    /// <summary>
    /// Gets suggested commands.
    /// </summary>
    public IReadOnlyList<string> SuggestedCommands { get; }

    /// <summary>
    /// Gets changed files.
    /// </summary>
    public IReadOnlyList<string> ChangedFiles { get; }

    /// <summary>
    /// Gets retryable.
    /// </summary>
    public bool Retryable { get; }

    /// <summary>
    /// Gets suggested actions.
    /// </summary>
    public IReadOnlyList<string> SuggestedActions { get; }

    /// <summary>
    /// Gets documentation links.
    /// </summary>
    public IReadOnlyList<string> DocumentationLinks { get; }

    /// <summary>
    /// Executes the succeeded operation.
    /// </summary>
    public static CliEnvelope Succeeded(string command, object? data)
    {
        return new CliEnvelope(command, success: true, CliExitCodes.Success, [], data);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static CliEnvelope Failed(
        string command,
        int exitCode,
        params CliDiagnostic[] diagnostics)
    {
        return new CliEnvelope(command, success: false, exitCode, diagnostics, data: new Dictionary<string, object?>());
    }

    /// <summary>
    /// Executes the failed with data operation.
    /// </summary>
    public static CliEnvelope FailedWithData(
        string command,
        int exitCode,
        object? data,
        params CliDiagnostic[] diagnostics)
    {
        return new CliEnvelope(command, success: false, exitCode, diagnostics, data);
    }

    private static object NormalizeData(object? data)
    {
        if (data is null)
        {
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        return NormalizeValue(data) ?? new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal));
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null or string)
        {
            return value;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                dictionary.ToDictionary(
                    pair => pair.Key,
                    pair => NormalizeValue(pair.Value),
                    StringComparer.Ordinal));
        }

        if (value is System.Collections.IDictionary nonGenericDictionary)
        {
            var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (System.Collections.DictionaryEntry entry in nonGenericDictionary)
            {
                if (entry.Key is not string key)
                {
                    return value;
                }

                normalized[key] = NormalizeValue(entry.Value);
            }

            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(normalized);
        }

        if (value is IReadOnlyList<object?> list)
        {
            return Array.AsReadOnly(list.Select(NormalizeValue).ToArray());
        }

        if (value is System.Collections.IList nonGenericList)
        {
            var normalized = new object?[nonGenericList.Count];
            for (var i = 0; i < nonGenericList.Count; i++)
            {
                normalized[i] = NormalizeValue(nonGenericList[i]);
            }

            return Array.AsReadOnly(normalized);
        }

        return value;
    }

    private static IReadOnlyList<object?> ExtractObjectList(object data, string key)
    {
        if (!TryGetDataValue(data, key, out var value) ||
            value is null or string ||
            value is not System.Collections.IEnumerable enumerable)
        {
            return [];
        }

        return Array.AsReadOnly(enumerable.Cast<object?>().ToArray());
    }

    private static IReadOnlyList<string> ExtractChangedFiles(
        object data,
        IReadOnlyList<object?> artifacts)
    {
        var explicitChangedFiles = ExtractStringList(data, "changedFiles");
        if (explicitChangedFiles.Count > 0)
        {
            return explicitChangedFiles;
        }

        return Array.AsReadOnly(
            artifacts
                .Select(TryGetArtifactPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private static IReadOnlyList<string> ExtractStringList(object data, string key)
    {
        if (!TryGetDataValue(data, key, out var value) ||
            value is null or string ||
            value is not System.Collections.IEnumerable enumerable)
        {
            return [];
        }

        return Array.AsReadOnly(
            enumerable
                .Cast<object?>()
                .OfType<string>()
                .ToArray());
    }

    private static bool TryGetDataValue(object data, string key, out object? value)
    {
        if (data is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary.TryGetValue(key, out value);
        }

        if (data is System.Collections.IDictionary nonGenericDictionary &&
            nonGenericDictionary.Contains(key))
        {
            value = nonGenericDictionary[key];
            return true;
        }

        value = null;
        return false;
    }

    private static string? TryGetArtifactPath(object? artifact)
    {
        if (artifact is IReadOnlyDictionary<string, object?> dictionary &&
            dictionary.TryGetValue("path", out var dictionaryPath))
        {
            return dictionaryPath?.ToString();
        }

        if (artifact is System.Collections.IDictionary nonGenericDictionary &&
            nonGenericDictionary.Contains("path"))
        {
            return nonGenericDictionary["path"]?.ToString();
        }

        var pathProperty = artifact?.GetType().GetProperty("path") ??
            artifact?.GetType().GetProperty("Path");
        return pathProperty?.GetValue(artifact)?.ToString();
    }

    private static IReadOnlyList<string> CreateSuggestedCommands(IReadOnlyList<CliDiagnostic> diagnostics)
    {
        return Array.AsReadOnly(
            diagnostics
                .Select(diagnostic => diagnostic.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .Select(code => $"atomui city explain {code} --json")
                .ToArray());
    }

    private static IReadOnlyList<string> CreateDocumentationLinks(IReadOnlyList<CliDiagnostic> diagnostics)
    {
        return Array.AsReadOnly(
            diagnostics
                .Select(diagnostic => diagnostic.DocumentationLink)
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Select(link => link!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }
}
