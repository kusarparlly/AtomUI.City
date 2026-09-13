using AtomUI.City.EventBus;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodBusinessWorkload
{
    private static readonly string[] WorkflowNames =
    [
        "platform-startup",
        "identity-connectivity",
        "catalog-pricing",
        "orders-billing",
        "fulfillment-logistics",
        "customer-care",
        "operations-governance",
        "desktop-composition",
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;
    private readonly IApplicationStateWriter _stateWriter;
    private readonly DogfoodRunLedger _ledger;
    private int _executed;

    public DogfoodBusinessWorkload(
        IServiceScopeFactory scopeFactory,
        IEventBus eventBus,
        IApplicationStateWriter stateWriter,
        DogfoodRunLedger ledger)
    {
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
        _stateWriter = stateWriter;
        _ledger = ledger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _executed, 1) != 0)
        {
            throw new InvalidOperationException("The business Service workload can only run once.");
        }

        var registrations = DogfoodServiceCatalog.Registrations;
        if (registrations.Count != 110)
        {
            throw new InvalidOperationException($"Expected 110 business Services; observed {registrations.Count}.");
        }

        using var scope = _scopeFactory.CreateScope();
        var resolved = registrations
            .Select(registration => Resolve(scope.ServiceProvider, registration))
            .ToArray();

        var cursor = 0;
        var dependencyEdges = 0;
        for (var workflowIndex = 0; workflowIndex < WorkflowNames.Length; workflowIndex++)
        {
            var remaining = resolved.Length - cursor;
            var count = workflowIndex == WorkflowNames.Length - 1
                ? remaining
                : Math.Min(14, remaining);
            var services = resolved.AsSpan(cursor, count).ToArray();
            cursor += count;
            dependencyEdges += await ExecuteWorkflowAsync(
                workflowIndex,
                WorkflowNames[workflowIndex],
                services,
                cancellationToken);
        }

        await VerifyCancellationBoundaryAsync(resolved[0], cancellationToken);
        var compensations = await VerifyCompensationAsync(resolved, cancellationToken);

        var calls = ServiceInvocationLedger.Snapshot();
        var missing = registrations
            .Select(static item => item.ServiceType.Name)
            .Where(serviceId => !calls.TryGetValue(serviceId, out var count) || count == 0)
            .ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidOperationException(
                $"Business workflows did not execute Services: {string.Join(", ", missing)}.");
        }

        _stateWriter.Set(DogfoodStateWorkload.HostHealth, "Eight cross-module business workflows completed");
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_SERVICES services={calls.Count} workflows={WorkflowNames.Length} edges={dependencyEdges} calls={ServiceInvocationLedger.TotalCount} compensations={compensations}");
    }

    public async Task<DogfoodServiceScenarioSummary> RunScenarioAsync(
        Guid operationId,
        string scenarioId,
        int serviceOffset,
        int serviceCount,
        bool injectFailure,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("A scenario Service operation id cannot be empty.", nameof(operationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentOutOfRangeException.ThrowIfNegative(serviceOffset);
        ArgumentOutOfRangeException.ThrowIfLessThan(serviceCount, 4);

        var registrations = DogfoodServiceCatalog.Registrations;
        if (serviceCount > registrations.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serviceCount),
                serviceCount,
                "A scenario Service slice cannot exceed the Service catalog.");
        }

        using var scope = _scopeFactory.CreateScope();
        var services = Enumerable.Range(0, serviceCount)
            .Select(index => Resolve(
                scope.ServiceProvider,
                registrations[(serviceOffset + index) % registrations.Count]))
            .ToArray();
        var completed = new List<IDogfoodWorkloadService>(serviceCount);
        var stages = 0;
        var dependencyEdges = 0;
        var digest = $"scenario-{scenarioId}-{operationId:N}";
        var faultAfter = injectFailure ? Math.Max(4, serviceCount / 2) : int.MaxValue;

        try
        {
            foreach (var branch in services.Chunk(4))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var input = digest;
                var stage = stages;
                var receipts = await Task.WhenAll(branch.Select(service => service.ExecuteAsync(
                        new DogfoodServiceRequest(
                            operationId,
                            scenarioId,
                            stage,
                            input,
                            IsCompensation: false),
                        cancellationToken)
                    .AsTask())).ConfigureAwait(false);
                completed.AddRange(branch);
                foreach (var receipt in receipts.OrderBy(static item => item.ServiceId, StringComparer.Ordinal))
                {
                    _ledger.Record("scenario-service", receipt.ServiceId);
                }

                digest = string.Join('-', receipts.Select(static receipt => receipt.Signature[..8]));
                dependencyEdges += stages == 0 ? Math.Max(0, receipts.Length - 1) : receipts.Length;
                stages++;
                if (completed.Count >= faultAfter)
                {
                    throw new DogfoodExpectedBusinessException(
                        $"Scenario '{scenarioId}' injected its deterministic Service failure.");
                }
            }
        }
        catch (DogfoodExpectedBusinessException) when (injectFailure)
        {
            _ledger.Record("scenario-fault", scenarioId, "expected-failure");
        }

        var compensations = 0;
        if (injectFailure)
        {
            for (var index = completed.Count - 1; index >= 0; index--)
            {
                var service = completed[index];
                await service.ExecuteAsync(
                    new DogfoodServiceRequest(
                        operationId,
                        scenarioId,
                        compensations,
                        "rollback",
                        IsCompensation: true),
                    CancellationToken.None);
                compensations++;
                _ledger.Record("scenario-compensation", service.Id);
            }

            if (compensations != completed.Count)
            {
                throw new InvalidOperationException(
                    $"Scenario '{scenarioId}' compensated {compensations} of {completed.Count} completed Services.");
            }
        }

        return new DogfoodServiceScenarioSummary(
            completed.Count + compensations,
            completed.Count,
            compensations,
            stages,
            dependencyEdges,
            injectFailure);
    }

    private async Task<int> ExecuteWorkflowAsync(
        int workflowIndex,
        string workflow,
        IReadOnlyList<IDogfoodWorkloadService> services,
        CancellationToken cancellationToken)
    {
        var operationId = CreateOperationId(_ledger.Options.Seed, workflowIndex);
        var digest = $"seed-{_ledger.Options.Seed:x8}";
        var edges = 0;
        var stage = 0;

        foreach (var branch in services.Chunk(4))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = digest;
            var tasks = branch.Select(service => service.ExecuteAsync(
                    new DogfoodServiceRequest(operationId, workflow, stage, input, IsCompensation: false),
                    cancellationToken)
                .AsTask()).ToArray();
            var receipts = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var receipt in receipts.OrderBy(static item => item.ServiceId, StringComparer.Ordinal))
            {
                _ledger.Record("service", receipt.ServiceId);
            }

            digest = string.Join('-', receipts.Select(static receipt => receipt.Signature[..8]));
            edges += stage == 0 ? Math.Max(0, receipts.Length - 1) : receipts.Length;
            stage++;
        }

        await PublishWorkflowEventAsync(workflowIndex, operationId, workflow, cancellationToken)
            .ConfigureAwait(false);
        _ledger.Record("workflow", workflow);
        return edges;
    }

    private async Task<int> VerifyCompensationAsync(
        IReadOnlyList<IDogfoodWorkloadService> services,
        CancellationToken cancellationToken)
    {
        const int successfulSteps = 12;
        var operationId = CreateOperationId(_ledger.Options.Seed, 100);
        var completed = new List<IDogfoodWorkloadService>(successfulSteps);
        try
        {
            for (var index = 0; index < successfulSteps; index++)
            {
                var service = services[42 + index];
                await service.ExecuteAsync(
                    new DogfoodServiceRequest(
                        operationId,
                        "order-payment-failure",
                        index,
                        $"order-{_ledger.Options.Seed}-{index}",
                        IsCompensation: false),
                    cancellationToken);
                completed.Add(service);
                _ledger.Record("service", service.Id);
            }

            throw new DogfoodExpectedBusinessException("Payment authorization was deliberately rejected.");
        }
        catch (DogfoodExpectedBusinessException)
        {
            _ledger.Record("failure", "payment-authorization", "expected-failure");
        }

        for (var index = completed.Count - 1; index >= 0; index--)
        {
            var service = completed[index];
            await service.ExecuteAsync(
                new DogfoodServiceRequest(
                    operationId,
                    "order-payment-compensation",
                    completed.Count - index,
                    "rollback",
                    IsCompensation: true),
                CancellationToken.None);
            _ledger.Record("compensation", service.Id);
        }

        var publication = await _eventBus.PublishAsync(
            new WorkflowCompensated(operationId, "order-payment", successfulSteps),
            cancellationToken: cancellationToken);
        if (!publication.Succeeded)
        {
            throw new InvalidOperationException("The compensation EventBus publication failed.");
        }

        _ledger.Record("event", nameof(WorkflowCompensated));
        return completed.Count;
    }

    private async Task VerifyCancellationBoundaryAsync(
        IDogfoodWorkloadService service,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = ServiceInvocationLedger.CountFor(service.Id);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            await service.ExecuteAsync(
                new DogfoodServiceRequest(Guid.NewGuid(), "cancel-before-start", 0, "cancelled", false),
                cancelled.Token);
            throw new InvalidOperationException("A cancelled Service operation unexpectedly executed.");
        }
        catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
        {
            _ledger.Record("cancellation", "service-before-start", "expected-failure");
        }

        if (ServiceInvocationLedger.CountFor(service.Id) != before)
        {
            throw new InvalidOperationException("A pre-cancelled Service operation produced a business side effect.");
        }
    }

    private async Task PublishWorkflowEventAsync(
        int workflowIndex,
        Guid operationId,
        string workflow,
        CancellationToken cancellationToken)
    {
        var revision = workflowIndex + 1;
        var succeeded = workflowIndex switch
        {
            0 => (await _eventBus.PublishAsync(new ApplicationReady(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            1 => (await _eventBus.PublishAsync(new TenantSwitched(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            2 => (await _eventBus.PublishAsync(new CatalogRebuilt(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            3 => (await _eventBus.PublishAsync(new OrderConfirmed(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            4 => (await _eventBus.PublishAsync(new ShipmentDispatched(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            5 => (await _eventBus.PublishAsync(new SupportTicketOpened(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            6 => (await _eventBus.PublishAsync(new ReportGenerated(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            7 => (await _eventBus.PublishAsync(new OutletReconciled(operationId, workflow, revision), cancellationToken: cancellationToken)).Succeeded,
            _ => throw new ArgumentOutOfRangeException(nameof(workflowIndex)),
        };
        if (!succeeded)
        {
            throw new InvalidOperationException($"Workflow EventBus publication failed for '{workflow}'.");
        }

        _ledger.Record("event", workflow);
    }

    private static IDogfoodWorkloadService Resolve(
        IServiceProvider services,
        DogfoodServiceCatalog.ServiceRegistration registration)
    {
        var value = registration.Key is null
            ? services.GetRequiredService(registration.ServiceType)
            : services.GetRequiredKeyedService(registration.ServiceType, registration.Key);
        return (IDogfoodWorkloadService)value;
    }

    private static Guid CreateOperationId(int seed, int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, seed);
        BitConverter.TryWriteBytes(bytes[4..], index);
        BitConverter.TryWriteBytes(bytes[8..], ((long)seed << 32) | (uint)index);
        return new Guid(bytes);
    }

    private sealed class DogfoodExpectedBusinessException(string message) : Exception(message);
}

internal sealed record DogfoodServiceScenarioSummary(
    int InvocationCount,
    int CompletedCount,
    int CompensationCount,
    int StageCount,
    int DependencyEdgeCount,
    bool ExpectedFailureInjected);
