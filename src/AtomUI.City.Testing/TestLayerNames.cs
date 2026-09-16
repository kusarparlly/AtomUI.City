using System.Collections.ObjectModel;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents test layer names.
/// </summary>
public static class TestLayerNames
{
    /// <summary>
    /// Represents the unit value.
    /// </summary>
    public const string Unit = nameof(Unit);
    /// <summary>
    /// Represents the contract value.
    /// </summary>
    public const string Contract = nameof(Contract);
    /// <summary>
    /// Represents the framework integration value.
    /// </summary>
    public const string FrameworkIntegration = nameof(FrameworkIntegration);
    /// <summary>
    /// Represents the runtime lifecycle value.
    /// </summary>
    public const string RuntimeLifecycle = nameof(RuntimeLifecycle);
    /// <summary>
    /// Represents the plugin lifecycle value.
    /// </summary>
    public const string PluginLifecycle = nameof(PluginLifecycle);
    /// <summary>
    /// Represents the platform integration value.
    /// </summary>
    public const string PlatformIntegration = nameof(PlatformIntegration);
    /// <summary>
    /// Represents the template smoke value.
    /// </summary>
    public const string TemplateSmoke = nameof(TemplateSmoke);
    /// <summary>
    /// Represents the generator value.
    /// </summary>
    public const string Generator = nameof(Generator);
    /// <summary>
    /// Represents the analyzer value.
    /// </summary>
    public const string Analyzer = nameof(Analyzer);
    /// <summary>
    /// Represents the build value.
    /// </summary>
    public const string Build = nameof(Build);

    /// <summary>
    /// Gets all categories.
    /// </summary>
    public static IReadOnlyList<string> AllCategories { get; } = new ReadOnlyCollection<string>(
        [
            Unit,
            Contract,
            FrameworkIntegration,
            RuntimeLifecycle,
            PluginLifecycle,
            PlatformIntegration,
            TemplateSmoke,
            Generator,
            Analyzer,
            Build,
        ]);

    /// <summary>
    /// Executes the is known category operation.
    /// </summary>
    public static bool IsKnownCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        return AllCategories.Contains(category, StringComparer.Ordinal);
    }

    /// <summary>
    /// Executes the get category operation.
    /// </summary>
    public static string GetCategory(TestLayer layer)
    {
        return layer switch
        {
            TestLayer.Unit => Unit,
            TestLayer.Contract => Contract,
            TestLayer.FrameworkIntegration => FrameworkIntegration,
            TestLayer.RuntimeLifecycle => RuntimeLifecycle,
            TestLayer.PluginLifecycle => PluginLifecycle,
            TestLayer.PlatformIntegration => PlatformIntegration,
            TestLayer.TemplateSmoke => TemplateSmoke,
            TestLayer.Generator => Generator,
            TestLayer.Analyzer => Analyzer,
            TestLayer.Build => Build,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown test layer."),
        };
    }
}
