namespace AtomUI.City.Data;

/// <summary>
/// Represents data client registry.
/// </summary>
public sealed class DataClientRegistry : IDataClientFactory
{
    private readonly Dictionary<Type, ClientRegistration> _clients = [];
    private readonly IDataDiagnostics? _diagnostics;
    private readonly object _syncRoot = new();

    /// <summary>
    /// Initializes a new instance of the <c>DataClientRegistry</c> type.
    /// </summary>
    public DataClientRegistry(IDataDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the register&lt;tclient&gt; operation.
    /// </summary>
    public void Register<TClient>(TClient client)
        where TClient : class, IDataClient
    {
        _ = RegisterCore(client);
    }

    /// <summary>
    /// Executes the register owned&lt;tclient&gt; operation.
    /// </summary>
    public IDisposable RegisterOwned<TClient>(TClient client)
        where TClient : class, IDataClient
    {
        return RegisterCore(client);
    }

    private IDisposable RegisterCore<TClient>(TClient client)
        where TClient : class, IDataClient
    {
        ArgumentNullException.ThrowIfNull(client);
        var clientId = client.ClientId;
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var token = new object();
        lock (_syncRoot)
        {
            _clients[typeof(TClient)] = new ClientRegistration(client, clientId, token);
        }

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.ClientRegistered,
            $"Data client '{typeof(TClient).FullName}' registered.",
            DataDiagnosticSeverity.Info,
            ClientId: clientId));

        return new ClientLease(this, typeof(TClient), token, clientId);
    }

    /// <summary>
    /// Executes the unregister&lt;tclient&gt; operation.
    /// </summary>
    public bool Unregister<TClient>()
        where TClient : class, IDataClient
    {
        ClientRegistration? removedClient = null;
        lock (_syncRoot)
        {
            if (_clients.Remove(typeof(TClient), out var client))
            {
                removedClient = client;
            }
        }

        if (removedClient is null)
        {
            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.ClientUnregistrationMissing,
                $"Data client '{typeof(TClient).FullName}' could not be unregistered because it is not registered.",
                DataDiagnosticSeverity.Warning));

            return false;
        }

        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.ClientUnregistered,
            $"Data client '{typeof(TClient).FullName}' unregistered.",
            DataDiagnosticSeverity.Info,
            ClientId: removedClient.ClientId));

        return true;
    }

    /// <summary>
    /// Executes the get required client&lt;tclient&gt; operation.
    /// </summary>
    public TClient GetRequiredClient<TClient>()
        where TClient : class, IDataClient
    {
        lock (_syncRoot)
        {
            if (_clients.TryGetValue(typeof(TClient), out var client))
            {
                return (TClient)client.Client;
            }
        }

        var clientTypeName = typeof(TClient).FullName;
        DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.ClientMissing,
            $"Data client '{clientTypeName}' is not registered.",
            DataDiagnosticSeverity.Warning));

        throw new KeyNotFoundException($"Data client '{clientTypeName}' is not registered.");
    }

    private void RevokeOwned(Type clientType, object token, string clientId)
    {
        var removed = false;
        lock (_syncRoot)
        {
            if (_clients.TryGetValue(clientType, out var registration)
                && ReferenceEquals(registration.Token, token))
            {
                _clients.Remove(clientType);
                removed = true;
            }
        }

        if (removed)
        {
            DataDiagnosticWriter.TryWrite(_diagnostics, new DataDiagnosticRecord(
                DataDiagnosticIds.ClientUnregistered,
                $"Data client '{clientType.FullName}' unregistered by its owner.",
                DataDiagnosticSeverity.Info,
                ClientId: clientId));
        }
    }

    private sealed record ClientRegistration(IDataClient Client, string ClientId, object Token);

    private sealed class ClientLease(
        DataClientRegistry registry,
        Type clientType,
        object token,
        string clientId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry.RevokeOwned(clientType, token, clientId);
            }
        }
    }
}
