namespace AtomUI.City.Presentation;

public enum PresentationFailureLevel
{
    Operation,
    Outlet,
    Window,
    Runtime,
}

public sealed record PresentationFailure(
    PresentationFailureLevel Level,
    PresentationError Error,
    string Message,
    long OperationId = 0,
    string? WindowId = null,
    string? OutletName = null,
    string? RouteId = null,
    Exception? Exception = null);

public interface IPresentationFailurePresenter
{
    ValueTask PresentAsync(
        PresentationFailure failure,
        CancellationToken cancellationToken = default);
}

public sealed class NullPresentationFailurePresenter : IPresentationFailurePresenter
{
    public ValueTask PresentAsync(
        PresentationFailure failure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
