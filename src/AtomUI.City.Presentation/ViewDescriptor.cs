namespace AtomUI.City.Presentation;

/// <summary>
/// Represents view descriptor.
/// </summary>
public sealed class ViewDescriptor
{
    private readonly Func<ViewFactoryContext, object> _viewFactory;

    /// <summary>
    /// Initializes a new instance of the <c>ViewDescriptor</c> type.
    /// </summary>
    public ViewDescriptor(
        Type viewModelType,
        Type viewType,
        string? viewKey,
        Func<ViewFactoryContext, object> viewFactory,
        string? pluginId = null,
        string? contributionId = null,
        IReadOnlyList<Type>? constructorParameterTypes = null)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ArgumentNullException.ThrowIfNull(viewType);
        ArgumentNullException.ThrowIfNull(viewFactory);

        ViewModelType = viewModelType;
        ViewType = viewType;
        ViewKey = string.IsNullOrWhiteSpace(viewKey) ? null : viewKey;
        PluginId = string.IsNullOrWhiteSpace(pluginId) ? null : pluginId;
        ContributionId = string.IsNullOrWhiteSpace(contributionId) ? null : contributionId;
        ConstructorParameterTypes = Array.AsReadOnly((constructorParameterTypes ?? []).ToArray());
        _viewFactory = viewFactory;
    }

    /// <summary>
    /// Gets view model type.
    /// </summary>
    public Type ViewModelType { get; }

    /// <summary>
    /// Gets view type.
    /// </summary>
    public Type ViewType { get; }

    /// <summary>
    /// Gets view key.
    /// </summary>
    public string? ViewKey { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    /// <summary>
    /// Gets constructor parameter types.
    /// </summary>
    public IReadOnlyList<Type> ConstructorParameterTypes { get; }

    /// <summary>
    /// Executes the create view operation.
    /// </summary>
    public object CreateView(ViewFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var view = _viewFactory(context);

        if (view is null)
        {
            throw new PresentationException(
                PresentationError.ViewCreationFailed,
                $"View factory for '{ViewModelType.FullName}' returned null, expected '{ViewType.FullName}'.");
        }

        if (!ViewType.IsInstanceOfType(view))
        {
            throw new PresentationException(
                PresentationError.ViewCreationFailed,
                $"View factory for '{ViewModelType.FullName}' returned '{view.GetType().FullName}', expected '{ViewType.FullName}'.");
        }

        return view;
    }
}
