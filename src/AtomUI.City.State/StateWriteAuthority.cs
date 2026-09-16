using System.Collections.Frozen;

namespace AtomUI.City.State;

/// <summary>
/// Represents state write authority.
/// </summary>
public sealed class StateWriteAuthority
{
    private readonly FrozenSet<string> _capabilities;

    private StateWriteAuthority(
        StateWriteAuthorityKind kind,
        string? moduleName,
        string? pluginId,
        IEnumerable<string>? capabilities)
    {
        Kind = kind;
        ModuleName = moduleName;
        PluginId = pluginId;
        _capabilities = (capabilities ?? []).ToFrozenSet(StringComparer.Ordinal);

        if (_capabilities.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Write capabilities must not contain null or whitespace values.", nameof(capabilities));
        }
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public StateWriteAuthorityKind Kind { get; }

    /// <summary>
    /// Gets module name.
    /// </summary>
    public string? ModuleName { get; }

    /// <summary>
    /// Gets plugin id.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Gets capabilities.
    /// </summary>
    public IReadOnlySet<string> Capabilities => _capabilities;

    internal static StateWriteAuthority Host(IEnumerable<string>? capabilities = null)
    {
        return new StateWriteAuthority(StateWriteAuthorityKind.Host, null, null, capabilities);
    }

    /// <summary>
    /// Executes the module operation.
    /// </summary>
    public static StateWriteAuthority Module(
        string moduleName,
        IEnumerable<string>? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        return new StateWriteAuthority(StateWriteAuthorityKind.Module, moduleName, null, capabilities);
    }

    /// <summary>
    /// Executes the plugin operation.
    /// </summary>
    public static StateWriteAuthority Plugin(
        string pluginId,
        IEnumerable<string>? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return new StateWriteAuthority(StateWriteAuthorityKind.Plugin, null, pluginId, capabilities);
    }

    internal bool HasCapability(string capability)
    {
        return _capabilities.Contains(capability);
    }
}
