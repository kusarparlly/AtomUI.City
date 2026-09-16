namespace AtomUI.City.Presentation;

/// <summary>
/// Represents view factory context.
/// </summary>
public sealed class ViewFactoryContext
{
    /// <summary>
    /// Initializes a new instance of the <c>ViewFactoryContext</c> type.
    /// </summary>
    public ViewFactoryContext(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Services = services;
    }

    /// <summary>
    /// Gets services.
    /// </summary>
    public IServiceProvider Services { get; }
}
