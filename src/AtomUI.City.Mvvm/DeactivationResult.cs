namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents deactivation result.
/// </summary>
/// <param name="Status">The status value.</param>
/// <param name="Reason">The reason value.</param>
/// <param name="Exception">The exception value.</param>
public sealed record DeactivationResult(
    DeactivationStatus Status,
    string? Reason = null,
    Exception? Exception = null)
{
    /// <summary>
    /// Executes the allow operation.
    /// </summary>
    public static DeactivationResult Allow()
    {
        return new DeactivationResult(DeactivationStatus.Allow);
    }

    /// <summary>
    /// Executes the reject operation.
    /// </summary>
    public static DeactivationResult Reject(string? reason = null)
    {
        return new DeactivationResult(DeactivationStatus.Reject, reason);
    }

    /// <summary>
    /// Executes the cancel operation.
    /// </summary>
    public static DeactivationResult Cancel(string? reason = null)
    {
        return new DeactivationResult(DeactivationStatus.Cancel, reason);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static DeactivationResult Failed(string? reason = null, Exception? exception = null)
    {
        return new DeactivationResult(DeactivationStatus.Failed, reason, exception);
    }
}
