namespace AtomUI.City.Data;

/// <summary>
/// Represents signal rconnection closed exception.
/// </summary>
public sealed class SignalRConnectionClosedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>SignalRConnectionClosedException</c> type.
    /// </summary>
    public SignalRConnectionClosedException(string? message = null)
        : base(message ?? "SignalR connection was closed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>SignalRConnectionClosedException</c> type.
    /// </summary>
    public SignalRConnectionClosedException(string? message, Exception? innerException)
        : base(message ?? "SignalR connection was closed.", innerException)
    {
    }
}

/// <summary>
/// Represents signal rreconnect failed exception.
/// </summary>
public sealed class SignalRReconnectFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>SignalRReconnectFailedException</c> type.
    /// </summary>
    public SignalRReconnectFailedException(string? message = null)
        : base(message ?? "SignalR reconnect failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>SignalRReconnectFailedException</c> type.
    /// </summary>
    public SignalRReconnectFailedException(string? message, Exception? innerException)
        : base(message ?? "SignalR reconnect failed.", innerException)
    {
    }
}
