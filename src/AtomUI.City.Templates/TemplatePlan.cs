using System.Collections.ObjectModel;

namespace AtomUI.City.Templates;

/// <summary>
/// Represents template plan.
/// </summary>
public sealed class TemplatePlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatePlan"/> type.
    /// </summary>
    public TemplatePlan(
        string operationId,
        string command,
        IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyList<TemplateChange> changes)
        : this(operationId, command, inputs, changes, [], [], [], [], [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatePlan"/> type.
    /// </summary>
    public TemplatePlan(
        string operationId,
        string command,
        IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyList<TemplateChange> changes,
        IReadOnlyList<string> buildTargets,
        IReadOnlyList<string> testTargets,
        IReadOnlyList<string> docsRequired,
        IReadOnlyList<string> risks,
        IReadOnlyList<string> rollback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(buildTargets);
        ArgumentNullException.ThrowIfNull(testTargets);
        ArgumentNullException.ThrowIfNull(docsRequired);
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(rollback);
        if (changes.Any(static change => change is null))
        {
            throw new ArgumentException("Template changes cannot contain null entries.", nameof(changes));
        }

        OperationId = operationId;
        Command = command;
        Inputs = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(inputs, StringComparer.Ordinal));
        Changes = Array.AsReadOnly(changes.ToArray());
        BuildTargets = Array.AsReadOnly(buildTargets.ToArray());
        TestTargets = Array.AsReadOnly(testTargets.ToArray());
        DocsRequired = Array.AsReadOnly(docsRequired.ToArray());
        Risks = Array.AsReadOnly(risks.ToArray());
        Rollback = Array.AsReadOnly(rollback.ToArray());
    }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public string SchemaVersion { get; } = "1.0";

    /// <summary>
    /// Gets operation id.
    /// </summary>
    public string OperationId { get; }

    /// <summary>
    /// Gets command.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets inputs.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Inputs { get; }

    /// <summary>
    /// Gets changes.
    /// </summary>
    public IReadOnlyList<TemplateChange> Changes { get; }

    /// <summary>
    /// Gets build targets.
    /// </summary>
    public IReadOnlyList<string> BuildTargets { get; }

    /// <summary>
    /// Gets test targets.
    /// </summary>
    public IReadOnlyList<string> TestTargets { get; }

    /// <summary>
    /// Gets docs required.
    /// </summary>
    public IReadOnlyList<string> DocsRequired { get; }

    /// <summary>
    /// Gets risks.
    /// </summary>
    public IReadOnlyList<string> Risks { get; }

    /// <summary>
    /// Gets rollback.
    /// </summary>
    public IReadOnlyList<string> Rollback { get; }

    /// <summary>
    /// Executes the validate operation.
    /// </summary>
    public IReadOnlyList<TemplateDiagnostic> Validate()
    {
        var diagnostics = new List<TemplateDiagnostic>();
        var normalizedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in Changes)
        {
            if (!string.Equals(change.Type, "create", StringComparison.Ordinal))
            {
                diagnostics.Add(new TemplateDiagnostic(
                    "AUCTPL1003",
                    "Template change type is not supported.",
                    new Dictionary<string, object?>
                    {
                        ["type"] = change.Type,
                        ["path"] = change.Path,
                    }));
            }

            if (!TemplateChange.TryNormalizePath(change.Path, out var normalizedPath, out var error))
            {
                diagnostics.Add(new TemplateDiagnostic(
                    "AUCTPL1001",
                    error,
                    new Dictionary<string, object?>
                    {
                        ["path"] = change.Path,
                    }));
                continue;
            }

            if (normalizedPaths.TryGetValue(normalizedPath, out var firstPath))
            {
                diagnostics.Add(new TemplateDiagnostic(
                    "AUCTPL1002",
                    "Template plan contains a duplicate output path.",
                    new Dictionary<string, object?>
                    {
                        ["path"] = change.Path,
                        ["firstPath"] = firstPath,
                        ["normalizedPath"] = normalizedPath,
                    }));
                continue;
            }

            normalizedPaths.Add(normalizedPath, change.Path);
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }
}
