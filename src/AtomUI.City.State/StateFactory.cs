using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.State;

/// <summary>
/// Represents state factory.
/// </summary>
public sealed class StateFactory : IStateFactory
{
    private readonly IStateScopeAccessor _scopeAccessor;
    private readonly IHostDiagnostics? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <c>StateFactory</c> type.
    /// </summary>
    public StateFactory(
        IStateScopeAccessor scopeAccessor,
        IHostDiagnostics? diagnostics = null)
    {
        _scopeAccessor = scopeAccessor ?? throw new ArgumentNullException(nameof(scopeAccessor));
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the create writable&lt;t&gt; operation.
    /// </summary>
    public WritableState<T> CreateWritable<T>(
        T initialValue,
        IEqualityComparer<T>? comparer = null,
        string? stateName = null,
        StateAccessPolicy access = StateAccessPolicy.HostWrite)
    {
        var state = new WritableState<T>(initialValue, comparer, _diagnostics, stateName, access);
        _scopeAccessor.Current?.Add(state);
        return state;
    }

    /// <summary>
    /// Executes the create computed&lt;t&gt; operation.
    /// </summary>
    public ComputedState<T> CreateComputed<T>(
        Func<T> compute,
        params IReadOnlyState[] dependencies)
    {
        var state = new ComputedState<T>(compute, _diagnostics, dependencies);
        _scopeAccessor.Current?.Add(state);
        return state;
    }

    /// <summary>
    /// Executes the create scope operation.
    /// </summary>
    public StateScope CreateScope(string id)
    {
        var scope = new StateScope(id, _diagnostics);
        _scopeAccessor.Current?.Add(scope);
        return scope;
    }
}
