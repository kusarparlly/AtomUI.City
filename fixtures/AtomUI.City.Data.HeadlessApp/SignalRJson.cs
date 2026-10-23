using System.Text.Json.Serialization;
using AtomUI.City.Data;

namespace AtomUI.City.Data.HeadlessApp;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(object))]
internal sealed partial class DataHeadlessSignalRJsonContext : JsonSerializerContext;

internal static class DataHeadlessSignalRJson
{
    public static SignalRJsonContract Contract { get; } =
        SignalRJsonContract.Create(DataHeadlessSignalRJsonContext.Default);
}
