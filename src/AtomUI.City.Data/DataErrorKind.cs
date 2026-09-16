namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data error kind values.
/// </summary>
public enum DataErrorKind
{
    /// <summary>
    /// Represents the cancelled value.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Represents the timeout value.
    /// </summary>
    Timeout,
    /// <summary>
    /// Represents the network unavailable value.
    /// </summary>
    NetworkUnavailable,
    /// <summary>
    /// Represents the credential unavailable value.
    /// </summary>
    CredentialUnavailable,
    /// <summary>
    /// Represents the authentication required value.
    /// </summary>
    AuthenticationRequired,
    /// <summary>
    /// Represents the authentication expired value.
    /// </summary>
    AuthenticationExpired,
    /// <summary>
    /// Represents the authorization forbidden value.
    /// </summary>
    AuthorizationForbidden,
    /// <summary>
    /// Represents the bad request value.
    /// </summary>
    BadRequest,
    /// <summary>
    /// Represents the not found value.
    /// </summary>
    NotFound,
    /// <summary>
    /// Represents the conflict value.
    /// </summary>
    Conflict,
    /// <summary>
    /// Represents the validation failed value.
    /// </summary>
    ValidationFailed,
    /// <summary>
    /// Represents the server error value.
    /// </summary>
    ServerError,
    /// <summary>
    /// Represents the service unavailable value.
    /// </summary>
    ServiceUnavailable,
    /// <summary>
    /// Represents the transport error value.
    /// </summary>
    TransportError,
    /// <summary>
    /// Represents the serialization error value.
    /// </summary>
    SerializationError,
    /// <summary>
    /// Represents the policy rejected value.
    /// </summary>
    PolicyRejected,
    /// <summary>
    /// Represents the connection failed value.
    /// </summary>
    ConnectionFailed,
    /// <summary>
    /// Represents the connection closed value.
    /// </summary>
    ConnectionClosed,
    /// <summary>
    /// Represents the reconnect failed value.
    /// </summary>
    ReconnectFailed,
    /// <summary>
    /// Represents the stream cancelled value.
    /// </summary>
    StreamCancelled,
    /// <summary>
    /// Represents the stream completed value.
    /// </summary>
    [Obsolete("Normal stream completion is not a data error and must not produce a failed DataResult.")]
    StreamCompleted,
    /// <summary>
    /// Represents the stream protocol error value.
    /// </summary>
    StreamProtocolError,
    /// <summary>
    /// Represents the deadline exceeded value.
    /// </summary>
    DeadlineExceeded,
    /// <summary>
    /// Represents the unavailable value.
    /// </summary>
    Unavailable,
    /// <summary>
    /// Represents the plugin unavailable value.
    /// </summary>
    PluginUnavailable,
    /// <summary>
    /// Represents the local storage error value.
    /// </summary>
    LocalStorageError,
    /// <summary>
    /// Represents the unknown value.
    /// </summary>
    Unknown,
}
