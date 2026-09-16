using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents activation scope.
/// </summary>
public sealed class ActivationScope : IActivationScope
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<IAsyncDisposable> _asyncDisposables = [];
    private readonly List<IDisposable> _disposables = [];
    private readonly IHostDiagnostics? _diagnostics;
    private readonly object _syncRoot = new();
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivationScope"/> type.
    /// </summary>
    public ActivationScope(IHostDiagnostics? diagnostics = null)
    {
        CancellationToken = _cancellationTokenSource.Token;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets id.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets a value indicating whether cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Represents the is disposed value.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_syncRoot)
            {
                return _isDisposed;
            }
        }
    }

    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public void Add(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        lock (_syncRoot)
        {
            if (!_isDisposed)
            {
                _disposables.Add(disposable);
                return;
            }
        }

        DisposeSubscription(disposable);
    }

    /// <summary>
    /// Executes the add async operation.
    /// </summary>
    public void AddAsync(IAsyncDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        lock (_syncRoot)
        {
            if (!_isDisposed)
            {
                _asyncDisposables.Add(disposable);
                return;
            }
        }

        DisposeSubscriptionSync(disposable);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        IAsyncDisposable[] asyncDisposables;
        IDisposable[] disposables;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            asyncDisposables = _asyncDisposables.ToArray();
            disposables = _disposables.ToArray();
            _asyncDisposables.Clear();
            _disposables.Clear();
        }

        _cancellationTokenSource.Cancel();

        for (var i = asyncDisposables.Length - 1; i >= 0; i--)
        {
            DisposeSubscriptionSync(asyncDisposables[i]);
        }

        for (var i = disposables.Length - 1; i >= 0; i--)
        {
            DisposeSubscription(disposables[i]);
        }

        _cancellationTokenSource.Dispose();
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        IAsyncDisposable[] asyncDisposables;
        IDisposable[] disposables;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            asyncDisposables = _asyncDisposables.ToArray();
            disposables = _disposables.ToArray();
            _asyncDisposables.Clear();
            _disposables.Clear();
        }

        await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

        for (var i = asyncDisposables.Length - 1; i >= 0; i--)
        {
            await DisposeSubscriptionAsync(asyncDisposables[i]).ConfigureAwait(false);
        }

        for (var i = disposables.Length - 1; i >= 0; i--)
        {
            DisposeSubscription(disposables[i]);
        }

        _cancellationTokenSource.Dispose();
    }

    private void DisposeSubscriptionSync(IAsyncDisposable disposable)
    {
        try
        {
            disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            WriteDisposeFailedDiagnostic(exception);
        }
    }

    private void DisposeSubscription(IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception exception)
        {
            WriteDisposeFailedDiagnostic(exception);
        }
    }

    private async ValueTask DisposeSubscriptionAsync(IAsyncDisposable disposable)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteDisposeFailedDiagnostic(exception);
        }
    }

    private void WriteDisposeFailedDiagnostic(Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            MvvmDiagnosticIds.ActivationScopeDisposeFailed,
            $"Activation scope '{Id}' resource disposal failed: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["scopeId"] = Id.ToString(),
            }
        });
    }
}
