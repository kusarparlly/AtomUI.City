using System.Text.Json;
using System.Text.Json.Serialization;
using AtomUI.City.Core.DependencyInjection;
using CityLearning.Workbench.Configuration;
using CityLearning.Workbench.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CityLearning.Workbench.Services;

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IWorkItemRepository))]
public sealed class JsonWorkItemRepository(WorkbenchOptions options) : IWorkItemRepository
{
    public async Task<IReadOnlyList<WorkItem>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.DataFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(options.DataFilePath);
        return await JsonSerializer.DeserializeAsync(
                stream,
                WorkbenchJsonSerializerContext.Default.ListWorkItem,
                cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    public async Task WriteAllAsync(
        IReadOnlyCollection<WorkItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var directory = Path.GetDirectoryName(options.DataFilePath)
            ?? throw new InvalidOperationException("The data file path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = options.DataFilePath + ".tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        items.ToList(),
                        WorkbenchJsonSerializerContext.Default.ListWorkItem,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, options.DataFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

[JsonSerializable(typeof(List<WorkItem>))]
internal sealed partial class WorkbenchJsonSerializerContext : JsonSerializerContext;
