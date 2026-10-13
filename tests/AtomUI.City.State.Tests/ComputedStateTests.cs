using AtomUI.City.Core.Diagnostics;
using AtomUI.City.State;

namespace AtomUI.City.State.Tests;

public sealed class ComputedStateTests
{
    [Fact]
    public void ObserveEmitsComputedCurrentValueBeforeDependencyChanges()
    {
        var source = new WritableState<int>(2);
        using var computed = new ComputedState<int>(() => source.Value * 2, source);
        var notifications = new List<StateChangedEventArgs<int>>();

        using var subscription = computed.Observe(notifications.Add);
        source.SetValue(3);

        Assert.Equal(2, notifications.Count);
        Assert.True(notifications[0].IsInitial);
        Assert.Equal(4, notifications[0].NewValue);
        Assert.False(notifications[1].IsInitial);
        Assert.Equal(6, notifications[1].NewValue);
    }

    [Fact]
    public void ComputedStateCachesValueUntilDependencyChanges()
    {
        var source = new WritableState<int>(1);
        var computeCount = 0;
        var computed = new ComputedState<int>(
            () =>
            {
                computeCount++;
                return source.Value * 2;
            },
            source);

        Assert.Equal(2, computed.Value);
        Assert.Equal(2, computed.Value);
        Assert.Equal(1, computeCount);

        source.SetValue(2);

        Assert.Equal(4, computed.Value);
        Assert.Equal(2, computeCount);
    }

    [Fact]
    public void ComputedStateInvalidatesWithoutSubscribersAndRecomputesOnRead()
    {
        var source = new WritableState<int>(1);
        var computeCount = 0;
        var computed = new ComputedState<int>(
            () =>
            {
                computeCount++;
                return source.Value * 2;
            },
            source);

        Assert.Equal(2, computed.Value);
        Assert.Equal(1, computeCount);

        source.SetValue(2);

        Assert.Equal(1, computeCount);
        Assert.Equal(0, computed.Version);

        Assert.Equal(4, computed.Value);
        Assert.Equal(2, computeCount);
        Assert.Equal(1, computed.Version);
    }

    [Fact]
    public void ComputedStateRejectsNullDependency()
    {
        IReadOnlyState[] dependencies = [null!];

        Assert.Throws<ArgumentException>(() => new ComputedState<int>(() => 1, dependencies));
    }

    [Fact]
    public void ComputedStateRejectsNullDependencyBeforeSubscribing()
    {
        var dependency = new TestReadOnlyState(new TestSubscription(() => { }));
        IReadOnlyState[] dependencies = [dependency, null!];

        Assert.Throws<ArgumentException>(() => new ComputedState<int>(() => 1, dependencies));

        Assert.Equal(0, dependency.SubscriptionCount);
    }

    [Fact]
    public void ComputedStateNotifiesWhenComputedValueChanges()
    {
        var source = new WritableState<int>(1);
        var computed = new ComputedState<int>(() => source.Value * 2, source);
        var changes = new List<StateChangedEventArgs<int>>();

        computed.OnChange(changes.Add);

        source.SetValue(2);

        Assert.Single(changes);
        Assert.Equal(2, changes[0].OldValue);
        Assert.Equal(4, changes[0].NewValue);
        Assert.Equal(1, computed.Version);
    }

    [Fact]
    public void ComputedStateKeepsLastValueWhenComputeFails()
    {
        var source = new WritableState<int>(1);
        var computed = new ComputedState<int>(
            () => source.Value == 2 ? throw new InvalidOperationException("bad value") : source.Value,
            source);

        Assert.Equal(1, computed.Value);

        source.SetValue(2);

        Assert.Equal(1, computed.Value);
        Assert.NotNull(computed.LastError);
    }

    [Fact]
    public void ComputedStateRecordsComputeFailureDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var source = new WritableState<int>(1);
        var computed = new ComputedState<int>(
            () => source.Value == 2 ? throw new InvalidOperationException("bad compute") : source.Value,
            diagnostics,
            source);

        Assert.Equal(1, computed.Value);

        source.SetValue(2);

