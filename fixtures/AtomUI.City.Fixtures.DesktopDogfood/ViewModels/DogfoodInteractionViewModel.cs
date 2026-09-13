using System.Collections.ObjectModel;
using AtomUI.City.Core.Threading;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Localization;
using AtomUI.City.Mvvm;
using AtomUI.City.Security;
using AtomUI.City.State;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodInteractionViewModel : ViewModelBase
{
    private readonly DogfoodRemoteOperations _remoteOperations;
    private readonly IEventBus _eventBus;
    private readonly ILocalizationService _localization;
    private readonly IAccountSessionManager _accountSessions;
    private readonly IApplicationStateWriter _stateWriter;
    private readonly IUiDispatcher _dispatcher;
    private readonly DogfoodRunLedger _ledger;
    private Action<IReadOnlyDictionary<string, string>>? _applyNavigationLabels;
    private Func<string?, bool, CancellationToken, Task<DogfoodScenarioBatchResult>>? _runScenarios;
    private string _searchQuery = string.Empty;
    private string _searchStatus = "Ready for an operator action";
    private string _selectedCulture = "en-US";
    private string _culturePreview = "en-US: Dashboard";
    private string _selectedAccount = "Administrator";
    private string _accountStatus = "Administrator: Online";
    private string _selectedScenario = DogfoodScenarioCatalog.DisplayNames[0];
    private string _scenarioStatus = "Ready: 12 deterministic scenarios";
    private string _scenarioSummary = "No scenario batch has completed";
    private bool _realtimeEnabled = true;
    private double _priority = 3;
    private int _eventRevision = 10_000;

    public DogfoodInteractionViewModel(
        DogfoodRemoteOperations remoteOperations,
        IEventBus eventBus,
        ILocalizationService localization,
        IAccountSessionManager accountSessions,
        IApplicationStateWriter stateWriter,
        IUiDispatcher dispatcher,
        DogfoodRunLedger ledger)
    {
        _remoteOperations = remoteOperations;
        _eventBus = eventBus;
        _localization = localization;
        _accountSessions = accountSessions;
        _stateWriter = stateWriter;
        _dispatcher = dispatcher;
        _ledger = ledger;

        SearchExecution = new CommandExecutionState(
            "Search",
            typeof(DogfoodInteractionViewModel));
        SearchCommand = CommandFactory.CreateAsync(SearchAsync, SearchExecution);
        CancelSearchCommand = CommandFactory.Create(SearchCommand.Cancel);
        ClearCommand = CommandFactory.Create(Clear);
        ApplyCultureCommand = CommandFactory.CreateAsync(ApplyCultureAsync);
        SwitchAccountCommand = CommandFactory.CreateAsync(SwitchAccountAsync);
        RunScenarioCommand = CommandFactory.CreateAsync(RunSelectedScenarioAsync);
        RunScenarioMatrixCommand = CommandFactory.CreateAsync(RunScenarioMatrixAsync);
        CancelScenarioCommand = CommandFactory.Create(CancelScenarios);
    }

    public IReadOnlyList<string> Cultures { get; } = ["en-US", "zh-CN"];

    public IReadOnlyList<string> AccountNames { get; } = ["Alice", "Bob", "Administrator"];

    public IReadOnlyList<string> ScenarioNames { get; } = DogfoodScenarioCatalog.DisplayNames;

    public ObservableCollection<string> SearchResults { get; } = [];

    public IAsyncRelayCommand SearchCommand { get; }

    public IRelayCommand CancelSearchCommand { get; }

    public IRelayCommand ClearCommand { get; }

    public IAsyncRelayCommand ApplyCultureCommand { get; }

    public IAsyncRelayCommand SwitchAccountCommand { get; }

    public IAsyncRelayCommand RunScenarioCommand { get; }

    public IAsyncRelayCommand RunScenarioMatrixCommand { get; }

    public IRelayCommand CancelScenarioCommand { get; }

    public CommandExecutionState SearchExecution { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value ?? string.Empty);
    }

    public string SearchStatus
    {
        get => _searchStatus;
        private set => SetProperty(ref _searchStatus, value);
    }

    public string SelectedCulture
    {
        get => _selectedCulture;
        set => SetProperty(ref _selectedCulture, value ?? "en-US");
    }

    public string CulturePreview
    {
        get => _culturePreview;
        private set => SetProperty(ref _culturePreview, value);
    }

    public string SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value ?? "Administrator");
    }

    public string AccountStatus
    {
        get => _accountStatus;
        private set => SetProperty(ref _accountStatus, value);
    }

    public string SelectedScenario
    {
        get => _selectedScenario;
        set => SetProperty(ref _selectedScenario, value ?? DogfoodScenarioCatalog.DisplayNames[0]);
    }

    public string ScenarioStatus
    {
        get => _scenarioStatus;
        private set => SetProperty(ref _scenarioStatus, value);
    }

    public string ScenarioSummary
    {
        get => _scenarioSummary;
        private set => SetProperty(ref _scenarioSummary, value);
    }

    public bool RealtimeEnabled
    {
        get => _realtimeEnabled;
        set
        {
            if (!SetProperty(ref _realtimeEnabled, value))
            {
                return;
            }

            _stateWriter.Set(
                DogfoodStateWorkload.NetworkReachability,
                value ? "Realtime enabled" : "Realtime paused");
            _ledger.Record("ui-state", value ? "realtime-enabled" : "realtime-paused");
        }
    }

    public double Priority
    {
        get => _priority;
        set
        {
            if (!SetProperty(ref _priority, value))
            {
                return;
            }

            _stateWriter.Update(DogfoodStateWorkload.AuditRevision, static revision => revision + 1);
            _ledger.Record("ui-state", $"priority-{value:0}");
        }
    }

    public void AttachNavigationProjection(Action<IReadOnlyDictionary<string, string>> applyNavigationLabels)
    {
        _applyNavigationLabels = applyNavigationLabels ??
            throw new ArgumentNullException(nameof(applyNavigationLabels));
    }

    public void AttachScenarioRunner(
        Func<string?, bool, CancellationToken, Task<DogfoodScenarioBatchResult>> runScenarios)
    {
        _runScenarios = runScenarios ?? throw new ArgumentNullException(nameof(runScenarios));
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        var query = SearchQuery.Trim();
        if (query.Length == 0)
        {
            SearchStatus = "A search term is required";
            _ledger.Record("ui-validation", "search-required");
            return;
        }

        SearchStatus = $"Searching for {query}";
        _stateWriter.Set(DogfoodStateWorkload.SearchQuery, query);
        var delayMilliseconds = query.StartsWith("cancel", StringComparison.OrdinalIgnoreCase) ? 1_000 : 120;
        var result = await _remoteOperations.SearchAsync(
            query,
            delayMilliseconds,
            DataConcurrencyPolicy.CancelPrevious,
            cancellationToken).ConfigureAwait(false);

        if (result.Status == DataResultStatus.Cancelled)
        {
            await _dispatcher.InvokeAsync(
                () => SearchStatus = $"Search cancelled: {query}",
                CancellationToken.None);
            _ledger.Record("ui-search", query, "cancelled");
            throw new OperationCanceledException(cancellationToken);
        }

        if (!result.Succeeded)
        {
            await _dispatcher.InvokeAsync(
                () => SearchStatus = $"Search failed: {result.Error?.Message ?? result.Status.ToString()}",
                CancellationToken.None);
            _ledger.Record("ui-search", query, "failed");
            return;
        }

        var publication = await _eventBus.PublishAsync(
            new EventChannel<SearchExecuted>("search"),
            new SearchExecuted(
                Guid.NewGuid(),
                query,
                Interlocked.Increment(ref _eventRevision)),
            new EventPublishOptions
            {
                CorrelationId = $"ui-search-{query}",
                CausationId = "headless-control-input",
            },
            cancellationToken).ConfigureAwait(false);
        if (!publication.Succeeded)
        {
            await _dispatcher.InvokeAsync(
                () => SearchStatus = $"Search event failed: {query}",
                CancellationToken.None);
            _ledger.Record("ui-search", query, "failed");
            return;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            SearchResults.Insert(0, $"{query} | {result.Value}");
            SearchStatus = $"Search completed: {query}";
        }, cancellationToken);
        _ledger.Record("ui-search", query);
    }

    private async Task ApplyCultureAsync(CancellationToken cancellationToken)
    {
        var selectedCulture = SelectedCulture;
        var result = await _localization.SetCultureAsync(selectedCulture, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await _dispatcher.InvokeAsync(
                () => CulturePreview = $"Culture failed: {selectedCulture}",
                CancellationToken.None);
            return;
        }

        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var sections = new[]
        {
            "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration",
        };
        for (var index = 0; index < sections.Length; index++)
        {
            var localized = await _localization.GetStringAsync(
                $"Shell.{index:000}",
                DogfoodLocalizationCatalog.LookupContext,
                cancellationToken).ConfigureAwait(false);
            if (localized.IsMissing)
            {
                throw new InvalidOperationException($"UI culture switch could not resolve '{localized.Key}'.");
            }

            labels.Add(sections[index], localized.Value);
        }

        await _dispatcher.InvokeAsync(() =>
        {
            _applyNavigationLabels?.Invoke(labels);
            CulturePreview = $"{_localization.CurrentCulture.Name}: {labels["Dashboard"]}";
        }, cancellationToken);
        _ledger.Record("ui-localization", _localization.CurrentCulture.Name);
    }

    private async Task SwitchAccountAsync(CancellationToken cancellationToken)
    {
        var selectedAccount = SelectedAccount;
        if (!DogfoodAccountCatalog.Accounts.TryGetValue(selectedAccount, out var accountKey))
        {
            await _dispatcher.InvokeAsync(
                () => AccountStatus = $"Unknown account: {selectedAccount}",
                CancellationToken.None);
            return;
        }

        var result = await _accountSessions.SwitchAccountAsync(
            accountKey,
            new AccountSwitchOptions(
                allowOffline: true,
                credentialResourceName: DogfoodRemoteOperations.ClientId),
            cancellationToken).ConfigureAwait(false);
        await _dispatcher.InvokeAsync(
            () => AccountStatus = result.Succeeded
                ? $"{selectedAccount}: {result.Session.Mode}"
                : $"{selectedAccount}: {result.Status}",
            CancellationToken.None);
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_UI_ACCOUNT selected={selectedAccount} result={result.Status} " +
            $"mode={result.Session.Mode} subject={result.Session.AccountKey?.SubjectId ?? "anonymous"}");
        _ledger.Record(
            "ui-security",
            selectedAccount,
            result.Succeeded ? "completed" : result.Status.ToString());
    }

    private Task RunSelectedScenarioAsync(CancellationToken cancellationToken) =>
        RunScenariosAsync(runMatrix: false, cancellationToken);

    private Task RunScenarioMatrixAsync(CancellationToken cancellationToken) =>
        RunScenariosAsync(runMatrix: true, cancellationToken);

    private async Task RunScenariosAsync(bool runMatrix, CancellationToken cancellationToken)
    {
        var runner = _runScenarios ??
            throw new InvalidOperationException("The desktop coordinator has not attached the scenario runner.");
        var expected = runMatrix ? DogfoodScenarioCatalog.All.Count : 1;
        ScenarioStatus = runMatrix
            ? $"Running scenario matrix: 0/{expected}"
            : "Running selected scenario: 0/1";
        ScenarioSummary = "Cross-module transactions are in flight";

        try
        {
            var result = await runner(
                runMatrix ? null : SelectedScenario,
                runMatrix,
                cancellationToken).ConfigureAwait(false);
            await RefreshContextProjectionAsync(cancellationToken).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                ScenarioStatus = runMatrix
                    ? $"Scenario matrix completed: {result.CompletedCount}/{expected}"
                    : $"Scenario completed: {result.CompletedCount}/1";
                ScenarioSummary = result.Summary;
            }, cancellationToken);
            _ledger.Record("ui-scenario", runMatrix ? "matrix" : result.Results[0].ScenarioId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                ScenarioStatus = runMatrix ? "Scenario matrix cancelled" : "Scenario cancelled";
                ScenarioSummary = "Cancellation reached the active cross-module operation";
            }, CancellationToken.None);
            _ledger.Record("ui-scenario-cancel", runMatrix ? "matrix" : "selected");
            throw;
        }
        catch (Exception exception)
        {
            _ledger.RecordFailure(exception);
            await _dispatcher.InvokeAsync(() =>
            {
                ScenarioStatus = runMatrix ? "Scenario matrix failed" : "Scenario failed";
                ScenarioSummary = exception.Message;
            }, CancellationToken.None);
            throw;
        }
    }

    private async Task RefreshContextProjectionAsync(CancellationToken cancellationToken)
    {
        var culture = _localization.CurrentCulture.Name;
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var sections = new[]
        {
            "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration",
        };
        for (var index = 0; index < sections.Length; index++)
        {
            var localized = await _localization.GetStringAsync(
                $"Shell.{index:000}",
                DogfoodLocalizationCatalog.LookupContext,
                cancellationToken).ConfigureAwait(false);
            if (localized.IsMissing)
            {
                throw new InvalidOperationException(
                    $"Scenario projection could not resolve '{localized.Key}'.");
            }

            labels.Add(sections[index], localized.Value);
        }

        var currentAccount = DogfoodAccountCatalog.Accounts
            .SingleOrDefault(pair => pair.Value.Equals(_accountSessions.Current.AccountKey));
        await _dispatcher.InvokeAsync(() =>
        {
            SelectedCulture = culture;
            CulturePreview = $"{culture}: {labels["Dashboard"]}";
            _applyNavigationLabels?.Invoke(labels);
            if (!string.IsNullOrEmpty(currentAccount.Key))
            {
                SelectedAccount = currentAccount.Key;
                AccountStatus = $"{currentAccount.Key}: {_accountSessions.Current.Mode}";
            }
        }, cancellationToken);
    }

    private void CancelScenarios()
    {
        RunScenarioCommand.Cancel();
        RunScenarioMatrixCommand.Cancel();
    }

    private void Clear()
    {
        SearchQuery = string.Empty;
        SearchResults.Clear();
        SearchStatus = "Cleared by operator";
        _ledger.Record("ui-command", "clear");
    }
}
