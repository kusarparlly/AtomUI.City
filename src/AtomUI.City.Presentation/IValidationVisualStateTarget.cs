namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for ivalidation visual state target.
/// </summary>
public interface IValidationVisualStateTarget
{
    /// <summary>
    /// Executes the apply validation state operation.
    /// </summary>
    void ApplyValidationState(ValidationVisualStateSnapshot snapshot);
}
