using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.StressCli.DataIntegration;

public interface IStressRemoteProjection
{
    void Activate(LifecycleScope owner);
}

public sealed class StressRemoteProjection(
    IEventBus eventBus,
    IApplicationState state,
    IApplicationStateWriter writer) : IStressRemoteProjection
{
    public void Activate(LifecycleScope owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        eventBus.Subscribe<RemoteProductLoaded>(owner, (context, _) =>
        {
            writer.Update(PhaseD.StateCatalog.RemoteProductsLoaded, value => value + 1);
            writer.Set(PhaseD.StateCatalog.RemoteInventory, context.Event.Product.Quantity);
            writer.Set(PhaseD.StateCatalog.RemoteStatus, "product-loaded");
        });
        eventBus.Subscribe<RemoteOrderSubmitted>(owner, (context, _) =>
        {
            writer.Update(PhaseD.StateCatalog.RemoteOrdersSubmitted, value => value + 1);
            writer.Update(PhaseD.StateCatalog.RemoteRevenue, value => value + context.Event.Receipt.Amount);
            writer.Set(PhaseD.StateCatalog.RemoteLastOrderId, context.Event.Receipt.OrderId);
            writer.Set(PhaseD.StateCatalog.RemoteStatus, "order-submitted");
        });
        eventBus.Subscribe<RemoteDataFailed>(owner, (context, _) =>
        {
            writer.Update(PhaseD.StateCatalog.RemoteFailures, value => value + 1);
            writer.Set(PhaseD.StateCatalog.RemoteStatus, $"failed:{context.Event.ErrorKind}");
            writer.Set(PhaseD.StateCatalog.RemoteMessage, context.Event.MessageKey);
        });
        eventBus.Subscribe<RemoteInventoryChanged>(owner, (context, _) =>
        {
            if (IsCurrentPrincipal(context.Event.PrincipalRevision))
            {
                writer.Set(PhaseD.StateCatalog.RemoteInventory, context.Event.Quantity);
                writer.Update(PhaseD.StateCatalog.RemoteRealtimeUpdates, value => value + 1);
            }
        });
        eventBus.Subscribe<RemotePriceChanged>(owner, (context, _) =>
        {
            if (IsCurrentPrincipal(context.Event.PrincipalRevision))
            {
                writer.Update(PhaseD.StateCatalog.RemoteRealtimeUpdates, value => value + 1);
                writer.Set(PhaseD.StateCatalog.RemoteStatus, $"price:{context.Event.Price:0.00}");
            }
        });
        eventBus.Subscribe<RemoteShipmentProgressed>(owner, (context, _) =>
        {
            if (IsCurrentPrincipal(context.Event.PrincipalRevision))
            {
                writer.Update(PhaseD.StateCatalog.RemoteRealtimeUpdates, value => value + 1);
                writer.Set(PhaseD.StateCatalog.RemoteStatus, $"shipment:{context.Event.Status}");
            }
        });
        eventBus.Subscribe<RemotePrincipalSwitched>(owner, (context, _) =>
        {
            writer.Set(PhaseD.StateCatalog.RemotePrincipal, context.Event.CurrentPrincipal);
            writer.Set(PhaseD.StateCatalog.RemotePrincipalRevision, context.Event.CurrentRevision);
            writer.Set(PhaseD.StateCatalog.RemoteStatus, "principal-switched");
        });
    }

    private bool IsCurrentPrincipal(string revision) =>
        string.Equals(
            state.Get(PhaseD.StateCatalog.RemotePrincipalRevision).Value,
            revision,
            StringComparison.Ordinal);
}

public sealed class StressInventoryOptimisticUpdate(
    IApplicationState state,
    IApplicationStateWriter writer,
    int quantity) : IDataOptimisticUpdate
{
    private int _before;

    public ValueTask ApplyAsync(DataRequestContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _before = state.Get(PhaseD.StateCatalog.RemoteInventory).Value;
        writer.Set(PhaseD.StateCatalog.RemoteInventory, Math.Max(0, _before - quantity));
        writer.Update(PhaseD.StateCatalog.RemotePendingOptimistic, value => value + 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask ConfirmAsync(DataRequestContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        writer.Update(PhaseD.StateCatalog.RemotePendingOptimistic, value => Math.Max(0, value - 1));
        return ValueTask.CompletedTask;
    }

    public ValueTask RollBackAsync(DataRequestContext context, CancellationToken cancellationToken = default)
    {
        writer.Set(PhaseD.StateCatalog.RemoteInventory, _before);
        writer.Update(PhaseD.StateCatalog.RemotePendingOptimistic, value => Math.Max(0, value - 1));
        return ValueTask.CompletedTask;
    }
}
