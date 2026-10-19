using System.Reflection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtomUI.City.Mvvm.Tests;

public sealed class ViewModelBaseTests
{
    [Fact]
    public void ActivationCallbacksRequireExplicitCancellationToken()
    {
        var methods = typeof(IActivatable).GetMethods();

        Assert.All(methods, method =>
        {
            var cancellationToken = Assert.Single(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(CancellationToken));
            Assert.False(cancellationToken.IsOptional);
        });
    }

    [Fact]
    public void SetPropertyRaisesPropertyChangedWithStablePropertyName()
    {
        var viewModel = new TestViewModel();
        var raisedProperties = new List<string?>();

        viewModel.PropertyChanged += (_, args) => raisedProperties.Add(args.PropertyName);

        var changed = viewModel.UpdateTitle("Settings");

        Assert.True(changed);
        Assert.Equal("Settings", viewModel.Title);
        Assert.Equal(new[] { nameof(TestViewModel.Title) }, raisedProperties);
    }

    [Fact]
    public void SetPropertySkipsEquivalentValueNotifications()
    {
        var viewModel = new TestViewModel();
        viewModel.UpdateTitle("Settings");
        var raisedProperties = new List<string?>();

        viewModel.PropertyChanged += (_, args) => raisedProperties.Add(args.PropertyName);

        var changed = viewModel.UpdateTitle("Settings");

        Assert.False(changed);
        Assert.Empty(raisedProperties);
    }

    [Fact]
    public void SetPropertyRejectsEmptyPropertyName()
    {
        var viewModel = new TestViewModel();

        var exception = Assert.Throws<ArgumentException>(() =>
            viewModel.UpdateTitleWithPropertyName("Settings", string.Empty));

        Assert.Equal("propertyName", exception.ParamName);
    }

    [Fact]
    public void DisposeIsIdempotentAndMarksViewModelDisposed()
    {
        var viewModel = new TestViewModel();
        var disposable = Assert.IsAssignableFrom<IDisposable>(viewModel);

        disposable.Dispose();
        disposable.Dispose();

        Assert.Equal(ActivationState.Disposed, viewModel.ActivationState);
        Assert.False(viewModel.IsActive);
    }

