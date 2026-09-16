namespace AtomUI.City.Presentation;

/// <summary>
/// Represents view for.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ViewForAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>ViewForAttribute</c> type.
    /// </summary>
    public ViewForAttribute(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        ViewModelType = viewModelType;
    }

    /// <summary>
    /// Gets view model type.
    /// </summary>
    public Type ViewModelType { get; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Gets or sets plugin id.
    /// </summary>
    public string? PluginId { get; init; }

    /// <summary>
    /// Gets or sets contribution id.
    /// </summary>
    public string? ContributionId { get; init; }
}
