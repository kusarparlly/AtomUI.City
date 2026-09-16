namespace AtomUI.City.Presentation;

/// <summary>
/// Represents bound view handle.
/// </summary>
public sealed class BoundViewHandle : IDisposable
{
    private readonly Action? _dispose;
    private int _disposed;

    private BoundViewHandle(
        ViewDescriptor? descriptor,
        object view,
        object viewModel,
        Action? dispose)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(viewModel);

        Descriptor = descriptor;
        View = view;
        ViewModel = viewModel;
        _dispose = dispose;
    }

    /// <summary>
    /// Gets descriptor.
    /// </summary>
    public ViewDescriptor? Descriptor { get; }

    /// <summary>
    /// Gets view.
    /// </summary>
    public object View { get; }

    /// <summary>
    /// Gets view model.
    /// </summary>
    public object ViewModel { get; }

    /// <summary>
    /// Gets a value indicating whether is disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Executes the from existing operation.
    /// </summary>
    public static BoundViewHandle FromExisting(
        object view,
        object viewModel,
        Action? dispose = null)
    {
        return new BoundViewHandle(
            descriptor: null,
            view,
            viewModel,
            dispose);
    }

    internal static BoundViewHandle Create(
        ViewDescriptor descriptor,
        object view,
        object viewModel,
        Action? dispose)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new BoundViewHandle(
            descriptor,
            view,
            viewModel,
            dispose);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _dispose?.Invoke();
    }
}
