namespace AtomUI.City.State;

/// <summary>
/// Represents state scope accessor.
/// </summary>
public sealed class StateScopeAccessor : IStateScopeAccessor
{
    private readonly AsyncLocal<IStateScope?> _current = new();

    /// <summary>
    /// Gets current.
    /// </summary>
    public IStateScope? Current => _current.Value;

    /// <summary>
    /// Executes the push operation.
    /// </summary>
    public IDisposable Push(IStateScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var previous = _current.Value;
        _current.Value = scope;

        return new RestoreHandle(this, previous);
    }

    private sealed class RestoreHandle : IDisposable
    {
        private readonly StateScopeAccessor _accessor;
        private readonly IStateScope? _previous;
        private int _disposed;

        public RestoreHandle(StateScopeAccessor accessor, IStateScope? previous)
        {
            _accessor = accessor;
            _previous = previous;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _accessor._current.Value = _previous;
        }
    }
}
