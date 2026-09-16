namespace AtomUI.City.Presentation;

/// <summary>
/// Represents visual identity.
/// </summary>
/// <param name="View">The view value.</param>
/// <param name="WindowId">The window id value.</param>
/// <param name="OutletName">The outlet name value.</param>
/// <param name="OperationId">The operation id value.</param>
/// <param name="EntryId">The entry id value.</param>
public sealed record VisualIdentity(
    object View,
    string? WindowId = null,
    string? OutletName = null,
    long? OperationId = null,
    string? EntryId = null)
{
    /// <summary>
    /// Gets or sets view.
    /// </summary>
    public object View { get; init; } = View ?? throw new ArgumentNullException(nameof(View));
}
