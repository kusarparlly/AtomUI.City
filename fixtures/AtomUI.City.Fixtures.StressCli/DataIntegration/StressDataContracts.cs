using AtomUI.City.Data;

namespace AtomUI.City.Fixtures.StressCli.DataIntegration;

public sealed record StressProductSnapshot(
    string Sku,
    decimal Price,
    int Quantity,
    string Principal,
    long Revision);

public sealed record StressSubmitOrderRequest(string Sku, int Quantity, string RequestId);

public sealed record StressOrderReceipt(
    string OrderId,
    string Sku,
    int Quantity,
    decimal Amount,
    string Principal,
    long Revision);

public sealed record StressPrincipalSnapshot(string Principal, string Revision);

public sealed record StressSearchRequest(string Term, int DelayMilliseconds);

public sealed record StressInventoryPush(string Sku, int Quantity, long Sequence);

public sealed record StressShipmentPush(string ShipmentId, int Percent, long Sequence);

public static class StressDataFaults
{
    public const string GrpcUnavailableSku = "fault-unavailable";

    public const string GrpcDeadlineSku = "fault-deadline";

    public const string GrpcServerStreamAbortSku = "fault-server-stream-abort";

    public const string GrpcDuplexAbortSku = "fault-duplex-abort";
}

[DataClient("stress-operations", DataTransportKind.Http, Version = "1")]
public interface IStressOperationsDataClient
{
    [DataOperation(
        "get-product",
        DataAccessMode.Query,
        ConcurrencyPolicy = DataConcurrencyPolicy.AllowConcurrent,
        CacheEnabled = true,
        AuthenticationPolicy = "Bearer")]
    ValueTask<DataResult<StressProductSnapshot>> GetProductAsync(
        string sku,
        CancellationToken cancellationToken = default);

    [DataOperation(
        "submit-order",
        DataAccessMode.Mutation,
        ConcurrencyPolicy = DataConcurrencyPolicy.KeyedSerial,
        TimeoutMilliseconds = 5_000,
        AuthenticationPolicy = "Bearer")]
    ValueTask<DataResult<StressOrderReceipt>> SubmitOrderAsync(
        StressSubmitOrderRequest request,
        CancellationToken cancellationToken = default);

    [DataOperation(
        "search-orders",
        DataAccessMode.Query,
        ConcurrencyPolicy = DataConcurrencyPolicy.LatestWins,
        AuthenticationPolicy = "Bearer")]
    ValueTask<DataResult<string>> SearchOrdersAsync(
        StressSearchRequest request,
        CancellationToken cancellationToken = default);

    [DataOperation(
        "get-principal",
        DataAccessMode.Query,
        AuthenticationPolicy = "Bearer")]
    ValueTask<DataResult<StressPrincipalSnapshot>> GetPrincipalAsync(
        CancellationToken cancellationToken = default);
}

public sealed record StressDataEndpoints(Uri Http, Uri Grpc);
