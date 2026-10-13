using AtomUI.City.Core.Diagnostics;
using AtomUI.City.State;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.State.Tests;

public sealed class StateThreadingTests
{
    [Fact]
    public void StateDispatchPolicyKeepsStableValues()
    {
        Assert.Equal(0, (int)StateDispatchPolicy.Immediate);
        Assert.Equal(1, (int)StateDispatchPolicy.Queued);
        Assert.Equal(2, (int)StateDispatchPolicy.Dispatcher);
        Assert.Equal(3, (int)StateDispatchPolicy.Background);
    }

    [Fact]
    public async Task ConcurrentUpdatesAreAtomicAndVersioned()
    {
        var state = new WritableState<int>(0);
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => state.Update(value => value + 1)))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(100, state.Value);
        Assert.Equal(100, state.Version);
    }

    [Fact]
    public async Task ObserveDoesNotLoseOrReorderConcurrentUpdates()
    {
        var state = new WritableState<int>(0);
        var versions = new List<long>();
        var syncRoot = new object();
        using var subscription = state.Observe(args =>
        {
            lock (syncRoot)
            {
                versions.Add(args.Version);
            }
        });

        var updates = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => state.Update(value => value + 1)))
            .ToArray();

        await Task.WhenAll(updates);

        lock (syncRoot)
        {
            Assert.Equal(Enumerable.Range(0, 101).Select(value => (long)value), versions);
        }
    }

    [Fact]
    public void ChangeNotificationsRunOutsideMutationLock()
    {
        var state = new WritableState<int>(0);

        state.OnChange(_ =>
        {
            if (state.Value == 1)
            {
                state.SetValue(2);
            }
        });

        state.SetValue(1);

        Assert.Equal(2, state.Value);
        Assert.Equal(2, state.Version);
    }

    [Fact]
    public void DispatcherSubscriptionUsesUiDispatcher()
    {
        var dispatcher = new RecordingDispatcher();
        var state = new WritableState<int>(0);
        var observed = 0;

        state.OnChange(
            args => observed = args.NewValue,
            StateSubscriptionOptions.Dispatcher(dispatcher));

        state.SetValue(5);

        Assert.Equal(5, observed);
        Assert.Equal(1, dispatcher.DispatchCount);
    }

    [Fact]
    public async Task DispatcherSubscriptionDoesNotBlockStateCommitWhileUiWorkIsPending()
    {
        var dispatcher = new CompletingDeferredDispatcher();
        var state = new WritableState<int>(0);
        var observed = 0;
        state.OnChange(
            args => observed = args.NewValue,
            StateSubscriptionOptions.Dispatcher(dispatcher));

        var setValue = Task.Run(() => state.SetValue(5));

        await dispatcher.WorkQueued.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await setValue.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(5, state.Value);
        Assert.Equal(0, observed);

        dispatcher.RunPending();

        Assert.Equal(5, observed);
    }

    [Fact]
    public void UnavailableDispatcherSubscriptionRecordsDiagnosticsAndKeepsCommittedState()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new UnavailableUiDispatcher();
        var state = new WritableState<int>(0, diagnostics: diagnostics);
        var handlerCalled = false;

        state.OnChange(
            _ => handlerCalled = true,
            StateSubscriptionOptions.Dispatcher(dispatcher));

        state.SetValue(5);

        Assert.Equal(5, state.Value);
        Assert.False(handlerCalled);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.SubscriptionHandlerFailed, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Error, record.Severity);
        Assert.Equal(
            StateDispatchPolicy.Dispatcher.ToString(),
            record.Context["dispatchPolicy"]);
        Assert.Equal(
            typeof(UnavailableUiDispatcher).FullName,
            record.Context["dispatcherType"]);
        Assert.Equal("1", record.Context["version"]);
    }

    [Fact]
    public void DisposedDispatcherSubscriptionSkipsPendingCallback()
    {
        var dispatcher = new DeferredDispatcher();
        var state = new WritableState<int>(0);
        var observed = 0;

        var subscription = state.OnChange(
            args => observed = args.NewValue,
            StateSubscriptionOptions.Dispatcher(dispatcher));

        state.SetValue(5);
        subscription.Dispose();
        dispatcher.RunPending();

        Assert.Equal(0, observed);
    }

    [Fact]
    public void DisposedDispatcherObservationSkipsPendingInitialCallback()
    {
        var dispatcher = new DeferredDispatcher();
        var state = new WritableState<int>(7);
        var notifications = 0;

        var subscription = state.Observe(
            _ => notifications++,
            StateSubscriptionOptions.Dispatcher(dispatcher));

        subscription.Dispose();
        dispatcher.RunPending();

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void ReadOnlyStateSubscriptionCanDeclareDispatcherPolicy()
    {
        var dispatcher = new RecordingDispatcher();
        IReadOnlyState<int> state = new WritableState<int>(0);
        var observed = 0;

        state.OnChange(
            args => observed = args.NewValue,
            StateSubscriptionOptions.Dispatcher(dispatcher));

        ((IWritableState<int>)state).SetValue(7);

        Assert.Equal(7, observed);
        Assert.Equal(1, dispatcher.DispatchCount);
    }

    [Fact]
    public async Task BackgroundSubscriptionUsesBackgroundDispatchPolicy()
    {
        var state = new WritableState<int>(0);
        var observed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = StateSubscriptionOptions.Background();

        state.OnChange(
            args => observed.SetResult(args.NewValue),
            options);

        state.SetValue(9);

        Assert.Equal(9, await observed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(StateDispatchPolicy.Background, options.DispatchPolicy);
    }

    [Fact]
    public async Task BackgroundSubscriptionDoesNotBlockSetValue()
    {
        var state = new WritableState<int>(0);
        using var releaseHandler = new ManualResetEventSlim(false);
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        state.OnChange(
            _ =>
            {
                handlerEntered.SetResult();
                Assert.True(releaseHandler.Wait(TimeSpan.FromSeconds(5)));
            },
            StateSubscriptionOptions.Background());

        var setValue = Task.Run(() => state.SetValue(1));

        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var completedBeforeRelease = await Task.WhenAny(
            setValue,
            Task.Delay(TimeSpan.FromSeconds(1))) == setValue;

        releaseHandler.Set();

        await setValue.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(completedBeforeRelease);
    }

    [Fact]
    public async Task BackgroundSubscriptionRecordsHandlerFailures()
    {
        var diagnostics = new CompletingDiagnostics();
        var state = new WritableState<int>(0, diagnostics: diagnostics);

        state.OnChange(
            _ => throw new InvalidOperationException("bad background"),
            StateSubscriptionOptions.Background());

        state.SetValue(1);

        var record = await diagnostics.NextRecord.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(StateDiagnosticIds.SubscriptionHandlerFailed, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Error, record.Severity);
        Assert.Contains("bad background", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueuedSubscriptionDispatchesNotificationsInOrderWithoutBlockingSetValue()
    {
        var state = new WritableState<int>(0);
        var observed = new List<int>();
        using var releaseFirstHandler = new ManualResetEventSlim(false);
        var firstHandlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var syncRoot = new object();
        var options = StateSubscriptionOptions.Queued();

        state.OnChange(
            args =>
            {
                if (args.NewValue == 1)
                {
                    firstHandlerEntered.SetResult();
                    Assert.True(releaseFirstHandler.Wait(TimeSpan.FromSeconds(5)));
                }

                lock (syncRoot)
                {
                    observed.Add(args.NewValue);

                    if (observed.Count == 2)
                    {
                        completion.SetResult();
                    }
                }
            },
            options);

        var setValues = Task.Run(() =>
        {
            state.SetValue(1);
            state.SetValue(2);
        });

        await firstHandlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var completedBeforeRelease = await Task.WhenAny(
            setValues,
            Task.Delay(TimeSpan.FromSeconds(1))) == setValues;

        releaseFirstHandler.Set();

        Assert.True(completedBeforeRelease);
        await setValues.WaitAsync(TimeSpan.FromSeconds(5));
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 2], observed);
        Assert.Equal(StateDispatchPolicy.Queued, options.DispatchPolicy);
    }

    [Fact]
    public async Task BackgroundSubscriptionIsFifoAndDropsOldestWhenBoundedQueueOverflows()
    {
        var diagnostics = new CompletingDiagnostics();
        var state = new WritableState<int>(0, diagnostics: diagnostics);
        var observed = new List<int>();
        var syncRoot = new object();
        using var releaseFirst = new ManualResetEventSlim(false);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        state.OnChange(
            args =>
            {
                if (args.NewValue == 1)
                {
                    firstEntered.TrySetResult();
                    Assert.True(releaseFirst.Wait(TimeSpan.FromSeconds(5)));
                }

                lock (syncRoot)
                {
                    observed.Add(args.NewValue);
                    if (observed.Count == 3)
                    {
                        completed.TrySetResult();
                    }
                }
            },
            StateSubscriptionOptions.Background(maxPendingNotifications: 2));

        state.SetValue(1);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        state.SetValue(2);
        state.SetValue(3);
        state.SetValue(4);
        releaseFirst.Set();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([1, 3, 4], observed);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == StateDiagnosticIds.SubscriptionQueueOverflow &&
                      record.Context["maxPendingNotifications"] == "2");
    }

    [Fact]
    public void DelayedSubscriptionOptionsRejectNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StateSubscriptionOptions.Background(maxPendingNotifications: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StateSubscriptionOptions.Queued(maxPendingNotifications: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StateSubscriptionOptions.Dispatcher(new RecordingDispatcher(), maxPendingNotifications: 0));
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public int DispatchCount { get; private set; }

        public bool CheckAccess() => true;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            callback();

            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            DispatchCount++;

            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            return callback(cancellationToken);
        }
    }

    private sealed class CompletingDeferredDispatcher : IUiDispatcher
    {
        private readonly TaskCompletionSource _workQueued = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _workCompleted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<CancellationToken, ValueTask>? _pending;

        public Task WorkQueued => _workQueued.Task;

        public bool CheckAccess() => false;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            _pending = callback;
            _workQueued.TrySetResult();
            return new ValueTask(_workCompleted.Task);
        }

        public void RunPending()
        {
            var pending = _pending;
            _pending = null;
            pending?.Invoke(CancellationToken.None).GetAwaiter().GetResult();
            _workCompleted.TrySetResult();
        }
    }

    private sealed class DeferredDispatcher : IUiDispatcher
    {
        private Action? _pending;

        public bool CheckAccess() => false;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            _pending = callback;

            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            _pending = () => _ = callback();

            return ValueTask.FromResult(default(T)!);
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            _pending = () => callback(cancellationToken).AsTask().GetAwaiter().GetResult();

            return ValueTask.CompletedTask;
        }

        public void RunPending()
        {
            var pending = _pending;
            _pending = null;
            pending?.Invoke();
        }
    }

    private sealed class CompletingDiagnostics : IHostDiagnostics
    {
        private readonly InMemoryHostDiagnostics _inner = new();
        private readonly TaskCompletionSource<HostDiagnosticRecord> _nextRecord = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<HostDiagnosticRecord> Records => _inner.Records;

        public Task<HostDiagnosticRecord> NextRecord => _nextRecord.Task;

        public void Write(HostDiagnosticRecord record)
        {
            _inner.Write(record);
            _nextRecord.TrySetResult(record);
        }

        public void Complete()
        {
            _inner.Complete();
        }
    }
}
