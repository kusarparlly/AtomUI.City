using AtomUI.City.Core.Diagnostics;
using AtomUI.City.EventBus;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventPublicationTests
{
    [Fact]
    public async Task PublishAsyncInvokesMatchingHandlersWithEventContext()
    {
        var eventBus = new InMemoryEventBus();
        EventContext<TestEvent>? observedContext = null;

        eventBus.Subscribe<TestEvent>(context =>
        {
            observedContext = context;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("published"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.DeliveredCount);
        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.NotNull(observedContext);
        Assert.Equal("published", observedContext.Event.Value);
        Assert.Equal(result.EventId, observedContext.EventId);
        Assert.Equal(result.ContractId, observedContext.ContractId);
        Assert.Equal(0, observedContext.PublishDepth);
    }

    [Fact]
    public async Task PublishAsyncRejectsNullEvent()
    {
        var eventBus = new InMemoryEventBus();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await eventBus.PublishAsync<TestEvent>(null!));
    }

    [Fact]
    public async Task PublishAsyncRejectsDisposedBus()
    {
        var eventBus = new InMemoryEventBus();

        eventBus.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await eventBus.PublishAsync(new TestEvent("disposed")));
    }

    [Fact]
    public async Task PublishAsyncObservesAlreadyCanceledTokenWithoutSubscriptions()
    {
        var eventBus = new InMemoryEventBus();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await eventBus.PublishAsync(
                new TestEvent("cancel"),
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task PublishAsyncRejectsNegativePublishDepth()
    {
        var eventBus = new InMemoryEventBus();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await eventBus.PublishAsync(
                new TestEvent("negative-depth"),
                new EventPublishOptions { PublishDepth = -1 }));
    }

    [Fact]
    public void PublishOptionsRejectNegativePublishDepthInit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPublishOptions
        {
            PublishDepth = -1,
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" trace ")]
    [InlineData("trace\nid")]
    public void PublishOptionsRejectInvalidCorrelationIds(string correlationId)
    {
        Assert.Throws<ArgumentException>(() => new EventPublishOptions
        {
            CorrelationId = correlationId,
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" cause ")]
    [InlineData("cause\nid")]
    public void PublishOptionsRejectInvalidCausationIds(string causationId)
    {
        Assert.Throws<ArgumentException>(() => new EventPublishOptions
        {
            CausationId = causationId,
        });
    }

    [Fact]
    public void EventContextRejectsDefaultContractId()
    {
        Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
            new TestEvent("context"),
            default,
            Guid.NewGuid(),
            "correlation",
            causationId: null,
            DateTimeOffset.UtcNow,
            publishDepth: 0,
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized));
    }

    [Fact]
    public void EventContextRejectsEmptyEventId()
    {
        Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
            new TestEvent("context"),
            new EventContractId("atomui.city.tests.context.v1"),
            Guid.Empty,
            "correlation",
            causationId: null,
            DateTimeOffset.UtcNow,
            publishDepth: 0,
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized));
    }

    [Theory]
    [InlineData(" trace ")]
    [InlineData("trace\nid")]
    public void EventContextRejectsInvalidCorrelationIds(string correlationId)
    {
        Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
            new TestEvent("context"),
            new EventContractId("atomui.city.tests.context.v1"),
            Guid.NewGuid(),
            correlationId,
            causationId: null,
            DateTimeOffset.UtcNow,
            publishDepth: 0,
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized));
    }

    [Fact]
    public void EventContextRejectsNegativePublishDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventContext<TestEvent>(
            new TestEvent("context"),
            new EventContractId("atomui.city.tests.context.v1"),
            Guid.NewGuid(),
            "correlation",
            causationId: null,
            DateTimeOffset.UtcNow,
            publishDepth: -1,
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized));
    }

    [Fact]
    public void EventContextRejectsDefaultSubscriptionId()
    {
        Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
            new TestEvent("context"),
            new EventContractId("atomui.city.tests.context.v1"),
            Guid.NewGuid(),
            "correlation",
            causationId: null,
            DateTimeOffset.UtcNow,
            publishDepth: 0,
            default,
            EventDispatchPolicy.Serialized));
    }

    [Fact]
    public void EventContextRejectsUnknownDispatchPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventContext<TestEvent>(
            new TestEvent("context"),
            new EventContractId("atomui.city.tests.context.v1"),
            Guid.NewGuid(),
            "correlation",
            causationId: null,
            DateTimeOffset.UtcNow,
            publishDepth: 0,
            EventSubscriptionId.New(),
            (EventDispatchPolicy)999));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" cause ")]
    [InlineData("cause\nid")]
    public void EventContextRejectsInvalidCausationIds(string causationId)
    {
        Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
            new TestEvent("context"),
            new EventContractId("atomui.city.tests.context.v1"),
            Guid.NewGuid(),
            "correlation",
            causationId,
            DateTimeOffset.UtcNow,
            publishDepth: 0,
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized));
    }

    [Fact]
    public async Task PostAsyncRejectsNullEvent()
    {
        var eventBus = new InMemoryEventBus();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await eventBus.PostAsync<TestEvent>(null!));
    }

    [Fact]
    public async Task PostAsyncRejectsDisposedBus()
    {
        var eventBus = new InMemoryEventBus();

        eventBus.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await eventBus.PostAsync(new TestEvent("disposed")));
    }

    [Fact]
    public async Task PublishAsyncSupportsSyncAndAsyncHandlers()
    {
        var eventBus = new InMemoryEventBus();
        var received = new List<string>();

        eventBus.Subscribe<TestEvent>(context => received.Add("sync:" + context.Event.Value));
        eventBus.Subscribe<TestEvent>(async context =>
        {
            await Task.Yield();
            received.Add("async:" + context.Event.Value);
        });

        var result = await eventBus.PublishAsync(new TestEvent("value"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DeliveredCount);
        Assert.Contains("sync:value", received);
        Assert.Contains("async:value", received);
    }

    [Fact]
    public async Task PublishAsyncUsesRegisteredContractId()
    {
        var contracts = new InMemoryEventContractRegistry();
        var contractId = new EventContractId("atomui.city.tests.event.v1");
        contracts.Register(EventContractDescriptor.Shared<TestEvent>(contractId, typeof(TestEvent).Assembly));
        var eventBus = new InMemoryEventBus(contractRegistry: contracts);
        EventContractId observedContractId = default;

        eventBus.Subscribe<TestEvent>(context =>
        {
            observedContractId = context.ContractId;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("published"));

        Assert.Equal(contractId, result.ContractId);
        Assert.Equal(contractId, observedContractId);
    }

    [Fact]
    public async Task PublishAsyncPropagatesCorrelationAndCausationIds()
    {
        var eventBus = new InMemoryEventBus();
        EventContext<TestEvent>? observedContext = null;

        eventBus.Subscribe<TestEvent>(context =>
        {
            observedContext = context;

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(
            new TestEvent("correlated"),
            new EventPublishOptions
            {
                CorrelationId = "correlation-1",
                CausationId = "cause-1",
                PublishDepth = 2,
            });

        Assert.True(result.Succeeded);
        Assert.NotNull(observedContext);
        Assert.Equal("correlation-1", observedContext.CorrelationId);
        Assert.Equal("cause-1", observedContext.CausationId);
        Assert.Equal(2, observedContext.PublishDepth);
        Assert.Equal(result.EventId, observedContext.EventId);
    }

    [Fact]
    public void PublishResultDeliveriesRejectExternalListMutation()
    {
        var delivery = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true);
        var replacement = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Background,
            Succeeded: false,
            ErrorMessage: "replaced");
        var result = new EventPublishResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            [delivery]);
        var list = Assert.IsAssignableFrom<IList<EventDeliveryResult>>(result.Deliveries);

        Assert.Throws<NotSupportedException>(() => list[0] = replacement);
        Assert.Equal(delivery.SubscriptionId, result.Deliveries[0].SubscriptionId);
    }

    [Fact]
    public void PublishResultRejectsNullDeliveryEntries()
    {
        Assert.Throws<ArgumentException>(() => new EventPublishResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            [null!]));
    }

    [Fact]
    public void PublishResultRejectsEmptyEventId()
    {
        Assert.Throws<ArgumentException>(() => new EventPublishResult(
            Guid.Empty,
            new EventContractId("atomui.city.tests.event.v1"),
            []));
    }

    [Fact]
    public void PublishResultRejectsDefaultContractId()
    {
        Assert.Throws<ArgumentException>(() => new EventPublishResult(
            Guid.NewGuid(),
            default,
            []));
    }

    [Fact]
    public void DeliveryResultRejectsDefaultSubscriptionId()
    {
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            default,
            EventDispatchPolicy.Serialized,
            Succeeded: true));
    }

    [Fact]
    public void DeliveryResultRejectsDefaultSubscriptionIdInitMutation()
    {
        var delivery = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true);

        Assert.Throws<ArgumentException>(() => delivery with
        {
            SubscriptionId = default,
        });
    }

    [Fact]
    public void DeliveryResultRejectsUnknownDispatchPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            (EventDispatchPolicy)999,
            Succeeded: true));
    }

    [Fact]
    public void DeliveryResultRejectsUnknownDispatchPolicyInitMutation()
    {
        var delivery = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => delivery with
        {
            DispatchPolicy = (EventDispatchPolicy)999,
        });
    }

    [Fact]
    public void DeliveryResultRejectsSuccessfulCancellation()
    {
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true,
            Canceled: true));
    }

    [Fact]
    public void DeliveryResultRejectsSuccessfulCancellationInitMutation()
    {
        var delivery = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true);

        Assert.Throws<ArgumentException>(() => delivery with
        {
            Canceled = true,
        });
    }

    [Fact]
    public void DeliveryResultRejectsSuccessfulErrorMessage()
    {
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true,
            ErrorMessage: "should not be present"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DeliveryResultRejectsSuccessfulBlankErrorMessage(string errorMessage)
    {
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true,
            ErrorMessage: errorMessage));
    }

    [Fact]
    public void DeliveryResultRejectsSuccessfulErrorMessageInitMutation()
    {
        var delivery = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true);

        Assert.Throws<ArgumentException>(() => delivery with
        {
            ErrorMessage = "should not be present",
        });
    }

    [Fact]
    public void DeliveryResultRejectsSucceededInitMutationWithErrorMessage()
    {
        var delivery = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: false,
            ErrorMessage: "boom");

        Assert.Throws<ArgumentException>(() => delivery with
        {
            Succeeded = true,
        });
    }

    [Fact]
    public async Task PostAsyncReturnsAcceptedEventIdUsedByDelivery()
    {
        var eventBus = new InMemoryEventBus();
        var observedEventId = new TaskCompletionSource<Guid>();

        eventBus.Subscribe<TestEvent>(context =>
        {
            observedEventId.SetResult(context.EventId);

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PostAsync(new TestEvent("posted"));

        Assert.True(result.Accepted);
        Assert.Equal(result.EventId, await observedEventId.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task PostAsyncRejectsAlreadyCanceledPublication()
    {
        var eventBus = new InMemoryEventBus();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await eventBus.PostAsync(
            new TestEvent("posted"),
            cancellationToken: cancellation.Token);

        Assert.False(result.Accepted);
        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.False(string.IsNullOrWhiteSpace(result.RejectionReason));
    }

    [Fact]
    public void PostResultRejectsEmptyEventId()
    {
        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.Empty,
            new EventContractId("atomui.city.tests.event.v1"),
            Accepted: true));
    }

    [Fact]
    public void PostResultRejectsEmptyEventIdInitMutation()
    {
        var result = new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.post.v1"),
            Accepted: true);

        Assert.Throws<ArgumentException>(() => result with
        {
            EventId = Guid.Empty,
        });
    }

    [Fact]
    public void PostResultRejectsDefaultContractId()
    {
        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.NewGuid(),
            default,
            Accepted: true));
    }

    [Fact]
    public void PostResultRejectsDefaultContractIdInitMutation()
    {
        var result = new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.post.v1"),
            Accepted: true);

        Assert.Throws<ArgumentException>(() => result with
        {
            ContractId = default,
        });
    }

    [Fact]
    public void PostResultEnforcesRejectionReasonConsistency()
    {
        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            Accepted: true,
            RejectionReason: "not rejected"));

        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            Accepted: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PostResultRejectsAcceptedBlankRejectionReason(string rejectionReason)
    {
        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            Accepted: true,
            RejectionReason: rejectionReason));
    }

    [Fact]
    public void PostResultRejectsAcceptedRejectionReasonInitMutation()
    {
        var result = new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.post.v1"),
            Accepted: true);

        Assert.Throws<ArgumentException>(() => result with
        {
            RejectionReason = "not rejected",
        });
    }

    [Fact]
    public void PostResultRejectsRejectedMissingReasonInitMutation()
    {
        var result = new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.post.v1"),
            Accepted: true);

        Assert.Throws<ArgumentException>(() => result with
        {
            Accepted = false,
        });
    }

    [Fact]
    public void PostResultRejectsAcceptedReasonStateInitMutation()
    {
        var result = new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.post.v1"),
            Accepted: false,
            RejectionReason: "not accepted");

        Assert.Throws<ArgumentException>(() => result with
        {
            Accepted = true,
        });
    }

    [Fact]
    public async Task PostAsyncRejectsNegativePublishDepthBeforeAcceptance()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await eventBus.PostAsync(
                new TestEvent("negative-depth"),
                new EventPublishOptions { PublishDepth = -1 }));

        Assert.DoesNotContain(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventAccepted);
    }

    private sealed record TestEvent(string Value);
}
