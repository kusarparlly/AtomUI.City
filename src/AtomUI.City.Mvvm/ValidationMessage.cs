namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents validation message.
/// </summary>
public sealed class ValidationMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationMessage"/> type.
    /// </summary>
    public ValidationMessage(
        string key,
        string message,
        string? messageKey = null,
        IReadOnlyList<object?>? messageArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Key = key;
        Message = message;
        MessageKey = messageKey;
        MessageArguments = messageArguments is null ? null : Array.AsReadOnly(messageArguments.ToArray());
    }

    /// <summary>
    /// Gets key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets message key.
    /// </summary>
    public string? MessageKey { get; }

    /// <summary>
    /// Gets message arguments.
    /// </summary>
    public IReadOnlyList<object?>? MessageArguments { get; }
}
