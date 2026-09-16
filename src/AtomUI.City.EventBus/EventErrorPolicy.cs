namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the supported event error policy values.
/// </summary>
public enum EventErrorPolicy
{
    /// <summary>
    /// Represents the continue and report value.
    /// </summary>
    ContinueAndReport = 0,
    /// <summary>
    /// Represents the stop publication value.
    /// </summary>
    StopPublication = 1,
    /// <summary>
    /// Represents the fail publisher value.
    /// </summary>
    FailPublisher = 2,
    /// <summary>
    /// Represents the disable subscription value.
    /// </summary>
    DisableSubscription = 3,
}
