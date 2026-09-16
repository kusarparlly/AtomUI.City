using System.Runtime.ExceptionServices;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.State;

/// <summary>
/// Represents computed state&lt;t&gt;.
/// </summary>
public sealed class ComputedState<T> : IComputedState<T>, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Func<T> _compute;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly List<StateSubscription> _subscriptions = [];
    private readonly List<IStateSubscription> _dependencySubscriptions = [];
    private readonly Queue<StateChangedEventArgs<T>> _pendingNotifications = [];
    private bool _hasComputeFailure;
    private bool _hasValue;
    private bool _isDirty = true;
    private bool _isDisposed;
    private bool _notificationWorkerActive;
    private long _invalidationVersion;
    private long _version;
    private Exception? _lastError;
    private T? _value;

    /// <summary>
    /// Executes the computed state operation.
    /// </summary>
    public ComputedState(Func<T> compute, params IReadOnlyState[] dependencies)
        : this(compute, diagnostics: null, dependencies)
    {
    }

    /// <summary>
    /// Executes the computed state operation.
    /// </summary>
    public ComputedState(
        Func<T> compute,
        IHostDiagnostics? diagnostics,
        params IReadOnlyState[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(compute);
        ArgumentNullException.ThrowIfNull(dependencies);

        _compute = compute;
        _diagnostics = diagnostics;

        foreach (var dependency in dependencies)
        {
            if (dependency is null)
            {
                throw new ArgumentException("Computed state dependencies must not contain null.", nameof(dependencies));
            }
        }

        try
        {
            foreach (var dependency in dependencies)
            {
                _dependencySubscriptions.Add(dependency.OnChange(_ => InvalidateAndNotify()));
            }
        }
        catch
        {
            foreach (var subscription in _dependencySubscriptions)
            {
                DisposeSubscription(subscription);
            }

            _dependencySubscriptions.Clear();
            throw;
        }
    }

    /// <summary>
    /// Gets value.
    /// </summary>
    public T Value => EnsureValue().Value;

    object? IReadOnlyState.Value => Value;

    /// <summary>
    /// Represents the version value.
    /// </summary>
    public long Version
    {
        get
        {
            lock (_syncRoot)
            {
                return _version;
            }
        }
    }

    /// <summary>
    /// Gets value type.
    /// </summary>
    public Type ValueType => typeof(T);

    /// <summary>
    /// Represents the last error value.
    /// </summary>
    public Exception? LastError
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastError;
            }
        }
    }

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    public IStateSubscription OnChange(Action<StateChangedEventArgs<T>> handler)
    {
        return OnChange(handler, StateSubscriptionOptions.Immediate);
    }

    /// <summary>
    /// Executes the on change operation.
    /// </summary>
    public IStateSubscription OnChange(
        Action<StateChangedEventArgs<T>> handler,
        StateSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        _ = EnsureValue();

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            var subscription = new StateSubscription(
                args => handler((StateChangedEventArgs<T>)args),
                options,
                _diagnostics);

            _subscriptions.Add(subscription);

            return new RemovingStateSubscription(this, subscription);
        }
    }

    IStateSubscription IReadOnlyState.OnChange(Action<StateChangedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return OnChange(args => handler(args));
    }

    IStateSubscription IReadOnlyState.OnChange(
        Action<StateChangedEventArgs> handler,
        StateSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return OnChange(args => handler(args), options);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        IStateSubscription[] dependencySubscriptions;
        StateSubscription[] subscriptions;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            dependencySubscriptions = _dependencySubscriptions.ToArray();
            subscriptions = _subscriptions.ToArray();
            _dependencySubscriptions.Clear();
            _subscriptions.Clear();
            _pendingNotifications.Clear();
        }

        foreach (var subscription in dependencySubscriptions)
        {
            DisposeSubscription(subscription);
        }

        foreach (var subscription in subscriptions)
        {
            DisposeSubscription(subscription);
        }
    }

    private void InvalidateAndNotify()
    {
        var shouldProcessNotifications = false;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDirty = true;
            _invalidationVersion++;

            if (_subscriptions.Count > 0 && !_notificationWorkerActive)
            {
                _notificationWorkerActive = true;
                shouldProcessNotifications = true;
            }
        }

        if (shouldProcessNotifications)
        {
            ProcessInvalidations();
        }
    }

    private void ProcessInvalidations()
    {
        while (true)
        {
            try
            {
                _ = EnsureValue();
            }
            catch
            {
                lock (_syncRoot)
                {
                    if (_isDisposed || !_isDirty)
                    {
                        _notificationWorkerActive = false;
                        return;
                    }
                }

                continue;
            }

            StateChangedEventArgs<T>? change;
            StateSubscription[] subscriptions;

            lock (_syncRoot)
            {
                if (_isDisposed)
                {
                    _notificationWorkerActive = false;
                    return;
                }

                change = _pendingNotifications.Count == 0
                    ? null
                    : _pendingNotifications.Dequeue();
                subscriptions = change is null ? [] : _subscriptions.ToArray();
            }

            if (change is not null)
            {
                foreach (var subscription in subscriptions)
                {
                    subscription.Notify(change);
                }
            }

            lock (_syncRoot)
            {
                if (!_isDirty && _pendingNotifications.Count == 0)
                {
                    _notificationWorkerActive = false;
                    return;
                }
            }
        }
    }

    private EvaluationResult EnsureValue()
    {
        while (true)
        {
            long invalidationVersion;
            T? oldValue;
            bool hadValue;

            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (!_isDirty)
                {
                    if (_hasComputeFailure && !_hasValue)
                    {
                        ExceptionDispatchInfo.Capture(_lastError!).Throw();
                    }

                    return new EvaluationResult(_value!);
                }

                invalidationVersion = _invalidationVersion;
                oldValue = _value;
                hadValue = _hasValue;
            }

            T? computedValue = default;
            Exception? computeError = null;

            try
            {
                using var _ = ComputedStateEvaluationContext.Enter(this);
                computedValue = _compute();
            }
            catch (Exception exception)
            {
                computeError = exception;
            }

            StateChangedEventArgs<T>? change = null;
            var shouldWriteDiagnostic = false;
            var shouldThrow = false;
            T? committedValue;

            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (!_isDirty || invalidationVersion != _invalidationVersion)
                {
                    continue;
                }

                if (computeError is not null)
                {
                    _hasComputeFailure = true;
                    _isDirty = false;
                    _lastError = computeError;
                    shouldWriteDiagnostic = true;
                    shouldThrow = !_hasValue;
                }
                else
                {
                    _value = computedValue;
                    _hasComputeFailure = false;
                    _hasValue = true;
                    _isDirty = false;
                    _lastError = null;

                    if (hadValue && !EqualityComparer<T>.Default.Equals(oldValue, computedValue))
                    {
                        _version++;
                        change = new StateChangedEventArgs<T>(oldValue!, computedValue!, _version);

                        if (_subscriptions.Count > 0)
                        {
                            _pendingNotifications.Enqueue(change);
                        }
                    }
                }

                committedValue = _value;
            }

            if (shouldWriteDiagnostic)
            {
                WriteComputeFailedDiagnostic(computeError!);
            }

            if (shouldThrow)
            {
                ExceptionDispatchInfo.Capture(computeError!).Throw();
            }

            return new EvaluationResult(committedValue!);
        }
    }

    private void WriteComputeFailedDiagnostic(Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.ComputedStateComputeFailed,
            $"Computed state failed to compute value type '{typeof(T).FullName}': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = StateDiagnosticContext.Create(
                ("valueType", StateDiagnosticContext.TypeName(typeof(T))))
        });
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ComputedState<T>));
        }
    }

    private void DisposeSubscription(IDisposable subscription)
    {
        try
        {
            subscription.Dispose();
        }
        catch (Exception exception)
        {
            _diagnostics?.Write(new HostDiagnosticRecord(
                StateDiagnosticIds.ComputedStateDisposeFailed,
                $"Computed state failed to dispose value type '{typeof(T).FullName}' subscription: {exception.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = StateDiagnosticContext.Create(
                    ("valueType", StateDiagnosticContext.TypeName(typeof(T))))
            });
        }
    }

    private void Remove(StateSubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed record EvaluationResult(T Value);

    private sealed class RemovingStateSubscription : IStateSubscription
    {
        private readonly ComputedState<T> _state;
        private readonly StateSubscription _subscription;
        private int _disposed;

        public RemovingStateSubscription(
            ComputedState<T> state,
            StateSubscription subscription)
        {
            _state = state;
            _subscription = subscription;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _subscription.Dispose();
            _state.Remove(_subscription);
        }
    }
}
