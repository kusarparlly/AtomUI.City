using CityLearning.Workbench.Models;

namespace CityLearning.Workbench.Services;

public interface IWorkItemRepository
{
    Task<IReadOnlyList<WorkItem>> ReadAllAsync(CancellationToken cancellationToken = default);

    Task WriteAllAsync(
        IReadOnlyCollection<WorkItem> items,
        CancellationToken cancellationToken = default);
}
