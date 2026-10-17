using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.EventBus;

internal sealed class EventBusContributionLease : IEventBusContributionLease, IPluginEventPublisher, IPluginEventSubscriber
{
    private readonly IEventBus _sharedBus;
    private readonly IEventContractRegistry _sharedRegistry;
    private InMemoryEventBus? _privateBus;
    private IEventContractRegistry? _privateRegistry;
    private readonly LifecycleScope _scope;
    private readonly IReadOnlyDictionary<(EventContractId ContractId, string Channel), EventPluginAccess> _access;
    private readonly int _maximumSubscriptions;
    private readonly TimeSpan _drainTimeout;
    private readonly Action<string, EventBusContributionLease> _onTerminated;
    private readonly EventDiagnosticWriter _diagnostics;
    private readonly object _syncRoot = new();
    private readonly HashSet<CountedSubscription> _subscriptions = [];
    private readonly List<Exception> _pendingRegistrationFailures = [];
    private Task? _terminationTask;
    private Task? _lateCleanupTask;
    private int _state = (int)EventBusContributionState.Activating;
    private int _terminationReleased;
    private int _subscriptionCount;
    private int _pendingRegistrationCount;
    private int _activeOperations;
    private TaskCompletionSource? _operationsDrained;
    private TaskCompletionSource? _registrationsDrained;

    public EventBusContributionLease(EventBusContributionRequest request, IEventBus sharedBus,
        IEventContractRegistry sharedRegistry, InMemoryEventBus privateBus,
        IEventContractRegistry privateRegistry, LifecycleScope scope,
        EventDiagnosticWriter diagnostics,
        Action<string, EventBusContributionLease> onTerminated)
    {
        PluginId = request.PluginId;
        _sharedBus = sharedBus;
        _sharedRegistry = sharedRegistry;
        _privateBus = privateBus;
        _privateRegistry = privateRegistry;
        _scope = scope;
        _diagnostics = diagnostics;
        _maximumSubscriptions = request.Quotas.MaximumSubscriptions;
        _drainTimeout = request.Quotas.DrainTimeout;
        _onTerminated = onTerminated;
        _access = request.SharedAccess.ToDictionary(
            rule => (rule.ContractId, rule.ChannelName),
            rule => rule.Access);
        Publisher = this;
        Subscriber = this;
    }

    public string PluginId { get; }
    public EventBusContributionState State => (EventBusContributionState)Volatile.Read(ref _state);
    public IPluginEventPublisher Publisher { get; }
    public IPluginEventSubscriber Subscriber { get; }

    internal bool TryActivate()
    {
        lock (_syncRoot)
        {
            if ((EventBusContributionState)_state != EventBusContributionState.Activating)
            {
                return false;
            }

            Volatile.Write(ref _state, (int)EventBusContributionState.Active);
        }

        WriteDiagnostic(
            EventDiagnosticIds.PluginContributionActivated,
            $"Plugin EventBus contribution '{PluginId}' was activated.",
            HostDiagnosticSeverity.Info);
        return true;
    }

    public ValueTask<EventPublishResult> PublishAsync<TEvent>(EventPluginPlane plane, TEvent eventData,
        EventPublishOptions? options = null, CancellationToken cancellationToken = default) =>
        PublishAsync(plane, EventChannel<TEvent>.Default, eventData, options, cancellationToken);

