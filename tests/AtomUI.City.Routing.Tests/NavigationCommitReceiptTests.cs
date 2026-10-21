using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Routing;

namespace AtomUI.City.Routing.Tests;

public sealed class NavigationCommitReceiptTests
{
    [Fact]
    public async Task SuccessfulCommitReturnsMonotonicReceiptWithBothSnapshots()
    {
        var scope = CreateScope();

        var home = await scope.NavigateByPathAsync("home");
        var settings = await scope.NavigateByPathAsync("settings");

        var homeReceipt = Assert.IsType<NavigationCommitReceipt>(home.CommitReceipt);
        var settingsReceipt = Assert.IsType<NavigationCommitReceipt>(settings.CommitReceipt);
        Assert.Null(homeReceipt.Previous.ActiveRoute);
        Assert.Equal("home", homeReceipt.Current.Route.RouteId);
        Assert.Same(homeReceipt.Current, settingsReceipt.Previous);
        Assert.Equal("settings", settingsReceipt.Current.Route.RouteId);
        Assert.True(settingsReceipt.Generation > homeReceipt.Generation);
    }

    [Fact]
    public async Task CurrentReceiptRestoresSnapshotAndCanOnlyBeUsedOnce()
    {
        var scope = CreateScope();
        await scope.NavigateByPathAsync("home");
        var settings = await scope.NavigateByPathAsync("settings");
        var receipt = Assert.IsType<NavigationCommitReceipt>(settings.CommitReceipt);

        var restored = await scope.TryRestoreAsync(receipt);
        var repeated = await scope.TryRestoreAsync(receipt);

        Assert.Equal(NavigationRestoreResultStatus.Restored, restored.Status);
        Assert.Same(receipt.Previous, scope.CurrentSnapshot);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
        Assert.Equal(NavigationRestoreResultStatus.Rejected, repeated.Status);
        Assert.Equal("CITY-NAVIGATION-RESTORE-SUPERSEDED", repeated.Code);
    }

    [Fact]
    public async Task LaterCommitRejectsStaleReceiptWithoutUndoingCurrentRoute()
    {
        var scope = CreateScope();
        await scope.NavigateByPathAsync("home");
        var settings = await scope.NavigateByPathAsync("settings");
        await scope.NavigateByPathAsync("profile");

        var restored = await scope.TryRestoreAsync(
            Assert.IsType<NavigationCommitReceipt>(settings.CommitReceipt));

        Assert.Equal(NavigationRestoreResultStatus.Rejected, restored.Status);
        Assert.Equal("CITY-NAVIGATION-RESTORE-SUPERSEDED", restored.Code);
        Assert.Equal("profile", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ReceiptFromDifferentScopeIsRejected()
    {
        var first = CreateScope();
        var second = CreateScope();
        var committed = await first.NavigateByPathAsync("home");

        var restored = await second.TryRestoreAsync(
            Assert.IsType<NavigationCommitReceipt>(committed.CommitReceipt));

        Assert.Equal(NavigationRestoreResultStatus.Rejected, restored.Status);
        Assert.Equal("CITY-NAVIGATION-RESTORE-FOREIGN-RECEIPT", restored.Code);
        Assert.Null(second.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task RestoreReinstatesJournalCheckpointWithoutBackSideEffects()
    {
        var scope = CreateScope();
        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("settings");
        var profile = await scope.NavigateByPathAsync("profile");
        var receipt = Assert.IsType<NavigationCommitReceipt>(profile.CommitReceipt);

        var restored = await scope.TryRestoreAsync(receipt);
        var back = await scope.BackAsync();
        var forward = await scope.ForwardAsync();

        Assert.Equal(NavigationRestoreResultStatus.Restored, restored.Status);
        Assert.Equal("home", back.Route.RouteId);
        Assert.Equal("settings", forward.Route.RouteId);
    }

    [Fact]
    public async Task CompletedRedirectPreservesFinalCommitReceipt()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                new RouteDescriptor(
                    "old-home",
                    RouteDefinitionKind.Redirect,
                    "old-home",
                    viewModelTarget: null,
                    redirectTargetRouteId: "home"),
                Route("home"),
            ]);
        var scope = new NavigationScope(graph);

        var result = await scope.NavigateByPathAsync("old-home");

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        var receipt = Assert.IsType<NavigationCommitReceipt>(result.CommitReceipt);
        Assert.Equal("home", receipt.Current.Route.RouteId);
        Assert.Same(receipt.Current, scope.CurrentSnapshot);
    }

    [Fact]
    public async Task PreCancelledRestoreDoesNotMutateCurrentCommit()
    {
        var scope = CreateScope();
        var committed = await scope.NavigateByPathAsync("home");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var restored = await scope.TryRestoreAsync(
            Assert.IsType<NavigationCommitReceipt>(committed.CommitReceipt),
            cancellation.Token);

        Assert.Equal(NavigationRestoreResultStatus.Cancelled, restored.Status);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task DisposedScopeRejectsReceiptWithoutMutatingSnapshot()
    {
        var scope = CreateScope();
        var committed = await scope.NavigateByPathAsync("home");
        await scope.DisposeAsync();

        var restored = await scope.TryRestoreAsync(
            Assert.IsType<NavigationCommitReceipt>(committed.CommitReceipt));

        Assert.Equal(NavigationRestoreResultStatus.Rejected, restored.Status);
        Assert.Equal("CITY-NAVIGATION-RESTORE-SCOPE-DISPOSED", restored.Code);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ThrowingDiagnosticsCannotChangeRestoreResult()
    {
        var scope = CreateScope(new ThrowingDiagnostics());
        var committed = await scope.NavigateByPathAsync("home");

        var restored = await scope.TryRestoreAsync(
            Assert.IsType<NavigationCommitReceipt>(committed.CommitReceipt));

        Assert.Equal(NavigationRestoreResultStatus.Restored, restored.Status);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    private static NavigationScope CreateScope(IHostDiagnostics? diagnostics = null)
    {
        return new NavigationScope(RouteGraphSnapshot.Create(
            [
                Route("home"),
                Route("settings"),
                Route("profile"),
            ]), diagnostics: diagnostics);
    }

    private static RouteDescriptor Route(string id) =>
        new(
            id,
            RouteDefinitionKind.Route,
            id,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            parentRouteId: null);

    private sealed class TestViewModel;

    private sealed class ThrowingDiagnostics : IHostDiagnostics
    {
        public IReadOnlyList<HostDiagnosticRecord> Records => [];

        public void Write(HostDiagnosticRecord record) =>
            throw new InvalidOperationException("diagnostics failed");

        public void Complete()
        {
        }
    }
}
