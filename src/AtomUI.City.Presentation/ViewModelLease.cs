using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the supported view model ownership values.
/// </summary>
public enum ViewModelOwnership
{
    /// <summary>
    /// Represents the entry owned value.
    /// </summary>
    EntryOwned,
    /// <summary>
    /// Represents the service scope owned value.
    /// </summary>
    ServiceScopeOwned,
    /// <summary>
    /// Represents the borrowed value.
    /// </summary>
    Borrowed,
}

/// <summary>
/// Represents view model lease.
/// </summary>
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

    /// <summary>
    /// Gets instance.
    /// </summary>
    public object Instance { get; }

    /// <summary>
    /// Gets ownership.
    /// </summary>
    public ViewModelOwnership Ownership { get; }

    /// <summary>
    /// Gets entry owned.
    /// </summary>
    public static ViewModelLease EntryOwned(object instance) =>
        new(instance, ViewModelOwnership.EntryOwned, serviceScope: null);

    /// <summary>
    /// Gets service scope owned.
    /// </summary>
    public static ViewModelLease ServiceScopeOwned(object instance, IServiceScope serviceScope) =>
        new(instance, ViewModelOwnership.ServiceScopeOwned,
            serviceScope ?? throw new ArgumentNullException(nameof(serviceScope)));

    /// <summary>
    /// Gets borrowed.
    /// </summary>
    public static ViewModelLease Borrowed(object instance) =>
        new(instance, ViewModelOwnership.Borrowed, serviceScope: null);

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
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
