namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported presentation failure level values.
/// </summary>
public enum PresentationFailureLevel
{
    /// <summary>
    /// Represents the operation value.
    /// </summary>
    Operation,
    /// <summary>
    /// Represents the outlet value.
    /// </summary>
    Outlet,
    /// <summary>
    /// Represents the window value.
    /// </summary>
    Window,
    /// <summary>
    /// Represents the runtime value.
    /// </summary>
    Runtime,
}

/// <summary>
/// Represents presentation failure.
/// </summary>
/// <param name="Level">The level value.</param>
/// <param name="Error">The error value.</param>
/// <param name="Message">The message value.</param>
/// <param name="OperationId">The operation id value.</param>
/// <param name="WindowId">The window id value.</param>
/// <param name="OutletName">The outlet name value.</param>
/// <param name="RouteId">The route id value.</param>
/// <param name="Exception">The exception value.</param>
public sealed record PresentationFailure(
    PresentationFailureLevel Level,
    PresentationError Error,
    string Message,
    long OperationId = 0,
    string? WindowId = null,
    string? OutletName = null,
    string? RouteId = null,
    Exception? Exception = null);

/// <summary>
/// Defines the contract for ipresentation failure presenter.
/// </summary>
public interface IPresentationFailurePresenter
{
    /// <summary>
    /// Executes the present async operation.
    /// </summary>
    ValueTask PresentAsync(
        PresentationFailure failure,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents null presentation failure presenter.
/// </summary>
public sealed class NullPresentationFailurePresenter : IPresentationFailurePresenter
{
    /// <summary>
    /// Executes the present async operation.
    /// </summary>
    public ValueTask PresentAsync(
        PresentationFailure failure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
