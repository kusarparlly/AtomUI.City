using System.Runtime.CompilerServices;
using AtomUI.City.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents view model base.
/// </summary>
public abstract class ViewModelBase :
    ObservableValidator,
    IActivatable,
    IActivationScopeOwner,
    IDisposable,
    IAsyncDisposable
{
    private readonly IHostDiagnostics? _diagnostics;
    private readonly object _stateSyncRoot = new();
    private bool _disposed;
    private ActivationState _activationState = ActivationState.Constructed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> type.
    /// </summary>
    protected ViewModelBase(IHostDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Represents the activation state value.
    /// </summary>
    public ActivationState ActivationState
    {
        get
        {
            lock (_stateSyncRoot)
            {
                return _activationState;
            }
        }

    }

    /// <inheritdoc />
    public event EventHandler? ActivationStateChanged;

    /// <summary>
    /// Gets a value indicating whether is active.
    /// </summary>
    public bool IsActive => ActivationState == ActivationState.Active;

    /// <summary>
    /// Represents the is disposed value.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_stateSyncRoot)
            {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// Gets current activation scope.
    /// </summary>
    public IActivationScope? CurrentActivationScope => ActivationContext?.Scope;

    /// <summary>
    /// Gets or sets activation context.
    /// </summary>
    public ActivationContext? ActivationContext { get; private set; }

    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    public async ValueTask ActivateAsync(
        IActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await ActivateAsync(new ActivationContext(scope), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    public async ValueTask ActivateAsync(
        ActivationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_stateSyncRoot)
        {
            ThrowIfDisposed();

            if (ActivationState == ActivationState.Active)
            {
                return;
            }

            if (ActivationState is ActivationState.Activating or ActivationState.Deactivating)
            {
                throw new InvalidOperationException(
                    $"ViewModel activation is already in progress ({ActivationState}).");
            }

            _activationState = ActivationState.Activating;
            ActivationContext = context;
        }
        OnActivationStateChanged();

        using var effectiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            context.Scope.CancellationToken);
        var effectiveCancellationToken = effectiveCancellation.Token;

        try
        {
            effectiveCancellationToken.ThrowIfCancellationRequested();

            await OnActivatedAsync(context, effectiveCancellationToken).ConfigureAwait(false);

            effectiveCancellationToken.ThrowIfCancellationRequested();

            lock (_stateSyncRoot)
            {
                ThrowIfDisposed();
                effectiveCancellationToken.ThrowIfCancellationRequested();
                _activationState = ActivationState.Active;
            }
            OnActivationStateChanged();
        }
        catch (OperationCanceledException exception)
        {
            await AbortActivationAsync(context, exception).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            EnrichActivationException(exception, context, "Activating");
            WriteActivationFailedDiagnostic(exception, context);
            await AbortActivationAsync(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Executes the deactivate async operation.
    /// </summary>
    public async ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        IActivationScope? scope;

        lock (_stateSyncRoot)
        {
            ThrowIfDisposed();

            if (ActivationState is ActivationState.Constructed
                or ActivationState.Deactivated
                or ActivationState.Deactivating)
            {
                return;
            }

            if (ActivationState is ActivationState.Activating)
            {
                throw new InvalidOperationException(
                    "ViewModel deactivation conflicts with an activation in progress.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            _activationState = ActivationState.Deactivating;
            scope = ActivationContext?.Scope;
        }
        OnActivationStateChanged();

        using var effectiveCancellation = scope is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                scope.CancellationToken);

        try
        {
            await OnDeactivatedAsync(effectiveCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteDeactivationFailedDiagnostic(exception);
            throw;
        }
        finally
        {
            try
            {
                if (scope is not null)
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                var stateChanged = false;
                lock (_stateSyncRoot)
                {
                    ActivationContext = null;
                    if (!_disposed)
                    {
                        _activationState = ActivationState.Deactivated;
                        stateChanged = true;
                    }
                }

                if (stateChanged)
                {
                    OnActivationStateChanged();
                }
            }
        }
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        IActivationScope? scope;

        lock (_stateSyncRoot)
        {
            if (_disposed)
            {
                return;
            }

            scope = ActivationContext?.Scope;
        }

        // A synchronous scope disposal can reject asynchronous ownership. Perform
        // it before committing the ViewModel's terminal state so DisposeAsync can
        // still recover through the retained activation context.
        scope?.Dispose();

        lock (_stateSyncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ActivationContext = null;
            _activationState = ActivationState.Disposed;
        }
        OnActivationStateChanged();

        try
        {
            OnDisposed();
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Asynchronously releases the current activation session and then the ViewModel.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        IActivationScope? scope;

        lock (_stateSyncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            scope = ActivationContext?.Scope;
            ActivationContext = null;
            _activationState = ActivationState.Disposed;
        }
        OnActivationStateChanged();

        try
        {
            if (scope is not null)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            OnDisposed();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Executes the set property&lt;t&gt; operation.
    /// </summary>
    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        [CallerMemberName] string? propertyName = null)
    {
        return SetProperty(ref field, newValue, EqualityComparer<T>.Default, propertyName);
    }

    /// <summary>
    /// Executes the set property&lt;t&gt; operation.
    /// </summary>
    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        IEqualityComparer<T> comparer,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return base.SetProperty(ref field, newValue, comparer, propertyName);
    }

    /// <summary>
    /// Executes the set property&lt;t&gt; operation.
    /// </summary>
    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        bool validate,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return base.SetProperty(ref field, newValue, validate, propertyName);
    }

    /// <summary>
    /// Executes the set property&lt;t&gt; operation.
    /// </summary>
    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        IEqualityComparer<T> comparer,
        bool validate,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return base.SetProperty(ref field, newValue, comparer, validate, propertyName);
    }

    /// <summary>
    /// Runs activation with the framework-composed effective cancellation token.
    /// </summary>
    protected virtual ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the on deactivated async operation.
    /// </summary>
    protected virtual ValueTask OnDeactivatedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the on disposed operation.
    /// </summary>
    protected virtual void OnDisposed()
    {
    }

    /// <summary>
    /// Executes the throw if disposed operation.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private async ValueTask AbortActivationAsync(
        ActivationContext context,
        Exception? primaryException = null)
    {
        try
        {
            await context.Scope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            WriteActivationScopeCleanupFailedDiagnostic(cleanupException, context);
            if (primaryException is null)
            {
                throw;
            }

            AddExceptionData(
                primaryException,
                "AtomUI.City.Mvvm.ActivationCleanupException",
                cleanupException);
        }

        var stateChanged = false;
        lock (_stateSyncRoot)
        {
            if (!_disposed)
            {
                ActivationContext = null;
                _activationState = ActivationState.Deactivated;
                stateChanged = true;
            }
        }

        if (stateChanged)
        {
            OnActivationStateChanged();
        }
    }

    private void OnActivationStateChanged()
    {
        var handlers = ActivationStateChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
                    MvvmDiagnosticIds.ActivationStateNotificationFailed,
                    $"Activation state observer failed for '{GetType().FullName}': {exception.Message}",
                    HostDiagnosticSeverity.Error)
                {
                    Context = new Dictionary<string, string?>
                    {
                        ["viewModelType"] = GetType().FullName,
                        ["activationState"] = ActivationState.ToString(),
                        ["exceptionType"] = exception.GetType().FullName,
                    }
                });
            }
        }
    }

    private void WriteActivationScopeCleanupFailedDiagnostic(
        Exception exception,
        ActivationContext context)
    {
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.ActivationScopeDisposeFailed,
            $"Activation scope '{context.Scope.Id}' cleanup failed for '{GetType().FullName}': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["viewModelType"] = GetType().FullName,
                ["scopeId"] = context.Scope.Id.ToString(),
            }
        });
    }

    private void EnrichActivationException(
        Exception exception,
        ActivationContext context,
        string stage)
    {
        AddExceptionData(exception, "AtomUI.City.Mvvm.ViewModelType", GetType().FullName);
        AddExceptionData(exception, "AtomUI.City.Mvvm.ActivationStage", stage);
        AddExceptionData(exception, "AtomUI.City.Mvvm.ScopeId", context.Scope.Id);
    }

    private void WriteActivationFailedDiagnostic(Exception exception, ActivationContext context)
    {
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.ActivationFailed,
            $"ViewModel activation failed for '{GetType().FullName}' at stage 'Activating': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["viewModelType"] = GetType().FullName,
                ["scopeId"] = context.Scope.Id.ToString(),
                ["stage"] = "Activating",
            }
        });
    }

    private void WriteDeactivationFailedDiagnostic(Exception exception)
    {
        MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
            MvvmDiagnosticIds.DeactivationFailed,
            $"ViewModel deactivation handler failed for '{GetType().FullName}': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["viewModelType"] = GetType().FullName,
            }
        });
    }

    private static void AddExceptionData(Exception exception, string key, object? value)
    {
        if (exception.Data.Contains(key))
        {
            return;
        }

        exception.Data[key] = value;
    }
}
