namespace AtomUI.City.Routing;

/// <summary>
/// Represents navigation target.
/// </summary>
public sealed class NavigationTarget
{
    private NavigationTarget(
        NavigationTargetKind kind,
        string? routeId,
        string? path,
        IReadOnlyDictionary<string, string> parameters,
        NavigationOptions options,
        IReadOnlyDictionary<string, object?>? restoredData = null)
    {
        Kind = kind;
        RouteId = routeId;
        Path = path;
        Parameters = RouteParameters.Copy(parameters);
        Options = options;
        RestoredData = restoredData is null
            ? null
            : new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(restoredData, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public NavigationTargetKind Kind { get; }

    /// <summary>
    /// Gets route id.
    /// </summary>
    public string? RouteId { get; }

    /// <summary>
    /// Gets path.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Gets options.
    /// </summary>
    public NavigationOptions Options { get; }

    internal IReadOnlyDictionary<string, object?>? RestoredData { get; }

    /// <summary>
    /// Executes the from path operation.
    /// </summary>
    public static NavigationTarget FromPath(
        string path,
        NavigationOptions options)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);

        return new NavigationTarget(
            NavigationTargetKind.Path,
            routeId: null,
            path,
            RouteParameters.Empty(),
            options);
    }

    /// <summary>
    /// Executes the from route reference operation.
    /// </summary>
    public static NavigationTarget FromRouteReference(
        string routeId,
        IReadOnlyDictionary<string, string>? parameters,
        NavigationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentNullException.ThrowIfNull(options);

        return new NavigationTarget(
            NavigationTargetKind.RouteReference,
            routeId,
            path: null,
            parameters ?? RouteParameters.Empty(),
            options);
    }

    /// <summary>
    /// Executes the from deep link operation.
    /// </summary>
    public static NavigationTarget FromDeepLink(Uri uri, NavigationOptions options)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(options);

        var raw = uri.IsAbsoluteUri ? uri.PathAndQuery + uri.Fragment : uri.OriginalString;
        var fragmentSeparator = raw.IndexOf('#', StringComparison.Ordinal);
        var fragment = fragmentSeparator < 0 ? string.Empty : raw[(fragmentSeparator + 1)..];
        if (fragmentSeparator >= 0)
        {
            raw = raw[..fragmentSeparator];
        }

        var querySeparator = raw.IndexOf('?', StringComparison.Ordinal);
        var path = querySeparator < 0 ? raw : raw[..querySeparator];
        var query = uri.IsAbsoluteUri
            ? uri.Query
            : querySeparator >= 0
                ? raw[(querySeparator + 1)..]
                : string.Empty;
        var parameters = new Dictionary<string, string>(ParseQuery(query), StringComparer.OrdinalIgnoreCase);
        if (fragment.Length > 0)
        {
            parameters["fragment"] = Uri.UnescapeDataString(fragment);
        }

        return new NavigationTarget(
            NavigationTargetKind.DeepLink,
            routeId: null,
            path,
            parameters,
            options);
    }

    internal static NavigationTarget FromJournal(NavigationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new NavigationTarget(
            NavigationTargetKind.Journal,
            routeId: null,
            path: null,
            RouteParameters.Empty(),
            options);
    }

    internal static NavigationTarget FromJournalEntry(
        string routeId,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, object?> resolvedData,
        NavigationOptions options)
    {
        return new NavigationTarget(
            NavigationTargetKind.RouteReference,
            routeId,
            path: null,
            parameters,
            options,
            resolvedData);
    }

    internal NavigationTarget InheritRedirectContextFrom(NavigationTarget source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var parameters = new Dictionary<string, string>(
            source.Parameters,
            StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in Parameters)
        {
            parameters[parameter.Key] = parameter.Value;
        }

        return new NavigationTarget(
            Kind,
            RouteId,
            Path,
            parameters,
            source.Options);
    }

    /// <summary>
    /// Executes the to string operation.
    /// </summary>
    public override string ToString()
    {
        return Kind switch
        {
            NavigationTargetKind.Path => Path ?? string.Empty,
            NavigationTargetKind.DeepLink => Path ?? string.Empty,
            NavigationTargetKind.RouteReference => RouteId ?? string.Empty,
            NavigationTargetKind.Journal => "journal",
            _ => Kind.ToString(),
        };
    }


    private static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1].Replace('+', ' '))
                : string.Empty;
            values[key] = value;
        }

        return RouteParameters.Copy(values);
    }

}
