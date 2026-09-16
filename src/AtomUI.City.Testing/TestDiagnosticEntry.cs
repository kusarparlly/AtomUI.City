namespace AtomUI.City.Testing;

/// <summary>
/// Represents test diagnostic entry.
/// </summary>
public sealed record TestDiagnosticEntry(string Code, string Message, TestLayer? Layer = null);
