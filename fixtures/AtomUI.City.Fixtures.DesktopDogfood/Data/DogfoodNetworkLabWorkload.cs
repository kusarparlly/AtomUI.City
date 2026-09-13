using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AtomUI.City.Data;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.DataIntegration.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodNetworkLabWorkload
{
    private const string GrpcServiceName = "stressdata.StressDataProbe";
    private static readonly Method<InventoryRequest, InventoryReply> InventoryMethod = new(
        MethodType.Unary,
        GrpcServiceName,
        "Inventory",
        Marshallers.Create(
            static message => message.ToByteArray(),
            static payload => InventoryRequest.Parser.ParseFrom(payload)),
        Marshallers.Create(
            static message => message.ToByteArray(),
            static payload => InventoryReply.Parser.ParseFrom(payload)));

    private readonly StressDataServer _server;
    private readonly IDataDiagnostics _diagnostics;
    private readonly DogfoodRunOptions _options;
    private readonly DogfoodRunLedger _ledger;
    private readonly List<DogfoodNetworkCaseResult> _results = [];
    private int _executed;

    public DogfoodNetworkLabWorkload(
        StressDataServer server,
        IDataDiagnostics diagnostics,
        DogfoodRunOptions options,
        DogfoodRunLedger ledger)
    {
        _server = server;
        _diagnostics = diagnostics;
        _options = options;
        _ledger = ledger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _executed, 1) != 0)
        {
            throw new InvalidOperationException("The network lab can only run once.");
        }

        await VerifyHttpProxyAsync(cancellationToken).ConfigureAwait(false);
        await VerifyGrpcProxyAsync(cancellationToken).ConfigureAwait(false);
        await VerifySignalRProxyAsync(cancellationToken).ConfigureAwait(false);
        await VerifyTlsAndLogicalDnsAsync(cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(_options.ArtifactRoot, "network-chaos-report.json");
        Directory.CreateDirectory(_options.ArtifactRoot);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                new DogfoodNetworkReport(1, _results.Count, _results),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }),
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"DESKTOP_DOGFOOD_NETWORK cases={_results.Count} passed=true path={path}");
    }

    private async Task VerifyHttpProxyAsync(CancellationToken cancellationToken)
    {
        await using var proxy = await DogfoodTcpFaultProxy.StartAsync(_server.Endpoints.Http).ConfigureAwait(false);
        using var healthyClient = CreateHttpClient(new Uri($"http://127.0.0.1:{proxy.Port}"), TimeSpan.FromSeconds(3));
        var transport = new HttpDataTransport(new SingleHttpClientFactory("network", healthyClient));
        await RequireHttpAsync(transport, "/api/fault/health", succeeded: true, null, cancellationToken)
            .ConfigureAwait(false);
        Record("NET-HTTP-HEALTHY", "http", "healthy");

        proxy.SetProfile(new DogfoodTcpFaultProfile(TimeSpan.FromMilliseconds(60)));
        proxy.ResetConnections();
        var stopwatch = Stopwatch.StartNew();
        await RequireHttpAsync(transport, "/api/fault/health", succeeded: true, null, cancellationToken)
            .ConfigureAwait(false);
        Require(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(40), "HTTP latency injection was not observable.");
        Record("NET-HTTP-LATENCY", "http", "latency");

        proxy.ResetConnections();
        proxy.SetProfile(new DogfoodTcpFaultProfile(TimeSpan.Zero, Blackhole: true));
        using var timeoutClient = CreateHttpClient(
            new Uri($"http://127.0.0.1:{proxy.Port}"),
            TimeSpan.FromMilliseconds(180));
        var timeoutTransport = new HttpDataTransport(new SingleHttpClientFactory("network", timeoutClient));
        await RequireHttpAsync(
            timeoutTransport,
            "/api/fault/health",
            succeeded: false,
            DataErrorKind.Timeout,
            cancellationToken).ConfigureAwait(false);
        Record("NET-HTTP-BLACKHOLE", "http", "timeout", expectedFailure: true);

        proxy.SetProfile(new DogfoodTcpFaultProfile(TimeSpan.Zero, ResetAfterBytes: 48));
        proxy.ResetConnections();
        await RequireHttpAsync(
            transport,
            "/api/products/SKU-NET-RESET",
            succeeded: false,
            DataErrorKind.TransportError,
            cancellationToken,
            authorize: true).ConfigureAwait(false);
        Record("NET-HTTP-RESET", "http", "connection-reset", expectedFailure: true);
    }

    private async Task VerifyGrpcProxyAsync(CancellationToken cancellationToken)
    {
        await using var proxy = await DogfoodTcpFaultProxy.StartAsync(_server.Endpoints.Grpc).ConfigureAwait(false);
        proxy.SetProfile(new DogfoodTcpFaultProfile(TimeSpan.FromMilliseconds(15), 1024, TimeSpan.FromMilliseconds(2)));
        using var channel = GrpcChannel.ForAddress(
            $"http://127.0.0.1:{proxy.Port}",
            new GrpcChannelOptions { HttpHandler = new SocketsHttpHandler { UseProxy = false } });
        using var connection = new GrpcChannelConnection(
            "dogfood-network-grpc",
            new DataConnectionOwner(DataConnectionOwnerKind.Application, "dogfood-network"),
            channel);
        await connection.StartAsync(cancellationToken).ConfigureAwait(false);
        var client = new NativeGrpcClient(connection, _diagnostics);
        var healthy = await client.UnaryAsync(
            InventoryMethod,
            new InventoryRequest { Sku = "SKU-NET-GRPC" },
            new GrpcCallOptions { DeadlineUtc = DateTime.UtcNow.AddSeconds(3) },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        Require(healthy.Succeeded, $"gRPC proxy health call failed: {healthy.Error?.Kind}.");
        Record("NET-GRPC-THROTTLED", "grpc", "healthy-with-throttle");

        proxy.SetProfile(new DogfoodTcpFaultProfile(TimeSpan.Zero, ResetAfterBytes: 32));
        proxy.ResetConnections();
        var failed = await client.UnaryAsync(
            InventoryMethod,
            new InventoryRequest { Sku = "SKU-NET-GRPC-RESET" },
            new GrpcCallOptions { DeadlineUtc = DateTime.UtcNow.AddSeconds(2) },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        Require(!failed.Succeeded, "gRPC connection reset unexpectedly succeeded.");
        Record("NET-GRPC-RESET", "grpc", failed.Error?.Kind.ToString() ?? "failed", expectedFailure: true);
        await connection.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task VerifySignalRProxyAsync(CancellationToken cancellationToken)
    {
        await using var proxy = await DogfoodTcpFaultProxy.StartAsync(_server.Endpoints.Http).ConfigureAwait(false);
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "dogfood-network-signalr");
        await using var connection = SignalRRealtimeConnection.Create(
            new SignalRConnectionOptions
            {
                ConnectionId = "dogfood-network-signalr",
                Endpoint = new Uri($"http://127.0.0.1:{proxy.Port}/stress-hub"),
                Owner = owner,
                AccessTokenProvider = () => ValueTask.FromResult<string?>("stress/admin/r10"),
                ReconnectDelays =
                [
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(250),
                ],
            },
            _diagnostics);
        var states = new ConcurrentQueue<DataConnectionState>();
        connection.StateChanged += (_, args) => states.Enqueue(args.CurrentState);
        await connection.StartAsync(cancellationToken).ConfigureAwait(false);
        var before = await connection.InvokeAsync<int>("Echo", [41], cancellationToken).ConfigureAwait(false);
        Require(before.Succeeded && before.Value == 42, "SignalR pre-reset invoke failed.");
        proxy.ResetConnections();
        await WaitForAsync(
            () => states.Contains(DataConnectionState.Reconnecting) &&
                  connection.State == DataConnectionState.Connected,
            TimeSpan.FromSeconds(8),
            cancellationToken).ConfigureAwait(false);
        var after = await connection.InvokeAsync<int>("Echo", [41], cancellationToken).ConfigureAwait(false);
        Require(after.Succeeded && after.Value == 42, "SignalR did not recover through the proxy.");
        await connection.StopAsync(CancellationToken.None).ConfigureAwait(false);
        Record("NET-SIGNALR-RECONNECT", "signalr", "reconnected", expectedFailure: true);
    }

    private async Task VerifyTlsAndLogicalDnsAsync(CancellationToken cancellationToken)
    {
        await using var endpoint = await DogfoodTlsEndpoint.StartAsync(cancellationToken).ConfigureAwait(false);
        using (var pinned = CreatePinnedTlsClient(endpoint.Certificate))
        {
            var transport = new HttpDataTransport(new SingleHttpClientFactory("network", pinned));
            await RequireHttpAsync(
                transport,
                $"https://127.0.0.1:{endpoint.EndPoint.Port}/health",
                succeeded: true,
                null,
                cancellationToken).ConfigureAwait(false);
        }
        Record("NET-TLS-PINNED", "https", "certificate-pinned");

        var resolver = new DogfoodLogicalEndpointResolver(endpoint.PlainTextEndPoint);
        resolver.Current = null;
        using (var dnsUnavailable = CreateLogicalDnsClient(resolver))
        {
            var transport = new HttpDataTransport(new SingleHttpClientFactory("network", dnsUnavailable));
            await RequireHttpAsync(
                transport,
                $"http://dogfood.test:{endpoint.PlainTextEndPoint.Port}/health",
                succeeded: false,
                DataErrorKind.TransportError,
                cancellationToken).ConfigureAwait(false);
        }
        Record("NET-DNS-UNAVAILABLE", "dns", "resolver-unavailable", expectedFailure: true);

        resolver.Current = endpoint.PlainTextEndPoint;
        using (var dnsRecovered = CreateLogicalDnsClient(resolver))
        {
            var transport = new HttpDataTransport(new SingleHttpClientFactory("network", dnsRecovered));
            await RequireHttpAsync(
                transport,
                $"http://dogfood.test:{endpoint.PlainTextEndPoint.Port}/health",
                succeeded: true,
                null,
                cancellationToken).ConfigureAwait(false);
        }
        Record("NET-DNS-RECOVERED", "dns", "resolver-recovered");

        using var wrongPin = CreatePinnedTlsClient(null);
        var rejectedTransport = new HttpDataTransport(new SingleHttpClientFactory("network", wrongPin));
        await RequireHttpAsync(
            rejectedTransport,
            $"https://127.0.0.1:{endpoint.EndPoint.Port}/health",
            succeeded: false,
            DataErrorKind.TransportError,
            cancellationToken).ConfigureAwait(false);
        Record("NET-TLS-PIN-MISMATCH", "https", "certificate-rejected", expectedFailure: true);
    }

    private static HttpClient CreateHttpClient(Uri baseAddress, TimeSpan timeout) => new(
        new SocketsHttpHandler { UseProxy = false })
    {
        BaseAddress = baseAddress,
        Timeout = timeout,
    };

    private static HttpClient CreatePinnedTlsClient(X509Certificate2? expectedCertificate)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                expectedCertificate is not null &&
                certificate is not null &&
                string.Equals(
                    certificate.GetCertHashString(),
                    expectedCertificate.GetCertHashString(),
                    StringComparison.OrdinalIgnoreCase),
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
    }

    private static HttpClient CreateLogicalDnsClient(DogfoodLogicalEndpointResolver resolver)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = async (_, cancellationToken) =>
            {
                var endpoint = resolver.Current ?? throw new SocketException((int)SocketError.HostNotFound);
                var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
    }

    private static async Task RequireHttpAsync(
        HttpDataTransport transport,
        string uri,
        bool succeeded,
        DataErrorKind? errorKind,
        CancellationToken cancellationToken,
        bool authorize = false)
    {
        var request = new HttpDataRequest<string>(
            "dogfood-network",
            "network-probe",
            "network",
            _ =>
            {
                var message = new HttpRequestMessage(HttpMethod.Get, uri);
                if (authorize)
                {
                    message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer",
                        "stress/admin/r10");
                }

                return message;
            },
            static async (response, token) =>
                await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        Require(result.Succeeded == succeeded,
            $"HTTP network probe expected success={succeeded}; status={result.Status}, error={result.Error?.Kind}, " +
            $"message={result.Error?.Message}, exception={result.Error?.Exception}.");
        if (errorKind is not null)
        {
            Require(result.Error?.Kind == errorKind,
                $"HTTP network probe expected {errorKind}; observed {result.Error?.Kind}.");
        }
    }

    private void Record(string id, string transport, string outcome, bool expectedFailure = false)
    {
        _results.Add(new DogfoodNetworkCaseResult(id, transport, outcome, true));
        _ledger.Record("network-chaos", id, expectedFailure ? "expected-failure" : "completed");
    }

    private static async Task WaitForAsync(
        Func<bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate())
            {
                return;
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The network lab condition did not complete before its deadline.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SingleHttpClientFactory(string name, HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string requestedName) =>
            string.Equals(name, requestedName, StringComparison.Ordinal)
                ? client
                : throw new InvalidOperationException($"Unexpected HTTP client '{requestedName}'.");
    }

    private sealed class DogfoodLogicalEndpointResolver(IPEndPoint endpoint)
    {
        public IPEndPoint? Current { get; set; } = endpoint;
    }
}

