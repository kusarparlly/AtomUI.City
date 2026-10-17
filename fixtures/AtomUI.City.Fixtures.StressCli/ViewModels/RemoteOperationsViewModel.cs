using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.Localization;
using AtomUI.City.Mvvm;
using AtomUI.City.State;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Fixtures.StressCli.ViewModels;

public sealed class RemoteOperationsViewModel : ViewModelBase
{
    private readonly IStressRemoteOperations _operations;
    private readonly IEventBus _eventBus;
    private readonly LifecycleScope _eventOwnerRoot;
    private readonly IApplicationState _state;
    private readonly IApplicationStateWriter _writer;
    private readonly ILocalizationService _localization;
    private LifecycleScope? _requestScope;
    private string _status = "idle";
    private string _message = string.Empty;
    private string _lastOrderId = string.Empty;

    public RemoteOperationsViewModel(
        IStressRemoteOperations operations,
        IEventBus eventBus,
        LifecycleScope eventOwnerRoot,
        IApplicationState state,
        IApplicationStateWriter writer,
        ILocalizationService localization,
        IHostDiagnostics diagnostics)
        : base(diagnostics)
    {
        _operations = operations;
        _eventBus = eventBus;
        _eventOwnerRoot = eventOwnerRoot;
        _state = state;
        _writer = writer;
        _localization = localization;

        LoadProductCommand = CommandFactory.CreateAsync(
            cancellationToken => LoadProductAsync("SKU-0001", cancellationToken),
            new CommandExecutionState("remote.load-product", GetType()),
            diagnostics: diagnostics);
        SubmitOrderCommand = CommandFactory.CreateAsync(
            cancellationToken => SubmitOrderAsync("SKU-0001", 1, cancellationToken),
            new CommandExecutionState("remote.submit-order", GetType()),
            diagnostics: diagnostics);
        SearchCommand = CommandFactory.CreateAsync(
            cancellationToken => SearchAsync("orders", 20, cancellationToken),
            new CommandExecutionState("remote.search", GetType()),
            diagnostics: diagnostics);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string LastOrderId
    {
        get => _lastOrderId;
        private set => SetProperty(ref _lastOrderId, value);
    }

    public IAsyncRelayCommand LoadProductCommand { get; }

    public IAsyncRelayCommand SubmitOrderCommand { get; }

    public IAsyncRelayCommand SearchCommand { get; }

    public async Task<DataResult<StressProductSnapshot>> LoadProductAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        Status = "loading";
        var result = await _operations.GetProductAsync(
            sku,
            _requestScope,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            await _eventBus.PublishAsync(new RemoteProductLoaded(result.Value!), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var message = await _localization.GetMessageAsync(
                "Data.ProductLoaded",
                [result.Value!.Sku, result.Value.Quantity],
                cancellationToken).ConfigureAwait(false);
            Message = message.Value;
            Status = "ready";
        }
        else
        {
            await PublishFailureAsync("get-product", result.Error, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<DataResult<StressOrderReceipt>> SubmitOrderAsync(
        string sku,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        Status = "submitting";
        var optimistic = new StressInventoryOptimisticUpdate(_state, _writer, quantity);
        var result = await _operations.SubmitOrderAsync(
            new StressSubmitOrderRequest(sku, quantity, Guid.NewGuid().ToString("N")),
            optimistic,
            _requestScope,
            cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            await _eventBus.PublishAsync(new RemoteOrderSubmitted(result.Value!), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            LastOrderId = result.Value!.OrderId;
            var message = await _localization.GetMessageAsync(
                "Data.OrderSubmitted",
                [result.Value.OrderId, result.Value.Amount],
                cancellationToken).ConfigureAwait(false);
            Message = message.Value;
            Status = "ready";
        }
        else
        {
            await PublishFailureAsync("submit-order", result.Error, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<DataResult<string>> SearchAsync(
        string term,
        int delayMilliseconds,
        CancellationToken cancellationToken = default)
    {
        Status = "searching";
        var result = await _operations.SearchAsync(
            new StressSearchRequest(term, delayMilliseconds),
            DataConcurrencyPolicy.LatestWins,
            _requestScope,
            cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            Message = result.Value!;
            Status = "ready";
        }
        else if (result.Status is not (DataResultStatus.Cancelled or DataResultStatus.StaleSuppressed))
        {
            await PublishFailureAsync("search-orders", result.Error, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    protected override ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        _requestScope = _eventOwnerRoot.CreateChild(LifecycleScopeKind.Operation, "remote-operations-requests");
        context.Scope.Add(_requestScope);
        context.Scope.Add(_state.Get(PhaseD.StateCatalog.RemoteMessage).OnChange(change => Message = change.NewValue));
        context.Scope.Add(_state.Get(PhaseD.StateCatalog.RemoteStatus).OnChange(change => Status = change.NewValue));
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnDeactivatedAsync(CancellationToken cancellationToken)
    {
        _requestScope = null;
        return ValueTask.CompletedTask;
    }

    private async Task PublishFailureAsync(
        string operation,
        DataError? error,
        CancellationToken cancellationToken)
    {
        var messageKey = MapMessageKey(error?.Kind ?? DataErrorKind.Unknown);
        await _eventBus.PublishAsync(
            new RemoteDataFailed(operation, (error?.Kind ?? DataErrorKind.Unknown).ToString(), messageKey),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        Message = (await _localization.GetStringAsync(messageKey, cancellationToken).ConfigureAwait(false)).Value;
        Status = "failed";
    }

    private static string MapMessageKey(DataErrorKind kind) => kind switch
    {
        DataErrorKind.NetworkUnavailable or DataErrorKind.Timeout or DataErrorKind.ConnectionFailed => "Data.Errors.Network",
        DataErrorKind.AuthenticationRequired or DataErrorKind.AuthorizationForbidden => "Data.Errors.Authentication",
        DataErrorKind.Conflict => "Data.Errors.Conflict",
        DataErrorKind.Cancelled => "Data.Errors.Cancelled",
        _ => "Data.Errors.Unknown",
    };
}
