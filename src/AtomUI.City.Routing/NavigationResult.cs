namespace AtomUI.City.Routing;

/// <summary>
/// Represents navigation result.
/// </summary>
public sealed class NavigationResult
{
    private NavigationResult(
        Guid navigationId,
        NavigationResultStatus status,
        NavigationTarget target,
        RouteDescriptor? route,
        IReadOnlyDictionary<string, string> parameters,
        NavigationError? error,
        NavigationTarget? redirectTarget)
    {
        NavigationId = navigationId;
        Status = status;
        Target = target;
        ActiveRoute = route;
        Parameters = RouteParameters.Copy(parameters);
        Error = error;
        RedirectTarget = redirectTarget;
    }

    /// <summary>
    /// Gets navigation id.
    /// </summary>
    public Guid NavigationId { get; }

    /// <summary>
    /// Gets status.
    /// </summary>
    public NavigationResultStatus Status { get; }

    /// <summary>
    /// Gets target.
    /// </summary>
    public NavigationTarget Target { get; }

    /// <summary>
    /// Gets route.
    /// </summary>
    public RouteDescriptor Route => ActiveRoute ?? throw new InvalidOperationException("Navigation did not produce an active route.");

    /// <summary>
    /// Gets active route.
    /// </summary>
    public RouteDescriptor? ActiveRoute { get; }

    /// <summary>
    /// Gets parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Gets error.
    /// </summary>
    public NavigationError? Error { get; }

    /// <summary>
    /// Gets redirect target.
    /// </summary>
    public NavigationTarget? RedirectTarget { get; }

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static NavigationResult Success(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(parameters);

        return new NavigationResult(
            navigationId,
            NavigationResultStatus.Success,
            target,
            route,
            parameters,
            error: null,
            redirectTarget: null);
    }

    /// <summary>
    /// Executes the not found operation.
    /// </summary>
    public static NavigationResult NotFound(
        Guid navigationId,
        NavigationTarget target,
        string message)
    {
        return Failure(
            navigationId,
            NavigationResultStatus.NotFound,
            target,
            "CITY-NAVIGATION-NOT-FOUND",
            message);
    }

    /// <summary>
    /// Executes the rejected operation.
    /// </summary>
    public static NavigationResult Rejected(
        Guid navigationId,
        NavigationTarget target,
        string code,
        string? message = null)
    {
        return Failure(
            navigationId,
            NavigationResultStatus.Rejected,
            target,
            code,
            message ?? "Navigation was rejected.");
    }

    /// <summary>
    /// Executes the cancelled operation.
    /// </summary>
    public static NavigationResult Cancelled(
        Guid navigationId,
        NavigationTarget target,
        string? message = null)
    {
        return Failure(
            navigationId,
            NavigationResultStatus.Cancelled,
            target,
            "CITY-NAVIGATION-CANCELLED",
            message ?? "Navigation was cancelled.");
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static NavigationResult Failed(
        Guid navigationId,
        NavigationTarget target,
        string code,
        string message,
        Exception? exception = null)
    {
        return new NavigationResult(
            navigationId,
            NavigationResultStatus.Failed,
            target,
            route: null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new NavigationError(code, message, exception),
            redirectTarget: null);
    }

    /// <summary>
    /// Executes the redirected operation.
    /// </summary>
    public static NavigationResult Redirected(
        Guid navigationId,
        NavigationTarget target,
        NavigationTarget redirectTarget)
    {
        ArgumentNullException.ThrowIfNull(redirectTarget);

        return new NavigationResult(
            navigationId,
            NavigationResultStatus.Redirected,
            target,
            route: null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new NavigationError("CITY-NAVIGATION-REDIRECTED", $"Navigation redirected to '{redirectTarget}'."),
            redirectTarget);
    }

    /// <summary>
    /// Executes the redirected operation.
    /// </summary>
    public static NavigationResult Redirected(
        Guid navigationId,
        NavigationTarget target,
        NavigationTarget redirectTarget,
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(redirectTarget);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(parameters);

        return new NavigationResult(
            navigationId,
            NavigationResultStatus.Redirected,
            target,
            route,
            parameters,
            new NavigationError("CITY-NAVIGATION-REDIRECTED", $"Navigation redirected to '{redirectTarget}'."),
            redirectTarget);
    }

    private static NavigationResult Failure(
        Guid navigationId,
        NavigationResultStatus status,
        NavigationTarget target,
        string code,
        string message)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new NavigationResult(
            navigationId,
            status,
            target,
            route: null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new NavigationError(code, message),
            redirectTarget: null);
    }
}
