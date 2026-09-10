using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationResourceDictionaryRevokerTests
{
    [Fact]
    public async Task ServiceCollectionRegistersGenericResourceDictionaryRevoker()
    {
        var services = new ServiceCollection();
        var dispatcher = new RecordingDispatcher();
        services.AddSingleton<IUiDispatcher>(dispatcher);
        services.AddSingleton<IPresentationResourceDictionaryTarget, RecordingTarget>();
        services.AddPresentation();

        await using var provider = services.BuildServiceProvider();
        var revoker = provider.GetRequiredService<IPresentationResourceDictionaryRevoker>();

        var result = await revoker.RevokeAsync(
            new PresentationResourceDictionaryRevocation(
                "plugin.orders",
                "plugin.orders.resources"));

        Assert.True(result.Succeeded);
        var target = provider.GetRequiredService<IEnumerable<IPresentationResourceDictionaryTarget>>()
            .OfType<RecordingTarget>()
            .Single();
        Assert.Equal(["plugin.orders"], target.PluginIds);
        Assert.Equal(["plugin.orders.resources"], target.ContributionIds);
        Assert.Equal([true], target.DispatcherAccess);
    }

    [Fact]
    public async Task RevokerContinuesAfterTargetFailureAndReportsAllErrors()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new RecordingDispatcher();
        var firstFailure = new InvalidOperationException("first revoke failed");
        var secondFailure = new ApplicationException("second revoke failed");
        var first = new RecordingTarget(dispatcher) { Failure = firstFailure };
        var next = new RecordingTarget(dispatcher);
        var last = new RecordingTarget(dispatcher) { Failure = secondFailure };
        var revoker = new PresentationResourceDictionaryRevoker(
            dispatcher,
            [first, next, last],
            diagnostics);

        var result = await revoker.RevokeAsync(
            new PresentationResourceDictionaryRevocation("plugin.orders"));

        Assert.False(result.Succeeded);
        Assert.Equal([firstFailure, secondFailure], result.Errors);
        Assert.Equal(["plugin.orders"], first.PluginIds);
        Assert.Equal(["plugin.orders"], next.PluginIds);
        Assert.Equal(["plugin.orders"], last.PluginIds);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.ResourceDictionaryRevokeFailed &&
                record.Severity == HostDiagnosticSeverity.Error &&
                record.Context["pluginId"] == "plugin.orders" &&
                record.Context["targetCount"] == "3" &&
                record.Context["failureCount"] == "2");
    }

    [Fact]
    public async Task SuccessfulRevokeWritesStructuredDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new RecordingDispatcher();
        var revoker = new PresentationResourceDictionaryRevoker(
            dispatcher,
            [new RecordingTarget(dispatcher)],
            diagnostics);

        var result = await revoker.RevokeAsync(
            new PresentationResourceDictionaryRevocation(
                "plugin.orders",
                "plugin.orders.resources"));

        Assert.True(result.Succeeded);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.ResourceDictionaryRevoked &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Context["pluginId"] == "plugin.orders" &&
                record.Context["contributionId"] == "plugin.orders.resources" &&
                record.Context["targetCount"] == "1" &&
                record.Context["failureCount"] == "0");
    }

    private sealed class RecordingTarget : IPresentationResourceDictionaryTarget
    {
        private readonly RecordingDispatcher? _dispatcher;

        public RecordingTarget()
        {
        }

        public RecordingTarget(RecordingDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public Exception? Failure { get; init; }

        public List<string> PluginIds { get; } = [];

        public List<string?> ContributionIds { get; } = [];

        public List<bool> DispatcherAccess { get; } = [];

        public ValueTask RevokeResourcesAsync(
            PresentationResourceDictionaryRevocation revocation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PluginIds.Add(revocation.PluginId);
            ContributionIds.Add(revocation.ContributionId);
            DispatcherAccess.Add(_dispatcher?.CheckAccess() ?? true);

            return Failure is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(Failure);
        }
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        private readonly AsyncLocal<bool> _isExecuting = new();

        public bool CheckAccess() => _isExecuting.Value;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(callback());
        }

        public async ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _isExecuting.Value = true;
            try
            {
                await callback(cancellationToken);
            }
            finally
            {
                _isExecuting.Value = false;
            }
        }
    }
}
