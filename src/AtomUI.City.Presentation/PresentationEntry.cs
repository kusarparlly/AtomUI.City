using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

public sealed class PresentationEntry : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly RouteOutletCommitPlan? _sourcePlan;
    private readonly IDisposable? _visualLifecycleSubscription;
    private readonly IUiDispatcher _dispatcher;
    private Task? _disposeTask;

    internal PresentationEntry(
        long operationId,
        BoundViewHandle handle,
        ActivationScope activationScope,
        ViewModelLease? viewModelLease,
        string? routeId,
        string? reuseKey,
        IUiDispatcher dispatcher,
        RouteOutletCommitPlan? sourcePlan = null,
        IDisposable? visualLifecycleSubscription = null)
    {
        OperationId = operationId;
        Handle = handle ?? throw new ArgumentNullException(nameof(handle));
        ActivationScope = activationScope ?? throw new ArgumentNullException(nameof(activationScope));
        ViewModelLease = viewModelLease;
        RouteId = string.IsNullOrWhiteSpace(routeId) ? null : routeId;
        ReuseKey = string.IsNullOrWhiteSpace(reuseKey) ? null : reuseKey;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _sourcePlan = sourcePlan;
        _visualLifecycleSubscription = visualLifecycleSubscription;
    }

    public long OperationId { get; }

    public BoundViewHandle Handle { get; }

    public object View => Handle.View;

    public object ViewModel => Handle.ViewModel;

    public ActivationScope ActivationScope { get; }

    public ViewModelLease? ViewModelLease { get; }

    public string? RouteId { get; }

    public string? ReuseKey { get; }

    public bool IsActivated { get; internal set; }

    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        TaskCompletionSource? completion = null;

        lock (_gate)
        {
            if (_disposeTask is null)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = completion.Task;
            }

            disposeTask = _disposeTask;
        }

        if (completion is not null)
        {
            _ = RunDisposeAsync(completion);
        }

        return new ValueTask(disposeTask);
    }

    private async Task RunDisposeAsync(TaskCompletionSource completion)
    {
        var failures = new List<Exception>();

        if (IsActivated && ViewModel is IActivatable activatable)
        {
            try
            {
                await activatable.DeactivateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        try
        {
            await ActivationScope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await _dispatcher.InvokeAsync(
                () =>
                {
                    try
                    {
                        _visualLifecycleSubscription?.Dispose();
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }

                    try
                    {
                        Handle.Dispose();
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (ViewModelLease is not null)
        {
            try
            {
                await ViewModelLease.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        try
        {
            _sourcePlan?.Release(OperationId);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count == 0)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(new AggregateException(
                "Presentation entry cleanup failed.",
                failures));
        }
    }
}
