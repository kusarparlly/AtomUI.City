using System.Collections.ObjectModel;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents route test match.
/// </summary>
public sealed class RouteTestMatch
{
    private RouteTestMatch(
        bool isMatch,
        string? routeName,
        Type? viewModelType,
        IReadOnlyDictionary<string, string> parameters,
        string? errorCode)
    {
        IsMatch = isMatch;
        RouteName = routeName;
        ViewModelType = viewModelType;
        Parameters = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase));
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets a value indicating whether is match.
    /// </summary>
    public bool IsMatch { get; }

    /// <summary>
    /// Gets route name.
    /// </summary>
    public string? RouteName { get; }

    /// <summary>
    /// Gets view model type.
    /// </summary>
    public Type? ViewModelType { get; }

    /// <summary>
    /// Gets parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Gets error code.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    internal static RouteTestMatch Success(RouteTestDefinition route, IReadOnlyDictionary<string, string> parameters)
    {
        return new RouteTestMatch(true, route.Name, route.ViewModelType, parameters, null);
    }

    /// <summary>
    /// Executes the not found operation.
    /// </summary>
    internal static RouteTestMatch NotFound()
    {
        return new RouteTestMatch(false, null, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "CITY-ROUTE-NOT-FOUND");
    }
}
