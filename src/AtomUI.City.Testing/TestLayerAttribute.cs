namespace AtomUI.City.Testing;

/// <summary>
/// Represents test layer.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TestLayerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <c>TestLayerAttribute</c> type.
    /// </summary>
    public TestLayerAttribute(TestLayer layer)
    {
        Layer = layer;
        Category = TestLayerNames.GetCategory(layer);
    }

    /// <summary>
    /// Initializes a new instance of the <c>TestLayerAttribute</c> type.
    /// </summary>
    public TestLayerAttribute(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        if (!Enum.TryParse<TestLayer>(category, ignoreCase: false, out var layer)
            || !string.Equals(TestLayerNames.GetCategory(layer), category, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unknown test layer category '{category}'.", nameof(category));
        }

        Layer = layer;
        Category = category;
    }

    /// <summary>
    /// Gets layer.
    /// </summary>
    public TestLayer Layer { get; }

    /// <summary>
    /// Gets category.
    /// </summary>
    public string Category { get; }
}
