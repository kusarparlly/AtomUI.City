using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation.Tests;

public sealed class ViewModelCompositionTests
{
    [Fact]
    public async Task RootAndChildUseIndependentLinkedScopesAndDisposeLeafFirst()
    {
        var events = new List<string>();
        var diagnostics = new InMemoryHostDiagnostics();
        await using var owner = new ViewModelCompositionOwner(
            "window:main",
            diagnostics: diagnostics);
        var rootViewModel = new TestActivatable("root", events);
        var childViewModel = new TestActivatable("child", events);
        var root = await owner.AttachRootAsync(
            "shell",
            ViewModelLease.Borrowed(rootViewModel));
        var child = await root.AttachChildAsync(
            "status-bar",
            ViewModelLease.Borrowed(childViewModel));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var childTask = child.ActivationScope.StartAsync(
            "status-loop",
            async cancellationToken =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });

        await started.Task;

        Assert.NotSame(root.ActivationScope, child.ActivationScope);
        Assert.True(root.IsActive);
        Assert.True(child.IsActive);
        Assert.Equal("shell", child.ParentId);

        await root.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => childTask);
        Assert.Equal(["activate:root", "activate:child", "deactivate:child", "deactivate:root"], events);
        Assert.True(child.IsDisposed);
        Assert.True(root.IsDisposed);
        Assert.Empty(owner.Roots);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.ViewModelCompositionAttached
                && record.Context?["compositionId"] == "status-bar"
                && record.Context["role"] == "Child");
    }

    [Fact]
    public async Task OwnerCancellationFlowsToRootAndChildScopes()
    {
        using var cancellation = new CancellationTokenSource();
        await using var owner = new ViewModelCompositionOwner(
            "window:main",
            cancellation.Token);
        var root = await owner.AttachRootAsync(
            "shell",
            ViewModelLease.Borrowed(new TestActivatable("root")));
        var child = await root.AttachChildAsync(
            "status-bar",
            ViewModelLease.Borrowed(new TestActivatable("child")));

        await cancellation.CancelAsync();

        Assert.True(root.ActivationScope.CancellationToken.IsCancellationRequested);
        Assert.True(child.ActivationScope.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task DuplicateIdentityAndOwnedInstanceAreRejected()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var owner = new ViewModelCompositionOwner(
            "window:main",
            diagnostics: diagnostics);
        var viewModel = new TestActivatable("root");
        var root = await owner.AttachRootAsync(
            "shell",
            ViewModelLease.Borrowed(viewModel));

        var duplicateIdentity = await Assert.ThrowsAsync<PresentationException>(async () =>
            await owner.AttachRootAsync(
                "shell",
                ViewModelLease.Borrowed(new TestActivatable("other"))));
        var duplicateInstance = await Assert.ThrowsAsync<PresentationException>(async () =>
            await root.AttachChildAsync(
                "child",
                ViewModelLease.Borrowed(viewModel)));

        Assert.Equal(PresentationError.ViewModelCompositionRejected, duplicateIdentity.Error);
        Assert.Equal(PresentationError.ViewModelCompositionRejected, duplicateInstance.Error);
        Assert.Single(owner.Roots);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.ViewModelCompositionRejected
                && record.Context?["reason"] == "duplicate-identity");
    }

    [Fact]
    public async Task ChildCleanupFailureDoesNotSkipSiblingOrParent()
    {
        var events = new List<string>();
        var diagnostics = new InMemoryHostDiagnostics();
        await using var owner = new ViewModelCompositionOwner(
            "window:main",
            diagnostics: diagnostics);
        var rootViewModel = new TestActivatable("root", events);
        var firstChild = new TestActivatable("first", events);
        var failingChild = new TestActivatable("failing", events)
        {
            DeactivationException = new InvalidOperationException("deactivation failed"),
        };
        var root = await owner.AttachRootAsync(
            "root",
            ViewModelLease.Borrowed(rootViewModel));
        var firstLease = await root.AttachChildAsync(
            "first",
            ViewModelLease.Borrowed(firstChild));
        var failingLease = await root.AttachChildAsync(
            "failing",
            ViewModelLease.Borrowed(failingChild));

        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            await root.DisposeAsync());

        Assert.Contains(
            exception.Flatten().InnerExceptions,
            static item => item is InvalidOperationException { Message: "deactivation failed" });
        Assert.True(firstLease.IsDisposed);
        Assert.True(failingLease.IsDisposed);
        Assert.True(root.IsDisposed);
        Assert.Contains("deactivate:first", events);
        Assert.Contains("deactivate:root", events);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.ViewModelCompositionCleanupFailed
                && record.Context?["compositionId"] == "failing"
                && record.Context["stage"] == "Deactivate");
    }

    [Fact]
    public async Task EntryOwnedViewModelIsFinallyDisposedButBorrowedViewModelIsNot()
    {
        await using var owner = new ViewModelCompositionOwner("window:main");
        var owned = new TestActivatable("owned");
        var borrowed = new TestActivatable("borrowed");
        var ownedLease = await owner.AttachRootAsync(
            "owned",
            ViewModelLease.EntryOwned(owned));
        var borrowedLease = await owner.AttachRootAsync(
            "borrowed",
            ViewModelLease.Borrowed(borrowed));

        await ownedLease.DisposeAsync();
        await borrowedLease.DisposeAsync();

        Assert.True(owned.IsDisposed);
        Assert.False(borrowed.IsDisposed);
    }

    [Fact]
    public async Task DisposedOwnerRejectsNewRoots()
    {
        var owner = new ViewModelCompositionOwner("window:main");
        await owner.DisposeAsync();

        var exception = await Assert.ThrowsAsync<PresentationException>(async () =>
            await owner.AttachRootAsync(
                "late",
                ViewModelLease.Borrowed(new TestActivatable("late"))));

        Assert.Equal(PresentationError.ViewModelCompositionRejected, exception.Error);
    }

    [Fact]
    public async Task OwnedInstanceCannotBeAttachedAcrossOwners()
    {
        await using var firstOwner = new ViewModelCompositionOwner("window:first");
        await using var secondOwner = new ViewModelCompositionOwner("window:second");
        var viewModel = new TestActivatable("shared");

        _ = await firstOwner.AttachRootAsync(
            "root",
            ViewModelLease.EntryOwned(viewModel));

        var exception = await Assert.ThrowsAsync<PresentationException>(async () =>
            await secondOwner.AttachRootAsync(
                "root",
                ViewModelLease.Borrowed(viewModel)));

        Assert.Equal(PresentationError.ViewModelCompositionRejected, exception.Error);
        Assert.Empty(secondOwner.Roots);
    }

    [Fact]
    public async Task OwnerDisposalCancelsActivationInProgressAndRollsBackLease()
    {
        var owner = new ViewModelCompositionOwner("window:main");
        var viewModel = new BlockingActivatable();
        var attach = owner.AttachRootAsync(
            "root",
            ViewModelLease.EntryOwned(viewModel)).AsTask();
        await viewModel.Started;

        var dispose = owner.DisposeAsync().AsTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attach);
        await dispose;
        Assert.True(viewModel.IsDisposed);
        Assert.Empty(owner.Roots);
    }

    [Fact]
    public async Task NestedChildrenDrainLeafTasksBeforeParentDeactivation()
    {
        var events = new List<string>();
        await using var owner = new ViewModelCompositionOwner("window:main");
        var root = await owner.AttachRootAsync(
            "root",
            ViewModelLease.Borrowed(new TestActivatable("root", events)));
        var child = await root.AttachChildAsync(
            "child",
            ViewModelLease.Borrowed(new TestActivatable("child", events)));
        var grandchild = await child.AttachChildAsync(
            "grandchild",
            ViewModelLease.Borrowed(new TestActivatable("grandchild", events)));
        var taskStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var grandchildTask = grandchild.ActivationScope.StartAsync(
            "grandchild-loop",
            async token =>
            {
                taskStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    events.Add("drain:grandchild");
                }
            });
        await taskStarted.Task;

        await root.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => grandchildTask);
        Assert.True(events.IndexOf("deactivate:grandchild") < events.IndexOf("drain:grandchild"));
        Assert.True(events.IndexOf("drain:grandchild") < events.IndexOf("deactivate:child"));
        Assert.True(events.IndexOf("deactivate:child") < events.IndexOf("deactivate:root"));
        Assert.True(grandchild.IsDisposed);
        Assert.True(child.IsDisposed);
        Assert.True(root.IsDisposed);
    }

    [Fact]
    public async Task ChildActivationFailureRollsBackCandidateWithoutStoppingSiblingOrParent()
    {
        await using var owner = new ViewModelCompositionOwner("window:main");
        var root = await owner.AttachRootAsync(
            "root",
            ViewModelLease.Borrowed(new TestActivatable("root")));
        var sibling = await root.AttachChildAsync(
            "sibling",
            ViewModelLease.Borrowed(new TestActivatable("sibling")));
        var candidate = new ThrowingActivationViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await root.AttachChildAsync(
                "candidate",
                ViewModelLease.EntryOwned(candidate)));

        Assert.True(root.IsActive);
        Assert.True(sibling.IsActive);
        Assert.True(candidate.IsDisposed);
        Assert.DoesNotContain(root.Children, child => child.CompositionId == "candidate");
        Assert.Single(root.Children);
    }

    [Fact]
    public async Task ExplicitLeaseAndOwnerDisposeShareOneCleanupTransaction()
    {
        var owner = new ViewModelCompositionOwner("window:main");
        var viewModel = new BlockingDeactivationViewModel();
        var root = await owner.AttachRootAsync(
            "root",
            ViewModelLease.EntryOwned(viewModel));

        var explicitDispose = root.DisposeAsync().AsTask();
        await viewModel.DeactivationStarted.Task;
        var ownerDispose = owner.DisposeAsync().AsTask();

        Assert.False(explicitDispose.IsCompleted);
        Assert.False(ownerDispose.IsCompleted);
        viewModel.ReleaseDeactivation.TrySetResult();
        await Task.WhenAll(explicitDispose, ownerDispose);

        Assert.Equal(1, viewModel.DeactivationCount);
        Assert.Equal(1, viewModel.DisposeCount);
        Assert.True(root.IsDisposed);
        Assert.Empty(owner.Roots);
    }

    [Fact]
    public async Task ThrowingDiagnosticsCannotChangeAttachOrCleanupResults()
    {
        await using var owner = new ViewModelCompositionOwner(
            "window:main",
            diagnostics: new ThrowingDiagnostics());
        var viewModel = new TestActivatable("root")
        {
            DeactivationException = new InvalidOperationException("expected cleanup failure"),
        };

        var root = await owner.AttachRootAsync(
            "root",
            ViewModelLease.Borrowed(viewModel));
        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            await root.DisposeAsync());

        Assert.Contains(
            exception.Flatten().InnerExceptions,
            item => item is InvalidOperationException { Message: "expected cleanup failure" });
        Assert.True(root.IsDisposed);
        Assert.Empty(owner.Roots);
    }

    [Fact]
    public async Task BorrowedInstanceHasOneActivationOwnerAtATimeAndCanMoveAfterRelease()
    {
        await using var firstOwner = new ViewModelCompositionOwner("window:first");
        await using var secondOwner = new ViewModelCompositionOwner("window:second");
        var viewModel = new TestActivatable("shared");
        var first = await firstOwner.AttachRootAsync(
            "root",
            ViewModelLease.Borrowed(viewModel));

        var rejection = await Assert.ThrowsAsync<PresentationException>(async () =>
            await secondOwner.AttachRootAsync(
                "root",
                ViewModelLease.Borrowed(viewModel)));
        await first.DisposeAsync();
        var second = await secondOwner.AttachRootAsync(
            "root",
            ViewModelLease.Borrowed(viewModel));

        Assert.Equal(PresentationError.ViewModelCompositionRejected, rejection.Error);
        Assert.True(second.IsActive);
    }

    [Fact]
    public async Task RouteOutletEntryAndCompositionTreeDisposeIndependently()
    {
        await using var owner = new ViewModelCompositionOwner("window:main");
        var root = await owner.AttachRootAsync(
            "root",
            ViewModelLease.Borrowed(new TestActivatable("root")));
        var outlet = new RouteOutlet("primary", new InlineDispatcher());
        var entryHandle = BoundViewHandle.FromExisting(new object(), new TestActivatable("route"));
        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", entryHandle));

        await outlet.DisposeAsync();

        Assert.True(entryHandle.IsDisposed);
        Assert.True(root.IsActive);
        Assert.False(root.IsDisposed);

        await root.DisposeAsync();
        Assert.True(root.IsDisposed);
    }

    private sealed class TestActivatable : IActivatable, IAsyncDisposable
    {
        private readonly string _name;
        private readonly List<string>? _events;

        public TestActivatable(string name, List<string>? events = null)
        {
            _name = name;
            _events = events;
        }

        public Exception? DeactivationException { get; init; }

        public bool IsDisposed { get; private set; }

        public ValueTask ActivateAsync(
            IActivationScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events?.Add($"activate:{_name}");
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events?.Add($"deactivate:{_name}");
            if (DeactivationException is not null)
            {
                throw DeactivationException;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingActivatable : IActivatable, IAsyncDisposable
    {
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public bool IsDisposed { get; private set; }

        public async ValueTask ActivateAsync(
            IActivationScope scope,
            CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public ValueTask DeactivateAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingActivationViewModel : IActivatable, IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask ActivateAsync(IActivationScope scope, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("activation failed");

        public ValueTask DeactivateAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingDeactivationViewModel : IActivatable, IAsyncDisposable
    {
        public TaskCompletionSource DeactivationStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseDeactivation { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int DeactivationCount { get; private set; }

        public int DisposeCount { get; private set; }

        public ValueTask ActivateAsync(IActivationScope scope, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public async ValueTask DeactivateAsync(CancellationToken cancellationToken)
        {
            DeactivationCount++;
            DeactivationStarted.TrySetResult();
            await ReleaseDeactivation.Task;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingDiagnostics : IHostDiagnostics
    {
        public IReadOnlyList<HostDiagnosticRecord> Records => [];

        public void Write(HostDiagnosticRecord record) =>
            throw new InvalidOperationException("diagnostics failed");

        public void Complete()
        {
        }
    }

    private sealed class InlineDispatcher : AtomUI.City.Core.Threading.IUiDispatcher
    {
        public bool CheckAccess() => true;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default) =>
            callback(cancellationToken);
    }
}
