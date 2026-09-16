using System.Collections.ObjectModel;

namespace AtomUI.City.Routing;

/// <summary>
/// Represents route resolve result.
/// </summary>
public sealed class RouteResolveResult
{
    private RouteResolveResult(
        RouteResolveResultStatus status,
        IReadOnlyDictionary<string, object?> data,
        NavigationTarget? redirectTarget,
        string? code,
        string? message,
        Exception? exception)
    {
        Status = status;
        Data = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(data, StringComparer.Ordinal));
        RedirectTarget = redirectTarget;
        Code = code;
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public RouteResolveResultStatus Status { get; }
    /// <summary>
    /// Gets data.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Data { get; }
    /// <summary>
    /// Gets redirect target.
    /// </summary>
    public NavigationTarget? RedirectTarget { get; }
    /// <summary>
    /// Gets code.
    /// </summary>
    public string? Code { get; }
    /// <summary>
    /// Gets message.
    /// </summary>
    public string? Message { get; }
    /// <summary>
    /// Gets exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets success.
    /// </summary>
    public static RouteResolveResult Success(IReadOnlyDictionary<string, object?>? data = null) =>
        new(RouteResolveResultStatus.Success, data ?? EmptyData(), null, null, null, null);

    /// <summary>
    /// Gets not found.
    /// </summary>
    public static RouteResolveResult NotFound(string? message = null) =>
        new(RouteResolveResultStatus.NotFound, EmptyData(), null, "CITY-NAVIGATION-RESOLVER-NOT-FOUND", message, null);

    /// <summary>
    /// Gets redirect.
    /// </summary>
    public static RouteResolveResult Redirect(NavigationTarget target) =>
        new(RouteResolveResultStatus.Redirect, EmptyData(), target ?? throw new ArgumentNullException(nameof(target)), null, null, null);

    /// <summary>
    /// Gets a value indicating whether cancelled.
    /// </summary>
    public static RouteResolveResult Cancelled(string? message = null) =>
        new(RouteResolveResultStatus.Cancelled, EmptyData(), null, "CITY-NAVIGATION-CANCELLED", message, null);

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static RouteResolveResult Failed(string code, string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(RouteResolveResultStatus.Failed, EmptyData(), null, code, message, exception);
    }

    private static IReadOnlyDictionary<string, object?> EmptyData() =>
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
