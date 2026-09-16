namespace AtomUI.City.Data;

/// <summary>
/// Represents data authentication context.
/// </summary>
public sealed class DataAuthenticationContext
{
    /// <summary>
    /// Initializes a new instance of the <c>DataAuthenticationContext</c> type.
    /// </summary>
    public DataAuthenticationContext(
        string clientId,
        string operationName,
        DataAuthenticationOptions authentication)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(authentication);

        ClientId = clientId;
        OperationName = operationName;
        Authentication = authentication;
    }

    /// <summary>
    /// Gets client id.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets operation name.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets authentication.
    /// </summary>
    public DataAuthenticationOptions Authentication { get; }
}
