using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation.Tests;

public sealed class UiCommandFactoryTests
{
    [Fact]
    public async Task AggregateRegistrationProvidesUiCommandFactoryWithInternalImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUiDispatcher>(new RecordingUiDispatcher());
        services.AddPresentation();
        services.AddPresentation();

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IUiCommandFactory>();

        Assert.False(factory.GetType().IsPublic);
        Assert.Same(factory, provider.GetRequiredService<IUiCommandFactory>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUiCommandFactory));
    }

    [Fact]
    public async Task AggregateRegistrationPreservesExistingUiCommandFactory()
    {
        var existing = new StubUiCommandFactory();
        var services = new ServiceCollection();
        services.AddSingleton<IUiCommandFactory>(existing);

        services.AddPresentation();

        await using var provider = services.BuildServiceProvider();
        Assert.Same(existing, provider.GetRequiredService<IUiCommandFactory>());
    }

    [Fact]
    public void PublicFactoryDoesNotExposeInfrastructureParameters()
    {
        var parameters = typeof(IUiCommandFactory)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(IUiDispatcher), parameters);
        Assert.DoesNotContain(typeof(IHostDiagnostics), parameters);
    }

    [Fact]
    public async Task SynchronousOverloadsForwardExecutionStateAndDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var factory = CreateFactory(new RecordingUiDispatcher(), diagnostics, out var provider);
        await using (provider)
        {
            var untypedCalls = 0;
            var untypedState = new CommandExecutionState("sync");
            var untyped = factory.Create(() => untypedCalls++, state: untypedState);
            string? typedValue = null;
            var typedState = new CommandExecutionState("typed-sync");
            var typed = factory.Create<string>(value => typedValue = value, state: typedState);

            untyped.Execute(null);
            typed.Execute("value");

            Assert.Equal(1, untypedCalls);
            Assert.Equal("value", typedValue);
            Assert.Equal(OperationStatus.Completed, untypedState.LastResult?.Status);
            Assert.Equal(OperationStatus.Completed, typedState.LastResult?.Status);
        }
    }

    [Fact]
    public async Task AllAsyncOverloadsUseTheInjectedDispatcher()
    {
        var dispatcher = new RecordingUiDispatcher();
        var factory = CreateFactory(dispatcher, diagnostics: null, out var provider);
        await using (provider)
        {
            var fixedState = new CommandExecutionState("fixed");
            var fixedScope = new ActivationScope();
            var fixedCommand = factory.CreateAsync(
                _ => Task.CompletedTask,
                fixedState,
                fixedScope);

            var ownerState = new CommandExecutionState("owner");
            var ownerCommand = factory.CreateAsync(
                _ => Task.CompletedTask,
                canExecute: () => true,
                state: ownerState);

            string? typedValue = null;
            var typedState = new CommandExecutionState("typed");
            var typedCommand = factory.CreateAsync<string>(
                (value, _) =>
                {
                    typedValue = value;
                    return Task.CompletedTask;
                },
                state: typedState);

            var beforeFixed = dispatcher.InvokeCount;
            await fixedCommand.ExecuteAsync(null);
            Assert.True(dispatcher.InvokeCount > beforeFixed);

            var beforeOwner = dispatcher.InvokeCount;
            await ownerCommand.ExecuteAsync(null);
            Assert.True(dispatcher.InvokeCount > beforeOwner);

            var beforeTyped = dispatcher.InvokeCount;
            await typedCommand.ExecuteAsync("typed-value");
            Assert.True(dispatcher.InvokeCount > beforeTyped);

            Assert.Equal("typed-value", typedValue);
            Assert.Equal(OperationStatus.Completed, fixedState.LastResult?.Status);
            Assert.Equal(OperationStatus.Completed, ownerState.LastResult?.Status);
            Assert.Equal(OperationStatus.Completed, typedState.LastResult?.Status);

            await fixedScope.DisposeAsync();
        }
    }

    [Fact]
    public async Task NotificationFailureUsesInjectedDiagnosticsWithoutChangingCommandResult()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new CommandExecutionState("ui-factory-notification");
        var factory = CreateFactory(new FailingUiDispatcher(), diagnostics, out var provider);
        await using (provider)
        {
            var command = factory.CreateAsync(_ => Task.CompletedTask, state);

            await command.ExecuteAsync(null);

            await WaitUntilAsync(() => diagnostics.Records.Any(
                record => record.Code == MvvmDiagnosticIds.CommandNotificationFailed));
            Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
        }
    }

    [Fact]
    public async Task AvaloniaDispatcherDeliversBackgroundCommandNotificationsOnItsUiThread()
    {
        var avaloniaDispatcher = Dispatcher.CurrentDispatcher;
        var uiThreadId = Environment.CurrentManagedThreadId;
        var notificationThreads = new System.Collections.Concurrent.ConcurrentQueue<int>();
        var propertyNotifications = new System.Collections.Concurrent.ConcurrentQueue<string?>();
        var canExecuteNotifications = 0;
        var dispatcher = new AvaloniaUiDispatcher(avaloniaDispatcher);
        var factory = CreateFactory(dispatcher, diagnostics: null, out var provider);
        await using (provider)
        {
            var command = factory.CreateAsync(async _ =>
            {
                await Task.Delay(10).ConfigureAwait(false);
            });
            var observable = Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(command);
            observable.PropertyChanged += (_, args) =>
            {
                propertyNotifications.Enqueue(args.PropertyName);
                notificationThreads.Enqueue(Environment.CurrentManagedThreadId);
            };
            command.CanExecuteChanged += (_, _) =>
            {
                Interlocked.Increment(ref canExecuteNotifications);
                notificationThreads.Enqueue(Environment.CurrentManagedThreadId);
            };

            var execution = Task.Run(() => command.ExecuteAsync(null));

            PumpDispatcherUntil(
                avaloniaDispatcher,
                () => execution.IsCompleted &&
                      HasCompleteNotificationSet(propertyNotifications) &&
                      Volatile.Read(ref canExecuteNotifications) >= 2);
            await execution;

            AssertNotificationSet(propertyNotifications, expectedCountPerProperty: 2);
            Assert.Equal(2, canExecuteNotifications);
            Assert.All(notificationThreads, threadId => Assert.Equal(uiThreadId, threadId));
        }
    }

    [Theory]
    [InlineData(PresentationRuntimeState.Stopped)]
    [InlineData(PresentationRuntimeState.Faulted)]
    public async Task TerminalRuntimeNotificationFailureIsDiagnosedWithoutChangingCommandResult(
        PresentationRuntimeState runtimeState)
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var runtime = new TerminalPresentationRuntime(runtimeState);
        var dispatcher = new AvaloniaUiDispatcher(
            Dispatcher.CurrentDispatcher,
            runtime,
            diagnostics);
        var state = new CommandExecutionState($"terminal-{runtimeState}");
        var factory = CreateFactory(dispatcher, diagnostics, out var provider);
        await using (provider)
        {
            var command = factory.CreateAsync(_ => Task.CompletedTask, state);

            await command.ExecuteAsync(null);

            await WaitUntilAsync(() => diagnostics.Records.Any(
                record => record.Code == MvvmDiagnosticIds.CommandNotificationFailed));

            Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
            Assert.Contains(
                diagnostics.Records,
                record => record.Code == PresentationDiagnosticIds.DispatcherOperationRejected);
            Assert.Contains(
                diagnostics.Records,
                record =>
                    record.Code == MvvmDiagnosticIds.CommandNotificationFailed &&
                    record.Context?["exceptionType"] == typeof(PresentationException).FullName);
        }
    }

    private static IUiCommandFactory CreateFactory(
        IUiDispatcher dispatcher,
        IHostDiagnostics? diagnostics,
        out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dispatcher);
        if (diagnostics is not null)
        {
            services.AddSingleton<IHostDiagnostics>(diagnostics);
        }

        services.AddPresentation();
        provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IUiCommandFactory>();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The expected asynchronous condition was not observed.");
            }

            await Task.Delay(10);
        }
    }

    private static void PumpDispatcherUntil(Dispatcher dispatcher, Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!predicate() && DateTimeOffset.UtcNow < deadline)
        {
            dispatcher.RunJobs(DispatcherPriority.Default);
            Thread.Sleep(TimeSpan.FromMilliseconds(10));
        }

        dispatcher.RunJobs(DispatcherPriority.Default);
        Assert.True(predicate(), "The expected Avalonia dispatcher work did not complete.");
    }

    private static bool HasCompleteNotificationSet(IEnumerable<string?> notifications)
    {
        var snapshot = notifications.ToArray();
        return snapshot.Count(name => name == nameof(IAsyncRelayCommand.ExecutionTask)) >= 2 &&
               snapshot.Count(name => name == nameof(IAsyncRelayCommand.CanBeCanceled)) >= 2 &&
               snapshot.Count(name => name == nameof(IAsyncRelayCommand.IsCancellationRequested)) >= 2 &&
               snapshot.Count(name => name == nameof(IAsyncRelayCommand.IsRunning)) >= 2;
    }

    private static void AssertNotificationSet(
        IEnumerable<string?> notifications,
        int expectedCountPerProperty)
    {
        var snapshot = notifications.ToArray();
        Assert.Equal(
            expectedCountPerProperty,
            snapshot.Count(name => name == nameof(IAsyncRelayCommand.ExecutionTask)));
        Assert.Equal(
            expectedCountPerProperty,
            snapshot.Count(name => name == nameof(IAsyncRelayCommand.CanBeCanceled)));
        Assert.Equal(
            expectedCountPerProperty,
            snapshot.Count(name => name == nameof(IAsyncRelayCommand.IsCancellationRequested)));
        Assert.Equal(
            expectedCountPerProperty,
            snapshot.Count(name => name == nameof(IAsyncRelayCommand.IsRunning)));
        Assert.Equal(expectedCountPerProperty * 4, snapshot.Length);
    }

    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public bool CheckAccess() =>
            throw new InvalidOperationException("UI commands must not query CheckAccess before dispatching notifications.");

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
        {
            InvokeCount++;
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
        {
            InvokeCount++;
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

    private sealed class FailingUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => false;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken) =>
            ValueTask.FromException(new InvalidOperationException("notification-failed"));

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken) =>
            ValueTask.FromException<T>(new InvalidOperationException("notification-failed"));

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("notification-failed"));
    }

    private sealed class StubUiCommandFactory : IUiCommandFactory
    {
        public IRelayCommand Create(
            Action execute,
            Func<bool>? canExecute = null,
            CommandExecutionState? state = null) =>
            throw new NotSupportedException();

        public IRelayCommand<T> Create<T>(
            Action<T?> execute,
            Predicate<T?>? canExecute = null,
            CommandExecutionState? state = null) =>
            throw new NotSupportedException();

        public IAsyncRelayCommand CreateAsync(
            Func<CancellationToken, Task> execute,
            CommandExecutionState? state = null,
            IActivationScope? activationScope = null) =>
            throw new NotSupportedException();

        public IAsyncRelayCommand CreateAsync(
            Func<CancellationToken, Task> execute,
            Func<bool>? canExecute,
            CommandExecutionState? state = null,
            IActivationScopeOwner? activationOwner = null,
            CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
            ICommandExceptionHandler? exceptionHandler = null) =>
            throw new NotSupportedException();

        public IAsyncRelayCommand<T> CreateAsync<T>(
            Func<T?, CancellationToken, Task> execute,
            Predicate<T?>? canExecute = null,
            CommandExecutionState? state = null,
            IActivationScopeOwner? activationOwner = null,
            CommandConcurrencyPolicy concurrencyPolicy = CommandConcurrencyPolicy.Reject,
            Func<T?, object?>? concurrencyKeySelector = null,
            ICommandExceptionHandler? exceptionHandler = null) =>
            throw new NotSupportedException();
    }

    private sealed class TerminalPresentationRuntime : IPresentationRuntime
    {
        public TerminalPresentationRuntime(PresentationRuntimeState state)
        {
            State = state;
        }

        public PresentationRuntimeState State { get; }

        public bool IsReady => false;

        public LifecycleScope? PresentationScope => null;

        public IApplicationLifetime? ApplicationLifetime => null;

        public IReadOnlyCollection<WindowSession> Windows => Array.Empty<WindowSession>();

        public ViewModelCompositionOwner ViewModels => throw new NotSupportedException();

        public void Attach(
            IApplicationLifetime applicationLifetime,
            LifecycleScope hostScope,
            string presentationScopeId = "presentation") =>
            throw new NotSupportedException();

        public WindowSession RegisterWindow(Window window, string windowId) =>
            throw new NotSupportedException();

        public ValueTask StartAsync(
            LifecycleScope applicationScope,
            string presentationScopeId = "presentation",
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public LifecycleScope CreateWindowScope(string windowScopeId) =>
            throw new NotSupportedException();

        public ValueTask StopAsync(CancellationToken waitCancellationToken = default) =>
            throw new NotSupportedException();
    }
}
