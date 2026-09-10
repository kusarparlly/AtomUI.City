namespace AtomUI.City.Presentation;

public sealed record ViewModelAcquisitionRequest(
    Type ViewModelType,
    IServiceProvider Services,
    Func<IServiceProvider, CancellationToken, ValueTask<object>>? Factory = null,
    object? ReusableInstance = null)
{
    public Type ViewModelType { get; } = ViewModelType ?? throw new ArgumentNullException(nameof(ViewModelType));
    public IServiceProvider Services { get; } = Services ?? throw new ArgumentNullException(nameof(Services));
}

public interface IViewModelFactory
{
    ValueTask<ViewModelLease> AcquireAsync(
        ViewModelAcquisitionRequest request,
        CancellationToken cancellationToken = default);
}
