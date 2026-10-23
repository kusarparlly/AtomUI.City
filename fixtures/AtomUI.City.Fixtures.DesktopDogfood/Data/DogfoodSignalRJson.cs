using System.Text.Json.Serialization;
using AtomUI.City.Data;
using AtomUI.City.Fixtures.StressCli.DataIntegration;

namespace AtomUI.City.Fixtures.DesktopDogfood;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(StressInventoryPush))]
[JsonSerializable(typeof(StressInventoryPush[]))]
internal sealed partial class DogfoodSignalRJsonContext : JsonSerializerContext;

internal static class DogfoodSignalRJson
{
    public static SignalRJsonContract Contract { get; } =
        SignalRJsonContract.Create(DogfoodSignalRJsonContext.Default);
}
