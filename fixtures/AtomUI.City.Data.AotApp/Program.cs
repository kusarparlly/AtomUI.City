using AtomUI.City.Data;
using AtomUI.City.Fixtures;

namespace AtomUI.City.Data.AotApp;

[DataClient("aot-probe", DataTransportKind.Http, Version = "1")]
public interface IAotProbeClient
{
    [DataOperation("load", DataAccessMode.Query, CacheEnabled = true)]
    ValueTask<DataResult<AotReply>> LoadAsync(AotRequest request, CancellationToken cancellationToken = default);
}

public sealed record AotRequest(int Value);

public sealed record AotReply(int Value);

internal static class Program
{
    public static Task<int> Main()
    {
        return ProcessEntryPoint.RunAsync(RunAsync);
    }

    private static async Task<int> RunAsync()
    {
        var catalog = new DataClientDescriptorCatalog();
        catalog.RegisterGenerated<
            global::AtomUI.City.Generated.GeneratedDataClientRegistrar_AtomUI_City_Data_AotApp_5F5EC4E4>();
        if (!catalog.TryGet("aot-probe", out var descriptor)
            || descriptor is null
            || descriptor.Operations.Count != 1)
        {
            return 1;
        }

        using var scheduler = new DataOperationScheduler();
        var request = new DataRequest<int>("aot", "schedule", DataTransportKind.Http);
        var result = await scheduler.ExecuteAsync(
            request,
            _ => ValueTask.FromResult(DataResult<int>.Success(42)));
        if (!result.Succeeded || result.Value != 42)
        {
            return 2;
        }

        var fingerprint = DataCacheFingerprint.Create("https://localhost/aot", "GET", "{}");
        if (fingerprint.Length != 64)
        {
            return 3;
        }

        Console.WriteLine("DATA_AOT_OK");
        return 0;
    }
}
