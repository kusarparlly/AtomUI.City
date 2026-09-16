namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data connection state values.
/// </summary>
public enum DataConnectionState
{
    /// <summary>
    /// Represents the created value.
    /// </summary>
    Created,
    /// <summary>
    /// Represents the connecting value.
    /// </summary>
    Connecting,
    /// <summary>
    /// Represents the connected value.
    /// </summary>
    Connected,
    /// <summary>
    /// Represents the reconnecting value.
    /// </summary>
    Reconnecting,
    /// <summary>
    /// Represents the disconnecting value.
    /// </summary>
    Disconnecting,
    /// <summary>
    /// Represents the stopped value.
    /// </summary>
    Stopped,
    /// <summary>
    /// Represents the faulted value.
    /// </summary>
    Faulted,
}
