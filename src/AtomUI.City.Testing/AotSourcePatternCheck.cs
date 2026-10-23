namespace AtomUI.City.Testing;

/// <summary>
/// Performs configurable ordinal source-text pattern linting for common AOT risks.
/// </summary>
/// <remarks>
/// This heuristic lint does not run the linker or AOT analyzers and cannot prove
/// that an application is NativeAOT compatible.
/// </remarks>
public sealed class AotSourcePatternCheck
{
    private readonly List<ForbiddenAotPattern> _forbiddenPatterns = [];

    private AotSourcePatternCheck()
    {
    }

    /// <summary>
    /// Creates an empty source pattern check.
    /// </summary>
    public static AotSourcePatternCheck Create()
    {
        return new AotSourcePatternCheck();
    }

    /// <summary>
    /// Adds an ordinal source-text pattern rule.
    /// </summary>
    public AotSourcePatternCheck ForbidPattern(string diagnosticId, string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        if (!_forbiddenPatterns.Any(existing =>
                string.Equals(existing.DiagnosticId, diagnosticId, StringComparison.Ordinal) &&
                string.Equals(existing.Pattern, pattern, StringComparison.Ordinal)))
        {
            _forbiddenPatterns.Add(new ForbiddenAotPattern(diagnosticId, pattern));
        }

        return this;
    }

    /// <summary>
    /// Adds the built-in heuristic patterns for runtime type scans, unbounded
    /// activation, and dynamic method generation.
    /// </summary>
    public AotSourcePatternCheck ForbidDefaultAotRiskPatterns()
    {
        ForbidPattern("AOT001", "Assembly.GetTypes");
        ForbidPattern("AOT002", "Activator.CreateInstance");
        ForbidPattern("AOT003", "DynamicMethod");

        return this;
    }

    /// <summary>
    /// Evaluates explicitly supplied source text against the configured patterns.
    /// </summary>
    public IReadOnlyList<AotSourcePatternDiagnostic> Evaluate(
        IEnumerable<SourceFile> sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var diagnostics = new List<AotSourcePatternDiagnostic>();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var pattern in _forbiddenPatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (source.Text.Contains(pattern.Pattern, StringComparison.Ordinal))
                {
                    diagnostics.Add(new AotSourcePatternDiagnostic(
                        pattern.DiagnosticId,
                        source.Path,
                        $"Source '{source.Path}' contains AOT risk pattern '{pattern.Pattern}'."));
                }
            }
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private sealed record ForbiddenAotPattern(string DiagnosticId, string Pattern);
}
