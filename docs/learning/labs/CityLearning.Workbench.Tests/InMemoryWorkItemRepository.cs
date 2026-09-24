using CityLearning.Workbench.Models;
using CityLearning.Workbench.Services;

namespace CityLearning.Workbench.Tests;

internal sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private List<WorkItem> _items = [];

    public Task<IReadOnlyList<WorkItem>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WorkItem>>([.. _items]);
    }

    public Task WriteAllAsync(
        IReadOnlyCollection<WorkItem> items,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items = [.. items];
        return Task.CompletedTask;
    }
}
