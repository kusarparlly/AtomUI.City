using AtomUI.City.EventBus;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodConcurrencyWorkload
{
    private readonly DogfoodRunLedger _ledger;
    private int _executed;

    public DogfoodConcurrencyWorkload(DogfoodRunLedger ledger)
    {
        _ledger = ledger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _executed, 1) != 0)
        {
            throw new InvalidOperationException("The concurrency workload can only run once.");
        }

        await VerifyBoundedEventQueueAsync(cancellationToken);
        var maximumConcurrency = await VerifyConcurrentEventExecutionAsync(cancellationToken);
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_CONCURRENCY queueTimeouts=1 maxEventConcurrency={maximumConcurrency} cancellationBoundaries=2");
    }

    private async Task VerifyBoundedEventQueueAsync(CancellationToken cancellationToken)
    {
        await using var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                Capacity = 1,
                QueueWaitTimeout = TimeSpan.FromMilliseconds(30),
            });
        using var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Operation, "dogfood.eventbus.backpressure");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = 0;
        using var subscription = eventBus.Subscribe<PressureEvent>(owner, async context =>
        {
            if (context.Event.Sequence == 1)
            {
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
            }

            if (Interlocked.Increment(ref handled) == 2)
            {
                completed.TrySetResult();
            }
        });

        if (!(await eventBus.PostAsync(new PressureEvent(1), cancellationToken: cancellationToken)).Accepted)
        {
            throw new InvalidOperationException("EventBus rejected the pressure probe's first event.");
        }

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        if (!(await eventBus.PostAsync(new PressureEvent(2), cancellationToken: cancellationToken)).Accepted)
        {
            throw new InvalidOperationException("EventBus rejected the pressure probe's queued event.");
        }

        var rejected = await eventBus.PostAsync(new PressureEvent(3), cancellationToken: cancellationToken);
        if (rejected.Accepted || rejected.RejectionReason?.Contains("timeout", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidOperationException("EventBus bounded queue did not return its stable timeout rejection.");
        }

        _ledger.Record("backpressure", "eventbus-wait-timeout", "expected-failure");
        release.TrySetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    private async Task<int> VerifyConcurrentEventExecutionAsync(CancellationToken cancellationToken)
    {
        await using var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                Capacity = 16,
                ExecutionMode = EventChannelExecutionMode.Concurrent,
                MaximumConcurrency = 3,
            });
        using var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Operation, "dogfood.eventbus.concurrent");
        var threeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = 0;
        var maximum = 0;
        var handled = 0;
        using var subscription = eventBus.Subscribe<PressureEvent>(
            owner,
            async _ =>
            {
                var active = Interlocked.Increment(ref current);
                UpdateMaximum(ref maximum, active);
                if (active == 3)
                {
                    threeEntered.TrySetResult();
                }

                await release.Task.ConfigureAwait(false);
                Interlocked.Decrement(ref current);
                if (Interlocked.Increment(ref handled) == 12)
                {
                    completed.TrySetResult();
                }
            },
            EventSubscriptionOptions.Current);

        for (var index = 0; index < 12; index++)
        {
            var result = await eventBus.PostAsync(new PressureEvent(index), cancellationToken: cancellationToken);
            if (!result.Accepted)
            {
                throw new InvalidOperationException("EventBus concurrent channel rejected a pressure event.");
            }
        }

        await threeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        release.TrySetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        if (maximum != 3)
        {
            throw new InvalidOperationException($"EventBus maximum concurrency expected 3; observed {maximum}.");
        }

        for (var index = 0; index < 12; index++)
        {
            _ledger.Record("event-concurrent", $"pressure-{index}");
        }

        return maximum;
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var current = Volatile.Read(ref maximum);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref maximum, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private sealed record PressureEvent(int Sequence);
}
