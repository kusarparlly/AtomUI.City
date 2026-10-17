using AtomUI.City.Core.Diagnostics;
using AtomUI.City.EventBus;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventChannelRuntimeTests
{
    [Fact]
    public void ChannelContractsExposeStableDefaultsAndEnumValues()
    {
        Assert.Equal(256, EventChannelOptions.DefaultCapacity);
        Assert.Equal(EventChannelOptions.DefaultCapacity, EventChannelOptions.Default.Capacity);
        Assert.Equal(
            EventBusRuntimeOptions.DefaultMaximumChannelRuntimes,
            EventBusRuntimeOptions.Default.MaximumChannelRuntimes);
        Assert.Equal(EventChannelBackpressurePolicy.Wait, EventChannelOptions.Default.BackpressurePolicy);
        Assert.Equal(EventChannelExecutionMode.Serialized, EventChannelOptions.Default.ExecutionMode);
        Assert.Equal(1, EventChannelOptions.Default.MaximumConcurrency);
        Assert.Null(EventChannelOptions.Default.QueueWaitTimeout);
        Assert.Equal(0, (int)EventChannelBackpressurePolicy.Wait);
        Assert.Equal(1, (int)EventChannelBackpressurePolicy.Reject);
        Assert.Equal(2, (int)EventChannelBackpressurePolicy.DropOldest);
        Assert.Equal(3, (int)EventChannelBackpressurePolicy.DropNewest);
        Assert.Equal(4, (int)EventChannelBackpressurePolicy.CoalesceLatest);
        Assert.Equal(0, (int)EventChannelExecutionMode.Serialized);
        Assert.Equal(1, (int)EventChannelExecutionMode.Partitioned);
        Assert.Equal(2, (int)EventChannelExecutionMode.Concurrent);
    }

    [Fact]
    public void ChannelOptionsRejectInvalidBoundaries()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryEventBus(
            channelOptions: new EventChannelOptions { Capacity = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                BackpressurePolicy = (EventChannelBackpressurePolicy)999,
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryEventBus(
            channelOptions: new EventChannelOptions { MaximumConcurrency = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryEventBus(
            channelOptions: new EventChannelOptions { QueueWaitTimeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                QueueWaitTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1),
            }));
        Assert.Throws<ArgumentException>(() => new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                ExecutionMode = EventChannelExecutionMode.Serialized,
                MaximumConcurrency = 2,
            }));
    }

    [Fact]
    public void RuntimeOptionsRejectInvalidChannelRuntimeLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryEventBus(
            runtimeOptions: new EventBusRuntimeOptions { MaximumChannelRuntimes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryEventBus(
            runtimeOptions: new EventBusRuntimeOptions
            {
                MaximumChannelRuntimes = EventBusRuntimeOptions.MaximumAllowedChannelRuntimes + 1,
            }));
    }

    [Fact]
    public void ConfiguredChannelsCannotExceedRuntimeLimit()
    {
        var descriptors = new[]
        {
            EventChannelDescriptor.Create(
                new EventChannel<TestEvent>("first"),
                EventChannelOptions.Default),
            EventChannelDescriptor.Create(
                new EventChannel<TestEvent>("second"),
                EventChannelOptions.Default),
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new InMemoryEventBus(
            channelDescriptors: descriptors,
            runtimeOptions: new EventBusRuntimeOptions { MaximumChannelRuntimes = 1 }));

        Assert.Contains("configured event channels exceed", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" channel ")]
    [InlineData("channel\nname")]
    public void EventChannelRejectsInvalidNames(string name)
    {
        Assert.Throws<ArgumentException>(() => new EventChannel<TestEvent>(name));
    }

    [Fact]
    public async Task DefaultEventChannelValueCannotEnterRuntime()
    {
        await using var eventBus = new InMemoryEventBus();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await eventBus.PublishAsync(default(EventChannel<TestEvent>), new TestEvent(1)));
        Assert.Throws<ArgumentException>(() =>
            eventBus.Subscribe(default(EventChannel<TestEvent>), _ => { }));
    }

    [Fact]
    public async Task NamedChannelsIsolateSubscriptionsAndOrderingDomains()
    {
        await using var eventBus = new InMemoryEventBus();
        var documents = new EventChannel<TestEvent>("documents");
        var windows = new EventChannel<TestEvent>("windows");
        var documentEvents = new List<int>();
        var windowEvents = new List<int>();

        eventBus.Subscribe(documents, context => documentEvents.Add(context.Event.Sequence));
        eventBus.Subscribe(windows, context => windowEvents.Add(context.Event.Sequence));

        await eventBus.PublishAsync(documents, new TestEvent(1));
        await eventBus.PublishAsync(windows, new TestEvent(2));

        Assert.Equal([1], documentEvents);
        Assert.Equal([2], windowEvents);
        Assert.Equal(2, eventBus.GetChannelSnapshots().Count);
    }

    [Fact]
    public async Task PostThenPublishShareOneSerializedAdmissionOrder()
    {
        await using var eventBus = new InMemoryEventBus();
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();
        var observed = new List<string>();

        eventBus.Subscribe<TestEvent>(async (context, cancellationToken) =>
        {
            lock (observed)
            {
                observed.Add($"start:{context.Event.Sequence}");
            }

            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }

            lock (observed)
            {
                observed.Add($"end:{context.Event.Sequence}");
            }
        });

        var posted = await eventBus.PostAsync(new TestEvent(1));
        var second = eventBus.PublishAsync(new TestEvent(2)).AsTask();

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(posted.Accepted);
        Assert.False(second.IsCompleted);

        releaseFirst.TrySetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["start:1", "end:1", "start:2", "end:2"], observed);
    }

    [Fact]
    public async Task QueuedPublicationUsesTheSubscriptionSnapshotCapturedBeforeAdmission()
    {
        await using var eventBus = new InMemoryEventBus();
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();
        var lateSubscriberDeliveries = 0;

        eventBus.Subscribe<TestEvent>(async (context, cancellationToken) =>
        {
            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent(2))).Accepted);

        eventBus.Subscribe<TestEvent>(_ =>
        {
            Interlocked.Increment(ref lateSubscriberDeliveries);
            return ValueTask.CompletedTask;
        });

        releaseFirst.TrySetResult();
        await eventBus.PublishAsync(new TestEvent(3)).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, Volatile.Read(ref lateSubscriberDeliveries));
    }

    [Fact]
    public async Task WaitBackpressureBoundsPendingPublications()
    {
        await using var eventBus = CreateBus(1, EventChannelBackpressurePolicy.Wait);
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();

        eventBus.Subscribe<TestEvent>(async (context, cancellationToken) =>
        {
            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent(2))).Accepted);

        var blockedPost = eventBus.PostAsync(new TestEvent(3)).AsTask();
        await Task.Delay(100);
        Assert.False(blockedPost.IsCompleted);

        releaseFirst.TrySetResult();
        Assert.True((await blockedPost.WaitAsync(TimeSpan.FromSeconds(5))).Accepted);
        await eventBus.PublishAsync(new TestEvent(4)).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WaitingPostCanBeCanceledBeforeAdmission()
    {
        await using var eventBus = CreateBus(1, EventChannelBackpressurePolicy.Wait);
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent(2))).Accepted);

        using var cancellation = new CancellationTokenSource();
        var waiting = eventBus.PostAsync(new TestEvent(3), cancellationToken: cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        var result = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Accepted);
        Assert.Contains("canceled", result.RejectionReason, StringComparison.OrdinalIgnoreCase);
        releaseFirst.TrySetResult();
    }

    [Fact]
    public async Task WaitBackpressureReturnsTimedOutWhenCapacityDoesNotOpen()
    {
        await using var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                Capacity = 1,
                QueueWaitTimeout = TimeSpan.FromMilliseconds(50),
            });
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent(2))).Accepted);

        var timedOut = await eventBus.PostAsync(new TestEvent(3));
        Assert.False(timedOut.Accepted);
        Assert.Contains("timeout", timedOut.RejectionReason, StringComparison.OrdinalIgnoreCase);
        releaseFirst.TrySetResult();
    }

    [Fact]
    public async Task ConcurrentModeHonorsMaximumConcurrency()
    {
        await using var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                Capacity = 32,
                ExecutionMode = EventChannelExecutionMode.Concurrent,
                MaximumConcurrency = 3,
            });
        var threeEntered = NewCompletion();
        var allCompleted = NewCompletion();
        var release = NewCompletion();
        var current = 0;
        var maximum = 0;
        var completed = 0;

        eventBus.Subscribe<TestEvent>(async _ =>
        {
            var now = Interlocked.Increment(ref current);
            UpdateMaximum(ref maximum, now);
            if (now == 3)
            {
                threeEntered.TrySetResult();
            }

            await release.Task.ConfigureAwait(false);
            Interlocked.Decrement(ref current);
            if (Interlocked.Increment(ref completed) == 12)
            {
                allCompleted.TrySetResult();
            }
        }, EventSubscriptionOptions.Current);

        for (var sequence = 0; sequence < 12; sequence++)
        {
            Assert.True((await eventBus.PostAsync(new TestEvent(sequence))).Accepted);
        }

        await threeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, Volatile.Read(ref maximum));
        release.TrySetResult();
        await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.InRange(Volatile.Read(ref maximum), 1, 3);
    }

    [Fact]
    public async Task PartitionedModeSerializesSameKeyAndRunsDifferentKeysConcurrently()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            channelOptions: new EventChannelOptions
            {
                Capacity = 16,
                ExecutionMode = EventChannelExecutionMode.Partitioned,
                MaximumConcurrency = 4,
            });
        var a1Entered = NewCompletion();
        var b1Entered = NewCompletion();
        var a2Entered = NewCompletion();
        var releaseA1 = NewCompletion();
        var observedA = new List<int>();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Sequence is 1 or 2)
            {
                lock (observedA)
                {
                    observedA.Add(context.Event.Sequence);
                }
            }

            switch (context.Event.Sequence)
            {
                case 1:
                    a1Entered.TrySetResult();
                    await releaseA1.Task.ConfigureAwait(false);
                    break;
                case 2:
                    a2Entered.TrySetResult();
                    break;
                case 10:
                    b1Entered.TrySetResult();
                    break;
            }
        }, EventSubscriptionOptions.Current);

        Assert.True((await eventBus.PostAsync(
            new TestEvent(1),
            new EventPublishOptions { PartitionKey = "A" })).Accepted);
        await a1Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(
            new TestEvent(2),
            new EventPublishOptions { PartitionKey = "A" })).Accepted);
        Assert.True((await eventBus.PostAsync(
            new TestEvent(10),
            new EventPublishOptions { PartitionKey = "B" })).Accepted);

        await b1Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(a2Entered.Task.IsCompleted);
        releaseA1.TrySetResult();
        await a2Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([1, 2], observedA);
        var published = diagnostics.Records
            .Where(record => record.Code == EventDiagnosticIds.EventPublished)
            .ToArray();
        Assert.Equal(3, published.Length);
        Assert.All(published, record =>
        {
            var partitionHash = Assert.IsType<string>(record.Context!["partitionHash"]);
            Assert.Equal(16, partitionHash.Length);
            Assert.DoesNotContain(partitionHash, new[] { "A", "B" });
            Assert.False(record.Context.ContainsKey("partition"));
        });
    }

    [Fact]
    public async Task PartitionKeyMustMatchConfiguredExecutionMode()
    {
        await using var partitioned = new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                ExecutionMode = EventChannelExecutionMode.Partitioned,
                MaximumConcurrency = 2,
            });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await partitioned.PublishAsync(new TestEvent(1)));

        await using var serialized = new InMemoryEventBus();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await serialized.PublishAsync(
                new TestEvent(1),
                new EventPublishOptions { PartitionKey = "unexpected" }));
    }

    [Fact]
    public async Task PartitionedModePreservesEveryPartitionUnderSustainedAdmissions()
    {
        const int partitionCount = 8;
        const int eventsPerPartition = 250;
        await using var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                Capacity = 31,
                ExecutionMode = EventChannelExecutionMode.Partitioned,
                MaximumConcurrency = 4,
            });
        var observed = Enumerable.Range(0, partitionCount)
            .Select(_ => new List<int>(eventsPerPartition + 1))
            .ToArray();

        eventBus.Subscribe<TestEvent>(context =>
        {
            var partition = context.Event.Sequence % partitionCount;
            observed[partition].Add(context.Event.Sequence);
        }, EventSubscriptionOptions.Current);

        for (var round = 0; round < eventsPerPartition; round++)
        {
            for (var partition = 0; partition < partitionCount; partition++)
            {
                var sequence = (round * partitionCount) + partition;
                var result = await eventBus.PostAsync(
                    new TestEvent(sequence),
                    new EventPublishOptions { PartitionKey = partition.ToString() });
                Assert.True(result.Accepted);
            }
        }

        for (var partition = 0; partition < partitionCount; partition++)
        {
            var sentinel = (eventsPerPartition * partitionCount) + partition;
            await eventBus.PublishAsync(
                new TestEvent(sentinel),
                new EventPublishOptions { PartitionKey = partition.ToString() });
            Assert.Equal(
                Enumerable.Range(0, eventsPerPartition + 1)
                    .Select(round => (round * partitionCount) + partition),
                observed[partition]);
        }
    }

    [Theory]
    [InlineData(EventChannelBackpressurePolicy.Reject)]
    [InlineData(EventChannelBackpressurePolicy.DropNewest)]
    public async Task RejectingBackpressureReportsFullChannelWithoutCreatingBackgroundWork(
        EventChannelBackpressurePolicy policy)
    {
        await using var eventBus = CreateBus(1, policy);
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();
        var secondHandled = NewCompletion();
        var handled = new List<int>();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            lock (handled)
            {
                handled.Add(context.Event.Sequence);
            }

            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
            else if (context.Event.Sequence == 2)
            {
                secondHandled.TrySetResult();
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent(2))).Accepted);

        var rejected = await eventBus.PostAsync(new TestEvent(3));
        Assert.False(rejected.Accepted);
        Assert.Contains("full", rejected.RejectionReason, StringComparison.OrdinalIgnoreCase);

        releaseFirst.TrySetResult();
        await secondHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await eventBus.PublishAsync(new TestEvent(4)).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([1, 2, 4], handled);
    }

    [Fact]
    public async Task PublishAsyncReportsExplicitRejectionWhenConfiguredChannelIsFull()
    {
        await using var eventBus = CreateBus(1, EventChannelBackpressurePolicy.Reject);
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent(2))).Accepted);

        await Assert.ThrowsAsync<EventPublicationRejectedException>(async () =>
            await eventBus.PublishAsync(new TestEvent(3)));
        releaseFirst.TrySetResult();
    }

    [Theory]
    [InlineData(EventChannelBackpressurePolicy.DropOldest)]
    [InlineData(EventChannelBackpressurePolicy.CoalesceLatest)]
    public async Task ReplacementPoliciesKeepTheNewerPendingPublication(
        EventChannelBackpressurePolicy policy)
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            channelOptions: new EventChannelOptions
            {
                Capacity = 1,
                BackpressurePolicy = policy,
            });
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();
        var thirdHandled = NewCompletion();
        var handled = new List<int>();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            lock (handled)
            {
                handled.Add(context.Event.Sequence);
            }

            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
            else if (context.Event.Sequence == 3)
            {
                thirdHandled.TrySetResult();
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent(2))).Accepted);
        Assert.True((await eventBus.PostAsync(new TestEvent(3))).Accepted);

        releaseFirst.TrySetResult();
        await thirdHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await eventBus.PublishAsync(new TestEvent(4)).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 3, 4], handled);
        var dropped = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventDropped);
        var backpressure = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventChannelBackpressure);
        Assert.Equal(EventChannel<TestEvent>.Default.Name, dropped.Context!["channel"]);
        Assert.Equal(policy.ToString(), dropped.Context["backpressurePolicy"]);
        Assert.Equal(dropped.Context["eventId"], backpressure.Context["eventId"]);
        Assert.Equal(policy.ToString(), backpressure.Context["backpressurePolicy"]);
        var metrics = Assert.Single(eventBus.GetChannelSnapshots());
        Assert.True(metrics.TotalQueueWaitDuration > TimeSpan.Zero);
        Assert.True(metrics.MaximumQueueWaitDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task DropOldestCompletesDisplacedPublishWithExplicitFailure()
    {
        await using var eventBus = CreateBus(1, EventChannelBackpressurePolicy.DropOldest);
        var firstEntered = NewCompletion();
        var releaseFirst = NewCompletion();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent(1))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var displaced = eventBus.PublishAsync(new TestEvent(2)).AsTask();
        Assert.True((await eventBus.PostAsync(new TestEvent(3))).Accepted);

        var exception = await Assert.ThrowsAsync<EventPublicationRejectedException>(async () =>
            await displaced.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("oldest", exception.Message, StringComparison.OrdinalIgnoreCase);
        releaseFirst.TrySetResult();
    }

    [Fact]
    public async Task SerializedHandlerCannotAwaitNestedPublishOnItsOwnChannel()
    {
        await using var eventBus = new InMemoryEventBus();
        Exception? nestedFailure = null;

        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Sequence != 1)
            {
                return;
            }

            nestedFailure = await Record.ExceptionAsync(async () =>
                await eventBus.PublishAsync(new TestEvent(2)));
        });

        var result = await eventBus.PublishAsync(new TestEvent(1));

        Assert.True(result.Succeeded);
        Assert.IsType<EventPublicationRejectedException>(nestedFailure);
    }

    [Fact]
    public async Task DisposeAsyncRejectsPendingPublishAndCancelsInFlightDelivery()
    {
        var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions { Capacity = 1 });
        var firstEntered = NewCompletion();

        eventBus.Subscribe<TestEvent>(async (context, cancellationToken) =>
        {
            if (context.Event.Sequence == 1)
            {
                firstEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        });

        var first = eventBus.PublishAsync(new TestEvent(1)).AsTask();
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pending = eventBus.PublishAsync(new TestEvent(2)).AsTask();

        await eventBus.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, firstResult.CanceledCount);
        await Assert.ThrowsAsync<EventPublicationRejectedException>(async () =>
            await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await eventBus.PublishAsync(new TestEvent(3)));
    }

    [Fact]
    public async Task ConcurrentDisposeCallsShareOneChannelTerminationTransaction()
    {
        var eventBus = new InMemoryEventBus();
        await eventBus.PublishAsync(new TestEvent(1));

        var disposals = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(async () => await eventBus.DisposeAsync()))
            .ToArray();
        eventBus.Dispose();

        await Task.WhenAll(disposals).WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await eventBus.PostAsync(new TestEvent(2)));
    }

    [Fact]
    public async Task SerializedChannelPreservesOrderAcrossOneThousandMixedAdmissions()
    {
        await using var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions { Capacity = 17 });
        var observed = new List<int>();

        eventBus.Subscribe<TestEvent>(context =>
        {
            lock (observed)
            {
                observed.Add(context.Event.Sequence);
            }

            return ValueTask.CompletedTask;
        });

        for (var sequence = 0; sequence < 1_000; sequence++)
        {
            var result = await eventBus.PostAsync(new TestEvent(sequence));
            Assert.True(result.Accepted);
        }

        await eventBus.PublishAsync(new TestEvent(1_000)).AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(Enumerable.Range(0, 1_001), observed);
    }

    [Fact]
    public async Task BoundedChannelDoesNotLoseConcurrentWaitAdmissions()
    {
        await using var eventBus = new InMemoryEventBus(
            channelOptions: new EventChannelOptions { Capacity = 7 });
        var observed = new HashSet<int>();

        eventBus.Subscribe<TestEvent>(context =>
        {
            lock (observed)
            {
                Assert.True(observed.Add(context.Event.Sequence));
            }

            return ValueTask.CompletedTask;
        });

        var producers = Enumerable.Range(0, 8)
            .Select(producer => Task.Run(async () =>
            {
                for (var offset = 0; offset < 250; offset++)
                {
                    var result = await eventBus.PostAsync(new TestEvent((producer * 250) + offset));
                    Assert.True(result.Accepted);
                }
            }))
            .ToArray();

        await Task.WhenAll(producers).WaitAsync(TimeSpan.FromSeconds(15));
        await eventBus.PublishAsync(new TestEvent(2_000)).AsTask().WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(2_001, observed.Count);
        Assert.Equal(Enumerable.Range(0, 2_001), observed.Order());
    }

    [Fact]
    public async Task ChannelMetricsExposeBoundedRuntimeCounters()
    {
        await using var eventBus = CreateBus(2, EventChannelBackpressurePolicy.Reject);

        await eventBus.PublishAsync(new TestEvent(1));
        await eventBus.PublishAsync(new TestEvent(2));

        var snapshot = Assert.Single(eventBus.GetChannelSnapshots());
        Assert.Equal(EventChannel<TestEvent>.Default.Name, snapshot.ChannelName);
        Assert.Equal(EventChannelExecutionMode.Serialized, snapshot.ExecutionMode);
        Assert.Equal(2, snapshot.Capacity);
        Assert.Equal(0, snapshot.PendingCount);
        Assert.Equal(0, snapshot.InFlightCount);
        Assert.Equal(2, snapshot.AcceptedCount);
        Assert.Equal(0, snapshot.RejectedCount);
        Assert.Equal(0, snapshot.DroppedCount);
        Assert.Equal(2, snapshot.CompletedCount);
        Assert.Equal(0, snapshot.FailedCount);
        Assert.True(snapshot.TotalQueueWaitDuration >= TimeSpan.Zero);
        Assert.True(snapshot.MaximumQueueWaitDuration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task ChannelRuntimeLimitRejectsNewIdentityWithoutDisruptingExistingRuntime()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            runtimeOptions: new EventBusRuntimeOptions { MaximumChannelRuntimes = 2 });
        var first = new EventChannel<TestEvent>("first");
        var second = new EventChannel<TestEvent>("second");
        var overflow = new EventChannel<TestEvent>("overflow");

        await eventBus.PublishAsync(first, new TestEvent(1));
        await eventBus.PublishAsync(second, new TestEvent(2));

        var rejectedPost = await eventBus.PostAsync(overflow, new TestEvent(3));
        var rejectedPublish = await Assert.ThrowsAsync<EventPublicationRejectedException>(async () =>
            await eventBus.PublishAsync(overflow, new TestEvent(4)));
        var existingResult = await eventBus.PublishAsync(first, new TestEvent(5));

        Assert.False(rejectedPost.Accepted);
        Assert.Contains("maximum number", rejectedPost.RejectionReason, StringComparison.Ordinal);
        Assert.Contains("maximum number", rejectedPublish.Message, StringComparison.Ordinal);
        Assert.True(existingResult.Succeeded);
        Assert.Equal(2, eventBus.GetChannelSnapshots().Count);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventRejected &&
                      record.Message.Contains("maximum number", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConcurrentDynamicChannelsCannotExceedRuntimeLimit()
    {
        const int runtimeLimit = 8;
        await using var eventBus = new InMemoryEventBus(
            runtimeOptions: new EventBusRuntimeOptions { MaximumChannelRuntimes = runtimeLimit });

        var admissions = await Task.WhenAll(
            Enumerable.Range(0, 128)
                .Select(index => eventBus.PostAsync(
                        new EventChannel<TestEvent>($"dynamic-{index}"),
                        new TestEvent(index))
                    .AsTask()));

        Assert.Equal(runtimeLimit, admissions.Count(result => result.Accepted));
        Assert.Equal(128 - runtimeLimit, admissions.Count(result => !result.Accepted));
        Assert.Equal(runtimeLimit, eventBus.GetChannelSnapshots().Count);
    }

    private static InMemoryEventBus CreateBus(
        int capacity,
        EventChannelBackpressurePolicy policy)
    {
        return new InMemoryEventBus(
            channelOptions: new EventChannelOptions
            {
                Capacity = capacity,
                BackpressurePolicy = policy,
            });
    }

    private static TaskCompletionSource NewCompletion()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var observed = Volatile.Read(ref maximum);
        while (candidate > observed)
        {
            var previous = Interlocked.CompareExchange(ref maximum, candidate, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }

    private sealed record TestEvent(int Sequence);
}
