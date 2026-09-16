namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iroute outlet target.
/// </summary>
public interface IRouteOutletTarget
{
    /// <summary>
    /// Gets content.
    /// </summary>
    object? Content { get; }

    /// <summary>
    /// Executes the set content operation.
    /// </summary>
    void SetContent(object? content);
}
