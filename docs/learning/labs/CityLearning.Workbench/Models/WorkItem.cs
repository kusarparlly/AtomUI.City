using System.Text.Json.Serialization;

namespace CityLearning.Workbench.Models;

public sealed record WorkItem(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    [JsonIgnore]
    public bool IsCompleted => CompletedAt is not null;
}
