namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data capability values.
/// </summary>
[Flags]
public enum DataCapability
{
    /// <summary>
    /// Represents the none value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Represents the use data client value.
    /// </summary>
    UseDataClient = 1 << 0,
    /// <summary>
    /// Represents the use http client value.
    /// </summary>
    UseHttpClient = 1 << 1,
    /// <summary>
    /// Represents the use grpc client value.
    /// </summary>
    UseGrpcClient = 1 << 2,
    /// <summary>
    /// Represents the use signal rhub value.
    /// </summary>
    UseSignalRHub = 1 << 3,
    /// <summary>
    /// Represents the use realtime connection value.
    /// </summary>
    UseRealtimeConnection = 1 << 4,
    /// <summary>
    /// Represents the use streaming value.
    /// </summary>
    UseStreaming = 1 << 5,
}

/// <summary>
/// Defines the supported data request origin kind values.
/// </summary>
public enum DataRequestOriginKind
{
    /// <summary>
    /// Represents the host value.
    /// </summary>
    Host,
    /// <summary>
    /// Represents the plugin value.
    /// </summary>
    Plugin,
}

/// <summary>
/// Represents data request origin.
/// </summary>
public sealed class DataRequestOrigin
{
    private DataRequestOrigin(
        DataRequestOriginKind kind,
        string? pluginId,
        string? contributionId,
        DataCapability capabilities,
        object? token)
    {
        Kind = kind;
        PluginId = pluginId;
        ContributionId = contributionId;
        Capabilities = capabilities;
        Token = token;
    }

    /// <summary>
    /// Gets host.
    /// </summary>
    public static DataRequestOrigin Host { get; } = new(
        DataRequestOriginKind.Host,
        pluginId: null,
        contributionId: null,
        DataCapabilityRules.All,
        token: null);

    /// <summary>
    /// Gets kind.
    /// </summary>
    public DataRequestOriginKind Kind { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    /// <summary>
    /// Gets capabilities.
    /// </summary>
    public DataCapability Capabilities { get; }

    internal object? Token { get; }

    internal static DataRequestOrigin Plugin(
        string pluginId,
        string contributionId,
        DataCapability capabilities,
        object token) =>
        new(DataRequestOriginKind.Plugin, pluginId, contributionId, capabilities, token);
}

/// <summary>
/// Defines the contract for idata capability authorizer.
/// </summary>
public interface IDataCapabilityAuthorizer
{
    /// <summary>
    /// Executes the is authorized operation.
    /// </summary>
    bool IsAuthorized(DataRequestOrigin origin, DataCapability capability);
}

/// <summary>
/// Represents default data capability authorizer.
/// </summary>
public sealed class DefaultDataCapabilityAuthorizer : IDataCapabilityAuthorizer
{
    /// <summary>
    /// Executes the is authorized operation.
    /// </summary>
    public bool IsAuthorized(DataRequestOrigin origin, DataCapability capability)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if ((capability & ~DataCapabilityRules.All) != 0)
        {
            return false;
        }

        return origin.Kind == DataRequestOriginKind.Host
            && (origin.Capabilities & capability) == capability;
    }
}

internal static class DataCapabilityRules
{
    public const DataCapability All = DataCapability.UseDataClient
        | DataCapability.UseHttpClient
        | DataCapability.UseGrpcClient
        | DataCapability.UseSignalRHub
        | DataCapability.UseRealtimeConnection
        | DataCapability.UseStreaming;

    public static DataCapability RequiredFor(DataTransportKind transportKind) =>
        DataCapability.UseDataClient | transportKind switch
        {
            DataTransportKind.Http => DataCapability.UseHttpClient,
            DataTransportKind.Grpc => DataCapability.UseGrpcClient,
            DataTransportKind.SignalR => DataCapability.UseSignalRHub,
            _ => DataCapability.None,
        };
}
