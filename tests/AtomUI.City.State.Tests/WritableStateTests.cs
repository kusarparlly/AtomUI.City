using AtomUI.City.Core.Diagnostics;
using AtomUI.City.State;

namespace AtomUI.City.State.Tests;

public sealed class WritableStateTests
{
    [Fact]
    public void ObserveEmitsCurrentValueOnceBeforeSubsequentChanges()
    {
        var state = new WritableState<int>(4);
        var notifications = new List<StateChangedEventArgs<int>>();

        using var subscription = state.Observe(notifications.Add);
        state.SetValue(5);

        Assert.Collection(
            notifications,
            initial =>
            {
                Assert.True(initial.IsInitial);
                Assert.Equal(4, initial.OldValue);
                Assert.Equal(4, initial.NewValue);
                Assert.Equal(0, initial.Version);
            },
            change =>
            {
                Assert.False(change.IsInitial);
                Assert.Equal(4, change.OldValue);
                Assert.Equal(5, change.NewValue);
                Assert.Equal(1, change.Version);
            });
    }

    [Fact]
    public void ObserveInitialHandlerRunsOutsideStateLock()
    {
        var state = new WritableState<int>(0);
        var notifications = new List<long>();

        using var subscription = state.Observe(args =>
        {
            notifications.Add(args.Version);
            if (args.IsInitial)
            {
                state.SetValue(1);
            }
        });

        Assert.Equal([0, 1], notifications);
        Assert.Equal(1, state.Value);
    }

    [Fact]
    public void ChangedRunsSynchronouslyBeforeManagedSubscriptions()
    {
        var state = new WritableState<int>(0);
        var calls = new List<string>();
        state.Changed += (_, _) => calls.Add("changed");
        state.OnChange(_ => calls.Add("subscription"));

        state.SetValue(1);

        Assert.Equal(["changed", "subscription"], calls);
    }

    [Fact]
    public void SetUpdatesValueAndRaisesChangedOnce()
    {
        var state = new WritableState<int>(1);
        var changeCount = 0;
        StateChangedEventArgs<int>? lastChange = null;

        state.Changed += (_, args) =>
        {
            changeCount++;
            lastChange = args;
        };

        state.Set(2);
        state.Set(2);

        Assert.Equal(2, state.Value);
        Assert.Equal(1, changeCount);
        Assert.NotNull(lastChange);
        Assert.Equal(1, lastChange.OldValue);
        Assert.Equal(2, lastChange.NewValue);
    }

    [Fact]
    public void UpdateTransformsCurrentValue()
    {
        var state = new WritableState<int>(2);

        state.Update(value => value + 3);

        Assert.Equal(5, state.Value);
    }

    [Fact]
    public void SetValueReturnsFalseForEqualValueAndDoesNotNotify()
    {
        var state = new WritableState<string>("ready");
        var changeCount = 0;

        state.Changed += (_, _) => changeCount++;

        var changed = state.SetValue("ready");

        Assert.False(changed);
        Assert.Equal(0, state.Version);
        Assert.Equal(0, changeCount);
    }

    [Fact]
    public void SetValueIncrementsVersionAndNotifiesAfterCommit()
    {
        var state = new WritableState<int>(1);
        long observedVersion = -1;
        int observedValue = -1;
        StateChangedEventArgs<int>? observedArgs = null;

        state.OnChange(args =>
        {
            observedVersion = state.Version;
            observedValue = state.Value;
            observedArgs = args;
        });

        var changed = state.SetValue(2);

        Assert.True(changed);
        Assert.Equal(1, state.Version);
        Assert.Equal(1, observedVersion);
        Assert.Equal(2, observedValue);
        Assert.NotNull(observedArgs);
        Assert.Equal(1, observedArgs.OldValue);
        Assert.Equal(2, observedArgs.NewValue);
        Assert.Equal(1, observedArgs.Version);
    }

    [Fact]
    public void DisposedSubscriptionStopsReceivingNotifications()
    {
        var state = new WritableState<int>(0);
        var notifications = 0;
        var subscription = state.OnChange(_ => notifications++);

        subscription.Dispose();
        state.SetValue(1);

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void DisposeRejectsMutatingApisAndKeepsReadPropertiesAvailable()
    {
        var state = new WritableState<int>(3);

        state.Dispose();
        state.Dispose();

        Assert.Equal(3, state.Value);
        Assert.Equal(0, state.Version);
        Assert.Throws<ObjectDisposedException>(() => state.SetValue(4));
        Assert.Throws<ObjectDisposedException>(() => state.Set(4));
        Assert.Throws<ObjectDisposedException>(() => state.Update(value => value + 1));
        Assert.Throws<ObjectDisposedException>(() => state.OnChange(_ => { }));
        Assert.Throws<ObjectDisposedException>(() => state.Observe(_ => { }));
    }

    [Fact]
    public void UpdateKeepsCurrentValueWhenUpdaterThrows()
    {
        var state = new WritableState<int>(3);

        Assert.Throws<InvalidOperationException>(
            () => state.Update(_ => throw new InvalidOperationException("bad update")));

        Assert.Equal(3, state.Value);
        Assert.Equal(0, state.Version);
    }

    [Fact]
    public void UpdateRecordsUpdaterFailureDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new WritableState<int>(3, diagnostics: diagnostics);

        Assert.Throws<InvalidOperationException>(
            () => state.Update(_ => throw new InvalidOperationException("bad update")));

        Assert.Equal(3, state.Value);
        Assert.Equal(0, state.Version);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.WritableStateUpdateFailed, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Error, record.Severity);
        Assert.Contains("bad update", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyAccessPolicyRejectsWritableStateMutations()
    {
        var state = new WritableState<int>(
            3,
            stateName: "AtomUI.City.Tests.ReadOnly",
            access: StateAccessPolicy.ReadOnly);
        var updaterCalled = false;

        var setValueException = Assert.Throws<StateAccessDeniedException>(
            () => state.SetValue(4));
        var setException = Assert.Throws<StateAccessDeniedException>(
            () => state.Set(4));
        var updateException = Assert.Throws<StateAccessDeniedException>(
            () => state.Update(value =>
            {
                updaterCalled = true;
                return value + 1;
            }));

        Assert.Equal("AtomUI.City.Tests.ReadOnly", setValueException.StateName);
        Assert.Equal(setValueException.StateName, setException.StateName);
        Assert.Equal(setValueException.StateName, updateException.StateName);
        Assert.False(updaterCalled);
        Assert.Equal(3, state.Value);
        Assert.Equal(0, state.Version);
    }

    [Fact]
    public void ReadOnlyAccessPolicyRecordsWriteDeniedDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new WritableState<int>(
            3,
            diagnostics: diagnostics,
            stateName: "AtomUI.City.Tests.ReadOnly",
            access: StateAccessPolicy.ReadOnly);

        var exception = Assert.Throws<StateAccessDeniedException>(
            () => state.SetValue(4));

        Assert.Equal("AtomUI.City.Tests.ReadOnly", exception.StateName);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.ApplicationStateWriteDenied, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Warning, record.Severity);
        Assert.Contains(exception.StateName, record.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(StateAccessPolicy.ReadOnly), record.Message, StringComparison.Ordinal);
    }
}
