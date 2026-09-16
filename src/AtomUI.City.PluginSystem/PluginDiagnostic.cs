namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin diagnostic.
/// </summary>
/// <param name="Code">The code value.</param>
/// <param name="Message">The message value.</param>
/// <param name="PluginId">The plugin id value.</param>
/// <param name="Field">The field value.</param>
/// <param name="Path">The path value.</param>
public sealed record PluginDiagnostic(
    string Code,
    string Message,
    string? PluginId = null,
    string? Field = null,
    string? Path = null)
{
    /// <summary>
    /// Gets or sets code.
    /// </summary>
    public string Code { get; init; } = !string.IsNullOrWhiteSpace(Code)
        ? Code
        : throw new ArgumentException("Plugin diagnostic code cannot be empty.", nameof(Code));

    /// <summary>
    /// Gets or sets message.
    /// </summary>
    public string Message { get; init; } = !string.IsNullOrWhiteSpace(Message)
        ? Message
        : throw new ArgumentException("Plugin diagnostic message cannot be empty.", nameof(Message));
}

/// <summary>
/// Represents plugin validation result.
/// </summary>
public sealed class PluginValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <c>PluginValidationResult</c> type.
    /// </summary>
    public PluginValidationResult(IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Diagnostics.Count == 0;

    /// <summary>
    /// Gets success.
    /// </summary>
    public static PluginValidationResult Success { get; } = new([]);
}
