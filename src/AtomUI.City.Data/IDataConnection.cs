namespace AtomUI.City.Data;

/// <summary>
/// Defines the contract for idata connection.
/// </summary>
public interface IDataConnection
{
    /// <summary>
    /// Gets connection id.
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// Gets owner.
    /// </summary>
    DataConnectionOwner Owner { get; }

    /// <summary>
    /// Gets state.
    /// </summary>
    DataConnectionState State { get; }

    /// <summary>
    /// Executes the start async operation.
    /// </summary>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
