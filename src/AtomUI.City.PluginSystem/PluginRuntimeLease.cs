namespace AtomUI.City.PluginSystem;

/// <summary>
/// Represents plugin runtime lease.
/// </summary>
public sealed class PluginRuntimeLease
{
    private readonly Func<CancellationToken, ValueTask> _revokeAsync;

    internal PluginRuntimeLease(
        string leaseId,
        string pluginId,
        string kind,
        Func<CancellationToken, ValueTask> revokeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(revokeAsync);

        LeaseId = leaseId;
        PluginId = pluginId;
        Kind = kind;
        _revokeAsync = revokeAsync;
        State = PluginRuntimeLeaseState.Active;
    }

    /// <summary>
    /// Gets lease id.
    /// </summary>
    public string LeaseId { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets or sets state.
    /// </summary>
    public PluginRuntimeLeaseState State { get; private set; }

    internal async ValueTask<IReadOnlyList<PluginDiagnostic>> RevokeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State == PluginRuntimeLeaseState.Revoked)
        {
            return [];
        }

        State = PluginRuntimeLeaseState.Revoking;

        try
        {
            await _revokeAsync(cancellationToken).ConfigureAwait(false);
            State = PluginRuntimeLeaseState.Revoked;

            return [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = PluginRuntimeLeaseState.RevokeFailed;

            return
            [
                new PluginDiagnostic(
                    PluginDiagnosticIds.PluginUnloadPending,
                    $"Plugin runtime lease '{LeaseId}' failed to revoke: {exception.Message}",
                    PluginId,
                    Kind,
                    LeaseId),
            ];
        }
    }
}
