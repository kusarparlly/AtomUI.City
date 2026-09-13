using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Localization;
using AtomUI.City.Mvvm;
using AtomUI.City.State;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodAutomationWorkload
{
    private static readonly string[] Sections =
    [
        "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration",
    ];

    private readonly IEventBus _eventBus;
    private readonly IEventBusMonitor _eventMonitor;
    private readonly IApplicationStateWriter _stateWriter;
    private readonly ILocalizationService _localization;
    private readonly DogfoodRemoteOperations _remoteOperations;
    private readonly DogfoodRefreshingAccessTokenProvider _refreshingTokens;
    private readonly DogfoodRunOptions _options;
    private readonly DogfoodRunLedger _ledger;
    private int _executed;

    public DogfoodAutomationWorkload(
        IEventBus eventBus,
        IEventBusMonitor eventMonitor,
        IApplicationStateWriter stateWriter,
        ILocalizationService localization,
        DogfoodRemoteOperations remoteOperations,
        DogfoodRefreshingAccessTokenProvider refreshingTokens,
        DogfoodRunOptions options,
        DogfoodRunLedger ledger)
    {
        _eventBus = eventBus;
        _eventMonitor = eventMonitor;
        _stateWriter = stateWriter;
        _localization = localization;
        _remoteOperations = remoteOperations;
        _refreshingTokens = refreshingTokens;
        _options = options;
        _ledger = ledger;
    }

    public async Task RunAsync(
        Func<string, CancellationToken, Task<object>> navigateAndPresentAsync,
        Action<string> applyStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(navigateAndPresentAsync);
        ArgumentNullException.ThrowIfNull(applyStatus);
        if (Interlocked.Exchange(ref _executed, 1) != 0)
        {
            throw new InvalidOperationException("The desktop automation workload can only run once.");
        }

        var commandExecutions = 0;
        var eventPublications = 0;
        var dataOperations = 0;
        var localizedLookups = 0;

        using var localizationScope = _localization.ActivateScope(DogfoodLocalizationCatalog.LookupContext);
        var measured = System.Diagnostics.Stopwatch.StartNew();
        var iteration = 0;
        while (iteration < 24 ||
               _ledger.ActionCount < _options.TargetActionCount ||
               measured.Elapsed < _options.MinimumDuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var section = Sections[iteration % Sections.Length];
            var viewModel = await navigateAndPresentAsync(section, cancellationToken);
            _ledger.Record("navigation-cycle", section);

            switch (viewModel)
            {
                case DashboardViewModel dashboard:
                    await dashboard.RefreshCommand.ExecuteAsync(null);
                    commandExecutions++;
                    _ledger.Record("command-cycle", nameof(DashboardViewModel));
                    break;
                case DogfoodPageViewModel page:
                    await page.PrimaryCommand.ExecuteAsync(null);
                    commandExecutions++;
                    _ledger.Record("command-cycle", page.GetType().Name + ".Primary");
                    if (page is DualCommandDogfoodPageViewModel dual)
                    {
                        dual.SecondaryCommand.Execute(null);
                        commandExecutions++;
                        _ledger.Record("command-cycle", page.GetType().Name + ".Secondary");
                    }

                    break;
                default:
                    throw new InvalidOperationException(
                        $"Automation reached unsupported ViewModel '{viewModel.GetType().FullName}'.");
            }

            if (!_stateWriter.Set(
                    DogfoodStateWorkload.HostHealth,
                    $"Automation iteration {iteration + 1}: {section}"))
            {
                throw new InvalidOperationException($"Automation State write {iteration + 1} made no change.");
            }
            _ledger.Record("state-write", DogfoodStateWorkload.HostHealth.Name);

            var published = await _eventBus.PublishAsync(
                new NavigationCommitted(CreateOperationId(iteration), section, iteration + 1),
                new EventPublishOptions
                {
                    CorrelationId = $"desktop-automation-{iteration:00}",
                    CausationId = iteration == 0 ? "desktop-startup" : $"desktop-automation-{iteration - 1:00}",
                    PublishDepth = iteration % 4,
                },
                cancellationToken);
            if (!published.Succeeded)
            {
                throw new InvalidOperationException($"Automation EventBus publication {iteration + 1} failed.");
            }

            eventPublications++;
            _ledger.Record("event-cycle", nameof(NavigationCommitted));

            var sku = $"SKU-AUTO-{iteration % 12:00}";
            var product = await _remoteOperations.GetProductAsync(sku, cancellationToken: cancellationToken);
            if (!product.Succeeded || !string.Equals(product.Value?.Sku, sku, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Automation Data query failed for '{sku}': {product.Error?.Kind}.");
            }

            dataOperations++;
            _ledger.Record("data", "get-product");

            var localized = await _localization.GetStringAsync(
                $"Navigation.{iteration % 70:000}",
                DogfoodLocalizationCatalog.LookupContext,
                cancellationToken);
            if (localized.IsMissing)
            {
                throw new InvalidOperationException($"Automation localization key '{localized.Key}' is missing.");
            }

            localizedLookups++;
            _ledger.Record("localization", localized.Key);
            iteration++;
        }

        for (var cultureIteration = 0; cultureIteration < 20; cultureIteration++)
        {
            var culture = cultureIteration % 2 == 0 ? "zh-CN" : "en-US";
            var result = await _localization.SetCultureAsync(culture, cancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error?.Message ?? "Automation culture switch failed.");
            }

            _ledger.Record("culture-switch", culture);
        }

        for (var checkpoint = 0; checkpoint < 6; checkpoint++)
        {
            var result = await _eventBus.PublishAsync(
                new AutomationCheckpointReached(
                    Guid.NewGuid(),
                    Subject: $"checkpoint-{checkpoint + 1}",
                    Revision: checkpoint + 1),
                cancellationToken: cancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Automation checkpoint {checkpoint + 1} failed to publish.");
            }

            eventPublications++;
            _ledger.Record("event-cycle", nameof(AutomationCheckpointReached));
        }

        await VerifyCommandFailureAndCancellationAsync(cancellationToken);

        var metrics = _eventMonitor.GetSnapshot();
        if (commandExecutions < iteration ||
            eventPublications != iteration + 6 ||
            dataOperations != iteration ||
            localizedLookups != iteration ||
            !string.Equals(_localization.CurrentCulture.Name, "en-US", StringComparison.Ordinal) ||
            metrics.PublicationCount < 72 + eventPublications)
        {
            throw new InvalidOperationException(
                $"Automation ledger mismatch: commands={commandExecutions}, events={eventPublications}, data={dataOperations}, localization={localizedLookups}, bus={metrics.PublicationCount}.");
        }

        if (_options.IsAutomated && _ledger.ActionCount < _options.TargetActionCount)
        {
            throw new InvalidOperationException(
                $"Profile '{_options.Profile}' expected at least {_options.TargetActionCount} actions; observed {_ledger.ActionCount}.");
        }

        if (_options.IsAutomated && measured.Elapsed < _options.MinimumDuration)
        {
            throw new InvalidOperationException(
                $"Profile '{_options.Profile}' expected to run for at least {_options.MinimumDuration}; observed {measured.Elapsed}.");
        }

        if (_options.MinimumDuration >= TimeSpan.FromMinutes(7) && _refreshingTokens.RefreshCount < 3)
        {
            throw new InvalidOperationException(
                $"Long-running automation expected at least three credential refreshes including the startup probe; observed {_refreshingTokens.RefreshCount}.");
        }

        if (_refreshingTokens.PermissionRefreshCount < 1 ||
            (_options.MinimumDuration >= TimeSpan.FromMinutes(35) &&
             _refreshingTokens.PermissionRefreshCount < 2))
        {
            throw new InvalidOperationException(
                "Long-running automation did not renew and publish permission snapshots at the expected cadence: " +
                $"observed {_refreshingTokens.PermissionRefreshCount}.");
        }

        applyStatus($"Cross-module automation completed {iteration} routed desktop operations.");
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_AUTOMATION profile={_options.Profile.ToString().ToLowerInvariant()} navigations={iteration} commands={commandExecutions} stateWrites={iteration} events={eventPublications} data={dataOperations} localization={localizedLookups} cultureSwitches=20 tokenRefreshes={_refreshingTokens.RefreshCount} permissionRefreshes={_refreshingTokens.PermissionRefreshCount} actions={_ledger.ActionCount}");
    }

    private async Task VerifyCommandFailureAndCancellationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failedState = new CommandExecutionState("dogfood.expected-failure", GetType());
        var failed = CommandFactory.CreateAsync(
            static (CancellationToken _) => Task.FromException(new InvalidOperationException("expected-command-failure")),
            failedState);
        await failed.ExecuteAsync(null);
        if (failedState.LastResult?.Status != OperationStatus.Failed ||
            failedState.LastError?.Message != "expected-command-failure")
        {
            throw new InvalidOperationException("A deliberately failing command did not publish its failed execution state.");
        }

        _ledger.Record("failure", "mvvm-command", "expected-failure");

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelledState = new CommandExecutionState("dogfood.expected-cancellation", GetType());
        var cancelled = CommandFactory.CreateAsync(async cancellation =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
        }, cancelledState);
        var execution = cancelled.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        await cancelled.ExecuteAsync(null);
        if (cancelledState.RejectedExecutionCount != 1 ||
            cancelledState.LastRejectedResult?.Status != OperationStatus.Rejected)
        {
            throw new InvalidOperationException("An overlapping command execution did not publish its rejected state.");
        }

        _ledger.Record("rejection", "mvvm-command", "expected-failure");
        cancelled.Cancel();
        await execution;
        if (cancelledState.LastResult?.Status != OperationStatus.Canceled)
        {
            throw new InvalidOperationException("A cancelled command did not publish its canceled execution state.");
        }

        _ledger.Record("cancellation", "mvvm-command", "expected-failure");
    }

    private Guid CreateOperationId(int iteration)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, _options.Seed);
        BitConverter.TryWriteBytes(bytes[4..], iteration);
        BitConverter.TryWriteBytes(bytes[8..], ((long)_options.Seed << 32) | (uint)iteration);
        return new Guid(bytes);
    }
}
