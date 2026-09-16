namespace AtomUI.City.Presentation;

/// <summary>
/// Represents view model acquisition request.
/// </summary>
/// <param name="ViewModelType">The view model type value.</param>
/// <param name="Services">The services value.</param>
/// <param name="Factory">The factory value.</param>
/// <param name="ReusableInstance">The reusable instance value.</param>
public sealed record ViewModelAcquisitionRequest(
    Type ViewModelType,
    IServiceProvider Services,
    Func<IServiceProvider, CancellationToken, ValueTask<object>>? Factory = null,
    object? ReusableInstance = null)
{
    /// <summary>
    /// Gets view model type.
    /// </summary>
    public Type ViewModelType { get; } = ViewModelType ?? throw new ArgumentNullException(nameof(ViewModelType));
    /// <summary>
    /// Gets services.
    /// </summary>
    public IServiceProvider Services { get; } = Services ?? throw new ArgumentNullException(nameof(Services));
}

/// <summary>
/// Defines the contract for iview model factory.
/// </summary>
public interface IViewModelFactory
{
    /// <summary>
    /// Executes the acquire async operation.
    /// </summary>
    ValueTask<ViewModelLease> AcquireAsync(
        ViewModelAcquisitionRequest request,
        CancellationToken cancellationToken = default);
}
