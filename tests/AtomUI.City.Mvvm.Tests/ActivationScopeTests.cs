using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Mvvm.Tests;

public sealed class ActivationScopeTests
{
    [Fact]
    public void ActivationScopeHasStableDiagnosticId()
    {
        using var scope = new ActivationScope();

        Assert.NotEqual(Guid.Empty, scope.Id);
        Assert.Equal(scope.Id, scope.Id);
    }

    [Fact]
    public void DisposeDisposesRegisteredResourcesAndCancelsToken()
    {
        var disposable = new TestDisposable();
        using var scope = new ActivationScope();

        scope.Add(disposable);
        scope.Dispose();

        Assert.True(disposable.IsDisposed);
        Assert.True(scope.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var calls = 0;
        using var scope = new ActivationScope();

        scope.Add(new TestDisposable(() => calls++));
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, calls);
        Assert.True(scope.IsDisposed);
    }

    [Fact]
    public void DisposeDisposesRegisteredResourcesInReverseOrder()
    {
        var calls = new List<string>();
        using var scope = new ActivationScope();

        scope.Add(new TestDisposable(() => calls.Add("first")));
        scope.Add(new TestDisposable(() => calls.Add("second")));

        scope.Dispose();

        Assert.Equal(["second", "first"], calls);
    }

    [Fact]
    public void AddAfterDisposeImmediatelyDisposesResource()
    {
        var disposable = new TestDisposable();
        using var scope = new ActivationScope();

        scope.Dispose();
        scope.Add(disposable);

        Assert.True(disposable.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsyncDisposesAsyncResourcesInReverseOrder()
    {
        var calls = new List<string>();
        var scope = new ActivationScope();

        scope.AddAsync(new TestAsyncDisposable(() => calls.Add("first")));
        scope.AddAsync(new TestAsyncDisposable(() => calls.Add("second")));

        await scope.DisposeAsync();

        Assert.Equal(["second", "first"], calls);
        Assert.True(scope.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task StartAsyncTracksTaskUntilCompletion()
    {
        await using var scope = new ActivationScope();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = scope.StartAsync(
            "refresh",
            cancellationToken => release.Task.WaitAsync(cancellationToken));

        Assert.Equal(1, scope.ActiveTaskCount);

        release.SetResult();
        await task;

        Assert.Equal(0, scope.ActiveTaskCount);
    }

    [Fact]
    public async Task StartUniqueAsyncRejectsDuplicateAndReleasesKeyAtCompletion()
    {
        await using var scope = new ActivationScope();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = scope.StartUniqueAsync(
            "transfer-refresh",
            cancellationToken => release.Task.WaitAsync(cancellationToken));

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = scope.StartUniqueAsync("transfer-refresh", _ => Task.CompletedTask);
        });

        Assert.Contains("transfer-refresh", exception.Message, StringComparison.Ordinal);

        release.SetResult();
        await first;
        await scope.StartUniqueAsync("transfer-refresh", _ => Task.CompletedTask);

        Assert.Equal(0, scope.ActiveTaskCount);
    }

    [Fact]
    public async Task DisposeAsyncClosesAdmissionCancelsAndDrainsTasksBeforeResources()
    {
        var calls = new List<string>();
        var scope = new ActivationScope();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = scope.StartAsync(
            "worker",
            async cancellationToken =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                finally
                {
                    calls.Add("task");
                }
            });
        scope.Add(new TestDisposable(() => calls.Add("resource")));

        await started.Task;
        await scope.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(["task", "resource"], calls);
        Assert.True(scope.IsClosing);
        Assert.True(scope.IsDisposed);
        Assert.Equal(0, scope.ActiveTaskCount);
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = scope.StartAsync("late", _ => Task.CompletedTask);
        });
    }

    [Fact]
    public async Task StartAsyncPreservesOriginalFailureAndReleasesRegistration()
    {
        await using var scope = new ActivationScope();
        var exception = new InvalidOperationException("boom");

        var task = scope.StartAsync("failing", _ => Task.FromException(exception));
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);

        Assert.Same(exception, thrown);
        Assert.Equal(0, scope.ActiveTaskCount);
    }

    [Fact]
    public async Task DisposeRejectsSynchronousShutdownWhenTaskIsActive()
    {
        var scope = new ActivationScope();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = scope.StartAsync(
            "worker",
            async cancellationToken =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        await started.Task;

        Assert.Throws<InvalidOperationException>(() => scope.Dispose());

        await scope.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task ParentCancellationRejectsNewTasksWithoutInvokingFactory()
    {
        using var parent = new CancellationTokenSource();
        await using var scope = new ActivationScope(parentCancellationToken: parent.Token);
        var invoked = false;

        await parent.CancelAsync();

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = scope.StartAsync(
                "late",
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                });
        });
        Assert.False(invoked);
    }

    [Fact]
    public async Task ConcurrentDisposeAsyncCallsShareOneShutdownTransaction()
    {
        var scope = new ActivationScope();
        var calls = 0;
        scope.AddAsync(new TestAsyncDisposable(() => calls++));

        var first = scope.DisposeAsync().AsTask();
        var second = scope.DisposeAsync().AsTask();

        Assert.Same(first, second);
        await Task.WhenAll(first, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TaskFailureAndRejectedAdmissionWriteStructuredDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var scope = new ActivationScope(diagnostics);
        var expected = new InvalidOperationException("task-failure");

        var failure = scope.StartAsync("failing", _ => Task.FromException(expected));
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => failure));

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unique = scope.StartUniqueAsync(
            "worker",
            cancellationToken => release.Task.WaitAsync(cancellationToken));
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = scope.StartUniqueAsync("worker", _ => Task.CompletedTask);
        });

        release.TrySetResult();
        await unique;

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.ActivationTaskFailed
                && record.Context?["taskName"] == "failing"
                && record.Context["exceptionType"] == expected.GetType().FullName);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.ActivationTaskRejected
                && record.Context?["taskKey"] == "worker"
                && record.Context["reason"] == "duplicate-key");
    }

    [Fact]
    public async Task DrainTimeoutIsObservableAndDoesNotClaimTaskTermination()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var scope = new ActivationScope(
            diagnostics,
            taskDrainTimeout: TimeSpan.FromMilliseconds(10));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = scope.StartAsync(
            "uncooperative",
            _ =>
            {
                started.TrySetResult();
                return release.Task;
            });
        await started.Task;

        await scope.DisposeAsync();

        Assert.True(scope.IsDisposed);
        Assert.Equal(1, scope.ActiveTaskCount);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.ActivationTaskDrainTimedOut
                && record.Context?["activeTaskCount"] == "1");

        release.TrySetResult();
        await task;
        Assert.Equal(0, scope.ActiveTaskCount);
    }

    private sealed class TestDisposable : IDisposable
    {
        private readonly Action? _dispose;

        public TestDisposable(Action? dispose = null)
        {
            _dispose = dispose;
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            _dispose?.Invoke();
        }
    }

    private sealed class TestAsyncDisposable : IAsyncDisposable
    {
        private readonly Action _dispose;

        public TestAsyncDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public ValueTask DisposeAsync()
        {
            _dispose();

            return ValueTask.CompletedTask;
        }
    }
}
