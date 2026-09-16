namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data concurrency policy values.
/// </summary>
public enum DataConcurrencyPolicy
{
    /// <summary>
    /// Represents the allow concurrent value.
    /// </summary>
    AllowConcurrent,
    /// <summary>
    /// Represents the disallow concurrent value.
    /// </summary>
    DisallowConcurrent,
    /// <summary>
    /// Represents the queue value.
    /// </summary>
    Queue,
    /// <summary>
    /// Represents the cancel previous value.
    /// </summary>
    CancelPrevious,
    /// <summary>
    /// Represents the latest wins value.
    /// </summary>
    LatestWins,
    /// <summary>
    /// Represents the keyed serial value.
    /// </summary>
    KeyedSerial,
}