        Assert.Equal(1, computed.Value);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.ComputedStateComputeFailed, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Error, record.Severity);
        Assert.Contains("bad compute", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisposedComputedStateRejectsReadsAndSubscriptionsWithoutComputing()
    {
        var computeCount = 0;
        var computed = new ComputedState<int>(() =>
        {
            computeCount++;
            return 1;
        });

        computed.Dispose();

        Assert.Throws<ObjectDisposedException>(() => computed.Value);
        Assert.Throws<ObjectDisposedException>(() => computed.OnChange(_ => { }));
        Assert.Equal(0, computeCount);
    }

    [Fact]
    public void ComputedStateDoesNotRepeatInitialComputeFailureUntilDependencyChanges()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var source = new WritableState<int>(1);
        var computeCount = 0;
        var computed = new ComputedState<int>(
            () =>
            {
                computeCount++;
                return source.Value == 1
                    ? throw new InvalidOperationException("initial failure")
                    : source.Value;
            },
            diagnostics,
            source);

        var firstFailure = Assert.Throws<InvalidOperationException>(() => computed.Value);
        var secondFailure = Assert.Throws<InvalidOperationException>(() => computed.Value);

        Assert.Same(firstFailure, secondFailure);
        Assert.Equal(1, computeCount);
        Assert.Single(diagnostics.Records);

        source.SetValue(2);

        Assert.Equal(2, computed.Value);
        Assert.Equal(2, computeCount);
    }

    [Fact]
    public void ComputedStateRejectsCircularDependencyWithoutRecursingUntilStackOverflow()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        ComputedState<int>? first = null;
        ComputedState<string>? second = null;
        first = new ComputedState<int>(() => second!.Value.Length, diagnostics);
        second = new ComputedState<string>(() => first.Value.ToString(), diagnostics);

        var exception = Assert.Throws<InvalidOperationException>(() => first.Value);

        Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            diagnostics.Records,
            record => Assert.Equal(StateDiagnosticIds.ComputedStateComputeFailed, record.Code));
    }

    [Fact]
    public async Task ComputedStateDoesNotHoldStateLockWhileUserComputeRuns()
    {
        using var computeEntered = new ManualResetEventSlim(false);
        using var releaseCompute = new ManualResetEventSlim(false);
        var computed = new ComputedState<int>(() =>
        {
            computeEntered.Set();
            Assert.True(releaseCompute.Wait(TimeSpan.FromSeconds(5)));
            return 1;
        });
        var read = Task.Run(() => Assert.Throws<ObjectDisposedException>(() => computed.Value));

        Assert.True(computeEntered.Wait(TimeSpan.FromSeconds(5)));
        var dispose = Task.Run(computed.Dispose);
        await dispose.WaitAsync(TimeSpan.FromSeconds(1));
        releaseCompute.Set();
        await read.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ComputedStatePublishesConcurrentDependencyUpdatesInCommitOrder()
    {
        var source = new WritableState<int>(1);
        using var staleComputeEntered = new ManualResetEventSlim(false);
        using var releaseStaleCompute = new ManualResetEventSlim(false);
        var computed = new ComputedState<int>(() =>
        {
            var value = source.Value;
            if (value == 2)
            {
                staleComputeEntered.Set();
                Assert.True(releaseStaleCompute.Wait(TimeSpan.FromSeconds(5)));
            }

            return value;
        }, source);
        var changes = new List<int>();
        computed.OnChange(args => changes.Add(args.NewValue));

        var staleWrite = Task.Run(() => source.SetValue(2));
        Assert.True(staleComputeEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(source.SetValue(3));
        releaseStaleCompute.Set();
        await staleWrite.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, computed.Value);
        Assert.Equal([2, 3], changes);
    }

    [Fact]
    public void ComputedStateRecordsDisposeFailuresAndContinuesDisposal()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var calls = new List<string>();
        var failingDependency = new TestReadOnlyState(
            new TestSubscription(() => throw new InvalidOperationException("bad dependency dispose")));
        var secondDependency = new TestReadOnlyState(
            new TestSubscription(() => calls.Add("second")));
        var computed = new ComputedState<int>(
            () => 1,
            diagnostics,
            failingDependency,
            secondDependency);

        computed.Dispose();

        Assert.Equal(["second"], calls);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal("AUCSTA010", record.Code);
        Assert.Equal(HostDiagnosticSeverity.Error, record.Severity);
        Assert.Contains("bad dependency dispose", record.Message, StringComparison.Ordinal);
    }

    private sealed class TestReadOnlyState : IReadOnlyState<int>
    {
        private readonly IStateSubscription _subscription;

        public TestReadOnlyState(IStateSubscription subscription)
        {
            _subscription = subscription;
        }

        public int Value => 0;

        object? IReadOnlyState.Value => Value;

        public int SubscriptionCount { get; private set; }

        public long Version => 0;

        public Type ValueType => typeof(int);

        public IStateSubscription OnChange(Action<StateChangedEventArgs<int>> handler)
        {
            SubscriptionCount++;
            return _subscription;
        }

        public IStateSubscription OnChange(
            Action<StateChangedEventArgs<int>> handler,
            StateSubscriptionOptions options)
        {
            SubscriptionCount++;
            return _subscription;
        }

        IStateSubscription IReadOnlyState.OnChange(Action<StateChangedEventArgs> handler)
        {
            SubscriptionCount++;
            return _subscription;
        }

        IStateSubscription IReadOnlyState.OnChange(
            Action<StateChangedEventArgs> handler,
            StateSubscriptionOptions options)
        {
            SubscriptionCount++;
            return _subscription;
        }

        public IStateSubscription Observe(Action<StateChangedEventArgs<int>> handler)
        {
            SubscriptionCount++;
            return _subscription;
        }

        public IStateSubscription Observe(
            Action<StateChangedEventArgs<int>> handler,
            StateSubscriptionOptions options)
        {
            SubscriptionCount++;
            return _subscription;
        }

        IStateSubscription IReadOnlyState.Observe(Action<StateChangedEventArgs> handler)
        {
            SubscriptionCount++;
            return _subscription;
        }

        IStateSubscription IReadOnlyState.Observe(
            Action<StateChangedEventArgs> handler,
            StateSubscriptionOptions options)
        {
            SubscriptionCount++;
            return _subscription;
        }
    }

    private sealed class TestSubscription : IStateSubscription
    {
        private readonly Action _dispose;

        public TestSubscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose();
        }
    }
}
