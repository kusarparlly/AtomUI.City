namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iview locator.
/// </summary>
public interface IViewLocator
{
    /// <summary>
    /// Executes the try locate operation.
    /// </summary>
    bool TryLocate(
        Type viewModelType,
        string? viewKey,
        out ViewDescriptor? descriptor);

    /// <summary>
    /// Executes the try locate operation.
    /// </summary>
    bool TryLocate(
        ViewLookupRequest request,
        out ViewDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryLocate(request.ViewModelType, request.ViewKey, out descriptor);
    }

    /// <summary>
    /// Executes the locate operation.
    /// </summary>
    ViewDescriptor Locate(Type viewModelType, string? viewKey = null);

    /// <summary>
    /// Executes the locate operation.
    /// </summary>
    ViewDescriptor Locate(ViewLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Locate(request.ViewModelType, request.ViewKey);
    }
}
