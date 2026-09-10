using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class ViewLocatorTests
{
    [Fact]
    public void ServiceCollectionRegistersViewRegistry()
    {
        var services = new ServiceCollection();

        services.AddViewRegistry();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IViewRegistry>();

        Assert.Same(provider.GetRequiredService<ViewRegistry>(), registry);
        Assert.Same(provider.GetRequiredService<IViewLocator>(), registry);
    }

    [Fact]
    public void RegistryLocatesDefaultViewForViewModel()
    {
        var registry = new ViewRegistry();
        var descriptor = new ViewDescriptor(
            typeof(SettingsViewModel),
            typeof(SettingsView),
            viewKey: null,
            _ => new SettingsView());

        registry.Register(descriptor);

        Assert.True(registry.TryLocate(typeof(SettingsViewModel), out var located));
        Assert.Same(descriptor, located);
        Assert.Same(descriptor, registry.Locate(typeof(SettingsViewModel)));
    }

    [Fact]
    public void RegistrySupportsNamedViewsForSameViewModel()
    {
        var registry = new ViewRegistry();
        var defaultDescriptor = new ViewDescriptor(
            typeof(SettingsViewModel),
            typeof(SettingsView),
            viewKey: null,
            _ => new SettingsView());
        var compactDescriptor = new ViewDescriptor(
            typeof(SettingsViewModel),
            typeof(CompactSettingsView),
            "compact",
            _ => new CompactSettingsView());

        registry.Register(defaultDescriptor);
        registry.Register(compactDescriptor);

        Assert.Same(defaultDescriptor, registry.Locate(typeof(SettingsViewModel)));
        Assert.Same(compactDescriptor, registry.Locate(typeof(SettingsViewModel), "compact"));
    }

    [Fact]
    public void RegistryRegistersManifestEntriesAtomically()
    {
        var registry = new ViewRegistry();
        var settingsDescriptor = Descriptor<SettingsViewModel, SettingsView>();
        var profileDescriptor = Descriptor<ProfileViewModel, ProfileView>();

        registry.RegisterManifest([settingsDescriptor, profileDescriptor]);

        Assert.Same(settingsDescriptor, registry.Locate(typeof(SettingsViewModel)));
        Assert.Same(profileDescriptor, registry.Locate(typeof(ProfileViewModel)));
    }

    [Fact]
    public void ManifestRegistrationRejectsDuplicateEntriesWithoutPartialRegistration()
    {
        var registry = new ViewRegistry();
        var settingsDescriptor = Descriptor<SettingsViewModel, SettingsView>();
        var duplicateSettingsDescriptor = Descriptor<SettingsViewModel, AlternativeSettingsView>();
        var profileDescriptor = Descriptor<ProfileViewModel, ProfileView>();

        var exception = Assert.Throws<PresentationException>(
            () => registry.RegisterManifest(
                [
                    settingsDescriptor,
                    duplicateSettingsDescriptor,
                    profileDescriptor,
                ]));

        Assert.Equal(PresentationError.DuplicateView, exception.Error);
        Assert.False(registry.TryLocate(typeof(SettingsViewModel), out _));
        Assert.False(registry.TryLocate(typeof(ProfileViewModel), out _));
    }

    [Fact]
    public void ExplicitOverrideReplacesExistingDescriptor()
    {
        var registry = new ViewRegistry();
        var manifestDescriptor = Descriptor<SettingsViewModel, SettingsView>();
        var explicitDescriptor = Descriptor<SettingsViewModel, AlternativeSettingsView>();

        registry.Register(manifestDescriptor);
        registry.Register(
            explicitDescriptor,
            new ViewRegistrationOptions { ReplaceExisting = true });

        Assert.Same(explicitDescriptor, registry.Locate(typeof(SettingsViewModel)));
    }

    [Fact]
    public void RevokingOverrideRestoresPreviousDescriptorLayer()
    {
        var registry = new ViewRegistry();
        var hostDescriptor = Descriptor<SettingsViewModel, SettingsView>();
        var pluginDescriptor = new ViewDescriptor(
            typeof(SettingsViewModel),
            typeof(AlternativeSettingsView),
            viewKey: null,
            _ => new AlternativeSettingsView(),
            pluginId: "plugin.settings",
            contributionId: "plugin.settings.view");

        registry.Register(hostDescriptor);
        registry.Register(pluginDescriptor, new ViewRegistrationOptions { ReplaceExisting = true });

        Assert.Same(pluginDescriptor, registry.Locate(typeof(SettingsViewModel)));
        Assert.Equal(1, registry.RevokePlugin("plugin.settings"));
        Assert.Same(hostDescriptor, registry.Locate(typeof(SettingsViewModel)));
    }

    [Fact]
    public void RegistryRejectsDuplicateDefaultViews()
    {
        var registry = new ViewRegistry();
        registry.Register(
            new ViewDescriptor(
                typeof(SettingsViewModel),
                typeof(SettingsView),
                viewKey: null,
                _ => new SettingsView()));

        var exception = Assert.Throws<PresentationException>(
            () => registry.Register(
                new ViewDescriptor(
                    typeof(SettingsViewModel),
                    typeof(AlternativeSettingsView),
                    viewKey: null,
                    _ => new AlternativeSettingsView())));

        Assert.Equal(PresentationError.DuplicateView, exception.Error);
    }

    [Fact]
    public void RegistryRejectsDuplicateAfterManifestRegistration()
    {
        var registry = new ViewRegistry();
        registry.RegisterManifest([Descriptor<SettingsViewModel, SettingsView>()]);

        var exception = Assert.Throws<PresentationException>(
            () => registry.Register(Descriptor<SettingsViewModel, AlternativeSettingsView>()));

        Assert.Equal(PresentationError.DuplicateView, exception.Error);
    }

    [Fact]
    public void RegistryReportsMissingView()
    {
        var registry = new ViewRegistry();

        var exception = Assert.Throws<PresentationException>(
            () => registry.Locate(typeof(SettingsViewModel)));

        Assert.Equal(PresentationError.ViewNotFound, exception.Error);
    }

    [Fact]
    public void RegistryRecordsLocateHitDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ViewRegistry(diagnostics);
        var descriptor = new ViewDescriptor(
            typeof(SettingsViewModel),
            typeof(SettingsView),
            viewKey: null,
            _ => new SettingsView());
        registry.Register(descriptor);

        var located = registry.Locate(typeof(SettingsViewModel));

        Assert.Same(descriptor, located);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.ViewLocatorMatched &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains(typeof(SettingsViewModel).FullName!, StringComparison.Ordinal) &&
                record.Message.Contains(typeof(SettingsView).FullName!, StringComparison.Ordinal) &&
                record.Context["viewModelType"] == typeof(SettingsViewModel).FullName &&
                record.Context["viewType"] == typeof(SettingsView).FullName);
    }

    [Fact]
    public void LookupDiagnosticsCarryRouteAndOwnerContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ViewRegistry(diagnostics);
        var descriptor = new ViewDescriptor(
            typeof(SettingsViewModel),
            typeof(SettingsView),
            viewKey: "settings",
            _ => new SettingsView(),
            pluginId: "com.company.sales",
            contributionId: "plugin.settings");
        registry.Register(descriptor);

        var located = registry.Locate(
            new ViewLookupRequest(
                typeof(SettingsViewModel),
                viewKey: "settings",
                routeId: "routes.settings",
                ownerId: "com.company.sales"));

        var record = Assert.Single(
            diagnostics.Records,
            static record => record.Code == PresentationDiagnosticIds.ViewLocatorMatched);

        Assert.Same(descriptor, located);
        Assert.Equal("routes.settings", record.Context["routeId"]);
        Assert.Equal("com.company.sales", record.Context["ownerId"]);
        Assert.Equal("plugin.settings", record.Context["contributionId"]);
    }

    [Fact]
    public void RegistryRecordsLocateFailureDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ViewRegistry(diagnostics);

        Assert.Throws<PresentationException>(() => registry.Locate(typeof(SettingsViewModel)));

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.ViewLocatorFailed &&
                record.Severity == HostDiagnosticSeverity.Warning &&
                record.Message.Contains(typeof(SettingsViewModel).FullName!, StringComparison.Ordinal) &&
                record.Context["viewModelType"] == typeof(SettingsViewModel).FullName);
    }

    [Fact]
    public void LookupUsesExactKeyPathWithoutAssignableFallback()
    {
        var registry = new ViewRegistry();
        registry.Register(Descriptor<BaseViewModel, SettingsView>());

        Assert.False(registry.TryLocate(typeof(DerivedViewModel), out _));
    }

    [Fact]
    public void RegistryAllowsConcurrentLookupReads()
    {
        var registry = new ViewRegistry();
        var descriptor = Descriptor<SettingsViewModel, SettingsView>();
        registry.Register(descriptor);

        Parallel.For(
            0,
            256,
            _ =>
            {
                Assert.True(registry.TryLocate(typeof(SettingsViewModel), out var located));
                Assert.Same(descriptor, located);
            });
    }

    [Fact]
    public void RegistryRevokesViewsByContributionId()
    {
        var registry = new ViewRegistry();
        registry.Register(
            new ViewDescriptor(
                typeof(SettingsViewModel),
                typeof(SettingsView),
                viewKey: null,
                _ => new SettingsView(),
                contributionId: "plugin.settings"));

        Assert.True(registry.TryLocate(typeof(SettingsViewModel), out _));

        registry.RevokeContribution("plugin.settings");

        Assert.False(registry.TryLocate(typeof(SettingsViewModel), out _));
    }

    [Fact]
    public void RegistryRevokesViewsByPluginId()
    {
        var registry = new ViewRegistry();
        registry.Register(
            new ViewDescriptor(
                typeof(SettingsViewModel),
                typeof(SettingsView),
                viewKey: null,
                _ => new SettingsView(),
                pluginId: "com.company.sales",
                contributionId: "plugin.settings"));
        var hostDescriptor = new ViewDescriptor(
            typeof(ProfileViewModel),
            typeof(ProfileView),
            viewKey: null,
            _ => new ProfileView(),
            pluginId: "com.company.host",
            contributionId: "host.profile");
        registry.Register(hostDescriptor);

        var revoked = registry.RevokePlugin("com.company.sales");

        Assert.Equal(1, revoked);
        Assert.False(registry.TryLocate(typeof(SettingsViewModel), out _));
        Assert.Same(hostDescriptor, registry.Locate(typeof(ProfileViewModel)));
    }

    [Fact]
    public void ViewForAttributeStoresPluginContributionMetadata()
    {
        var attribute = new ViewForAttribute(typeof(SettingsViewModel))
        {
            Key = "compact",
            PluginId = "com.company.sales",
            ContributionId = "plugin.settings",
        };

        Assert.Equal(typeof(SettingsViewModel), attribute.ViewModelType);
        Assert.Equal("compact", attribute.Key);
        Assert.Equal("com.company.sales", attribute.PluginId);
        Assert.Equal("plugin.settings", attribute.ContributionId);
    }

    private static ViewDescriptor Descriptor<TViewModel, TView>(string? viewKey = null)
        where TView : new()
    {
        return new ViewDescriptor(
            typeof(TViewModel),
            typeof(TView),
            viewKey,
            _ => new TView());
    }

    private class BaseViewModel;

    private sealed class DerivedViewModel : BaseViewModel;

    private sealed class SettingsViewModel;

    private sealed class ProfileViewModel;

    private sealed class SettingsView;

    private sealed class ProfileView;

    private sealed class CompactSettingsView;

    private sealed class AlternativeSettingsView;
}
