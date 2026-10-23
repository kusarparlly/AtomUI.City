namespace AtomUI.City.Testing;

/// <summary>
/// Describes a source-text pattern matched by <see cref="AotSourcePatternCheck"/>.
/// </summary>
public sealed record AotSourcePatternDiagnostic(string Id, string SourcePath, string Message);