    public ValueTask<EventPublishResult> PublishAsync<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        TEvent eventData, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        var publisher = BeginOperation<TEvent>(plane, channel, EventPluginAccess.Publish);
        return new ValueTask<EventPublishResult>(PublishTrackedAsync(
            publisher, channel, eventData, options, cancellationToken));
    }

    public ValueTask<EventPostResult> PostAsync<TEvent>(EventPluginPlane plane, TEvent eventData,
        EventPublishOptions? options = null, CancellationToken cancellationToken = default) =>
        PostAsync(plane, EventChannel<TEvent>.Default, eventData, options, cancellationToken);

    public ValueTask<EventPostResult> PostAsync<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        TEvent eventData, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        var publisher = BeginOperation<TEvent>(plane, channel, EventPluginAccess.Publish);
        return new ValueTask<EventPostResult>(PostTrackedAsync(
            publisher, channel, eventData, options, cancellationToken));
    }

    public IEventSubscription Subscribe<TEvent>(EventPluginPlane plane,
        Func<EventContext<TEvent>, CancellationToken, ValueTask> handler,
        EventSubscriptionOptions? options = null) =>
        Subscribe(plane, EventChannel<TEvent>.Default, handler, options);

    public IEventSubscription Subscribe<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, CancellationToken, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var subscriber = ValidateAccess<TEvent>(plane, channel, EventPluginAccess.Subscribe);
        lock (_syncRoot)
        {
            EnsureActive();
            if (_subscriptionCount + _pendingRegistrationCount >= _maximumSubscriptions)
            {
                throw new InvalidOperationException($"Plugin '{PluginId}' exceeded its EventBus subscription quota.");
            }

            _pendingRegistrationCount++;
        }

        CountedSubscription counted;
        try
        {
            counted = new CountedSubscription(
                subscriber.Subscribe(_scope, channel, handler, options),
                OnSubscriptionReleased);
        }
        catch
        {
            CompletePendingRegistration();
            throw;
        }

        var committed = false;
        lock (_syncRoot)
        {
            if ((EventBusContributionState)_state == EventBusContributionState.Active)
            {
                _subscriptions.Add(counted);
                _subscriptionCount++;
                committed = true;
            }
        }

        if (committed)
        {
            CompletePendingRegistration();
            return counted;
        }

        ObserveFault(RollbackPendingRegistrationAsync(counted));
        throw new ObjectDisposedException($"EventBus contribution '{PluginId}'");
    }

    public void Dispose() => ObserveFault(RequestTermination());
    public ValueTask DisposeAsync() => new(RequestTermination());

    public ValueTask StopAsync(CancellationToken waitCancellationToken = default)
    {
        var task = RequestTermination();
        return !waitCancellationToken.CanBeCanceled || task.IsCompletedSuccessfully
            ? new ValueTask(task)
            : new ValueTask(task.WaitAsync(waitCancellationToken));
    }

    private Task RequestTermination()
    {
        Task terminationTask;
        var writeQuiescingDiagnostic = false;
        lock (_syncRoot)
        {
            if (_terminationTask is not null) return _terminationTask;
            Volatile.Write(ref _state, (int)EventBusContributionState.Quiescing);
            var operations = _activeOperations == 0
                ? Task.CompletedTask
                : (_operationsDrained ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            var registrations = _pendingRegistrationCount == 0
                ? Task.CompletedTask
                : (_registrationsDrained ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            terminationTask = TerminateAsync(
                _subscriptions.ToArray(),
                registrations,
                operations,
                System.Diagnostics.Stopwatch.GetTimestamp());
            _terminationTask = terminationTask;
            writeQuiescingDiagnostic = true;
        }

        if (writeQuiescingDiagnostic)
        {
            WriteDiagnostic(
                EventDiagnosticIds.PluginContributionQuiescing,
                $"Plugin EventBus contribution '{PluginId}' is quiescing.",
                HostDiagnosticSeverity.Info);
        }

        return terminationTask;
    }

    private async Task TerminateAsync(
        IReadOnlyList<CountedSubscription> subscriptions,
        Task registrations,
        Task operations,
        long terminationStartedAt)
    {
        await Task.Yield();
        Volatile.Write(ref _state, (int)EventBusContributionState.Draining);
        var cleanupTask = StartDrainAndCleanup(subscriptions, registrations, operations);
        List<Exception> failures;
        try
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(terminationStartedAt);
            var remaining = _drainTimeout - elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException();
            }

            failures = await cleanupTask.WaitAsync(remaining).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            int activeOperations;
            int activeSubscriptions;
            int pendingRegistrations;
            lock (_syncRoot)
            {
                activeOperations = _activeOperations;
                activeSubscriptions = _subscriptionCount;
                pendingRegistrations = _pendingRegistrationCount;
            }

            var exception = new EventPluginDrainTimeoutException(
                PluginId,
                _drainTimeout,
                activeOperations,
                activeSubscriptions,
                pendingRegistrations);
            Volatile.Write(ref _state, (int)EventBusContributionState.Faulted);
            WriteDrainTimeoutDiagnostic(exception);
            var lateCleanupTask = ObserveLateCleanupAsync(cleanupTask);
            lock (_syncRoot)
            {
                _lateCleanupTask = lateCleanupTask;
            }
            throw exception;
        }

        ReleaseTerminationReferences(failures);
        Volatile.Write(ref _state, failures.Count == 0
            ? (int)EventBusContributionState.Disposed
            : (int)EventBusContributionState.Faulted);
        WriteDiagnostic(EventDiagnosticIds.PluginContributionDisposed,
            $"Plugin EventBus contribution '{PluginId}' terminated with state '{State}'.",
            failures.Count == 0 ? HostDiagnosticSeverity.Info : HostDiagnosticSeverity.Error);
        ThrowFailures(failures);
    }

    private async Task<List<Exception>> DrainAndCleanupAsync(
        IReadOnlyList<CountedSubscription> subscriptions,
        Task registrations,
        Task operations)
    {
        var failures = new List<Exception>();
        var drains = subscriptions
            .Select(subscription => CaptureFailureAsync(() => subscription.StopAsync()))
            .Append(CaptureFailureAsync(() => new ValueTask(registrations)))
            .Append(CaptureFailureAsync(() => new ValueTask(operations)))
            .ToArray();
        foreach (var exception in await Task.WhenAll(drains).ConfigureAwait(false))
        {
            if (exception is not null) AddFailure(failures, exception);
        }

        var scopeFailure = await CaptureFailureAsync(() => _scope.DisposeAsync()).ConfigureAwait(false);
        if (scopeFailure is not null) AddFailure(failures, scopeFailure);

        var privateBus = _privateBus;
        if (privateBus is not null)
        {
            var privateBusFailure = await CaptureFailureAsync(() => privateBus.DisposeAsync()).ConfigureAwait(false);
            if (privateBusFailure is not null) AddFailure(failures, privateBusFailure);
        }

        return failures;
    }

    private Task<List<Exception>> StartDrainAndCleanup(
        IReadOnlyList<CountedSubscription> subscriptions,
        Task registrations,
        Task operations)
    {
        if (ExecutionContext.IsFlowSuppressed())
        {
            return Task.Run(() => DrainAndCleanupAsync(subscriptions, registrations, operations));
        }

        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(() => DrainAndCleanupAsync(subscriptions, registrations, operations));
        }
    }

    private async Task ObserveLateCleanupAsync(Task<List<Exception>> cleanupTask)
    {
        List<Exception> failures;
        try
        {
            failures = await cleanupTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures = [];
            AddFailure(failures, exception);
        }

        ReleaseTerminationReferences(failures);
        WriteDiagnostic(
            EventDiagnosticIds.PluginContributionDisposed,
            failures.Count == 0
                ? $"Plugin EventBus contribution '{PluginId}' completed late cleanup after its drain timeout; the lease remains Faulted."
                : $"Plugin EventBus contribution '{PluginId}' completed late cleanup after its drain timeout with {failures.Count} cleanup failure(s); the lease remains Faulted.",
            failures.Count == 0 ? HostDiagnosticSeverity.Warning : HostDiagnosticSeverity.Error);
    }

    private void ReleaseTerminationReferences(List<Exception> failures)
    {
        lock (_syncRoot)
        {
            _subscriptions.Clear();
            _subscriptionCount = 0;
            _privateBus = null;
            _privateRegistry = null;
        }

        if (Interlocked.Exchange(ref _terminationReleased, 1) == 0)
        {
            try
            {
                _onTerminated(PluginId, this);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }
    }

    private void WriteDrainTimeoutDiagnostic(EventPluginDrainTimeoutException exception)
    {
        _diagnostics.Write(new HostDiagnosticRecord(
            EventDiagnosticIds.EventPluginDrainTimedOut,
            exception.Message,
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["pluginId"] = PluginId,
                ["drainTimeoutMilliseconds"] = exception.DrainTimeout.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["activeOperations"] = exception.ActiveOperations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["activeSubscriptions"] = exception.ActiveSubscriptions.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["pendingRegistrations"] = exception.PendingRegistrations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
        });
    }

    private static async Task<Exception?> CaptureFailureAsync(Func<ValueTask> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void ThrowFailures(List<Exception> failures)
    {
        if (failures.Count == 1) throw failures[0];
        if (failures.Count > 1) throw new AggregateException(failures);
    }

    private static void ObserveFault(Task task)
    {
        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }

        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private IEventBus ValidateAccess<TEvent>(
        EventPluginPlane plane,
        EventChannel<TEvent> channel,
        EventPluginAccess required)
    {
        if (!Enum.IsDefined(plane)) throw new ArgumentOutOfRangeException(nameof(plane));
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        if (State != EventBusContributionState.Active)
            throw new ObjectDisposedException($"EventBus contribution '{PluginId}'");
        var registry = plane == EventPluginPlane.Shared
            ? _sharedRegistry
            : Volatile.Read(ref _privateRegistry);
        var eventBus = plane == EventPluginPlane.Shared
            ? _sharedBus
            : Volatile.Read(ref _privateBus);
        if (registry is null || eventBus is null)
        {
            throw new ObjectDisposedException($"EventBus contribution '{PluginId}'");
        }

        if (!registry.TryGet(typeof(TEvent), out var descriptor) || descriptor is null)
            throw Reject($"Plugin '{PluginId}' cannot access unregistered {plane} event type '{typeof(TEvent).FullName}'.",
                typeof(TEvent).FullName, null, channel.Name, required);
        if (plane == EventPluginPlane.Shared &&
            (!_access.TryGetValue((descriptor.ContractId, channel.Name), out var access) || (access & required) == 0))
            throw Reject($"Plugin '{PluginId}' is not authorized to {required} contract '{descriptor.ContractId.Value}' on channel '{channel.Name}'.",
                typeof(TEvent).FullName, descriptor.ContractId.Value, channel.Name, required);

        return eventBus;
    }

    private IEventPublisher BeginOperation<TEvent>(
        EventPluginPlane plane,
        EventChannel<TEvent> channel,
        EventPluginAccess required)
    {
        var publisher = ValidateAccess<TEvent>(plane, channel, required);
        lock (_syncRoot)
        {
            EnsureActive();
            _activeOperations++;
        }

        return publisher;
    }

    private void EndOperation()
    {
        TaskCompletionSource? completion = null;
        lock (_syncRoot)
        {
            _activeOperations--;
            if (_activeOperations == 0) completion = _operationsDrained;
        }
        completion?.TrySetResult();
    }

    private async Task<EventPublishResult> PublishTrackedAsync<TEvent>(IEventPublisher publisher,
        EventChannel<TEvent> channel, TEvent eventData, EventPublishOptions? options, CancellationToken cancellationToken)
    {
        try { return await publisher.PublishAsync(channel, eventData, options, cancellationToken).ConfigureAwait(false); }
        finally { EndOperation(); }
    }

    private async Task<EventPostResult> PostTrackedAsync<TEvent>(IEventPublisher publisher,
        EventChannel<TEvent> channel, TEvent eventData, EventPublishOptions? options, CancellationToken cancellationToken)
    {
        try { return await publisher.PostAsync(channel, eventData, options, cancellationToken).ConfigureAwait(false); }
        finally { EndOperation(); }
    }

    private void EnsureActive()
    {
        if ((EventBusContributionState)_state != EventBusContributionState.Active)
        {
            throw new ObjectDisposedException($"EventBus contribution '{PluginId}'");
        }
    }

    private async Task RollbackPendingRegistrationAsync(CountedSubscription subscription)
    {
        Exception? failure = null;
        try
        {
            await subscription.StopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            CompletePendingRegistration(failure);
        }
    }

    private void CompletePendingRegistration(Exception? failure = null)
    {
        TaskCompletionSource? completion = null;
        Exception? completionFailure = null;
        lock (_syncRoot)
        {
            if (failure is not null)
            {
                AddFailure(_pendingRegistrationFailures, failure);
            }

            _pendingRegistrationCount--;
            if (_pendingRegistrationCount == 0)
            {
                completion = _registrationsDrained;
                completionFailure = _pendingRegistrationFailures.Count switch
                {
                    0 => null,
                    1 => _pendingRegistrationFailures[0],
                    _ => new AggregateException(_pendingRegistrationFailures.ToArray()),
                };
            }
        }

        if (completionFailure is null)
        {
            completion?.TrySetResult();
        }
        else
        {
            completion?.TrySetException(completionFailure);
        }
    }

    private void OnSubscriptionReleased(CountedSubscription subscription)
    {
        lock (_syncRoot)
        {
            if (_subscriptions.Remove(subscription)) _subscriptionCount--;
        }
    }

    private UnauthorizedAccessException Reject(string message, string? eventType, string? contractId,
        string channel, EventPluginAccess access)
    {
        var context = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["pluginId"] = PluginId,
            ["eventType"] = eventType,
            ["contractId"] = contractId,
            ["channel"] = channel,
            ["requestedAccess"] = access.ToString(),
        };
        _diagnostics.Write(new HostDiagnosticRecord(
            EventDiagnosticIds.PluginContributionRejected,
            message,
            HostDiagnosticSeverity.Warning) { Context = context });
        return new UnauthorizedAccessException(message);
    }

    private void WriteDiagnostic(string code, string message, HostDiagnosticSeverity severity)
    {
        _diagnostics.Write(new HostDiagnosticRecord(code, message, severity)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal) { ["pluginId"] = PluginId },
        });
    }

    private static void AddFailure(List<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregate) failures.AddRange(aggregate.Flatten().InnerExceptions);
        else failures.Add(exception);
    }

    private sealed class CountedSubscription : IEventSubscription
    {
        private IEventSubscription? _inner;
        private readonly EventSubscriptionId _id;
        private Action? _release;
        public CountedSubscription(IEventSubscription inner, Action<CountedSubscription> release)
        { _inner = inner; _id = inner.Id; _release = () => release(this); }
        public EventSubscriptionId Id => _id;
        public EventSubscriptionState State => _inner?.State ?? EventSubscriptionState.Disposed;
        public void Dispose() { _inner?.Dispose(); Release(); }
        public async ValueTask DisposeAsync() { try { if (_inner is { } inner) await inner.DisposeAsync().ConfigureAwait(false); } finally { Release(); } }
        public async ValueTask StopAsync(CancellationToken waitCancellationToken = default)
        { try { if (_inner is { } inner) await inner.StopAsync(waitCancellationToken).ConfigureAwait(false); } finally { Release(); } }
        private void Release()
        {
            _inner = null;
            Interlocked.Exchange(ref _release, null)?.Invoke();
        }
    }
}
