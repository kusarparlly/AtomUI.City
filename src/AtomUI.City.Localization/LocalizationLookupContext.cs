namespace AtomUI.City.Localization;

/// <summary>
/// Represents localization lookup context.
/// </summary>
public sealed class LocalizationLookupContext
{
    /// <summary>
    /// Gets global.
    /// </summary>
    public static LocalizationLookupContext Global { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <c>LocalizationLookupContext</c> type.
    /// </summary>
    public LocalizationLookupContext(
        string? moduleId = null,
        string? pluginId = null,
        string? routeId = null,
        string? windowId = null)
    {
        ModuleId = Normalize(moduleId, nameof(moduleId));
        PluginId = Normalize(pluginId, nameof(pluginId));
        RouteId = Normalize(routeId, nameof(routeId));
        WindowId = Normalize(windowId, nameof(windowId));
    }

    /// <summary>
    /// Gets module id.
    /// </summary>
    public string? ModuleId { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Gets route id.
    /// </summary>
    public string? RouteId { get; }

    /// <summary>
    /// Gets window id.
    /// </summary>
    public string? WindowId { get; }

    internal IReadOnlyList<LocalizationScopeKey> GetScopeKeys()
    {
        var keys = new List<LocalizationScopeKey>(4);
        AddIfPresent(keys, ResourceScope.Module, ModuleId);
        AddIfPresent(keys, ResourceScope.Plugin, PluginId);
        AddIfPresent(keys, ResourceScope.Route, RouteId);
        AddIfPresent(keys, ResourceScope.Window, WindowId);

        return keys;
    }

    internal bool Matches(LanguagePackageDescriptor descriptor)
    {
        return descriptor.Scope switch
        {
            ResourceScope.Host or ResourceScope.Presentation => true,
            ResourceScope.Module => Matches(descriptor.ScopeId, ModuleId),
            ResourceScope.Plugin => Matches(descriptor.ScopeId, PluginId),
            ResourceScope.Route => Matches(descriptor.ScopeId, RouteId),
            ResourceScope.Window => Matches(descriptor.ScopeId, WindowId),
            _ => false,
        };
    }

    private static string? Normalize(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        return value;
    }

    private static void AddIfPresent(
        ICollection<LocalizationScopeKey> keys,
        ResourceScope scope,
        string? scopeId)
    {
        if (scopeId is not null)
        {
            keys.Add(new LocalizationScopeKey(scope, scopeId));
        }
    }

    private static bool Matches(string? descriptorScopeId, string? contextScopeId)
    {
        return contextScopeId is not null
            && string.Equals(descriptorScopeId, contextScopeId, StringComparison.Ordinal);
    }
}

internal readonly record struct LocalizationScopeKey(ResourceScope Scope, string ScopeId);
