using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AtomUI.City.Data;

/// <summary>
/// Defines the direction of a SignalR payload at the City protocol boundary.
/// </summary>
public enum SignalRPayloadDirection
{
    /// <summary>
    /// The payload is an invocation argument sent to the server.
    /// </summary>
    Argument,

    /// <summary>
    /// The payload is an invocation response received from the server.
    /// </summary>
    Response,

    /// <summary>
    /// The payload is a server message received by a subscription.
    /// </summary>
    ServerMessage,
}

/// <summary>
/// Reports that a SignalR payload type is missing from the configured static JSON metadata.
/// </summary>
public sealed class SignalRPayloadMetadataException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRPayloadMetadataException"/> class.
    /// </summary>
    public SignalRPayloadMetadataException(
        string connectionId,
        string methodName,
        Type payloadType,
        SignalRPayloadDirection direction,
        Exception? innerException = null)
        : base(CreateMessage(connectionId, methodName, payloadType, direction), innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(payloadType);
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "SignalR payload direction is not supported.");
        }

        ConnectionId = connectionId;
        MethodName = methodName;
        PayloadType = payloadType;
        Direction = direction;
    }

    /// <summary>
    /// Gets the connection identity.
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// Gets the hub method name.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the payload type that has no static metadata.
    /// </summary>
    public Type PayloadType { get; }

    /// <summary>
    /// Gets the payload direction.
    /// </summary>
    public SignalRPayloadDirection Direction { get; }

    private static string CreateMessage(
        string connectionId,
        string methodName,
        Type payloadType,
        SignalRPayloadDirection direction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(payloadType);
        return $"SignalR {direction} payload type '{payloadType.FullName ?? payloadType.Name}' for method " +
               $"'{methodName}' on connection '{connectionId}' has no registered static JSON metadata.";
    }
}

/// <summary>
/// Provides immutable source-generated JSON metadata for a SignalR connection.
/// </summary>
public sealed class SignalRJsonContract
{
    private readonly JsonSerializerOptions _serializerOptions;

    private SignalRJsonContract(JsonSerializerOptions serializerOptions)
    {
        _serializerOptions = serializerOptions;
    }

    /// <summary>
    /// Creates a contract from one or more source-generated resolvers.
    /// </summary>
    public static SignalRJsonContract Create(params IJsonTypeInfoResolver[] resolvers) =>
        Create((IEnumerable<IJsonTypeInfoResolver>)resolvers);

    /// <summary>
    /// Creates a contract from one or more source-generated resolvers and an optional serializer configuration.
    /// </summary>
    public static SignalRJsonContract Create(
        IEnumerable<IJsonTypeInfoResolver> resolvers,
        Action<JsonSerializerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        var resolverArray = resolvers.ToArray();
        if (resolverArray.Length == 0)
        {
            throw new ArgumentException("At least one SignalR JSON type info resolver is required.", nameof(resolvers));
        }

        if (resolverArray.Any(static resolver => resolver is null))
        {
            throw new ArgumentException("SignalR JSON type info resolvers cannot contain null values.", nameof(resolvers));
        }

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        configure?.Invoke(serializerOptions);
        serializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(resolverArray);
        serializerOptions.MakeReadOnly();
        return new SignalRJsonContract(serializerOptions);
    }

    internal JsonSerializerOptions SerializerOptions => _serializerOptions;

    internal void EnsureMetadata(
        Type payloadType,
        string connectionId,
        string methodName,
        SignalRPayloadDirection direction)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        try
        {
            if (_serializerOptions.GetTypeInfo(payloadType) is not null)
            {
                return;
            }
        }
        catch (NotSupportedException exception)
        {
            throw new SignalRPayloadMetadataException(
                connectionId,
                methodName,
                payloadType,
                direction,
                exception);
        }

        throw new SignalRPayloadMetadataException(connectionId, methodName, payloadType, direction);
    }
}
