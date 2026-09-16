namespace AtomUI.City.Data;

/// <summary>
/// Represents data credential result.
/// </summary>
public sealed class DataCredentialResult
{
    private DataCredentialResult(
        DataCredentialResultStatus status,
        DataCredential? credential,
        string? message)
    {
        Status = status;
        Credential = credential;
        Message = ValidateOptionalMessage(message);
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public DataCredentialResultStatus Status { get; }

    /// <summary>
    /// Gets credential.
    /// </summary>
    public DataCredential? Credential { get; }

    /// <summary>
    /// Gets message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Status == DataCredentialResultStatus.Success;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static DataCredentialResult Success(DataCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return new DataCredentialResult(DataCredentialResultStatus.Success, credential, message: null);
    }

    /// <summary>
    /// Executes the none operation.
    /// </summary>
    public static DataCredentialResult None()
    {
        return new DataCredentialResult(DataCredentialResultStatus.None, credential: null, message: null);
    }

    /// <summary>
    /// Executes the required operation.
    /// </summary>
    public static DataCredentialResult Required(string? message = null)
    {
        return new DataCredentialResult(DataCredentialResultStatus.Required, credential: null, message);
    }

    /// <summary>
    /// Executes the expired operation.
    /// </summary>
    public static DataCredentialResult Expired(string? message = null)
    {
        return new DataCredentialResult(DataCredentialResultStatus.Expired, credential: null, message);
    }

    /// <summary>
    /// Executes the unavailable operation.
    /// </summary>
    public static DataCredentialResult Unavailable(string? message = null)
    {
        return new DataCredentialResult(DataCredentialResultStatus.Unavailable, credential: null, message);
    }

    /// <summary>
    /// Executes the cancelled operation.
    /// </summary>
    public static DataCredentialResult Cancelled(string? message = null)
    {
        return new DataCredentialResult(DataCredentialResultStatus.Cancelled, credential: null, message);
    }

    private static string? ValidateOptionalMessage(string? message)
    {
        if (message is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }

        return message;
    }
}

/// <summary>
/// Defines the supported data credential result status values.
/// </summary>
public enum DataCredentialResultStatus
{
    /// <summary>
    /// Represents the none value.
    /// </summary>
    None,
    /// <summary>
    /// Represents the success value.
    /// </summary>
    Success,
    /// <summary>
    /// Represents the required value.
    /// </summary>
    Required,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired,
    /// <summary>
    /// Represents the unavailable value.
    /// </summary>
    Unavailable,
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
}
