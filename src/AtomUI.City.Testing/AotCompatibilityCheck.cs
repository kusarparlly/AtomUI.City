namespace AtomUI.City.Testing;

/// <summary>
/// Represents aot compatibility check.
/// </summary>
public sealed class AotCompatibilityCheck
{
    private readonly List<ForbiddenAotPattern> _forbiddenPatterns = [];

    private AotCompatibilityCheck()
    {
    }

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static AotCompatibilityCheck Create()
    {
        return new AotCompatibilityCheck();
    }

    /// <summary>
    /// Executes the forbid pattern operation.
    /// </summary>
    public AotCompatibilityCheck ForbidPattern(string diagnosticId, string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        if (!_forbiddenPatterns.Any(existing =>
                string.Equals(existing.DiagnosticId, diagnosticId, StringComparison.Ordinal)
                && string.Equals(existing.Pattern, pattern, StringComparison.Ordinal)))
        {
            _forbiddenPatterns.Add(new ForbiddenAotPattern(diagnosticId, pattern));
        }

        return this;
    }

    /// <summary>
    /// Executes the forbid default aot patterns operation.
    /// </summary>
    public AotCompatibilityCheck ForbidDefaultAotPatterns()
    {
        ForbidPattern("AOT001", "Assembly.GetTypes");
        ForbidPattern("AOT002", "Activator.CreateInstance");
        ForbidPattern("AOT003", "DynamicMethod");

        return this;
    }

    /// <summary>
    /// Executes the evaluate operation.
    /// </summary>
    public IReadOnlyList<AotCompatibilityDiagnostic> Evaluate(
        IEnumerable<SourceFile> sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var diagnostics = new List<AotCompatibilityDiagnostic>();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var pattern in _forbiddenPatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (source.Text.Contains(pattern.Pattern, StringComparison.Ordinal))
                {
                    diagnostics.Add(new AotCompatibilityDiagnostic(
                        pattern.DiagnosticId,
                        source.Path,
                        $"Source '{source.Path}' uses forbidden AOT pattern '{pattern.Pattern}'."));
                }
            }
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private sealed record ForbiddenAotPattern(string DiagnosticId, string Pattern);
}
