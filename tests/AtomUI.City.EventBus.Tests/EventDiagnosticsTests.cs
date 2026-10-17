using System.Reflection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.EventBus;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventDiagnosticsTests
{
    [Fact]
    public void EventDiagnosticIdsExposeStableCodeContract()
    {
        var fields = typeof(EventDiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();

        var codes = fields
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "EventBus.EventAccepted",
                "EventBus.EventChannelBackpressure",
                "EventBus.EventContractRejected",
                "EventBus.EventDeliveryCancelled",
                "EventBus.EventDeliveryCompleted",
                "EventBus.EventDeliveryFailed",
                "EventBus.EventDeliveryStarted",
                "EventBus.EventDeliveryTimedOut",
                "EventBus.EventDropped",
                "EventBus.EventPayloadProjectionFailed",
                "EventBus.EventPluginDrainTimedOut",
                "EventBus.EventPublished",
                "EventBus.EventRejected",
                "EventBus.EventSubscriptionAdded",
                "EventBus.EventSubscriptionDisabled",
                "EventBus.EventSubscriptionDisposed",
                "EventBus.EventSubscriptionQuiescing",
                "EventBus.EventSubscriptionTerminationFailed",
                "EventBus.PluginContributionActivated",
                "EventBus.PluginContributionDisposed",
                "EventBus.PluginContributionQuiescing",
                "EventBus.PluginContributionRejected"
            ],
            codes);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.StartsWith("EventBus.", code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EventPublishedDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var result = await eventBus.PublishAsync(new TestEvent("published"));

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventPublished);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EventAcceptedDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var result = await eventBus.PostAsync(new TestEvent("accepted"));

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventAccepted);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EventRejectedDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await eventBus.PostAsync(
            new TestEvent("rejected"),
            cancellationToken: cancellation.Token);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventRejected);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerFailureIsReportedAndDoesNotStopIndependentHandlers()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var independentHandlerCalled = false;

        eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
        eventBus.Subscribe<TestEvent>(_ =>
        {
            independentHandlerCalled = true;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        Assert.False(result.Succeeded);
        Assert.True(independentHandlerCalled);
        Assert.Equal(2, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(result.Deliveries, delivery => !delivery.Succeeded);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task HandlerFailureDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var subscription = eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
        Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
        AssertDeliveryContext(record, result.ContractId, result.EventId, subscription.Id);
    }

    [Fact]
    public async Task StopPublicationErrorPolicySkipsLaterHandlers()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var laterHandlerCalled = false;

        eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.StopPublication));
        eventBus.Subscribe<TestEvent>(_ =>
        {
            laterHandlerCalled = true;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        Assert.False(result.Succeeded);
        Assert.False(laterHandlerCalled);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task FailPublisherErrorPolicyPropagatesHandlerFailure()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.FailPublisher));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await eventBus.PublishAsync(new TestEvent("failure")));

        Assert.Equal("boom", exception.Message);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task HandlerCancellationIsTrackedSeparatelyFromFailure()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();

        eventBus.Subscribe<TestEvent>((context, cancellationToken) =>
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(
            new TestEvent("cancel"),
            cancellationToken: cancellation.Token);

        var delivery = Assert.Single(result.Deliveries);
        Assert.False(result.Succeeded);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.CanceledCount);
        Assert.False(delivery.Succeeded);
        Assert.True(delivery.Canceled);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryCancelled);
        Assert.DoesNotContain(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task HandlerCancellationDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();

        var subscription = eventBus.Subscribe<TestEvent>((context, cancellationToken) =>
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(
            new TestEvent("cancel"),
            cancellationToken: cancellation.Token);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventDeliveryCancelled);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
        Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
        AssertDeliveryContext(record, result.ContractId, result.EventId, subscription.Id);
    }

    [Fact]
    public async Task PostedFailPublisherFailureDiagnosticsKeepDeliveryContext()
    {
        var diagnostics = new SignalingHostDiagnostics(EventDiagnosticIds.EventDeliveryFailed, expectedCount: 2);
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var subscription = eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.FailPublisher));

        var result = await eventBus.PostAsync(new TestEvent("failure"));
        Assert.True(result.Accepted);

        await diagnostics.WaitForExpectedRecordsAsync();

        var records = diagnostics.Records
            .Where(record => record.Code == EventDiagnosticIds.EventDeliveryFailed)
            .ToArray();

        Assert.Equal(2, records.Length);
        Assert.All(records, record =>
        {
            Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
            Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
            Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
            AssertDeliveryContext(record, result.ContractId, result.EventId, subscription.Id);
        });
    }

    [Fact]
    public async Task HandlerCancellationStopsLaterDeliveriesWithoutThrowing()
    {
        var eventBus = new InMemoryEventBus();
        using var cancellation = new CancellationTokenSource();
        var laterHandlerCalled = false;

        eventBus.Subscribe<TestEvent>((context, cancellationToken) =>
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        });
        eventBus.Subscribe<TestEvent>(_ =>
        {
            laterHandlerCalled = true;

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(
            new TestEvent("cancel"),
            cancellationToken: cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.False(laterHandlerCalled);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(1, result.CanceledCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task PostAsyncAcceptedPublicationWritesDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var result = await eventBus.PostAsync(new TestEvent("accepted"));

        Assert.True(result.Accepted);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventAccepted);
    }

    [Fact]
    public async Task PostAsyncRejectedPublicationWritesDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await eventBus.PostAsync(
            new TestEvent("rejected"),
            cancellationToken: cancellation.Token);

        Assert.False(result.Accepted);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventRejected);
    }

    [Fact]
    public async Task DiagnosticSinkFailureCannotChangeEventBusBusinessResult()
    {
        var diagnostics = new ThrowingHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var handled = 0;

        var subscription = eventBus.Subscribe<TestEvent>(_ =>
        {
            Interlocked.Increment(ref handled);
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("isolated"));
        await subscription.DisposeAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, handled);
        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
        Assert.Equal(0, eventBus.GetSnapshot().ActiveSubscriptionCount);
        Assert.True(eventBus.GetSnapshot().DiagnosticWriteFailureCount >= 4);
    }

    [Fact]
    public async Task SuccessfulDeliveryWritesCompletedRecordWithCausalityOwnershipAndDuration()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var subscription = eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);

        var result = await eventBus.PublishAsync(
            new TestEvent("complete"),
            new EventPublishOptions
            {
                CorrelationId = "operation-42",
                CausationId = "command-7",
                PublishDepth = 2
            });

        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == EventDiagnosticIds.EventDeliveryCompleted);
        AssertContextValue(record, "correlationId", "operation-42");
        AssertContextValue(record, "causationId", "command-7");
        AssertContextValue(record, "publishDepth", "2");
        AssertContextValue(record, "deliveryResult", EventDeliveryStatus.Succeeded.ToString());
        AssertContextValue(record, "subscriptionId", subscription.Id.ToString());
        AssertContextValue(record, "dispatchTarget", EventDispatchPolicy.Serialized.ToString());
        Assert.False(string.IsNullOrWhiteSpace(record.Context["ownerScopeId"]));
        Assert.False(string.IsNullOrWhiteSpace(record.Context["handlerTypeId"]));
        Assert.True(double.Parse(record.Context["handlerDurationMs"]!, System.Globalization.CultureInfo.InvariantCulture) >= 0d);
        Assert.Equal(result.Deliveries[0].Duration.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), record.Context["handlerDurationMs"]);
    }

    [Fact]
    public async Task NestedPublicationAutomaticallyInheritsCausality()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        EventContext<ChildEvent>? childContext = null;
        eventBus.Subscribe<ChildEvent>(context =>
        {
            childContext = context;
            return ValueTask.CompletedTask;
        });
        eventBus.Subscribe<TestEvent>(async parent =>
        {
            await eventBus.PublishAsync(new ChildEvent(parent.Event.Value));
        });

        var parentResult = await eventBus.PublishAsync(new TestEvent("parent"));

        Assert.NotNull(childContext);
        Assert.Equal(parentResult.EventId.ToString("D"), childContext.CorrelationId);
        Assert.Equal(parentResult.EventId.ToString("D"), childContext.CausationId);
        Assert.Equal(1, childContext.PublishDepth);
        var childRecord = Assert.Single(
            diagnostics.Records,
            item => item.Code == EventDiagnosticIds.EventPublished &&
                    item.Context["eventId"] == childContext.EventId.ToString("D"));
        AssertContextValue(childRecord, "correlationId", childContext.CorrelationId);
        AssertContextValue(childRecord, "causationId", childContext.CausationId!);
        AssertContextValue(childRecord, "publishDepth", "1");
    }

    [Fact]
    public async Task TraceSamplingCanBeDisabledWithoutSuppressingFailures()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            diagnosticsOptions: new EventBusDiagnosticsOptions { TraceSamplingRate = 0d });
        eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("sampled failure"));

        var result = await eventBus.PublishAsync(new TestEvent("sampling"));

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(diagnostics.Records, item => item.Code == EventDiagnosticIds.EventPublished);
        Assert.DoesNotContain(diagnostics.Records, item => item.Code == EventDiagnosticIds.EventDeliveryStarted);
        Assert.Contains(diagnostics.Records, item => item.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task PayloadProjectionIsExplicitBoundedAndDoesNotCallPayloadToString()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var payload = new PayloadEvent("secret", "safe");
        var descriptor = EventPayloadDiagnosticProjectorDescriptor.Create<PayloadEvent>(new SafePayloadProjector());
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            diagnosticsOptions: new EventBusDiagnosticsOptions
            {
                EnablePayloadProjection = true,
                MaximumPayloadFieldCount = 1,
                MaximumPayloadValueLength = 4
            },
            payloadProjectors: [descriptor]);

        await eventBus.PublishAsync(payload);

        var record = Assert.Single(diagnostics.Records, item => item.Code == EventDiagnosticIds.EventPublished);
        Assert.Equal("v1", record.Context["payloadSchemaVersion"]);
        Assert.Equal("12", record.Context["payloadSizeEstimate"]);
        Assert.Equal("safe", record.Context["payload.summary"]);
        Assert.DoesNotContain(record.Context, item => item.Key == "payload.zz-second");
        Assert.False(payload.ToStringCalled);
    }

    [Fact]
    public async Task PayloadProjectorFailureIsIsolatedAndReported()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var descriptor = EventPayloadDiagnosticProjectorDescriptor.Create<PayloadEvent>(new ThrowingPayloadProjector());
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            diagnosticsOptions: new EventBusDiagnosticsOptions { EnablePayloadProjection = true },
            payloadProjectors: [descriptor]);

        var result = await eventBus.PublishAsync(new PayloadEvent("secret", "safe"));

        Assert.True(result.Succeeded);
        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == EventDiagnosticIds.EventPayloadProjectionFailed);
        Assert.Equal(typeof(InvalidOperationException).FullName, record.Context["exceptionType"]);
    }

    [Fact]
    public async Task MetricsSnapshotTracksDeliveriesAndDiagnosticFailures()
    {
        var diagnostics = new ThrowingHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);
        eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("metrics"));

        await eventBus.PublishAsync(new TestEvent("metrics"));

        var snapshot = eventBus.GetSnapshot();
        Assert.Equal(2, snapshot.ActiveSubscriptionCount);
        Assert.Equal(1, snapshot.PublicationCount);
        Assert.Equal(1, snapshot.DeliverySucceededCount);
        Assert.Equal(1, snapshot.DeliveryFailedCount);
        Assert.True(snapshot.TotalHandlerDuration >= TimeSpan.Zero);
        Assert.True(snapshot.DiagnosticWriteFailureCount > 0);
    }

    [Fact]
    public void MetricsSnapshotsRejectImpossiblePublicStates()
    {
        Action[] invalidBusSnapshots =
        [
            () => CreateBusSnapshot(activeSubscriptionCount: -1),
            () => CreateBusSnapshot(publicationCount: -1),
            () => CreateBusSnapshot(deliverySucceededCount: -1),
            () => CreateBusSnapshot(deliveryFailedCount: -1),
            () => CreateBusSnapshot(deliveryCanceledCount: -1),
            () => CreateBusSnapshot(deliveryTimedOutCount: -1),
            () => CreateBusSnapshot(deliverySkippedCount: -1),
            () => CreateBusSnapshot(totalHandlerDuration: TimeSpan.FromTicks(-1)),
            () => CreateBusSnapshot(diagnosticWriteFailureCount: -1)
        ];
        Action[] invalidChannelSnapshots =
        [
            () => _ = new EventChannelMetricsSnapshot(
                default,
                "metrics",
                EventChannelExecutionMode.Serialized,
                1,
                0,
                0,
                0,
                0,
                0,
                0,
                0),
            () => CreateChannelSnapshot(channelName: " "),
            () => CreateChannelSnapshot(executionMode: (EventChannelExecutionMode)int.MaxValue),
            () => CreateChannelSnapshot(capacity: 0),
            () => CreateChannelSnapshot(pendingCount: -1),
            () => CreateChannelSnapshot(inFlightCount: -1),
            () => CreateChannelSnapshot(acceptedCount: -1),
            () => CreateChannelSnapshot(rejectedCount: -1),
            () => CreateChannelSnapshot(droppedCount: -1),
            () => CreateChannelSnapshot(completedCount: -1),
            () => CreateChannelSnapshot(failedCount: -1),
            () => _ = CreateChannelSnapshot() with { TotalQueueWaitDuration = TimeSpan.FromTicks(-1) },
            () => _ = CreateChannelSnapshot() with { MaximumQueueWaitDuration = TimeSpan.FromTicks(-1) }
        ];

        Assert.All(invalidBusSnapshots, action => Assert.IsAssignableFrom<ArgumentException>(Record.Exception(action)));
        Assert.All(invalidChannelSnapshots, action => Assert.IsAssignableFrom<ArgumentException>(Record.Exception(action)));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void DiagnosticsOptionsRejectInvalidSamplingRate(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventBusDiagnosticsOptions { TraceSamplingRate = value });
    }

    [Fact]
    public async Task ConcurrentDiagnosticSinkFailuresRemainIsolatedAndCounted()
    {
        var diagnostics = new ThrowingHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            channelOptions: new EventChannelOptions
            {
                Capacity = 256,
                ExecutionMode = EventChannelExecutionMode.Concurrent,
                MaximumConcurrency = 8
            });
        var handled = 0;
        eventBus.Subscribe<TestEvent>(_ =>
        {
            Interlocked.Increment(ref handled);
            return ValueTask.CompletedTask;
        });

        var publications = Enumerable.Range(0, 128)
            .Select(index => eventBus.PublishAsync(new TestEvent(index.ToString())).AsTask())
            .ToArray();
        var results = await Task.WhenAll(publications);

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(128, handled);
        Assert.Equal(128, eventBus.GetSnapshot().PublicationCount);
        Assert.True(eventBus.GetSnapshot().DiagnosticWriteFailureCount >= 385);
    }

    [Fact]
    public async Task RejectedPublishWritesStableRejectionAndBackpressureDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            channelOptions: new EventChannelOptions
            {
                Capacity = 1,
                BackpressurePolicy = EventChannelBackpressurePolicy.Reject
            });
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Value == "first")
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task;
            }
        });

        Assert.True((await eventBus.PostAsync(new TestEvent("first"))).Accepted);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await eventBus.PostAsync(new TestEvent("queued"))).Accepted);

        var exception = await Assert.ThrowsAsync<EventPublicationRejectedException>(
            async () => await eventBus.PublishAsync(new TestEvent("rejected")));
        releaseFirst.TrySetResult();

        var rejected = Assert.Single(
            diagnostics.Records,
            item => item.Code == EventDiagnosticIds.EventRejected &&
                    item.Context["eventId"] == exception.EventId.ToString("D"));
        var backpressure = Assert.Single(
            diagnostics.Records,
            item => item.Code == EventDiagnosticIds.EventChannelBackpressure &&
                    item.Context["eventId"] == exception.EventId.ToString("D"));
        AssertContextValue(rejected, "backpressurePolicy", EventChannelBackpressurePolicy.Reject.ToString());
        AssertContextValue(backpressure, "backpressurePolicy", EventChannelBackpressurePolicy.Reject.ToString());
    }

    private sealed record TestEvent(string Value);

    private sealed record ChildEvent(string Value);

    private static EventBusMetricsSnapshot CreateBusSnapshot(
        int activeSubscriptionCount = 0,
        long publicationCount = 0,
        long deliverySucceededCount = 0,
        long deliveryFailedCount = 0,
        long deliveryCanceledCount = 0,
        long deliveryTimedOutCount = 0,
        long deliverySkippedCount = 0,
        TimeSpan? totalHandlerDuration = null,
        long diagnosticWriteFailureCount = 0) =>
        new(
            activeSubscriptionCount,
            publicationCount,
            deliverySucceededCount,
            deliveryFailedCount,
            deliveryCanceledCount,
            deliveryTimedOutCount,
            deliverySkippedCount,
            totalHandlerDuration ?? TimeSpan.Zero,
            diagnosticWriteFailureCount);

    private static EventChannelMetricsSnapshot CreateChannelSnapshot(
        EventContractId? contractId = null,
        string channelName = "metrics",
        EventChannelExecutionMode executionMode = EventChannelExecutionMode.Serialized,
        int capacity = 1,
        int pendingCount = 0,
        int inFlightCount = 0,
        long acceptedCount = 0,
        long rejectedCount = 0,
        long droppedCount = 0,
        long completedCount = 0,
        long failedCount = 0) =>
        new(
            contractId ?? new EventContractId("tests.metrics"),
            channelName,
            executionMode,
            capacity,
            pendingCount,
            inFlightCount,
            acceptedCount,
            rejectedCount,
            droppedCount,
            completedCount,
            failedCount);

    private sealed class PayloadEvent(string secret, string summary)
    {
        public string Secret { get; } = secret;

        public string Summary { get; } = summary;

        public bool ToStringCalled { get; private set; }

        public override string ToString()
        {
            ToStringCalled = true;
            return Secret;
        }
    }

    private sealed class SafePayloadProjector : IEventPayloadDiagnosticProjector<PayloadEvent>
    {
        public EventPayloadDiagnosticSnapshot Project(PayloadEvent eventData)
        {
            return new EventPayloadDiagnosticSnapshot(
                new Dictionary<string, string?>
                {
                    ["summary"] = eventData.Summary,
                    ["zz-second"] = "omitted"
                },
                schemaVersion: "v1",
                sizeEstimate: 12);
        }
    }

    private sealed class ThrowingPayloadProjector : IEventPayloadDiagnosticProjector<PayloadEvent>
    {
        public EventPayloadDiagnosticSnapshot Project(PayloadEvent eventData)
        {
            throw new InvalidOperationException("projection failed");
        }
    }

    private sealed class ThrowingHostDiagnostics : IHostDiagnostics
    {
        public IReadOnlyList<HostDiagnosticRecord> Records => [];

        public void Write(HostDiagnosticRecord record)
        {
            throw new InvalidOperationException("diagnostic sink unavailable");
        }

        public void Complete()
        {
        }
    }

    private static void AssertDeliveryContext(
        HostDiagnosticRecord record,
        EventContractId contractId,
        Guid eventId,
        EventSubscriptionId subscriptionId)
    {
        AssertContextValue(record, "contractId", contractId.Value);
        AssertContextValue(record, "eventId", eventId.ToString("D"));
        AssertContextValue(record, "subscriptionId", subscriptionId.ToString());
    }

    private static void AssertContextValue(
        HostDiagnosticRecord record,
        string key,
        string expectedValue)
    {
        Assert.True(
            record.Context.TryGetValue(key, out var actualValue),
            $"Diagnostic '{record.Code}' must include context key '{key}'.");
        Assert.Equal(expectedValue, actualValue);
    }

    private sealed class SignalingHostDiagnostics : IHostDiagnostics
    {
        private readonly object _syncRoot = new();
        private readonly List<HostDiagnosticRecord> _records = [];
        private readonly string _targetCode;
        private readonly int _expectedCount;
        private readonly TaskCompletionSource _expectedRecords = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public SignalingHostDiagnostics(string targetCode, int expectedCount)
        {
            _targetCode = targetCode;
            _expectedCount = expectedCount;
        }

        public IReadOnlyList<HostDiagnosticRecord> Records
        {
            get
            {
                lock (_syncRoot)
                {
                    return Array.AsReadOnly(_records.ToArray());
                }
            }
        }

        public void Write(HostDiagnosticRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            lock (_syncRoot)
            {
                _records.Add(record);
                if (_records.Count(item => item.Code == _targetCode) >= _expectedCount)
                {
                    _expectedRecords.TrySetResult();
                }
            }
        }

        public void Complete()
        {
        }

        public async Task WaitForExpectedRecordsAsync()
        {
            await _expectedRecords.Task
                .WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
        }
    }
}
