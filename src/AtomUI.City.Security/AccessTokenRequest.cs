namespace AtomUI.City.Security;

/// <summary>
/// Represents access token request.
/// </summary>
public sealed class AccessTokenRequest
{
    /// <summary>
    /// Initializes a new instance of the <c>AccessTokenRequest</c> type.
    /// </summary>
    public AccessTokenRequest(
        string resourceName,
        string? scheme = null,
        string? operationName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        ValidateOptional(scheme, nameof(scheme));
        ValidateOptional(operationName, nameof(operationName));

        ResourceName = resourceName;
        Scheme = scheme;
        OperationName = operationName;
    }

    /// <summary>
    /// Gets resource name.
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// Gets scheme.
    /// </summary>
    public string? Scheme { get; }

    /// <summary>
    /// Gets operation name.
    /// </summary>
    public string? OperationName { get; }

    private static void ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }
    }
}
