using AtomUI.City.Routing;

namespace AtomUI.City.Routing.Tests;

public sealed class RouteGraphAndMatcherTests
{
    [Fact]
    public void GraphBuildsParentChildIndexesAndFindsRoutesById()
    {
        var snapshot = RouteGraphSnapshot.Create(
            [
                Layout("shell", typeof(ShellViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel), parentRouteId: "shell"),
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel), parentRouteId: "shell"),
            ]);

        Assert.Equal(1, snapshot.Version);
        Assert.Equal("shell", snapshot.GetRequiredRoute("shell").RouteId);
        Assert.Equal(["settings", "profile"], snapshot.GetChildren("shell").Select(route => route.RouteId));
    }

    [Fact]
    public void MatcherPrefersLiteralRoutesOverParameterRoutes()
    {
        var snapshot = RouteGraphSnapshot.Create(
            [
                Layout("shell", typeof(ShellViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel), parentRouteId: "shell"),
                Route("dynamic", "{section}", typeof(DynamicViewModel), parentRouteId: "shell"),
            ]);

        var match = snapshot.Matcher.Match("settings");

        Assert.Equal(RouteMatchStatus.Success, match.Status);
        Assert.Equal("settings", match.Route.RouteId);
    }

    [Fact]
    public void MatcherReturnsParametersForMatchedRoute()
    {
        var snapshot = RouteGraphSnapshot.Create(
            [
                Layout("shell", typeof(ShellViewModel)),
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel), parentRouteId: "shell"),
            ]);

        var match = snapshot.Matcher.Match("profile/42");

        Assert.Equal(RouteMatchStatus.Success, match.Status);
        Assert.Equal("profile", match.Route.RouteId);
        Assert.Equal("42", match.Parameters["id"]);
    }

    [Fact]
    public async Task MatcherCanRunConcurrentMatchesAgainstImmutableSnapshot()
    {
        var snapshot = RouteGraphSnapshot.Create(
            [
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);

        var matches = await Task.WhenAll(
            Enumerable
                .Range(1, 32)
                .Select(index => Task.Run(() => snapshot.Matcher.Match("profile/" + index.ToString()))));

        Assert.All(
            matches,
            match =>
            {
                Assert.Equal(RouteMatchStatus.Success, match.Status);
                Assert.Equal("profile", match.Route.RouteId);
                Assert.True(int.TryParse(match.Parameters["id"], out _));
            });
    }

    [Fact]
    public void MatcherMatchAllRejectsExternalListMutation()
    {
        var snapshot = RouteGraphSnapshot.Create(
            [
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel)),
            ]);
        var matches = snapshot.Matcher.MatchAll("profile/42");
        var matchList = Assert.IsAssignableFrom<IList<RouteMatch>>(matches);
        var replacement = snapshot.Matcher.Match("changed");

        Assert.Throws<NotSupportedException>(() => matchList[0] = replacement);
        Assert.Equal("profile", matches[0].Route.RouteId);
    }

    [Fact]
    public void GraphRejectsDuplicateRouteIds()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create(
                [
                    Route("settings", "settings", typeof(SettingsViewModel)),
                    Route("settings", "settings/profile", typeof(ProfileViewModel)),
                ]));

        Assert.Equal(RouteGraphError.DuplicateRouteId, exception.Error);
    }

    [Fact]
    public void GraphRejectsSiblingTemplateConflicts()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create(
                [
                    Route("settings", "settings", typeof(SettingsViewModel)),
                    Route("settings-copy", "settings", typeof(ProfileViewModel)),
                ]));

        Assert.Equal(RouteGraphError.DuplicateRouteTemplate, exception.Error);
    }

    [Fact]
    public void GraphRejectsSemanticallyEquivalentParameterTemplates()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create(
                [
                    Route("by-id", "items/{id}", typeof(SettingsViewModel)),
                    Route("by-name", "items/{name}", typeof(ProfileViewModel)),
                ]));

        Assert.Equal(RouteGraphError.DuplicateRouteTemplate, exception.Error);
    }

    [Fact]
    public void GraphRejectsVersionRegression()
    {
        var graph = RouteGraphSnapshot.Create([Route("home", "home", typeof(SettingsViewModel))], version: 5);

        var exception = Assert.Throws<RouteGraphException>(
            () => graph.WithoutContribution("unknown", version: 5));

        Assert.Equal(RouteGraphError.InvalidVersion, exception.Error);
    }

    [Fact]
    public void MatcherPrefersIndexRouteOverLayoutForSamePath()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Layout("shell", typeof(ShellViewModel)),
                new RouteDescriptor(
                    "home",
                    RouteDefinitionKind.Index,
                    template: null,
                    new ViewModelTargetDescriptor(typeof(SettingsViewModel)),
                    parentRouteId: "shell"),
            ]);

        Assert.Equal("home", graph.Matcher.Match(string.Empty).Route.RouteId);
    }

    [Fact]
    public void GraphRejectsStaticRedirectLoop()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create(
                [
                    Redirect("old-a", "old-a", "old-b"),
                    Redirect("old-b", "old-b", "old-a"),
                ]));

        Assert.Equal(RouteGraphError.CircularRedirect, exception.Error);
    }

    [Fact]
    public void GraphRejectsCircularParentHierarchy()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create(
                [
                    Route("route-a", "a", typeof(SettingsViewModel), parentRouteId: "route-b"),
                    Route("route-b", "b", typeof(ProfileViewModel), parentRouteId: "route-a"),
                ]));

        Assert.Equal(RouteGraphError.CircularParentRoute, exception.Error);
        Assert.Contains("route-a -> route-b -> route-a", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GraphRejectsSelfReferencingParent()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create(
                [
                    Route("self", "self", typeof(SettingsViewModel), parentRouteId: "self"),
                ]));

        Assert.Equal(RouteGraphError.CircularParentRoute, exception.Error);
        Assert.Contains("self -> self", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContributionCannotIntroduceCircularParentHierarchy()
    {
        var host = RouteGraphSnapshot.Create(
            [
                Route("home", "home", typeof(SettingsViewModel)),
            ]);

        var exception = Assert.Throws<RouteGraphException>(
            () => host.WithContribution(
                "plugin.circular",
                [
                    Route("plugin-a", "a", typeof(ProfileViewModel), parentRouteId: "plugin-b", contributionId: "plugin.circular"),
                    Route("plugin-b", "b", typeof(DynamicViewModel), parentRouteId: "plugin-a", contributionId: "plugin.circular"),
                ]));

        Assert.Equal(RouteGraphError.CircularParentRoute, exception.Error);
        Assert.False(host.TryGetRoute("plugin-a", out _));
        Assert.Equal(1, host.Version);
    }

    [Fact]
    public void SnapshotRevokesContributionWithoutMutatingExistingSnapshot()
    {
        var snapshot = RouteGraphSnapshot.Create(
            [
                Route("settings", "settings", typeof(SettingsViewModel)),
                Route("plugin-settings", "plugin-settings", typeof(DynamicViewModel), contributionId: "plugin.settings"),
            ],
            version: 7);

        var next = snapshot.WithoutContribution("plugin.settings", version: 8);

        Assert.Equal(7, snapshot.Version);
        Assert.True(snapshot.TryGetRoute("plugin-settings", out _));
        Assert.Equal(["plugin-settings"], snapshot.GetContributionRoutes("plugin.settings").Select(route => route.RouteId));
        Assert.Equal(8, next.Version);
        Assert.False(next.TryGetRoute("plugin-settings", out _));
        Assert.Empty(next.GetContributionRoutes("plugin.settings"));
        Assert.True(next.TryGetRoute("settings", out _));
    }

    [Fact]
    public void SnapshotAddsPluginContributionAndCanRevokeItWithoutMutatingOlderSnapshots()
    {
        var host = RouteGraphSnapshot.Create(
            [
                Route("settings", "settings", typeof(SettingsViewModel)),
            ],
            version: 10);
        var contributed = host.WithContribution(
            "plugin.profile",
            [
                Route("plugin-profile", "plugin/profile", typeof(ProfileViewModel), contributionId: "plugin.profile"),
            ],
            version: 11);
        var revoked = contributed.WithoutContribution("plugin.profile", version: 12);

        Assert.Equal(10, host.Version);
        Assert.False(host.TryGetRoute("plugin-profile", out _));
        Assert.Equal(11, contributed.Version);
        Assert.True(contributed.TryGetRoute("plugin-profile", out _));
        Assert.Equal(["plugin-profile"], contributed.GetContributionRoutes("plugin.profile").Select(route => route.RouteId));
        Assert.Equal(12, revoked.Version);
        Assert.False(revoked.TryGetRoute("plugin-profile", out _));
        Assert.True(contributed.TryGetRoute("plugin-profile", out _));
    }

    [Fact]
    public void SnapshotRejectsConflictingPluginContributionWithoutMutatingHostSnapshot()
    {
        var host = RouteGraphSnapshot.Create(
            [
                Route("settings", "settings", typeof(SettingsViewModel)),
            ],
            version: 10);

        var exception = Assert.Throws<RouteGraphException>(
            () => host.WithContribution(
                "plugin.conflict",
                [
                    Route("plugin-settings", "settings", typeof(ProfileViewModel), contributionId: "plugin.conflict"),
                ],
                version: 11));

        Assert.Equal(RouteGraphError.DuplicateRouteTemplate, exception.Error);
        Assert.False(host.TryGetRoute("plugin-settings", out _));
        Assert.Equal(10, host.Version);
        Assert.Empty(host.GetContributionRoutes("plugin.conflict"));
    }

    [Fact]
    public void RouteDescriptorStoresLocalizationMetadata()
    {
        var descriptor = new RouteDescriptor(
            "settings",
            RouteDefinitionKind.Route,
            "settings",
            new ViewModelTargetDescriptor(typeof(SettingsViewModel)),
            metadata: new RouteMetadataDescriptor(
                titleKey: "Routes.Settings.Title",
                descriptionKey: "Routes.Settings.Description",
                breadcrumbKey: "Routes.Settings.Breadcrumb",
                groupKey: "Routes.Settings.Group",
                errorTitleKey: "Routes.Settings.ErrorTitle"));

        Assert.Equal("Routes.Settings.Title", descriptor.Metadata.TitleKey);
        Assert.Equal("Routes.Settings.Description", descriptor.Metadata.DescriptionKey);
        Assert.Equal("Routes.Settings.Breadcrumb", descriptor.Metadata.BreadcrumbKey);
        Assert.Equal("Routes.Settings.Group", descriptor.Metadata.GroupKey);
        Assert.Equal("Routes.Settings.ErrorTitle", descriptor.Metadata.ErrorTitleKey);
    }

    [Fact]
    public void ViewModelTargetDescriptorStoresStableTargetMetadata()
    {
        var parameterBindings = new[] { "id", "tab" };

        var descriptor = new ViewModelTargetDescriptor(
            typeof(ProfileViewModel),
            parameterBindings,
            reuseKey: "profile:{id}",
            activationHint: "profile-detail");
        var exposedBindings = Assert.IsAssignableFrom<IList<string>>(descriptor.ParameterBindings);

        parameterBindings[0] = "changed";

        Assert.Equal(typeof(ProfileViewModel), descriptor.ViewModelType);
        Assert.Equal(["id", "tab"], descriptor.ParameterBindings);
        Assert.Equal("profile:{id}", descriptor.ReuseKey);
        Assert.Equal("profile-detail", descriptor.ActivationHint);
        Assert.Throws<NotSupportedException>(() => exposedBindings[0] = "changed");
    }

    [Fact]
    public void RouteDescriptorGuardCollectionsRejectExternalListMutation()
    {
        Type[] enterGuards = [typeof(SettingsViewModel)];
        Type[] leaveGuards = [typeof(ProfileViewModel)];
        Type[] matchPolicies = [typeof(DynamicViewModel)];
        var descriptor = new RouteDescriptor(
            "settings",
            RouteDefinitionKind.Route,
            "settings",
            new ViewModelTargetDescriptor(typeof(SettingsViewModel)),
            enterGuardTypes: enterGuards,
            leaveGuardTypes: leaveGuards,
            matchPolicyTypes: matchPolicies);
        var enterGuardList = Assert.IsAssignableFrom<IList<Type>>(descriptor.EnterGuardTypes);
        var leaveGuardList = Assert.IsAssignableFrom<IList<Type>>(descriptor.LeaveGuardTypes);
        var matchPolicyList = Assert.IsAssignableFrom<IList<Type>>(descriptor.MatchPolicyTypes);

        Assert.Throws<NotSupportedException>(() => enterGuardList[0] = typeof(ProfileViewModel));
        Assert.Throws<NotSupportedException>(() => leaveGuardList[0] = typeof(SettingsViewModel));
        Assert.Throws<NotSupportedException>(() => matchPolicyList[0] = typeof(SettingsViewModel));
        Assert.Equal(typeof(SettingsViewModel), descriptor.EnterGuardTypes[0]);
        Assert.Equal(typeof(ProfileViewModel), descriptor.LeaveGuardTypes[0]);
        Assert.Equal(typeof(DynamicViewModel), descriptor.MatchPolicyTypes[0]);
    }

    [Fact]
    public void RouteGraphCollectionsRejectExternalListMutation()
    {
        var shell = Layout("shell", typeof(ShellViewModel));
        var settings = Route("settings", "settings", typeof(SettingsViewModel), parentRouteId: "shell");
        var replacement = Route("replacement", "replacement", typeof(DynamicViewModel), parentRouteId: "shell");
        var snapshot = RouteGraphSnapshot.Create([shell, settings]);
        var routes = Assert.IsAssignableFrom<IList<RouteDescriptor>>(snapshot.Routes);
        var children = Assert.IsAssignableFrom<IList<RouteDescriptor>>(snapshot.GetChildren("shell"));

        Assert.Throws<NotSupportedException>(() => routes[0] = replacement);
        Assert.Throws<NotSupportedException>(() => children[0] = replacement);
        Assert.Equal(shell.RouteId, snapshot.Routes[0].RouteId);
        Assert.Equal(settings.RouteId, snapshot.GetChildren("shell")[0].RouteId);
    }

    [Fact]
    public void GraphRejectsRouteWithMissingViewModelTarget()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
                new RouteDescriptor(
                    "missing-target",
                    RouteDefinitionKind.Route,
                    "missing-target",
                    viewModelTarget: null),
        ]));

        Assert.Equal(RouteGraphError.InvalidRouteDefinition, exception.Error);
    }

    [Fact]
    public async Task NavigationFailsBeforeCommitWhenViewModelTargetIsNotConstructable()
    {
        var snapshot = RouteGraphSnapshot.Create(
            [
                new RouteDescriptor(
                    "abstract-target",
                    RouteDefinitionKind.Route,
                    "abstract-target",
                    new ViewModelTargetDescriptor(typeof(AbstractViewModel))),
            ]);
        var scope = new NavigationScope(snapshot);

        var result = await scope.Router.NavigateByPathAsync("abstract-target");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-TARGET-NOT-CONSTRUCTABLE", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task NavigationResolvesTargetDescriptorWithoutCreatingViewModel()
    {
        ConstructedViewModel.ConstructorCalls = 0;
        var snapshot = RouteGraphSnapshot.Create(
            [
                Route("constructed", "constructed", typeof(ConstructedViewModel)),
            ]);
        var scope = new NavigationScope(snapshot);

        var result = await scope.Router.NavigateByPathAsync("constructed");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal(typeof(ConstructedViewModel), result.Route.ViewModelTarget?.ViewModelType);
        Assert.Equal(0, ConstructedViewModel.ConstructorCalls);
    }

    [Fact]
    public void GraphReportsMissingParentRoute()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            Route("profile", "profile", typeof(ProfileViewModel), parentRouteId: "missing"),
        ]));

        Assert.Equal(RouteGraphError.MissingParentRoute, exception.Error);
    }

    [Fact]
    public void GraphReportsInvalidContributionOwnership()
    {
        var graph = RouteGraphSnapshot.Create([]);
        var route = Route(
            "profile",
            "profile",
            typeof(ProfileViewModel),
            contributionId: "plugin.other");

        var exception = Assert.Throws<RouteGraphException>(() =>
            graph.WithContribution("plugin.profile", [route]));

        Assert.Equal(RouteGraphError.InvalidContribution, exception.Error);
    }

    [Fact]
    public void GraphReportsDuplicateIndexRoute()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            Layout("shell", typeof(ShellViewModel)),
            Index("home", "shell"),
            Index("dashboard", "shell"),
        ]));

        Assert.Equal(RouteGraphError.DuplicateIndexRoute, exception.Error);
    }

    [Fact]
    public void GraphReportsDuplicateExtensionPoint()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            ExtensionPoint("first-slot", "tools"),
            ExtensionPoint("second-slot", "tools"),
        ]));

        Assert.Equal(RouteGraphError.DuplicateExtensionPoint, exception.Error);
    }

    [Fact]
    public void GraphReportsMissingExtensionPoint()
    {
        var route = new RouteDescriptor(
            "plugin-profile",
            RouteDefinitionKind.Route,
            "profile",
            new ViewModelTargetDescriptor(typeof(ProfileViewModel)),
            extensionPoint: "missing-slot",
            contributionId: "plugin.profile");

        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create([route]));

        Assert.Equal(RouteGraphError.MissingExtensionPoint, exception.Error);
    }

    [Fact]
    public void GraphReportsMissingRedirectTarget()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            Redirect("legacy", "legacy", "missing"),
        ]));

        Assert.Equal(RouteGraphError.MissingRedirectTarget, exception.Error);
    }

    private static RouteDescriptor Layout(string id, Type viewModelType)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Layout,
            template: null,
            new ViewModelTargetDescriptor(viewModelType),
            parentRouteId: null);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        Type viewModelType,
        string? parentRouteId = null,
        string? contributionId = null)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(viewModelType),
            parentRouteId,
            contributionId: contributionId);
    }

    private static RouteDescriptor Redirect(string id, string template, string target)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Redirect,
            template,
            viewModelTarget: null,
            redirectTargetRouteId: target);
    }

    private static RouteDescriptor Index(string id, string parentRouteId)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Index,
            template: null,
            new ViewModelTargetDescriptor(typeof(ProfileViewModel)),
            parentRouteId);
    }

    private static RouteDescriptor ExtensionPoint(string id, string extensionPoint)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.ExtensionPoint,
            template: null,
            viewModelTarget: null,
            extensionPoint: extensionPoint);
    }

    private sealed class ShellViewModel;

    private sealed class SettingsViewModel;

    private sealed class ProfileViewModel;

    private sealed class DynamicViewModel;

    private abstract class AbstractViewModel;

    private sealed class ConstructedViewModel
    {
        public static int ConstructorCalls { get; set; }

        public ConstructedViewModel()
        {
            ConstructorCalls++;
        }
    }
}