    [Fact]
    public void ViewModelBaseExposesDisposeInheritanceHook()
    {
        var method = typeof(ViewModelBase).GetMethod(
            "OnDisposed",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True(method.IsVirtual);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void SetPropertyAfterDisposeThrowsObjectDisposedException()
    {
        var viewModel = new TestViewModel();
        var disposable = Assert.IsAssignableFrom<IDisposable>(viewModel);
        disposable.Dispose();

        var exception = Assert.Throws<ObjectDisposedException>(() => viewModel.UpdateTitle("Settings"));

        Assert.Equal(typeof(TestViewModel).FullName, exception.ObjectName);
        Assert.Equal(ActivationState.Disposed, viewModel.ActivationState);
    }

    [Fact]
    public async Task ActivateAndDeactivateCallTemplateMethods()
    {
        using var scope = new ActivationScope();
        var viewModel = new TestViewModel();

        await viewModel.ActivateAsync(scope);
        await viewModel.DeactivateAsync();

        Assert.Equal(1, viewModel.ActivatedCount);
        Assert.Equal(1, viewModel.DeactivatedCount);
        Assert.IsAssignableFrom<ObservableValidator>(viewModel);
    }

    [Fact]
    public async Task ActivateAndDeactivateUpdateStateAndCurrentScope()
    {
        using var scope = new ActivationScope();
        var viewModel = new TestViewModel();

        Assert.Equal(ActivationState.Constructed, viewModel.ActivationState);
        Assert.False(viewModel.IsActive);

        await viewModel.ActivateAsync(new ActivationContext(scope, "settings-route"));

        Assert.Equal(ActivationState.Active, viewModel.ActivationState);
        Assert.True(viewModel.IsActive);
        Assert.Same(scope, viewModel.CurrentActivationScope);
        Assert.Equal("settings-route", viewModel.ActivationContext?.Source);

        await viewModel.DeactivateAsync();

        Assert.Equal(ActivationState.Deactivated, viewModel.ActivationState);
        Assert.False(viewModel.IsActive);
        Assert.Null(viewModel.CurrentActivationScope);
        Assert.True(scope.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task ActivationStateChangedPublishesEachLifecycleTransition()
    {
        var scope = new ActivationScope();
        var viewModel = new TestViewModel();
        var states = new List<ActivationState>();
        viewModel.ActivationStateChanged += (_, _) => states.Add(viewModel.ActivationState);

        await viewModel.ActivateAsync(scope);
        await viewModel.DeactivateAsync();
        await viewModel.DisposeAsync();

        Assert.Equal(
            [
                ActivationState.Activating,
                ActivationState.Active,
                ActivationState.Deactivating,
                ActivationState.Deactivated,
                ActivationState.Disposed,
            ],
            states);
    }

    [Fact]
    public async Task ThrowingActivationStateObserverDoesNotBreakLifecycleOrScopeCleanup()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var scope = new ActivationScope();
        var binding = new TestDisposable();
        var viewModel = new TestViewModel(diagnostics);
        var observedStates = new List<ActivationState>();
        scope.Add(binding);
        viewModel.ActivationStateChanged += (_, _) =>
            throw new InvalidOperationException("observer-failure");
        viewModel.ActivationStateChanged += (_, _) =>
            observedStates.Add(viewModel.ActivationState);

        await viewModel.ActivateAsync(scope);
        await viewModel.DeactivateAsync();

        Assert.Equal(
            [
                ActivationState.Activating,
                ActivationState.Active,
                ActivationState.Deactivating,
                ActivationState.Deactivated,
            ],
            observedStates);
        Assert.True(scope.IsDisposed);
        Assert.True(binding.IsDisposed);
        Assert.Equal(ActivationState.Deactivated, viewModel.ActivationState);
        Assert.Equal(
            4,
            diagnostics.Records.Count(record =>
                record.Code == MvvmDiagnosticIds.ActivationStateNotificationFailed));
    }

    [Fact]
    public async Task ActivateAsyncDisposesScopeAndDoesNotEnterActiveWhenActivationFails()
    {
        using var scope = new ActivationScope();
        var binding = new TestDisposable();
        var exception = new InvalidOperationException("activation failed");
        var viewModel = new FailingActivationViewModel(exception);

        scope.Add(binding);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await viewModel.ActivateAsync(new ActivationContext(scope)));

        Assert.Same(exception, thrown);
        Assert.False(viewModel.IsActive);
        Assert.Equal(ActivationState.Deactivated, viewModel.ActivationState);
        Assert.Null(viewModel.CurrentActivationScope);
        Assert.Null(viewModel.ActivationContext);
        Assert.True(binding.IsDisposed);
        Assert.Equal(typeof(FailingActivationViewModel).FullName, thrown.Data["AtomUI.City.Mvvm.ViewModelType"]);
        Assert.Equal("Activating", thrown.Data["AtomUI.City.Mvvm.ActivationStage"]);
        Assert.Equal(scope.Id, thrown.Data["AtomUI.City.Mvvm.ScopeId"]);
    }

    [Fact]
    public async Task ActivateAsyncWithCanceledTokenDisposesScopeAndDoesNotEnterActive()
    {
        using var scope = new ActivationScope();
        using var cancellation = new CancellationTokenSource();
        var binding = new TestDisposable();
        var viewModel = new TestViewModel();
        await cancellation.CancelAsync();

        scope.Add(binding);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await viewModel.ActivateAsync(new ActivationContext(scope), cancellation.Token));

        Assert.False(viewModel.IsActive);
        Assert.Equal(ActivationState.Deactivated, viewModel.ActivationState);
        Assert.Null(viewModel.CurrentActivationScope);
        Assert.True(binding.IsDisposed);
    }

    [Fact]
    public async Task RepeatedActivateOnActiveViewModelRemainsIdempotentWithCanceledCallerToken()
    {
        var activeScope = new ActivationScope();
        var unusedScope = new ActivationScope();
        using var cancellation = new CancellationTokenSource();
        var viewModel = new TestViewModel();
        await viewModel.ActivateAsync(new ActivationContext(activeScope));
        await cancellation.CancelAsync();

        await viewModel.ActivateAsync(
            new ActivationContext(unusedScope),
            cancellation.Token);

        Assert.True(viewModel.IsActive);
        Assert.Same(activeScope, viewModel.CurrentActivationScope);
        Assert.False(activeScope.IsDisposed);
        Assert.False(unusedScope.IsDisposed);

        unusedScope.Dispose();
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task DeactivateAsyncWithCanceledTokenKeepsActiveScope()
    {
        using var scope = new ActivationScope();
        using var cancellation = new CancellationTokenSource();
        var binding = new TestDisposable();
        var viewModel = new TestViewModel();

        scope.Add(binding);
        await viewModel.ActivateAsync(new ActivationContext(scope));
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await viewModel.DeactivateAsync(cancellation.Token));

        Assert.True(viewModel.IsActive);
        Assert.Equal(ActivationState.Active, viewModel.ActivationState);
        Assert.Same(scope, viewModel.CurrentActivationScope);
        Assert.False(binding.IsDisposed);
    }

    [Fact]
    public async Task DeactivateDisposesActivationResourcesForPresentationBindings()
    {
        using var scope = new ActivationScope();
        var binding = new TestDisposable();
        var viewModel = new TestViewModel();

        scope.Add(binding);
        await viewModel.ActivateAsync(new ActivationContext(scope));
        await viewModel.DeactivateAsync();

        Assert.True(binding.IsDisposed);
    }

    [Fact]
    public async Task DeactivateAsyncCancelsAndDrainsActivationTasks()
    {
        var scope = new ActivationScope();
        var viewModel = new TestViewModel();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = false;

        await viewModel.ActivateAsync(new ActivationContext(scope));
        var task = scope.StartAsync(
            "page-loop",
            async cancellationToken =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                finally
                {
                    stopped = true;
                }
            });

        await started.Task;
        await viewModel.DeactivateAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(stopped);
        Assert.True(scope.IsDisposed);
        Assert.Equal(ActivationState.Deactivated, viewModel.ActivationState);
    }

