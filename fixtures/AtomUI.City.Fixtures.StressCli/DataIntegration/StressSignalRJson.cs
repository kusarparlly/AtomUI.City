using System.Text.Json.Serialization;
using AtomUI.City.Data;

namespace AtomUI.City.Fixtures.StressCli.DataIntegration;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(int))]
internal sealed partial class StressSignalRJsonContext : JsonSerializerContext;

internal static class StressSignalRJson
{
    public static SignalRJsonContract Contract { get; } =
        SignalRJsonContract.Create(StressSignalRJsonContext.Default);
}
