namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin load result.
/// </summary>
public sealed class PluginLoadResult
{
    private PluginLoadResult(
        PluginRuntime? runtime,
        PluginRuntimeState state,
        IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        Runtime = runtime;
        State = state;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets runtime.
    /// </summary>
    public PluginRuntime? Runtime { get; }

    /// <summary>
    /// Gets state.
    /// </summary>
    public PluginRuntimeState State { get; }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets succeeded.
    /// </summary>
    public bool Succeeded => Runtime is not null && Diagnostics.Count == 0;

    /// <summary>
    /// Executes the success operation.
    /// </summary>
    public static PluginLoadResult Success(PluginRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        return new PluginLoadResult(runtime, PluginRuntimeState.Loaded, []);
    }

    /// <summary>
    /// Executes the failed operation.
    /// </summary>
    public static PluginLoadResult Failed(IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        return new PluginLoadResult(null, PluginRuntimeState.Faulted, diagnostics);
    }
}
