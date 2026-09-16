using System.Threading;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents activation scope accessor.
/// </summary>
public sealed class ActivationScopeAccessor
{
    private readonly AsyncLocal<IActivationScope?> _current = new();

    /// <summary>
    /// Gets current.
    /// </summary>
    public IActivationScope? Current => _current.Value;

    /// <summary>
    /// Executes the push operation.
    /// </summary>
    public IDisposable Push(IActivationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var previous = _current.Value;
        _current.Value = scope;

        return new RestoreHandle(this, previous);
    }

    private sealed class RestoreHandle : IDisposable
    {
        private readonly ActivationScopeAccessor _accessor;
        private readonly IActivationScope? _previous;
        private bool _disposed;

        public RestoreHandle(
            ActivationScopeAccessor accessor,
            IActivationScope? previous)
        {
            _accessor = accessor;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _accessor._current.Value = _previous;
        }
    }
}
