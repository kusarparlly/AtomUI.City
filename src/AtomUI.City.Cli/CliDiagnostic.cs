namespace AtomUI.City.Cli;

/// <summary>
/// Represents cli diagnostic.
/// </summary>
/// <param name="Code">The code value.</param>
/// <param name="Message">The message value.</param>
/// <param name="Severity">The severity value.</param>
/// <param name="SuggestedAction">The suggested action value.</param>
/// <param name="DocumentationLink">The documentation link value.</param>
public sealed record CliDiagnostic(
    string Code,
    string Message,
    string Severity,
    string? SuggestedAction = null,
    string? DocumentationLink = null)
{
    /// <summary>
    /// Gets or sets target.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Gets or sets position.
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// Executes the error operation.
    /// </summary>
    public static CliDiagnostic Error(
        string code,
        string message,
        string? target = null,
        int? position = null)
    {
        return new CliDiagnostic(code, message, "Error")
        {
            Target = target,
            Position = position,
        };
    }
}
