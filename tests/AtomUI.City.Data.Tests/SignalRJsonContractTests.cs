using System.Text.Json.Serialization;

namespace AtomUI.City.Data.Tests;

public sealed class SignalRJsonContractTests
{
    private static readonly DataConnectionOwner Owner =
        new(DataConnectionOwnerKind.Application, "signalr-json-tests");

    [Fact]
    public void ContractRequiresAtLeastOneResolver()
    {
        Assert.Throws<ArgumentException>(() => SignalRJsonContract.Create([]));
        Assert.Throws<ArgumentException>(() =>
            SignalRJsonContract.Create([SignalRTestJsonContext.Default, null!]));
    }

    [Fact]
    public async Task InvokeFailsClosedBeforeTransportWhenResponseMetadataIsMissing()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        await using var connection = CreateConnection(diagnostics);

        var result = await connection.InvokeAsync<string>("MissingResponse");

        Assert.Equal(DataResultStatus.Failed, result.Status);
        Assert.Equal(DataErrorKind.SerializationError, result.Error?.Kind);
        var exception = Assert.IsType<SignalRPayloadMetadataException>(result.Error?.Exception);
        Assert.Equal(SignalRPayloadDirection.Response, exception.Direction);
        Assert.Equal(typeof(string), exception.PayloadType);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == DataDiagnosticIds.SignalRPayloadMetadataMissing
            && record.ClientId == "signalr-json-tests"
            && record.OperationName == "MissingResponse");
    }

    [Fact]
    public async Task InvokeFailsClosedBeforeTransportWhenArgumentMetadataIsMissing()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        await using var connection = CreateConnection(diagnostics);

        var result = await connection.InvokeAsync<int>("MissingArgument", ["not-registered"]);

        Assert.Equal(DataErrorKind.SerializationError, result.Error?.Kind);
        var exception = Assert.IsType<SignalRPayloadMetadataException>(result.Error?.Exception);
        Assert.Equal(SignalRPayloadDirection.Argument, exception.Direction);
        Assert.Equal(typeof(string), exception.PayloadType);
    }

    [Fact]
    public async Task SubscribeFailsClosedBeforeRegistrationWhenMessageMetadataIsMissing()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        await using var connection = CreateConnection(diagnostics);

        var exception = Assert.Throws<SignalRPayloadMetadataException>(() =>
            connection.Subscribe<string>("MissingMessage", static (_, _) => ValueTask.CompletedTask));

        Assert.Equal(SignalRPayloadDirection.ServerMessage, exception.Direction);
        Assert.Equal(typeof(string), exception.PayloadType);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == DataDiagnosticIds.SignalRPayloadMetadataMissing);
    }

    [Fact]
    public async Task RegisteredMetadataContinuesToNormalConnectionStateValidation()
    {
        await using var connection = CreateConnection();

        var result = await connection.InvokeAsync<int>("KnownPayload", [42]);

        Assert.Equal(DataErrorKind.ConnectionClosed, result.Error?.Kind);
    }

    private static SignalRRealtimeConnection CreateConnection(IDataDiagnostics? diagnostics = null) =>
        SignalRRealtimeConnection.Create(
            new SignalRConnectionOptions
            {
                ConnectionId = "signalr-json-tests",
                Endpoint = new Uri("http://127.0.0.1:1/test-hub"),
                Owner = Owner,
                JsonContract = SignalRJsonContract.Create(SignalRTestJsonContext.Default),
            },
            diagnostics);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(int))]
internal sealed partial class SignalRTestJsonContext : JsonSerializerContext;
