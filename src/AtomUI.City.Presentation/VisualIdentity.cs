namespace AtomUI.City.Presentation;

public sealed record VisualIdentity(
    object View,
    string? WindowId = null,
    string? OutletName = null,
    long? OperationId = null,
    string? EntryId = null)
{
    public object View { get; init; } = View ?? throw new ArgumentNullException(nameof(View));
}
