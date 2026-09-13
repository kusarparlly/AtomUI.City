using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Fixtures.DesktopDogfood;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly DashboardProjectionService _projectionService;
    private string _activationStatus = "Waiting for activation";
    private string _projectionSample = "Not loaded";

    public DashboardViewModel(DashboardProjectionService projectionService)
    {
        _projectionService = projectionService;
        RefreshCommand = CommandFactory.CreateAsync(RefreshAsync);
        ResetCommand = CommandFactory.Create(Reset);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ResetCommand { get; }

    public string Title => "Operations overview";

    public string Subtitle => "A live cross-module view of the City desktop runtime";

    public string ActivationStatus
    {
        get => _activationStatus;
        private set => SetProperty(ref _activationStatus, value);
    }

    public string ProjectionSample
    {
        get => _projectionSample;
        private set => SetProperty(ref _projectionSample, value);
    }

    protected override async ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionSample = _projectionService.Execute("dashboard-activation");
        ActivationStatus = $"Active in scope {context.Scope.Id}";
    }

    protected override ValueTask OnDeactivatedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ActivationStatus = "Deactivated";
        return ValueTask.CompletedTask;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionSample = _projectionService.Execute("dashboard-command");
    }

    private void Reset()
    {
        ProjectionSample = "Reset by synchronous command";
    }
}
