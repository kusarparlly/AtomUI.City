namespace AtomUI.City.Data;

/// <summary>
/// Represents data cache key.
/// </summary>
public sealed record DataCacheKey(
    string ClientId,
    string OperationName,
    DataTransportKind TransportKind,
    DataAccessMode AccessMode,
    string RequestFingerprint,
    string AuthenticationScheme,
    string PrincipalRevision,
    string PermissionRevision,
    string? PluginContributionId,
    string ClientVersion,
    string PolicyVersion)
{
    /// <summary>
    /// Gets or sets client id.
    /// </summary>
    public string ClientId { get; init; } = Require(ClientId, nameof(ClientId));

    /// <summary>
    /// Gets or sets operation name.
    /// </summary>
    public string OperationName { get; init; } = Require(OperationName, nameof(OperationName));

    /// <summary>
    /// Gets or sets transport kind.
    /// </summary>
    public DataTransportKind TransportKind { get; init; } = Validate(TransportKind, nameof(TransportKind));

    /// <summary>
    /// Gets or sets access mode.
    /// </summary>
    public DataAccessMode AccessMode { get; init; } = Validate(AccessMode, nameof(AccessMode));

    /// <summary>
    /// Gets or sets request fingerprint.
    /// </summary>
    public string RequestFingerprint { get; init; } = Require(RequestFingerprint, nameof(RequestFingerprint));

    /// <summary>
    /// Gets or sets authentication scheme.
    /// </summary>
    public string AuthenticationScheme { get; init; } = Require(AuthenticationScheme, nameof(AuthenticationScheme));

    /// <summary>
    /// Gets or sets principal revision.
    /// </summary>
    public string PrincipalRevision { get; init; } = Require(PrincipalRevision, nameof(PrincipalRevision));

    /// <summary>
    /// Gets or sets permission revision.
    /// </summary>
    public string PermissionRevision { get; init; } = Require(PermissionRevision, nameof(PermissionRevision));

    /// <summary>
    /// Gets or sets plugin contribution id.
    /// </summary>
    public string? PluginContributionId { get; init; } =
        RequireOptional(PluginContributionId, nameof(PluginContributionId));

    /// <summary>
    /// Gets or sets client version.
    /// </summary>
    public string ClientVersion { get; init; } = Require(ClientVersion, nameof(ClientVersion));

    /// <summary>
    /// Gets or sets policy version.
    /// </summary>
    public string PolicyVersion { get; init; } = Require(PolicyVersion, nameof(PolicyVersion));

    /// <summary>
    /// Executes the create&lt;tresponse&gt; operation.
    /// </summary>
    public static DataCacheKey Create<TResponse>(
        DataRequest<TResponse> request,
        string authenticationScheme)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);

        var pluginContributionId = request.Origin.Kind == DataRequestOriginKind.Plugin
            ? request.Origin.ContributionId
            : request.Cache.PluginContributionId;

        return new DataCacheKey(
            request.ClientId,
            request.OperationName,
            request.TransportKind,
            request.AccessMode,
            request.Cache.RequestFingerprint,
            authenticationScheme,
            request.Cache.PrincipalRevision,
            request.Cache.PermissionRevision,
            pluginContributionId,
            request.Cache.ClientVersion,
            request.Cache.PolicyVersion);
    }

    private static string Require(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value;
    }

    private static string? RequireOptional(string? value, string paramName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        }

        return value;
    }

    private static TEnum Validate<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Data cache key enum value is not supported.");
    }
}
