using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.EventBus;

internal interface IEventBusLifecycleController
{
    ValueTask StartAsync(
        LifecycleScope applicationScope,
        CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken waitCancellationToken = default);
}

internal sealed class EventBusHostManagedMarker;
