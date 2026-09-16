using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents source generation test result.
/// </summary>
public sealed class SourceGenerationTestResult
{
    internal SourceGenerationTestResult(
        GeneratedSourceSnapshot snapshot,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<Diagnostic> compilationDiagnostics)
    {
        Snapshot = snapshot;
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics.ToArray());
        CompilationDiagnostics = new ReadOnlyCollection<Diagnostic>(compilationDiagnostics.ToArray());
    }

    /// <summary>
    /// Gets snapshot.
    /// </summary>
    public GeneratedSourceSnapshot Snapshot { get; }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets compilation diagnostics.
    /// </summary>
    public IReadOnlyList<Diagnostic> CompilationDiagnostics { get; }
}
