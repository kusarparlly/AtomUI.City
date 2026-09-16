using System.Collections.ObjectModel;

namespace AtomUI.City.Templates;

/// <summary>
/// Represents template render result.
/// </summary>
public sealed class TemplateRenderResult
{
    private TemplateRenderResult(
        TemplatePlan? plan,
        IReadOnlyList<TemplateDiagnostic> diagnostics,
        IReadOnlyList<string> appliedPaths)
    {
        Plan = plan;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        AppliedPaths = Array.AsReadOnly(appliedPaths.ToArray());
    }

    /// <summary>
    /// Gets plan.
    /// </summary>
    public TemplatePlan? Plan { get; }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public IReadOnlyList<TemplateDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets applied paths.
    /// </summary>
    public IReadOnlyList<string> AppliedPaths { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Plan is not null && Diagnostics.Count == 0;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static TemplateRenderResult Success(TemplatePlan plan, IReadOnlyList<string>? appliedPaths = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Validate().Count > 0)
        {
            throw new ArgumentException("A successful template result requires a valid plan.", nameof(plan));
        }

        var plannedPaths = plan.Changes.Select(static change => change.Path).ToArray();
        var resolvedAppliedPaths = appliedPaths?.ToArray() ?? plannedPaths;
        if (!plannedPaths.SequenceEqual(resolvedAppliedPaths, StringComparer.Ordinal))
        {
            throw new ArgumentException("Applied paths must exactly match the template plan.", nameof(appliedPaths));
        }

        return new TemplateRenderResult(
            plan,
            [],
            resolvedAppliedPaths);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static TemplateRenderResult Failed(params TemplateDiagnostic[] diagnostics)
    {
        return Failed(null, diagnostics);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static TemplateRenderResult Failed(TemplatePlan? plan, params TemplateDiagnostic[] diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Length == 0)
        {
            throw new ArgumentException("A failed template result must contain at least one diagnostic.", nameof(diagnostics));
        }

        if (diagnostics.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Template diagnostics cannot contain null entries.", nameof(diagnostics));
        }

        return new TemplateRenderResult(plan, diagnostics, []);
    }
}

/// <summary>
/// Represents template diagnostic.
/// </summary>
public sealed record TemplateDiagnostic
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateDiagnostic"/> type.
    /// </summary>
    public TemplateDiagnostic(string code, string message)
        : this(code, message, new Dictionary<string, object?>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateDiagnostic"/> type.
    /// </summary>
    public TemplateDiagnostic(
        string code,
        string message,
        IReadOnlyDictionary<string, object?> context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(context);

        Code = code;
        Message = message;
        Context = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(context, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets context.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Context { get; }
}
