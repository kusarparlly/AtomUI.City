using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.Data;

internal sealed class DataClientGenerationMetadata
{
    public DataClientGenerationMetadata(
        string typeName,
        string clientId,
        int transportKind,
        string version,
        IReadOnlyList<DataOperationGenerationMetadata> operations,
        IReadOnlyList<string> issues,
        Location? location)
    {
        TypeName = typeName;
        ClientId = clientId;
        TransportKind = transportKind;
        Version = version;
        Operations = operations;
        Issues = issues;
        Location = location;
    }

    public string TypeName { get; }
    public string ClientId { get; }
    public int TransportKind { get; }
    public string Version { get; }
    public IReadOnlyList<DataOperationGenerationMetadata> Operations { get; }
    public IReadOnlyList<string> Issues { get; }
    public Location? Location { get; }
}

internal sealed class DataOperationGenerationMetadata
{
    public DataOperationGenerationMetadata(
        string operationName,
        string requestTypeName,
        string responseTypeName,
        int accessMode,
        int concurrencyPolicy,
        int timeoutMilliseconds,
        int maxRetryAttempts,
        bool cacheEnabled,
        string authenticationPolicy)
    {
        OperationName = operationName;
        RequestTypeName = requestTypeName;
        ResponseTypeName = responseTypeName;
        AccessMode = accessMode;
        ConcurrencyPolicy = concurrencyPolicy;
        TimeoutMilliseconds = timeoutMilliseconds;
        MaxRetryAttempts = maxRetryAttempts;
        CacheEnabled = cacheEnabled;
        AuthenticationPolicy = authenticationPolicy;
    }

    public string OperationName { get; }
    public string RequestTypeName { get; }
    public string ResponseTypeName { get; }
    public int AccessMode { get; }
    public int ConcurrencyPolicy { get; }
    public int TimeoutMilliseconds { get; }
    public int MaxRetryAttempts { get; }
    public bool CacheEnabled { get; }
    public string AuthenticationPolicy { get; }
}
