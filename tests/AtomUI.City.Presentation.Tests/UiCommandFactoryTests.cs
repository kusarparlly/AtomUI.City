using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;
using AtomUI.City.Presentation;
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
}
