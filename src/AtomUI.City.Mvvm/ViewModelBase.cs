using System.Runtime.CompilerServices;
using AtomUI.City.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents view model base.
/// </summary>
public abstract class ViewModelBase : ObservableValidator, IActivatable, IDisposable
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

        private set
        {
            lock (_stateSyncRoot)
            {
                _activationState = value;
            }
        }
    }

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
    public async ValueTask ActivateAsync(IActivationScope scope)
    {
        await ActivateAsync(scope, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    public async ValueTask ActivateAsync(IActivationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await ActivateAsync(new ActivationContext(scope), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    public async ValueTask ActivateAsync(ActivationContext context)
    {
        await ActivateAsync(context, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the activate async operation.
    /// </summary>
    public async ValueTask ActivateAsync(ActivationContext context, CancellationToken cancellationToken)
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

            if (cancellationToken.IsCancellationRequested)
            {
                AbortActivation(context);
                cancellationToken.ThrowIfCancellationRequested();
            }

            ActivationState = ActivationState.Activating;
            ActivationContext = context;
        }

        try
        {
            context.Scope.CancellationToken.ThrowIfCancellationRequested();

            await OnActivatedAsync(context, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            lock (_stateSyncRoot)
            {
                ThrowIfDisposed();
                context.Scope.CancellationToken.ThrowIfCancellationRequested();
                ActivationState = ActivationState.Active;
            }
        }
        catch (OperationCanceledException)
        {
            AbortActivation(context);
            throw;
        }
        catch (Exception exception)
        {
            EnrichActivationException(exception, context, "Activating");
            WriteActivationFailedDiagnostic(exception, context);
            AbortActivation(context);
            throw;
        }
    }

    /// <summary>
    /// Executes the deactivate async operation.
    /// </summary>
    public async ValueTask DeactivateAsync()
    {
        await DeactivateAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the deactivate async operation.
    /// </summary>
    public async ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
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
            ActivationState = ActivationState.Deactivating;
        }

        try
        {
            await OnDeactivatedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteDeactivationFailedDiagnostic(exception);
            throw;
        }
        finally
        {
            ActivationContext?.Scope.Dispose();

            lock (_stateSyncRoot)
            {
                ActivationContext = null;
                ActivationState = ActivationState.Deactivated;
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

            _disposed = true;
            scope = ActivationContext?.Scope;
            ActivationContext = null;
            ActivationState = ActivationState.Disposed;
        }

        try
        {
            scope?.Dispose();
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
    /// Gets on activated async.
    /// </summary>
    protected virtual ValueTask OnActivatedAsync(ActivationContext context) => OnActivatedAsync(context.Scope);

    /// <summary>
    /// Executes the on activated async operation.
    /// </summary>
    protected virtual ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnActivatedAsync(context);
    }

    /// <summary>
    /// Gets on activated async.
    /// </summary>
    protected virtual ValueTask OnActivatedAsync(IActivationScope scope) => ValueTask.CompletedTask;

    /// <summary>
    /// Gets on deactivated async.
    /// </summary>
    protected virtual ValueTask OnDeactivatedAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Executes the on deactivated async operation.
    /// </summary>
    protected virtual ValueTask OnDeactivatedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnDeactivatedAsync();
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

    private void AbortActivation(ActivationContext context)
    {
        context.Scope.Dispose();

        lock (_stateSyncRoot)
        {
            ActivationContext = null;
            ActivationState = ActivationState.Deactivated;
        }
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
        _diagnostics?.Write(new HostDiagnosticRecord(
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
        _diagnostics?.Write(new HostDiagnosticRecord(
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
