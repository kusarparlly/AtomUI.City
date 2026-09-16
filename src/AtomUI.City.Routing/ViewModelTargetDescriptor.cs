namespace AtomUI.City.Routing;

/// <summary>
/// Represents view model target descriptor.
/// </summary>
public sealed class ViewModelTargetDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>ViewModelTargetDescriptor</c> type.
    /// </summary>
    public ViewModelTargetDescriptor(Type viewModelType)
        : this(viewModelType, parameterBindings: null, reuseKey: null, activationHint: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>ViewModelTargetDescriptor</c> type.
    /// </summary>
    public ViewModelTargetDescriptor(
        Type viewModelType,
        IReadOnlyList<string>? parameterBindings,
        string? reuseKey = null,
        string? activationHint = null)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        ViewModelType = viewModelType;
        ParameterBindings = AsReadOnly(parameterBindings);
        ReuseKey = string.IsNullOrWhiteSpace(reuseKey) ? null : reuseKey;
        ActivationHint = string.IsNullOrWhiteSpace(activationHint) ? null : activationHint;
    }

    /// <summary>
    /// Gets view model type.
    /// </summary>
    public Type ViewModelType { get; }

    /// <summary>
    /// Gets parameter bindings.
    /// </summary>
    public IReadOnlyList<string> ParameterBindings { get; }

    /// <summary>
    /// Gets reuse key.
    /// </summary>
    public string? ReuseKey { get; }

    /// <summary>
    /// Gets activation hint.
    /// </summary>
    public string? ActivationHint { get; }

    private static IReadOnlyList<string> AsReadOnly(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        foreach (var value in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(values));
        }

        return Array.AsReadOnly(values.ToArray());
    }
}
