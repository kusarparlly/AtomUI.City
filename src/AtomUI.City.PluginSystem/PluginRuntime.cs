using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace AtomUI.City.PluginSystem;

public sealed class PluginRuntime
{
    private readonly List<PluginRuntimeLease> _leases = [];
    private Assembly? _mainAssembly;
    private AssemblyLoadContext? _loadContext;

    internal PluginRuntime(
        PluginDescriptor descriptor,
        Assembly mainAssembly,
        AssemblyLoadContext loadContext)
    {
        Descriptor = descriptor;
        _mainAssembly = mainAssembly;
        _loadContext = loadContext;
        State = PluginRuntimeState.Loaded;
    }

    public PluginDescriptor Descriptor { get; }

    public PluginRuntimeState State { get; private set; }

    public Assembly MainAssembly => _mainAssembly ??
        throw new InvalidOperationException("Plugin main assembly is not available after unload.");

    public IReadOnlyList<PluginRuntimeLease> Leases => Array.AsReadOnly(_leases.ToArray());

    public PluginRuntimeLease RegisterUnloadLease(
        string leaseId,
        string kind,
        Func<CancellationToken, ValueTask> revokeAsync)
    {
        if (State is PluginRuntimeState.Unloading or PluginRuntimeState.Unloaded or PluginRuntimeState.UnloadPending)
        {
            throw new InvalidOperationException($"Plugin cannot register unload leases from state '{State}'.");
        }

        var lease = new PluginRuntimeLease(leaseId, Descriptor.PluginId, kind, revokeAsync);
        _leases.Add(lease);

        return lease;
    }

    public void Activate()
    {
        if (State is not (PluginRuntimeState.Loaded or PluginRuntimeState.Inactive))
        {
            throw new InvalidOperationException($"Plugin cannot be activated from state '{State}'.");
        }

        State = PluginRuntimeState.Active;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State == PluginRuntimeState.Active)
        {
            State = PluginRuntimeState.Deactivating;
            State = PluginRuntimeState.Inactive;
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<PluginUnloadResult> UnloadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State == PluginRuntimeState.Active)
        {
            await DeactivateAsync(cancellationToken).ConfigureAwait(false);
        }

        if (State == PluginRuntimeState.Unloaded)
        {
            return PluginUnloadResult.Success;
        }

        State = PluginRuntimeState.Unloading;
        var diagnostics = new List<PluginDiagnostic>();
        for (var i = _leases.Count - 1; i >= 0; i--)
        {
            diagnostics.AddRange(await _leases[i].RevokeAsync(cancellationToken).ConfigureAwait(false));
        }

        if (diagnostics.Count > 0 ||
            _leases.Any(lease => lease.State != PluginRuntimeLeaseState.Revoked))
        {
            State = PluginRuntimeState.UnloadPending;
            return PluginUnloadResult.Pending(diagnostics);
        }

        _leases.Clear();
        _mainAssembly = null;
        var unloadReference = BeginUnload(_loadContext);
        _loadContext = null;

        for (var attempt = 0; unloadReference?.IsAlive == true && attempt < 10; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        State = PluginRuntimeState.Unloaded;
        return PluginUnloadResult.Success;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference? BeginUnload(AssemblyLoadContext? loadContext)
    {
        if (loadContext is null)
        {
            return null;
        }

        var unloadReference = new WeakReference(loadContext, trackResurrection: true);
        loadContext.Unload();
        return unloadReference;
    }
}
