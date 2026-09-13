using AtomUI.City.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services.TryAddSingleton<UnavailableAccessTokenProvider>();
        services.TryAddSingleton<IDataDiagnostics, InMemoryDataDiagnostics>();
        services.TryAddSingleton<IDataCredentialProvider>(serviceProvider =>
            new AccessTokenCredentialProvider(
                serviceProvider.GetService<IAccessTokenProvider>()
                ?? serviceProvider.GetRequiredService<UnavailableAccessTokenProvider>()));
        services.TryAddSingleton<InMemoryDataRequestCache>();
        services.TryAddSingleton<IDataRequestCache>(serviceProvider =>
            serviceProvider.GetRequiredService<InMemoryDataRequestCache>());
        services.TryAddSingleton<IDataCacheInvalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<IDataRequestCache>() as IDataCacheInvalidator
            ?? new NoDataCacheInvalidator(serviceProvider.GetService<IDataDiagnostics>()));
        services.TryAddSingleton<IDataOperationScheduler, DataOperationScheduler>();
        services.TryAddSingleton<IDataResiliencePolicyProvider, DefaultDataResiliencePolicyProvider>();
        services.TryAddSingleton<IDataFallbackProvider, NoDataFallbackProvider>();
        services.TryAddSingleton<DataRuntimeGate>();
        services.TryAddTransient<DataLargePayloadClient>(serviceProvider => new DataLargePayloadClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DataLargePayloadClient)),
            serviceProvider.GetService<IDataDiagnostics>()));
        services.TryAddSingleton<DataConnectionManager>();
        services.TryAddSingleton<DataClientRegistry>();
        services.TryAddSingleton<DataClientDescriptorCatalog>();
        services.TryAddSingleton<DataContributionRegistry>();
        services.TryAddSingleton<IDataCapabilityAuthorizer>(serviceProvider =>
            serviceProvider.GetRequiredService<DataContributionRegistry>());
        services.TryAddSingleton<IDataRequestHandlerSource>(serviceProvider =>
            serviceProvider.GetRequiredService<DataContributionRegistry>());
        services.TryAddSingleton<IDataClientFactory>(
            serviceProvider => serviceProvider.GetRequiredService<DataClientRegistry>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestResponseTransport, HttpDataTransport>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestResponseTransport, GrpcDataTransport>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestResponseTransport, SignalRDataTransport>());
        services.TryAddSingleton<IDataRequestPipeline>(serviceProvider => new DataRequestPipeline(
            serviceProvider.GetServices<IRequestResponseTransport>(),
            serviceProvider.GetService<IDataCredentialProvider>(),
            serviceProvider.GetService<IDataDiagnostics>(),
            serviceProvider.GetService<IDataRequestCache>(),
            serviceProvider.GetService<IDataOperationScheduler>(),
            serviceProvider.GetService<IDataResiliencePolicyProvider>(),
            serviceProvider.GetService<IDataFallbackProvider>(),
            serviceProvider.GetServices<IDataRequestHandler>(),
            serviceProvider.GetService<IDataRequestHandlerSource>(),
            serviceProvider.GetService<IDataCapabilityAuthorizer>(),
            serviceProvider.GetRequiredService<DataRuntimeGate>()));

        return services;
    }
}

internal sealed class NoDataCacheInvalidator(IDataDiagnostics? diagnostics) : IDataCacheInvalidator
{
    public ValueTask<DataCacheInvalidationResult> InvalidateAsync(
        DataCacheInvalidation invalidation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        cancellationToken.ThrowIfCancellationRequested();
        DataDiagnosticWriter.TryWrite(diagnostics, new DataDiagnosticRecord(
            DataDiagnosticIds.CacheInvalidationUnsupported,
            "The configured data request cache does not support bulk invalidation.",
            DataDiagnosticSeverity.Warning,
            ErrorKind: DataErrorKind.PolicyRejected));
        return ValueTask.FromResult(new DataCacheInvalidationResult(0));
    }
}
