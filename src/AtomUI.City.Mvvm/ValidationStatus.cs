namespace AtomUI.City.Mvvm;

/// <summary>
/// Defines the supported validation status values.
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// Represents the valid value.
    /// </summary>
    Valid,
    /// <summary>
    /// Represents the invalid value.
    /// </summary>
    Invalid,
    /// <summary>
    /// Represents the pending value.
    /// </summary>
    Pending,
    /// <summary>
    /// Represents the canceled value.
    /// </summary>
    Canceled,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed,
}
