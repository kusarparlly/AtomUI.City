namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin unload result.
/// </summary>
public sealed class PluginUnloadResult
{
    private PluginUnloadResult(
        PluginRuntimeState state,
        IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        State = state;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

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
    public bool Succeeded => State == PluginRuntimeState.Unloaded && Diagnostics.Count == 0;

    /// <summary>
    /// Gets success.
    /// </summary>
    public static PluginUnloadResult Success { get; } = new(PluginRuntimeState.Unloaded, []);

    /// <summary>
    /// Executes the pending operation.
    /// </summary>
    public static PluginUnloadResult Pending(IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        return new PluginUnloadResult(PluginRuntimeState.UnloadPending, diagnostics);
    }
}
