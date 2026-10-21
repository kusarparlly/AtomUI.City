using AtomUI.City.Routing;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents the structured result of one coordinated route and presentation operation.
/// </summary>
public sealed class RoutePresentationResult
{
    internal RoutePresentationResult(
        RoutePresentationResultStatus status,
        NavigationResult? navigation,
        RouteOutletCommitResult? presentation,
        NavigationRestoreResult? restore,
        string? message,
        Exception? exception)
    {
        Status = status;
        Navigation = navigation;
        Presentation = presentation;
        Restore = restore;
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Gets the overall coordination status.
    /// </summary>
    public RoutePresentationResultStatus Status { get; }

    /// <summary>
    /// Gets the Router result when navigation ran to a structured result.
    /// </summary>
    public NavigationResult? Navigation { get; }

    /// <summary>
    /// Gets the Outlet result when a plan was submitted.
    /// </summary>
    public RouteOutletCommitResult? Presentation { get; }

    /// <summary>
    /// Gets the conditional restore result when recovery was attempted.
    /// </summary>
    public NavigationRestoreResult? Restore { get; }

    /// <summary>
    /// Gets a diagnostic message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the unexpected or Presentation-stage exception, when any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets a value indicating whether Router and Presentation both committed.
    /// </summary>
    public bool Succeeded => Status == RoutePresentationResultStatus.Succeeded;
}
