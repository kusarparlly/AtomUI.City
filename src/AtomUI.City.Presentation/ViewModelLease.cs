using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation;

public enum ViewModelOwnership
{
    EntryOwned,
    ServiceScopeOwned,
    Borrowed,
}

public sealed class ViewModelLease : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IServiceScope? _serviceScope;
    private Task? _disposeTask;

    private ViewModelLease(
        object instance,
        ViewModelOwnership ownership,
        IServiceScope? serviceScope)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Instance = instance;
        Ownership = ownership;
        _serviceScope = serviceScope;
    }

    public object Instance { get; }

    public ViewModelOwnership Ownership { get; }

    public static ViewModelLease EntryOwned(object instance) =>
        new(instance, ViewModelOwnership.EntryOwned, serviceScope: null);

    public static ViewModelLease ServiceScopeOwned(object instance, IServiceScope serviceScope) =>
        new(instance, ViewModelOwnership.ServiceScopeOwned,
            serviceScope ?? throw new ArgumentNullException(nameof(serviceScope)));

    public static ViewModelLease Borrowed(object instance) =>
        new(instance, ViewModelOwnership.Borrowed, serviceScope: null);

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
        try
        {
            if (_serviceScope is IAsyncDisposable asyncScope)
            {
                await asyncScope.DisposeAsync().ConfigureAwait(false);
            }
            else if (_serviceScope is not null)
            {
                _serviceScope.Dispose();
            }
            else if (Ownership == ViewModelOwnership.EntryOwned)
            {
                if (Instance is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (Instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
