using System.Collections.Concurrent;
using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.Fixtures.StressCli.Services;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli.ViewModels;

/// <summary>
/// 实战 ViewModel 基类：激活时把 State Reaction 与 EventBus 订阅
/// 全部绑定进 ActivationScope（EventBus 经 Core 子 scope 组合桥接），
/// 停用后全部失效（I8 零泄漏断言的载体）。
/// </summary>
public abstract class FixtureViewModelBase : ViewModelBase
{
    private readonly IEventBus _eventBus;
    private readonly LifecycleScope _eventOwnerRoot;
    private readonly IApplicationState _state;
    private readonly IApplicationStateWriter _writer;
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.Ordinal);

    protected FixtureViewModelBase(
        string name,
        IEventBus eventBus,
        LifecycleScope eventOwnerRoot,
        IApplicationState state,
        IApplicationStateWriter writer,
        IHostDiagnostics? diagnostics)
        : base(diagnostics)
    {
        _writer = writer;
        Name = name;
        _eventBus = eventBus;
        _eventOwnerRoot = eventOwnerRoot;
        _state = state;
        StateKey = new StateKey<int>($"fixtures.viewmodel.{name.ToLowerInvariant()}");

        RefreshState = new CommandExecutionState($"{name}.refresh", GetType());
        FailState = new CommandExecutionState($"{name}.fail", GetType());

        RefreshCommand = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                Bump("refresh");
            },
            RefreshState,
            diagnostics: diagnostics);

        FailCommand = CommandFactory.Create(
            () => throw new InvalidOperationException($"{name} 必然失败的命令。"),
            state: FailState,
            diagnostics: diagnostics);
    }

    public string Name { get; }

    public StateKey<int> StateKey { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public CommandExecutionState RefreshState { get; }

    public Interaction<string, bool> Confirm { get; } = new();

    public IRelayCommand FailCommand { get; }

    public CommandExecutionState FailState { get; }

    public int CountOf(string key)
    {
        lock (_counters)
        {
            return _counters.TryGetValue(key, out var value) ? value : 0;
        }
    }

    protected void Bump(string key)
    {
        lock (_counters)
        {
            _counters[key] = _counters.TryGetValue(key, out var value) ? value + 1 : 1;
        }
    }

    protected override async ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        // State Reaction → ActivationScope（停用即释放）。
        context.Scope.Add(_state.Get(StateKey).OnChange(_ => Bump("state-reaction")));

        // EventBus 订阅 → Core 子 scope 承载生命周期，桥接进 ActivationScope。
        var coreScope = _eventOwnerRoot.CreateChild(LifecycleScopeKind.Subscription, $"vm-{Name}");
        context.Scope.Add(coreScope);

        _eventBus.Subscribe<SettingsChanged>(
            coreScope,
            (_, _) =>
            {
                Bump("event");
                return ValueTask.CompletedTask;
            },
            EventSubscriptionOptions.Background());

        Bump("activated");
    }

    public void SeedState(int value)
    {
        _writer.GetWritable(StateKey).Set(value);
    }
}

// 16 个基础实战 ViewModel；第 17 个 RemoteOperationsViewModel 在 Data 场景中验证。

public sealed class ShellViewModel : FixtureViewModelBase
{
    public ShellViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Shell", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class DashboardViewModel : FixtureViewModelBase
{
    public DashboardViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Dashboard", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class OrdersViewModel : FixtureViewModelBase
{
    public OrdersViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Orders", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class InventoryViewModel : FixtureViewModelBase
{
    public InventoryViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Inventory", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class CustomersViewModel : FixtureViewModelBase
{
    public CustomersViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Customers", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class BillingViewModel : FixtureViewModelBase
{
    public BillingViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Billing", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class ReportViewModel : FixtureViewModelBase
{
    public ReportViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Report", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class NotificationsViewModel : FixtureViewModelBase
{
    public NotificationsViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Notifications", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class AuditViewModel : FixtureViewModelBase
{
    public AuditViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Audit", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class SettingsViewModel : FixtureViewModelBase
{
    public SettingsViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Settings", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class ProductsViewModel : FixtureViewModelBase
{
    public ProductsViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Products", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class PaymentsViewModel : FixtureViewModelBase
{
    public PaymentsViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Payments", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class FulfillmentViewModel : FixtureViewModelBase
{
    public FulfillmentViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Fulfillment", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class SearchViewModel : FixtureViewModelBase
{
    public SearchViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Search", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class SupportViewModel : FixtureViewModelBase
{
    public SupportViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Support", bus, owner, state, writer, diagnostics) => _ = writer;
}

public sealed class WorkflowViewModel : FixtureViewModelBase
{
    public WorkflowViewModel(IEventBus bus, LifecycleScope owner, IApplicationState state, IApplicationStateWriter writer, IHostDiagnostics? diagnostics)
        : base("Workflow", bus, owner, state, writer, diagnostics) => _ = writer;
}
