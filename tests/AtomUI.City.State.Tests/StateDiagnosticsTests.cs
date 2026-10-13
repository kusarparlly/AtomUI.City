using System.Reflection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.State;

namespace AtomUI.City.State.Tests;

public sealed class StateDiagnosticsTests
{
    [Fact]
    public void StateDiagnosticIdsExposeStableCodeContract()
    {
        var fields = typeof(StateDiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();

        var codes = fields
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(11, codes.Length);
        Assert.Equal(
            [
                "AUCSTA001",
                "AUCSTA002",
                "AUCSTA003",
                "AUCSTA004",
                "AUCSTA005",
                "AUCSTA006",
                "AUCSTA007",
                "AUCSTA008",
                "AUCSTA009",
                "AUCSTA010",
                "AUCSTA011"
            ],
            codes);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code =>
        {
            Assert.StartsWith("AUCSTA", code, StringComparison.Ordinal);
            Assert.Equal(9, code.Length);
            Assert.True(int.TryParse(code[6..], out _), $"Diagnostic code '{code}' must end with digits.");
        });
    }

    [Fact]
    public void StateDiagnosticRecordsIncludeStableLocatingContext()
    {
        var expectations = new[]
        {
            new DiagnosticContextExpectation(
                StateDiagnosticIds.ChangedEventHandlerFailed,
                TriggerChangedEventHandlerFailed,
                ["valueType", "version"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.SubscriptionHandlerFailed,
                TriggerSubscriptionHandlerFailed,
                ["dispatchPolicy", "version"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.ApplicationStateNotRegistered,
                TriggerApplicationStateNotRegistered,
                ["stateKey", "valueType"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.ApplicationStateWriteDenied,
                TriggerApplicationStateWriteDenied,
                ["accessPolicy", "stateKey", "valueType"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.ComputedStateComputeFailed,
                TriggerComputedStateComputeFailed,
                ["valueType"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.WritableStateUpdateFailed,
                TriggerWritableStateUpdateFailed,
                ["valueType", "version"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.SnapshotRestoreFailed,
                TriggerSnapshotRestoreFailed,
                ["reason", "stateKey", "valueType"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.ApplicationStateAlreadyRegistered,
                TriggerApplicationStateAlreadyRegistered,
                ["stateKey", "valueType"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.StateScopeDisposeFailed,
                TriggerStateScopeDisposeFailed,
                ["scopeId"]),
            new DiagnosticContextExpectation(
                StateDiagnosticIds.ComputedStateDisposeFailed,
                TriggerComputedStateDisposeFailed,
                ["valueType"])
        };

        foreach (var expectation in expectations)
        {
            var record = RecordSingle(expectation);

            Assert.Equal(expectation.Code, record.Code);
            Assert.All(expectation.RequiredContextKeys, key =>
            {
                Assert.True(
                    record.Context.TryGetValue(key, out var value),
                    $"Diagnostic '{expectation.Code}' must include context key '{key}'.");
                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"Diagnostic '{expectation.Code}' context key '{key}' must have a value.");
            });
        }
    }

    [Fact]
    public void WritableStateRecordsChangedEventHandlerFailuresAndContinuesNotification()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new WritableState<int>(0, diagnostics: diagnostics);
        var secondHandlerCalled = false;

        state.Changed += (_, _) => throw new InvalidOperationException("bad changed event");
        state.Changed += (_, _) => secondHandlerCalled = true;

        state.SetValue(1);

        Assert.True(secondHandlerCalled);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.ChangedEventHandlerFailed, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Error, record.Severity);
        Assert.Contains("bad changed event", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WritableStateRecordsSubscriptionHandlerFailuresAndContinuesNotification()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new WritableState<int>(0, diagnostics: diagnostics);
        var observed = 0;

        state.OnChange(_ => throw new InvalidOperationException("bad subscription"));
        state.OnChange(args => observed = args.NewValue);

        state.SetValue(5);

        Assert.Equal(5, observed);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.SubscriptionHandlerFailed, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Error, record.Severity);
        Assert.Contains("bad subscription", record.Message, StringComparison.Ordinal);
    }

    private static HostDiagnosticRecord RecordSingle(DiagnosticContextExpectation expectation)
    {
        var diagnostics = new InMemoryHostDiagnostics();

        expectation.Trigger(diagnostics);

        Assert.True(
            diagnostics.Records.Count == 1,
            $"Diagnostic '{expectation.Code}' must write exactly one record, but wrote {diagnostics.Records.Count}.");

        return Assert.Single(diagnostics.Records);
    }

    private static void TriggerChangedEventHandlerFailed(InMemoryHostDiagnostics diagnostics)
    {
        var state = new WritableState<int>(0, diagnostics: diagnostics);
        state.Changed += (_, _) => throw new InvalidOperationException("bad changed event");

        state.SetValue(1);
    }

    private static void TriggerSubscriptionHandlerFailed(InMemoryHostDiagnostics diagnostics)
    {
        var state = new WritableState<int>(0, diagnostics: diagnostics);
        state.OnChange(_ => throw new InvalidOperationException("bad subscription"));

        state.SetValue(1);
    }

    private static void TriggerApplicationStateNotRegistered(InMemoryHostDiagnostics diagnostics)
    {
        var registry = new ApplicationStateRegistry(diagnostics);
        var key = new StateKey<int>("AtomUI.City.Tests.Missing");

        Assert.Throws<StateNotRegisteredException>(() => registry.Get(key));
    }

    private static void TriggerApplicationStateWriteDenied(InMemoryHostDiagnostics diagnostics)
    {
        var registry = new ApplicationStateRegistry(diagnostics);
        var key = new StateKey<int>("AtomUI.City.Tests.ReadOnly");
        registry.Add(StateDefinition.Create(key, 1, access: StateAccessPolicy.ReadOnly));

        Assert.Throws<StateAccessDeniedException>(() => registry.Set(key, 2));
    }

    private static void TriggerComputedStateComputeFailed(InMemoryHostDiagnostics diagnostics)
    {
        var source = new WritableState<int>(1);
        var computed = new ComputedState<int>(
            () => source.Value == 2 ? throw new InvalidOperationException("bad compute") : source.Value,
            diagnostics,
            source);

        Assert.Equal(1, computed.Value);

        source.SetValue(2);
        _ = computed.Value;
    }

    private static void TriggerWritableStateUpdateFailed(InMemoryHostDiagnostics diagnostics)
    {
        var state = new WritableState<int>(0, diagnostics: diagnostics);

        Assert.Throws<InvalidOperationException>(
            () => state.Update(_ => throw new InvalidOperationException("bad update")));
    }

    private static void TriggerSnapshotRestoreFailed(InMemoryHostDiagnostics diagnostics)
    {
        var registry = new ApplicationStateRegistry(diagnostics);
        var entry = new StateSnapshotEntry(
            "AtomUI.City.Tests.Missing",
            typeof(int),
            value: 1,
            version: 0,
            schemaVersion: 1,
            ownerModule: null,
            pluginId: null);

        registry.Restore(new StateSnapshot([entry]));
    }

    private static void TriggerApplicationStateAlreadyRegistered(InMemoryHostDiagnostics diagnostics)
    {
        var registry = new ApplicationStateRegistry(diagnostics);
        var key = new StateKey<int>("AtomUI.City.Tests.Duplicate");
        registry.Add(StateDefinition.Create(key, 1));

        Assert.Throws<InvalidOperationException>(() => registry.Add(StateDefinition.Create(key, 2)));
    }

    private static void TriggerStateScopeDisposeFailed(InMemoryHostDiagnostics diagnostics)
    {
        var scope = new StateScope("activation", diagnostics);
        scope.Add(new TestSubscription(() => throw new InvalidOperationException("bad dispose")));

        scope.Dispose();
    }

    private static void TriggerComputedStateDisposeFailed(InMemoryHostDiagnostics diagnostics)
    {
        var dependency = new TestReadOnlyState(
            new TestSubscription(() => throw new InvalidOperationException("bad dependency dispose")));
        var computed = new ComputedState<int>(() => 1, diagnostics, dependency);

        computed.Dispose();
    }

    private sealed record DiagnosticContextExpectation(
        string Code,
        Action<InMemoryHostDiagnostics> Trigger,
        string[] RequiredContextKeys);

    private sealed class TestReadOnlyState : IReadOnlyState<int>
    {
        private readonly IStateSubscription _subscription;

        public TestReadOnlyState(IStateSubscription subscription)
        {
            _subscription = subscription;
        }

        public int Value => 0;

        object? IReadOnlyState.Value => Value;

        public long Version => 0;

        public Type ValueType => typeof(int);

        public IStateSubscription OnChange(Action<StateChangedEventArgs<int>> handler)
        {
            return _subscription;
        }

        public IStateSubscription OnChange(
            Action<StateChangedEventArgs<int>> handler,
            StateSubscriptionOptions options)
        {
            return _subscription;
        }

        IStateSubscription IReadOnlyState.OnChange(Action<StateChangedEventArgs> handler)
        {
            return _subscription;
        }

        IStateSubscription IReadOnlyState.OnChange(
            Action<StateChangedEventArgs> handler,
            StateSubscriptionOptions options)
        {
            return _subscription;
        }

        public IStateSubscription Observe(Action<StateChangedEventArgs<int>> handler)
        {
            return _subscription;
        }

        public IStateSubscription Observe(
            Action<StateChangedEventArgs<int>> handler,
            StateSubscriptionOptions options)
        {
            return _subscription;
        }

        IStateSubscription IReadOnlyState.Observe(Action<StateChangedEventArgs> handler)
        {
            return _subscription;
        }

        IStateSubscription IReadOnlyState.Observe(
            Action<StateChangedEventArgs> handler,
            StateSubscriptionOptions options)
        {
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
