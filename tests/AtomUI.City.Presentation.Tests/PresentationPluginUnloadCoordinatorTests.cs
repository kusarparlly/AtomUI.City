using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Presentation;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class PresentationPluginUnloadCoordinatorTests
{
    [Fact]
    public void UnloadResultErrorsRejectExternalListMutation()
    {
        var result = new PresentationPluginUnloadResult(
            "com.company.sales",
            contributionId: null,
            closedViewCount: 0,
            revokedInteractionHandlerCount: 0,
            revokedViewDescriptorCount: 0,
            revokedResourceContributionCount: 0,
            resourceDictionariesRevoked: false,
            [
                new PresentationPluginUnloadError(
                    PresentationPluginUnloadErrorKind.ActiveViewsRemaining,
                    "Active views remain"),
            ]);
        var errors = Assert.IsAssignableFrom<IList<PresentationPluginUnloadError>>(result.Errors);

        Assert.Throws<NotSupportedException>(() => errors[0] = new PresentationPluginUnloadError(
            PresentationPluginUnloadErrorKind.ResourceDictionaryRevokeFailed,
            "Changed"));
        Assert.Equal(PresentationPluginUnloadErrorKind.ActiveViewsRemaining, result.Errors[0].Kind);
    }

    [Fact]
    public void ServiceCollectionRegistersPresentationPluginUnloadCoordinator()
    {
        var services = new ServiceCollection();
        var activeViews = new RecordingActivePluginViewRegistry([]);
        var interactions = new RecordingInteractionHandlerRegistry();
        var viewRegistry = new RecordingViewRegistry();
        var resources = new RecordingResourceRegistry();
        var resourceDictionaries = new RecordingResourceDictionaryRevoker();

        services.AddSingleton<IActivePluginViewRegistry>(activeViews);
        services.AddSingleton<IInteractionHandlerRegistry>(interactions);
        services.AddSingleton<IViewRegistry>(viewRegistry);
        services.AddSingleton<IPresentationResourceRegistry>(resources);
        services.AddSingleton<IPresentationResourceDictionaryRevoker>(resourceDictionaries);
        services.AddPresentationPluginUnloadCoordinator();

        using var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IPresentationPluginUnloadCoordinator>();

        Assert.Same(
            provider.GetRequiredService<PresentationPluginUnloadCoordinator>(),
            coordinator);
    }

    [Fact]
    public async Task CleanupPluginClosesViewsBeforeRevokingPresentationContributions()
    {
        var callOrder = new List<string>();
        var diagnostics = new InMemoryHostDiagnostics();
        var activeViews = new RecordingActivePluginViewRegistry(callOrder)
        {
            ClosePluginViewsCount = 2,
        };
        var interactions = new RecordingInteractionHandlerRegistry(callOrder)
        {
            RevokePluginCount = 3,
        };
        var viewRegistry = new RecordingViewRegistry(callOrder)
        {
            RevokePluginCount = 4,
        };
        var resources = new RecordingResourceRegistry(callOrder)
        {
            RevokePluginCount = 5,
        };
        var resourceDictionaries = new RecordingResourceDictionaryRevoker(callOrder);
        var coordinator = new PresentationPluginUnloadCoordinator(
            activeViews,
            interactions,
            viewRegistry,
            resources,
            resourceDictionaries,
            diagnostics);

        var result = await coordinator.CleanupAsync(
            new PresentationPluginUnloadRequest("com.company.sales"));

        Assert.True(result.Succeeded);
        Assert.Equal("com.company.sales", result.PluginId);
        Assert.Null(result.ContributionId);
        Assert.Equal(2, result.ClosedViewCount);
        Assert.Equal(3, result.RevokedInteractionHandlerCount);
        Assert.Equal(4, result.RevokedViewDescriptorCount);
        Assert.Equal(5, result.RevokedResourceContributionCount);
        Assert.True(result.ResourceDictionariesRevoked);
        Assert.Equal(
            [
                "close-plugin:com.company.sales",
                "revoke-interactions-plugin:com.company.sales",
                "revoke-views-plugin:com.company.sales",
                "revoke-dictionaries:com.company.sales:<all>",
                "revoke-resources-plugin:com.company.sales",
            ],
            callOrder);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.PluginUnloadCleanupCompleted &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains("com.company.sales", StringComparison.Ordinal) &&
                record.Context["pluginId"] == "com.company.sales" &&
                record.Context["contributionId"] == "<all>" &&
                record.Context["closedViewCount"] == "2" &&
                record.Context["revokedInteractionHandlerCount"] == "3" &&
                record.Context["revokedViewDescriptorCount"] == "4" &&
                record.Context["revokedResourceContributionCount"] == "5" &&
                record.Context["resourceDictionariesRevoked"] == bool.TrueString);
    }

    [Fact]
    public async Task CleanupContributionUsesContributionSpecificRevocation()
    {
        var callOrder = new List<string>();
        var activeViews = new RecordingActivePluginViewRegistry(callOrder)
        {
            CloseContributionViewsCount = 1,
        };
        var interactions = new RecordingInteractionHandlerRegistry(callOrder)
        {
            RevokeContributionCount = 2,
        };
        var viewRegistry = new RecordingViewRegistry(callOrder)
        {
            RevokeContributionCount = 3,
        };
        var resources = new RecordingResourceRegistry(callOrder)
        {
            RevokeContributionCount = 4,
        };
        var resourceDictionaries = new RecordingResourceDictionaryRevoker(callOrder);
        var coordinator = new PresentationPluginUnloadCoordinator(
            activeViews,
            interactions,
            viewRegistry,
            resources,
            resourceDictionaries);

        var result = await coordinator.CleanupAsync(
            new PresentationPluginUnloadRequest(
                "com.company.sales",
                contributionId: "sales.settings"));

        Assert.True(result.Succeeded);
        Assert.Equal("sales.settings", result.ContributionId);
        Assert.Equal(1, result.ClosedViewCount);
        Assert.Equal(2, result.RevokedInteractionHandlerCount);
        Assert.Equal(3, result.RevokedViewDescriptorCount);
        Assert.Equal(4, result.RevokedResourceContributionCount);
        Assert.Equal(
            [
                "close-contribution:sales.settings",
                "revoke-interactions-contribution:sales.settings",
                "revoke-views-contribution:sales.settings",
                "revoke-dictionaries:com.company.sales:sales.settings",
                "revoke-resources-contribution:sales.settings",
            ],
            callOrder);
    }

    [Fact]
    public async Task CleanupStopsWhenActivePluginViewsRemain()
    {
        var callOrder = new List<string>();
        var diagnostics = new InMemoryHostDiagnostics();
        var activeViews = new RecordingActivePluginViewRegistry(
            callOrder,
            [new ActivePluginView("com.company.sales", new StubOutlet("primary"), BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel()))])
        {
            ClosePluginViewsCount = 0,
        };
        var interactions = new RecordingInteractionHandlerRegistry(callOrder);
        var viewRegistry = new RecordingViewRegistry(callOrder);
        var resources = new RecordingResourceRegistry(callOrder);
        var resourceDictionaries = new RecordingResourceDictionaryRevoker(callOrder);
        var coordinator = new PresentationPluginUnloadCoordinator(
            activeViews,
            interactions,
            viewRegistry,
            resources,
            resourceDictionaries,
            diagnostics);

        var result = await coordinator.CleanupAsync(
            new PresentationPluginUnloadRequest("com.company.sales"));

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.ClosedViewCount);
        Assert.Single(result.Errors);
        Assert.Equal(PresentationPluginUnloadErrorKind.ActiveViewsRemaining, result.Errors[0].Kind);
        Assert.Equal(["close-plugin:com.company.sales"], callOrder);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.PluginUnloadCleanupFailed &&
                record.Severity == HostDiagnosticSeverity.Error &&
                record.Message.Contains("active plugin view", StringComparison.OrdinalIgnoreCase) &&
                record.Context["pluginId"] == "com.company.sales" &&
                record.Context["errorKinds"] == nameof(PresentationPluginUnloadErrorKind.ActiveViewsRemaining));
    }

    [Fact]
    public async Task CleanupPluginCanBeRepeatedAfterContributionsAreRevoked()
    {
        var dispatcher = new InlineDispatcher();
        var activeViews = new ActivePluginViewRegistry();
        var interactions = new InteractionHandlerRegistry(dispatcher);
        var viewRegistry = new ViewRegistry();
        var resources = new PresentationResourceRegistry();
        var resourceDictionaries = new RecordingResourceDictionaryRevoker();
        var outlet = new RouteOutlet("primary", dispatcher);
        var handle = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());
        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", handle));
        activeViews.Track(new ActivePluginView(
            "com.company.sales",
            outlet,
            handle,
            contributionId: "sales.settings"));
        interactions.Register<ConfirmRequest, bool>(
            (_, _) => ValueTask.FromResult(true),
            new InteractionHandlerRegistrationOptions
            {
                PluginId = "com.company.sales",
                ContributionId = "sales.settings",
            });
        viewRegistry.Register(new ViewDescriptor(
            typeof(SettingsViewModel),
            typeof(SettingsView),
            viewKey: null,
            _ => new SettingsView(),
            pluginId: "com.company.sales",
            contributionId: "sales.settings"));
        resources.Register(new PresentationResourceContribution(
            "style",
            new DisposableResource(),
            pluginId: "com.company.sales",
            contributionId: "sales.settings"));
        var coordinator = new PresentationPluginUnloadCoordinator(
            activeViews,
            interactions,
            viewRegistry,
            resources,
            resourceDictionaries);

        var first = await coordinator.CleanupAsync(
            new PresentationPluginUnloadRequest("com.company.sales"));
        var second = await coordinator.CleanupAsync(
            new PresentationPluginUnloadRequest("com.company.sales"));

        Assert.True(first.Succeeded);
        Assert.Equal(1, first.ClosedViewCount);
        Assert.Equal(1, first.RevokedInteractionHandlerCount);
        Assert.Equal(1, first.RevokedViewDescriptorCount);
        Assert.Equal(1, first.RevokedResourceContributionCount);
        Assert.True(second.Succeeded);
        Assert.Equal(0, second.ClosedViewCount);
        Assert.Equal(0, second.RevokedInteractionHandlerCount);
        Assert.Equal(0, second.RevokedViewDescriptorCount);
        Assert.Equal(0, second.RevokedResourceContributionCount);
        Assert.Equal(2, resourceDictionaries.RevokeCount);
    }

    [Fact]
    public async Task CleanupRecordsResourceDictionaryFailureAndContinuesResourceRevocation()
    {
        var callOrder = new List<string>();
        var activeViews = new RecordingActivePluginViewRegistry(callOrder);
        var interactions = new RecordingInteractionHandlerRegistry(callOrder)
        {
            RevokePluginCount = 1,
        };
        var viewRegistry = new RecordingViewRegistry(callOrder)
        {
            RevokePluginCount = 2,
        };
        var resources = new RecordingResourceRegistry(callOrder)
        {
            RevokePluginCount = 3,
        };
        var resourceDictionaries = new RecordingResourceDictionaryRevoker(callOrder)
        {
            Failure = new InvalidOperationException("resource dictionary revoke failed"),
        };
        var coordinator = new PresentationPluginUnloadCoordinator(
            activeViews,
            interactions,
            viewRegistry,
            resources,
            resourceDictionaries);

        var result = await coordinator.CleanupAsync(
            new PresentationPluginUnloadRequest("com.company.sales"));

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.RevokedInteractionHandlerCount);
        Assert.Equal(2, result.RevokedViewDescriptorCount);
        Assert.Equal(3, result.RevokedResourceContributionCount);
        Assert.False(result.ResourceDictionariesRevoked);
        Assert.Single(result.Errors);
        Assert.Equal(PresentationPluginUnloadErrorKind.ResourceDictionaryRevokeFailed, result.Errors[0].Kind);
        Assert.Equal(
            [
                "close-plugin:com.company.sales",
                "revoke-interactions-plugin:com.company.sales",
                "revoke-views-plugin:com.company.sales",
                "revoke-dictionaries:com.company.sales:<all>",
                "revoke-resources-plugin:com.company.sales",
            ],
            callOrder);
    }

    [Fact]
    public async Task CleanupRecordsViewDescriptorFailureAndContinuesResourceRevocation()
    {
        var callOrder = new List<string>();
        var activeViews = new RecordingActivePluginViewRegistry(callOrder);
        var interactions = new RecordingInteractionHandlerRegistry(callOrder)
        {
            RevokePluginCount = 1,
        };
        var viewRegistry = new RecordingViewRegistry(callOrder)
        {
            Failure = new InvalidOperationException("view revoke failed"),
        };
        var resources = new RecordingResourceRegistry(callOrder)
        {
            RevokePluginCount = 2,
        };
        var resourceDictionaries = new RecordingResourceDictionaryRevoker(callOrder);
        var coordinator = new PresentationPluginUnloadCoordinator(
            activeViews,
            interactions,
            viewRegistry,
            resources,
            resourceDictionaries);

        var result = await coordinator.CleanupAsync(
            new PresentationPluginUnloadRequest("com.company.sales"));

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.RevokedInteractionHandlerCount);
        Assert.Equal(0, result.RevokedViewDescriptorCount);
        Assert.Equal(2, result.RevokedResourceContributionCount);
        Assert.True(result.ResourceDictionariesRevoked);
        Assert.Single(result.Errors);
        Assert.Equal(PresentationPluginUnloadErrorKind.ViewDescriptorRevokeFailed, result.Errors[0].Kind);
        Assert.Equal(
            [
                "close-plugin:com.company.sales",
                "revoke-interactions-plugin:com.company.sales",
                "revoke-views-plugin:com.company.sales",
                "revoke-dictionaries:com.company.sales:<all>",
                "revoke-resources-plugin:com.company.sales",
            ],
            callOrder);
    }

    private sealed class RecordingActivePluginViewRegistry : IActivePluginViewRegistry
    {
        private readonly List<string> _callOrder;

        public RecordingActivePluginViewRegistry(IReadOnlyList<ActivePluginView> activeViews)
            : this([], activeViews)
        {
        }

        public RecordingActivePluginViewRegistry(
            List<string> callOrder,
            IReadOnlyList<ActivePluginView>? activeViews = null)
        {
            _callOrder = callOrder;
            ActiveViews = activeViews?.ToArray() ?? [];
        }

        public IReadOnlyList<ActivePluginView> ActiveViews { get; private set; }

        public int ClosePluginViewsCount { get; init; }

        public int CloseContributionViewsCount { get; init; }

        public IActivePluginViewLease Track(ActivePluginView view)
        {
            throw new NotSupportedException();
        }

        public ValueTask<int> ClosePluginViewsAsync(
            string pluginId,
            CancellationToken cancellationToken = default)
        {
            _callOrder.Add($"close-plugin:{pluginId}");
            if (ClosePluginViewsCount > 0)
            {
                ActiveViews = ActiveViews
                    .Where(view => !string.Equals(view.PluginId, pluginId, StringComparison.Ordinal))
                    .ToArray();
            }

            return ValueTask.FromResult(ClosePluginViewsCount);
        }

        public ValueTask<int> CloseContributionViewsAsync(
            string contributionId,
            CancellationToken cancellationToken = default)
        {
            _callOrder.Add($"close-contribution:{contributionId}");
            if (CloseContributionViewsCount > 0)
            {
                ActiveViews = ActiveViews
                    .Where(view => !string.Equals(view.ContributionId, contributionId, StringComparison.Ordinal))
                    .ToArray();
            }

            return ValueTask.FromResult(CloseContributionViewsCount);
        }
    }

    private sealed class RecordingInteractionHandlerRegistry(List<string>? callOrder = null) : IInteractionHandlerRegistry
    {
        private readonly List<string> _callOrder = callOrder ?? [];

        public int RevokePluginCount { get; init; }

        public int RevokeContributionCount { get; init; }

        public IDisposable Register<TRequest, TResult>(
            Func<AtomUI.City.Mvvm.InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
            AtomUI.City.Mvvm.IActivationScope? activationScope = null)
        {
            throw new NotSupportedException();
        }

        public IDisposable Register<TRequest, TResult>(
            Func<AtomUI.City.Mvvm.InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
            InteractionHandlerRegistrationOptions options)
        {
            throw new NotSupportedException();
        }

        public ValueTask<AtomUI.City.Mvvm.InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
            TRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<AtomUI.City.Mvvm.InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
            TRequest request,
            InteractionDispatchContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public int RevokePlugin(string pluginId)
        {
            _callOrder.Add($"revoke-interactions-plugin:{pluginId}");

            return RevokePluginCount;
        }

        public int RevokeContribution(string contributionId)
        {
            _callOrder.Add($"revoke-interactions-contribution:{contributionId}");

            return RevokeContributionCount;
        }
    }

    private sealed class RecordingViewRegistry(List<string>? callOrder = null) : IViewRegistry
    {
        private readonly List<string> _callOrder = callOrder ?? [];

        public int RevokePluginCount { get; init; }

        public int RevokeContributionCount { get; init; }

        public Exception? Failure { get; init; }

        public void Register(ViewDescriptor descriptor)
        {
            throw new NotSupportedException();
        }

        public bool TryLocate(Type viewModelType, out ViewDescriptor? descriptor)
        {
            throw new NotSupportedException();
        }

        public bool TryLocate(
            Type viewModelType,
            string? viewKey,
            out ViewDescriptor? descriptor)
        {
            throw new NotSupportedException();
        }

        public ViewDescriptor Locate(Type viewModelType, string? viewKey = null)
        {
            throw new NotSupportedException();
        }

        public int RevokePlugin(string pluginId)
        {
            _callOrder.Add($"revoke-views-plugin:{pluginId}");

            if (Failure is not null)
            {
                throw Failure;
            }

            return RevokePluginCount;
        }

        public int RevokeContribution(string contributionId)
        {
            _callOrder.Add($"revoke-views-contribution:{contributionId}");

            if (Failure is not null)
            {
                throw Failure;
            }

            return RevokeContributionCount;
        }
    }

    private sealed class RecordingResourceRegistry(List<string>? callOrder = null) : IPresentationResourceRegistry
    {
        private readonly List<string> _callOrder = callOrder ?? [];

        public IReadOnlyList<PresentationResourceContribution> Contributions => [];

        public int RevokePluginCount { get; init; }

        public int RevokeContributionCount { get; init; }

        public IPresentationResourceLease Register(PresentationResourceContribution contribution)
        {
            throw new NotSupportedException();
        }

        public int RevokePlugin(string pluginId)
        {
            _callOrder.Add($"revoke-resources-plugin:{pluginId}");

            return RevokePluginCount;
        }

        public int RevokeContribution(string contributionId)
        {
            _callOrder.Add($"revoke-resources-contribution:{contributionId}");

            return RevokeContributionCount;
        }
    }

    private sealed class RecordingResourceDictionaryRevoker(List<string>? callOrder = null) : IPresentationResourceDictionaryRevoker
    {
        private readonly List<string> _callOrder = callOrder ?? [];

        public Exception? Failure { get; init; }

        public int RevokeCount { get; private set; }

        public ValueTask<PresentationResourceDictionaryRevokeResult> RevokeAsync(
            PresentationResourceDictionaryRevocation revocation,
            CancellationToken cancellationToken = default)
        {
            RevokeCount++;
            _callOrder.Add(
                $"revoke-dictionaries:{revocation.PluginId}:{revocation.ContributionId ?? "<all>"}");

            return ValueTask.FromResult(
                Failure is null
                    ? PresentationResourceDictionaryRevokeResult.Success()
                    : new PresentationResourceDictionaryRevokeResult([Failure]));
        }
    }

    private sealed class StubOutlet(string name) : IRouteOutlet
    {
        public string Name { get; } = name;

        public object? CurrentContent => null;

        public PresentationEntry? CurrentEntry => null;

        public RouteOutletState State => RouteOutletState.Empty;

        public ValueTask<RouteOutletCommitResult> CommitAsync(
            RouteOutletCommitPlan plan,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(RouteOutletCommitResult.Success());
        }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

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

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return callback(cancellationToken);
        }
    }

    private sealed record ConfirmRequest(string Message);

    private sealed class DisposableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class SettingsViewModel;

    private sealed class SettingsView;
}