internal sealed record DogfoodNetworkReport(
    int SchemaVersion,
    int CaseCount,
    IReadOnlyList<DogfoodNetworkCaseResult> Cases);

internal sealed record DogfoodNetworkCaseResult(
    string Id,
    string Transport,
    string Outcome,
    bool Passed);

internal sealed class DogfoodTlsEndpoint : IAsyncDisposable
{
    private readonly WebApplication _application;

    private DogfoodTlsEndpoint(
        WebApplication application,
        X509Certificate2 certificate,
        IPEndPoint endpoint,
        IPEndPoint plainTextEndPoint)
    {
        _application = application;
        Certificate = certificate;
        EndPoint = endpoint;
        PlainTextEndPoint = plainTextEndPoint;
    }

    public X509Certificate2 Certificate { get; }

    public IPEndPoint EndPoint { get; }

    public IPEndPoint PlainTextEndPoint { get; }

    public static async Task<DogfoodTlsEndpoint> StartAsync(CancellationToken cancellationToken)
    {
        var port = ReservePort();
        var plainTextPort = ReservePort();
        var certificate = CreateCertificate();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port, listen =>
            {
                listen.Protocols = HttpProtocols.Http1;
                listen.UseHttps(certificate);
            });
            options.Listen(IPAddress.Loopback, plainTextPort, listen =>
                listen.Protocols = HttpProtocols.Http1);
        });
        var application = builder.Build();
        application.MapGet("/health", () => Results.Text("tls-ready"));
        await application.StartAsync(cancellationToken).ConfigureAwait(false);
        return new DogfoodTlsEndpoint(
            application,
            certificate,
            new IPEndPoint(IPAddress.Loopback, port),
            new IPEndPoint(IPAddress.Loopback, plainTextPort));
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync().ConfigureAwait(false);
        await _application.DisposeAsync().ConfigureAwait(false);
        Certificate.Dispose();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=dogfood.test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("dogfood.test");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") },
            false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(4));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx),
            password: null,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