    [Fact]
    public void ViewModelBaseImplementsActivationScopeOwner()
    {
        var viewModel = new TestViewModel();

        Assert.IsAssignableFrom<IActivationScopeOwner>(viewModel);
    }

    [Fact]
    public async Task DisposeReleasesCurrentActivationResources()
    {
        using var scope = new ActivationScope();
        var binding = new TestDisposable();
        var viewModel = new TestViewModel();

        scope.Add(binding);
        await viewModel.ActivateAsync(new ActivationContext(scope));
        var disposable = Assert.IsAssignableFrom<IDisposable>(viewModel);

        disposable.Dispose();

        Assert.True(binding.IsDisposed);
        Assert.Null(viewModel.CurrentActivationScope);
        Assert.Null(viewModel.ActivationContext);
        Assert.Equal(ActivationState.Disposed, viewModel.ActivationState);
    }

    [Fact]
    public async Task RejectedSynchronousDisposeRetainsScopeForAsyncRecovery()
    {
        var scope = new ActivationScope();
        var resource = new TestAsyncDisposable();
        var viewModel = new TestViewModel();
        scope.AddAsync(resource);
        await viewModel.ActivateAsync(scope);

        Assert.Throws<InvalidOperationException>(() => viewModel.Dispose());

        Assert.False(viewModel.IsDisposed);
        Assert.Equal(ActivationState.Active, viewModel.ActivationState);
        Assert.Same(scope, viewModel.CurrentActivationScope);
        Assert.False(resource.IsDisposed);

        await viewModel.DisposeAsync();

        Assert.True(viewModel.IsDisposed);
        Assert.True(scope.IsDisposed);
        Assert.True(resource.IsDisposed);
    }

    [Fact]
    public void ActivationScopeAccessorRestoresPreviousScope()
    {
        var accessor = new ActivationScopeAccessor();
        using var first = new ActivationScope();
        using var second = new ActivationScope();

        using (accessor.Push(first))
        {
            Assert.Same(first, accessor.Current);

            using (accessor.Push(second))
            {
                Assert.Same(second, accessor.Current);
            }

            Assert.Same(first, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void ActivationContextPropertiesRejectExternalMutation()
    {
        using var scope = new ActivationScope();
        var properties = new Dictionary<string, object?> { ["route"] = "settings" };
        var context = new ActivationContext(scope, properties: properties);
        var exposedProperties = Assert.IsAssignableFrom<IDictionary<string, object?>>(context.Properties);

        properties["route"] = "changed";

        Assert.Throws<NotSupportedException>(() => exposedProperties["route"] = "changed");
        Assert.Equal("settings", context.Properties["route"]);
    }

    public class TestViewModel : ViewModelBase
    {
        private string? _title;

        public TestViewModel(IHostDiagnostics? diagnostics = null)
            : base(diagnostics)
        {
        }

        public string? Title => _title;

        public int ActivatedCount { get; private set; }

        public int DeactivatedCount { get; private set; }

        public bool UpdateTitle(string? value)
        {
            return SetProperty(ref _title, value, nameof(Title));
        }

        public bool UpdateTitleWithPropertyName(string? value, string propertyName)
        {
            return SetProperty(ref _title, value, propertyName);
        }

        protected override ValueTask OnActivatedAsync(
            ActivationContext context,
            CancellationToken cancellationToken)
        {
            ActivatedCount++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnDeactivatedAsync(CancellationToken cancellationToken)
        {
            DeactivatedCount++;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class FailingActivationViewModel(Exception exception) : TestViewModel
    {
        protected override ValueTask OnActivatedAsync(
            ActivationContext context,
            CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private sealed class TestDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class TestAsyncDisposable : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
