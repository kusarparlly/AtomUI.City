namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents lifecycle context.
/// </summary>
public sealed class LifecycleContext
{
    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();

        private NullServiceProvider()
        {
        }

        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    /// <summary>
    /// Initializes a new instance of the lifecycle context class.
    /// </summary>
    public LifecycleContext(
        LifecycleStage stage,
        IServiceProvider? services = null,
        string? operationId = null)
    {
        stage.ThrowIfInvalid(nameof(stage));

        if (operationId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId, nameof(operationId));
        }

        Stage = stage;
        Services = services ?? NullServiceProvider.Instance;
        OperationId = operationId ?? Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Gets the stage value.
    /// </summary>
    public LifecycleStage Stage { get; }

    /// <summary>
    /// Gets the services value.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the operation id value.
    /// </summary>
    public string OperationId { get; }

    /// <summary>
    /// Gets the items value.
    /// </summary>
    public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    /// <summary>
    /// Gets or sets the is short circuited value.
    /// </summary>
    public bool IsShortCircuited { get; private set; }

    /// <summary>
    /// Executes the short circuit operation.
    /// </summary>
    public void ShortCircuit()
    {
        IsShortCircuited = true;
    }
}
