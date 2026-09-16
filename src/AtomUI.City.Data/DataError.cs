namespace AtomUI.City.Data;

/// <summary>
/// Represents data error.
/// </summary>
/// <param name="Kind">The kind value.</param>
/// <param name="Message">The message value.</param>
/// <param name="TransportStatus">The transport status value.</param>
/// <param name="MessageKey">The message key value.</param>
/// <param name="MessageArguments">The message arguments value.</param>
/// <param name="Exception">The exception value.</param>
public sealed record DataError(
    DataErrorKind Kind,
    string Message,
    string? TransportStatus = null,
    string? MessageKey = null,
    IReadOnlyList<object?>? MessageArguments = null,
    Exception? Exception = null)
{
    private DataErrorKind _kind = ValidateKind(Kind);
    private string _message = ValidateMessage(Message);
    private string? _transportStatus = ValidateOptionalText(TransportStatus, nameof(TransportStatus));
    private string? _messageKey = ValidateOptionalText(MessageKey, nameof(MessageKey));
    private readonly IReadOnlyList<object?>? _messageArguments =
        MessageArguments is null ? null : Array.AsReadOnly(MessageArguments.ToArray());

    /// <summary>
    /// Represents the kind value.
    /// </summary>
    public DataErrorKind Kind
    {
        get => _kind;
        init => _kind = ValidateKind(value);
    }

    /// <summary>
    /// Represents the message value.
    /// </summary>
    public string Message
    {
        get => _message;
        init => _message = ValidateMessage(value);
    }

    /// <summary>
    /// Represents the transport status value.
    /// </summary>
    public string? TransportStatus
    {
        get => _transportStatus;
        init => _transportStatus = ValidateOptionalText(value, nameof(TransportStatus));
    }

    /// <summary>
    /// Represents the message key value.
    /// </summary>
    public string? MessageKey
    {
        get => _messageKey;
        init => _messageKey = ValidateOptionalText(value, nameof(MessageKey));
    }

    /// <summary>
    /// Represents the message arguments value.
    /// </summary>
    public IReadOnlyList<object?>? MessageArguments
    {
        get => _messageArguments;
        init => _messageArguments = value is null ? null : Array.AsReadOnly(value.ToArray());
    }

    private static DataErrorKind ValidateKind(DataErrorKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Data error kind is not supported.");
        }

        return kind;
    }

    private static string ValidateMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return message;
    }

    private static string? ValidateOptionalText(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }

        return value;
    }
}
