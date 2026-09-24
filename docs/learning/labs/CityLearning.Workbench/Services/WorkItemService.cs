using AtomUI.City.Core.DependencyInjection;
using CityLearning.Workbench.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CityLearning.Workbench.Services;

[Service(ServiceLifetime.Singleton)]
public sealed class WorkItemService(IWorkItemRepository repository) : IDisposable
{
    private readonly SemaphoreSlim _transactionGate = new(1, 1);

    public async Task<IReadOnlyList<WorkItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await _transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await repository.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _transactionGate.Release();
        }
    }

    public async Task<WorkItem> AddAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var normalizedTitle = title.Trim();

        await _transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = (await repository.ReadAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var item = new WorkItem(Guid.NewGuid(), normalizedTitle, DateTimeOffset.UtcNow, null);
            items.Add(item);
            await repository.WriteAllAsync(items, cancellationToken).ConfigureAwait(false);
            return item;
        }
        finally
        {
            _transactionGate.Release();
        }
    }

    public async Task<bool> CompleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = (await repository.ReadAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = items.FindIndex(item => item.Id == id);
            if (index < 0 || items[index].IsCompleted)
            {
                return false;
            }

            items[index] = items[index] with { CompletedAt = DateTimeOffset.UtcNow };
            await repository.WriteAllAsync(items, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _transactionGate.Release();
        }
    }

    public void Dispose() => _transactionGate.Dispose();
}
