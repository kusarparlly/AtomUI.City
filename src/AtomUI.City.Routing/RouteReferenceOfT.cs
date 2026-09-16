namespace AtomUI.City.Routing;

/// <summary>
/// Represents route reference&lt;tparameters&gt;.
/// </summary>
public readonly record struct RouteReference<TParameters>
{
    /// <summary>
    /// Executes the route reference operation.
    /// </summary>
    public RouteReference(string id)
        : this(id, parameterBinder: null)
    {
    }

    /// <summary>
    /// Executes the route reference operation.
    /// </summary>
    public RouteReference(
        string id,
        Func<TParameters, IReadOnlyDictionary<string, string>>? parameterBinder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        ParameterBinder = parameterBinder;
    }

    /// <summary>
    /// Gets id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets parameter binder.
    /// </summary>
    public Func<TParameters, IReadOnlyDictionary<string, string>>? ParameterBinder { get; }

    /// <summary>
    /// Executes the bind parameters operation.
    /// </summary>
    public IReadOnlyDictionary<string, string> BindParameters(TParameters parameters)
    {
        return ParameterBinder is null
            ? RouteParameters.Empty()
            : RouteParameters.Copy(ParameterBinder(parameters));
    }

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() => Id ?? string.Empty;
}
