using System.Collections.Concurrent;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Mvvm.Tests;

public sealed class CommandTests
{
    [Fact]
    public void CreateCommandUsesCommunityToolkitRelayCommand()
    {
        var calls = 0;
        var command = CommandFactory.Create(() => calls++);

        Assert.IsAssignableFrom<IRelayCommand>(command);

        command.Execute(null);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void SyncCommandRecordsFailureAndRethrowsOriginalException()
    {
        var exception = new InvalidOperationException("boom");
        var state = new CommandExecutionState("save", typeof(SaveViewModel));
        var command = CommandFactory.Create(
            () => throw exception,
            state: state);

        var thrown = Assert.Throws<InvalidOperationException>(() => command.Execute(null));

        Assert.Same(exception, thrown);
        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Failed, state.LastResult?.Status);
        Assert.Same(exception, state.LastError);
        Assert.Same(exception, state.LastResult?.Error);
        Assert.NotEqual(Guid.Empty, state.LastResult?.OperationId);
        Assert.Equal("save", state.CommandName);
        Assert.Equal(typeof(SaveViewModel), state.OwnerType);
    }

    [Fact]
    public void SyncCommandNotifiesCanExecuteChanges()
    {
        var enabled = false;
        var changes = 0;
        var state = new CommandExecutionState("refresh", typeof(SaveViewModel));
        var command = CommandFactory.Create(
            () => { },
            canExecute: () => enabled,
            state: state);

        command.CanExecuteChanged += (_, _) => changes++;

        Assert.False(command.CanExecute(null));

        enabled = true;
        command.NotifyCanExecuteChanged();

        Assert.True(command.CanExecute(null));
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task AsyncCommandTracksSuccessfulExecution()
    {
        var state = new CommandExecutionState();
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            },
            state);

        Assert.IsAssignableFrom<IAsyncRelayCommand>(command);

        await command.ExecuteAsync(null);

        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task AsyncCommandCompletesNotificationsOnCallingSynchronizationContext()
    {
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var context = new PumpSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(context);
                var callerThreadId = Environment.CurrentManagedThreadId;
                var completionThreadId = 0;
                var command = CommandFactory.CreateAsync(async _ =>
                {
                    await Task.Run(static () => { });
                });
                command.CanExecuteChanged += (_, _) =>
                {
                    if (!command.IsRunning)
                    {
                        completionThreadId = Environment.CurrentManagedThreadId;
                    }
                };

                var execution = command.ExecuteAsync(null);
                context.RunUntil(execution);
                result.TrySetResult(completionThreadId == callerThreadId);
            }
            catch (Exception exception)
            {
                result.TrySetException(exception);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        })
        {
            IsBackground = true,
            Name = "MVVM command synchronization context test",
        };

        thread.Start();
        Assert.True(await result.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void ActivationNotificationsArePostedThroughConfiguredDispatcher()
    {
        var scope = new ActivationScope();
        var owner = new TestActivationOwner(scope);
        var dispatcher = new QueueingUiDispatcher();
        var notifications = 0;
        var command = CommandFactory.CreateAsync(
            _ => Task.CompletedTask,
            canExecute: null,
            activationOwner: owner,
            notificationDispatcher: dispatcher);
        command.CanExecuteChanged += (_, _) => notifications++;

        owner.RaiseActivationStateChanged();

        Assert.Equal(0, notifications);
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.Drain();

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void NotificationDispatcherFailureWritesStructuredDiagnostic()
    {
        var scope = new ActivationScope();
        var owner = new TestActivationOwner(scope);
        var diagnostics = new InMemoryHostDiagnostics();
        var command = CommandFactory.CreateAsync(
            _ => Task.CompletedTask,
            canExecute: null,
            state: new CommandExecutionState("refresh", typeof(SaveViewModel)),
            activationOwner: owner,
            diagnostics: diagnostics,
            notificationDispatcher: new ThrowingUiDispatcher());
        command.CanExecuteChanged += (_, _) => { };

        owner.RaiseActivationStateChanged();

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.CommandNotificationFailed
                && record.Context?["commandName"] == "refresh"
                && record.Context["dispatcherType"] == typeof(ThrowingUiDispatcher).FullName
                && record.Context["exceptionType"] == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task InlineNotificationFailureIsDiagnosedWithoutChangingCommandResult()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new CommandExecutionState("inline-notification", typeof(SaveViewModel));
        var dispatcher = new InlineUiDispatcher();
        var command = CommandFactory.CreateAsync(
            _ => Task.CompletedTask,
            state,
            diagnostics: diagnostics,
            notificationDispatcher: dispatcher);
        var observable = Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(command);
        observable.PropertyChanged += static (_, args) =>
        {
            if (args.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
            {
                throw new InvalidOperationException("inline-notification-failed");
            }
        };

        await command.ExecuteAsync(null);

        Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
        Assert.True(dispatcher.InvokeCount >= 2);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.CommandNotificationFailed
                && record.Context?["commandName"] == "inline-notification"
                && record.Context["exceptionType"] == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task QueuedNotificationFailureIsDiagnosedWithoutChangingCommandResult()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new CommandExecutionState("queued-notification", typeof(SaveViewModel));
        var dispatcher = new QueueingUiDispatcher();
        var command = CommandFactory.CreateAsync(
            _ => Task.CompletedTask,
            state,
            diagnostics: diagnostics,
            notificationDispatcher: dispatcher);
        var observable = Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(command);
        observable.PropertyChanged += static (_, args) =>
        {
            if (args.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
            {
                throw new InvalidOperationException("queued-notification-failed");
            }
        };

        await command.ExecuteAsync(null);

        Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
        Assert.Empty(diagnostics.Records);
        Assert.True(dispatcher.PendingCount >= 2);

        dispatcher.Drain();

        await WaitUntilAsync(() => diagnostics.Records.Any(
            record => record.Code == MvvmDiagnosticIds.CommandNotificationFailed));

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.CommandNotificationFailed
                && record.Context?["commandName"] == "queued-notification"
                && record.Context["exceptionType"] == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task NotificationFailureDoesNotReplaceBusinessFailure()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new CommandExecutionState("failed-notification", typeof(SaveViewModel));
        var businessFailure = new ApplicationException("business-failed");
        var command = CommandFactory.CreateAsync(
            _ => Task.FromException(businessFailure),
            state,
            diagnostics: diagnostics,
            notificationDispatcher: new InlineUiDispatcher());
        var observable = Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(command);
        observable.PropertyChanged += static (_, _) =>
            throw new InvalidOperationException("failure-notification-failed");

        var thrown = await Assert.ThrowsAsync<ApplicationException>(() => command.ExecuteAsync(null));

        Assert.Same(businessFailure, thrown);
        Assert.Equal(OperationStatus.Failed, state.LastResult?.Status);
        Assert.Same(businessFailure, state.LastError);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == MvvmDiagnosticIds.CommandNotificationFailed);
    }

    [Fact]
    public async Task NotificationFailureDoesNotReplaceCommandCancellation()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var state = new CommandExecutionState("canceled-notification", typeof(SaveViewModel));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
            state,
            diagnostics: diagnostics,
            notificationDispatcher: new InlineUiDispatcher());
        var observable = Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(command);
        observable.PropertyChanged += static (_, _) =>
            throw new InvalidOperationException("cancellation-notification-failed");

        var execution = command.ExecuteAsync(null);
        await started.Task;

        var cancelException = Record.Exception(command.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);

        Assert.Null(cancelException);
        Assert.Equal(OperationStatus.Canceled, state.LastResult?.Status);
        Assert.Null(state.LastError);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == MvvmDiagnosticIds.CommandNotificationFailed);
    }

    [Fact]
    public async Task AsyncCommandRejectsConcurrentExecution()
    {
        var state = new CommandExecutionState("load", typeof(SaveViewModel));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                executions++;
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            },
            state);

        var first = command.ExecuteAsync(null);
        await started.Task;

        var second = command.ExecuteAsync(null);
        await second;

        Assert.True(state.IsExecuting);
        Assert.Equal(1, executions);
        Assert.Equal(1, state.RejectedExecutionCount);
        Assert.Equal(OperationStatus.Rejected, state.LastRejectedResult?.Status);
        Assert.NotEqual(Guid.Empty, state.LastRejectedResult?.OperationId);

        release.SetResult();
        await first;

        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
    }

    [Fact]
    public async Task AsyncCommandRecordsFailureAndRethrowsOriginalException()
    {
        var exception = new InvalidOperationException("boom");
        var state = new CommandExecutionState();
        var command = CommandFactory.CreateAsync(
            _ => throw exception,
            state);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(null));

        Assert.Same(exception, thrown);
        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Failed, state.LastResult?.Status);
        Assert.IsType<InvalidOperationException>(state.LastError);
    }

    [Fact]
    public async Task AsyncCommandIsCanceledWhenActivationScopeStops()
    {
        await using var scope = new ActivationScope();
        var state = new CommandExecutionState();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                started.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            },
            state,
            scope);

        var execution = command.ExecuteAsync(null);
        await started.Task;

        await scope.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);

        Assert.Equal(OperationStatus.Canceled, state.LastResult?.Status);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task TypedAsyncCommandPassesParameterAndUsesBusinessCanExecute()
    {
        var enabled = false;
        var observed = 0;
        var command = CommandFactory.CreateAsync<int>(
            (value, _) =>
            {
                observed = value;
                return Task.CompletedTask;
            },
            value => enabled && value > 0);

        Assert.False(command.CanExecute(42));

        enabled = true;
        command.NotifyCanExecuteChanged();

        Assert.True(command.CanExecute(42));
        await command.ExecuteAsync(42);
        Assert.Equal(42, observed);
    }

    [Fact]
    public async Task RejectByKeyRejectsSameKeyAndAllowsDifferentKeys()
    {
        var state = new CommandExecutionState("row-action", typeof(SaveViewModel));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        var command = CommandFactory.CreateAsync<int>(
            async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref executions) == 2)
                {
                    started.SetResult();
                }

                await release.Task.WaitAsync(cancellationToken);
            },
            state: state,
            concurrencyPolicy: CommandConcurrencyPolicy.RejectByKey,
            concurrencyKeySelector: value => value);

        var first = command.ExecuteAsync(1);
        var duplicate = command.ExecuteAsync(1);
        var secondKey = command.ExecuteAsync(2);

        await duplicate;
        await started.Task;

        Assert.Equal(2, executions);
        Assert.Equal(2, state.ActiveExecutionCount);
        Assert.Equal(1, state.RejectedExecutionCount);

        release.SetResult();
        await Task.WhenAll(first, secondKey);

        Assert.False(state.IsExecuting);
        Assert.Equal(0, state.ActiveExecutionCount);
    }

    [Fact]
    public async Task AllowPolicyTracksConcurrentExecutions()
    {
        var state = new CommandExecutionState("parallel", typeof(SaveViewModel));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        var command = CommandFactory.CreateAsync<int>(
            async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref executions) == 2)
                {
                    started.SetResult();
                }

                await release.Task.WaitAsync(cancellationToken);
            },
            state: state,
            concurrencyPolicy: CommandConcurrencyPolicy.Allow);

        var first = command.ExecuteAsync(1);
        var second = command.ExecuteAsync(1);
        await started.Task;

        Assert.Equal(2, state.ActiveExecutionCount);

        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(0, state.ActiveExecutionCount);
    }

    [Fact]
    public async Task OwnerBoundCommandRejectsInactiveAndUsesCurrentScopeAfterReactivation()
    {
        var owner = new SaveViewModel();
        var state = new CommandExecutionState("refresh", typeof(SaveViewModel));
        var command = CommandFactory.CreateAsync(
            _ => Task.CompletedTask,
            canExecute: null,
            state: state,
            activationOwner: owner);

        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync(null);
        Assert.Equal(1, state.RejectedExecutionCount);

        var scopeA = new ActivationScope();
        await owner.ActivateAsync(scopeA);
        Assert.True(command.CanExecute(null));
        await command.ExecuteAsync(null);
        Assert.True(scopeA.ActiveTaskCount == 0);

        await owner.DeactivateAsync();
        Assert.False(command.CanExecute(null));

        var scopeB = new ActivationScope();
        await owner.ActivateAsync(scopeB);
        await command.ExecuteAsync(null);

        Assert.True(scopeA.IsDisposed);
        Assert.True(scopeB.ActiveTaskCount == 0);
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task OwnerBoundCommandNotifiesCanExecuteWhenActivationChanges()
    {
        var owner = new SaveViewModel();
        var command = CommandFactory.CreateAsync(
            _ => Task.CompletedTask,
            canExecute: null,
            activationOwner: owner);
        var notifications = 0;
        command.CanExecuteChanged += (_, _) => notifications++;

        var scope = new ActivationScope();
        await owner.ActivateAsync(scope);
        Assert.True(command.CanExecute(null));

        await owner.DeactivateAsync();
        Assert.False(command.CanExecute(null));
        Assert.Equal(4, notifications);
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task InactiveOwnerRejectionWritesStructuredDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var owner = new SaveViewModel();
        var command = CommandFactory.CreateAsync(
            _ => Task.CompletedTask,
            canExecute: null,
            state: new CommandExecutionState("refresh", typeof(SaveViewModel)),
            activationOwner: owner,
            diagnostics: diagnostics);

        await command.ExecuteAsync(null);

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.CommandInactive
                && record.Context?["commandName"] == "refresh"
                && record.Context["activationState"] == ActivationState.Constructed.ToString());
    }

    [Fact]
    public async Task ScopeClosingDoesNotMisclassifyBusinessInvalidOperationException()
    {
        var scope = new ActivationScope();
        var owner = new TestActivationOwner(scope);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("business-failure-during-shutdown");
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw expected;
                }
            },
            canExecute: null,
            activationOwner: owner);

        var execution = command.ExecuteAsync(null);
        await started.Task;
        var disposal = scope.DisposeAsync().AsTask();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => execution);
        Assert.Same(expected, thrown);
        await Assert.ThrowsAsync<AggregateException>(() => disposal);
    }

    [Fact]
    public async Task ICommandExecuteDeliversOriginalExceptionToHandlerExactlyOnce()
    {
        var exception = new InvalidOperationException("boom");
        var handler = new RecordingExceptionHandler();
        var command = CommandFactory.CreateAsync(
            _ => throw exception,
            canExecute: null,
            exceptionHandler: handler);

        ((System.Windows.Input.ICommand)command).Execute(null);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecutionTask!);
        Assert.Same(exception, thrown);
        Assert.Single(handler.Contexts);
        Assert.Same(exception, handler.Contexts[0].Exception);
    }

    [Fact]
    public async Task FailedICommandExecuteRequiresApplicationExceptionHandler()
    {
        var original = new InvalidOperationException("boom");
        var command = CommandFactory.CreateAsync(_ => throw original);

        ((System.Windows.Input.ICommand)command).Execute(null);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecutionTask!);

        Assert.Contains(nameof(ICommandExceptionHandler), exception.Message, StringComparison.Ordinal);
        Assert.Same(original, exception.InnerException);
    }

    [Fact]
    public async Task ExceptionHandlerFailureIsDiagnosedWithoutReplacingOriginalException()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var original = new InvalidOperationException("command-failure");
        var command = CommandFactory.CreateAsync(
            _ => throw original,
            canExecute: null,
            state: new CommandExecutionState("save", typeof(SaveViewModel)),
            exceptionHandler: new ThrowingExceptionHandler(),
            diagnostics: diagnostics);

        ((System.Windows.Input.ICommand)command).Execute(null);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecutionTask!);

        Assert.Same(original, thrown);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == MvvmDiagnosticIds.CommandExceptionHandlerFailed
                && record.Context?["commandName"] == "save"
                && record.Context["exceptionType"] == typeof(ApplicationException).FullName);
    }

    [Fact]
    public async Task ICommandCancellationDoesNotInvokeExceptionHandler()
    {
        var handler = new RecordingExceptionHandler();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
            canExecute: null,
            exceptionHandler: handler);

        ((System.Windows.Input.ICommand)command).Execute(null);
        await started.Task;
        command.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecutionTask!);
        Assert.Empty(handler.Contexts);
    }

    [Fact]
    public void OperationScopeStartsRunningAndCompletesWithStableResult()
    {
        using var operation = OperationScope.Start(CancellationToken.None);

        Assert.NotEqual(Guid.Empty, operation.Id);
        Assert.Equal(OperationStatus.Running, operation.Status);
        Assert.Null(operation.Result);
        Assert.Null(operation.Error);
        Assert.True(operation.Elapsed >= TimeSpan.Zero);

        var result = operation.Complete();
        var elapsed = operation.Elapsed;

        Thread.Sleep(5);

        Assert.Equal(OperationStatus.Completed, operation.Status);
        Assert.Same(result, operation.Result);
        Assert.Null(operation.Error);
        Assert.Equal(result.Elapsed, operation.Elapsed);
        Assert.Equal(elapsed, operation.Elapsed);
    }

    [Fact]
    public void OperationScopeKeepsFirstTerminalResult()
    {
        using var operation = OperationScope.Start(CancellationToken.None);
        var result = operation.Complete();

        Assert.Same(result, operation.Fail(new InvalidOperationException("late")));
        Assert.Same(result, operation.Cancel());
        Assert.Same(result, operation.Reject());
        Assert.Equal(OperationStatus.Completed, operation.Status);
        Assert.Null(operation.Error);
        Assert.Equal(result.Elapsed, operation.Elapsed);
    }

    [Fact]
    public void OperationScopeMarksCanceledBeforeNotifyingCancellationCallbacks()
    {
        using var operation = OperationScope.Start(CancellationToken.None);
        OperationStatus? observedStatus = null;
        using var registration = operation.CancellationToken.Register(() =>
        {
            observedStatus = operation.Status;
        });

        var result = operation.Cancel();

        Assert.Equal(OperationStatus.Canceled, result.Status);
        Assert.Equal(OperationStatus.Canceled, observedStatus);
        Assert.True(operation.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void OperationScopeLinksExternalCancellationAfterStateChange()
    {
        using var cancellation = new CancellationTokenSource();
        using var operation = OperationScope.Start(cancellation.Token);
        OperationStatus? observedStatus = null;
        using var registration = operation.CancellationToken.Register(() =>
        {
            observedStatus = operation.Status;
        });

        cancellation.Cancel();

        Assert.Equal(OperationStatus.Canceled, operation.Status);
        Assert.Equal(OperationStatus.Canceled, operation.Result?.Status);
        Assert.Equal(OperationStatus.Canceled, observedStatus);
    }

    [Fact]
    public void OperationScopeDisposeCancelsActiveOperationAndRejectsMutation()
    {
        var operation = OperationScope.Start(CancellationToken.None);
        OperationStatus? observedStatus = null;
        using var registration = operation.CancellationToken.Register(() =>
        {
            observedStatus = operation.Status;
        });

        operation.Dispose();
        operation.Dispose();

        Assert.True(operation.IsDisposed);
        Assert.Equal(OperationStatus.Canceled, operation.Status);
        Assert.Equal(OperationStatus.Canceled, operation.Result?.Status);
        Assert.Equal(OperationStatus.Canceled, observedStatus);
        Assert.Throws<ObjectDisposedException>(() => operation.Complete());
        Assert.Throws<ObjectDisposedException>(() => operation.Cancel());
    }

    [Fact]
    public void CommandGroupExecutesOnlyActiveCommands()
    {
        var firstCalls = 0;
        var secondCalls = 0;
        var group = new CommandGroup();

        group.Register(CommandFactory.Create(() => firstCalls++), isActive: () => true);
        group.Register(CommandFactory.Create(() => secondCalls++), isActive: () => false);

        group.Execute(null);

        Assert.Equal(1, firstCalls);
        Assert.Equal(0, secondCalls);
    }

    [Fact]
    public void CommandGroupRegistrationIsRemovedWithActivationScope()
    {
        var calls = 0;
        var scope = new ActivationScope();
        var group = new CommandGroup();

        group.Register(CommandFactory.Create(() => calls++), activationScope: scope);
        scope.Dispose();
        group.Execute(null);

        Assert.Equal(0, calls);
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

    private sealed class PumpSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _queue.Add((callback, state));
        }

        public void RunUntil(Task task)
        {
            while (!task.IsCompleted)
            {
                if (!_queue.TryTake(out var work, TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("No synchronization-context continuation arrived.");
                }

                work.Callback(work.State);
            }

            task.GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _queue.Dispose();
        }
    }

    private sealed class RecordingExceptionHandler : ICommandExceptionHandler
    {
        public List<CommandExceptionContext> Contexts { get; } = [];

        public ValueTask HandleAsync(
            CommandExceptionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Contexts.Add(context);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingExceptionHandler : ICommandExceptionHandler
    {
        public ValueTask HandleAsync(
            CommandExceptionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new ApplicationException("handler-failure");
        }
    }

    private sealed class TestActivationOwner : IActivationScopeOwner
    {
        public TestActivationOwner(IActivationScope activationScope)
        {
            CurrentActivationScope = activationScope;
        }

        public ActivationState ActivationState => ActivationState.Active;

        public bool IsActive => true;

        public IActivationScope? CurrentActivationScope { get; }

        public event EventHandler? ActivationStateChanged;

        public void RaiseActivationStateChanged()
        {
            ActivationStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class QueueingUiDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _queue = [];

        public int PendingCount => _queue.Count;

        public bool CheckAccess() =>
            throw new InvalidOperationException("Command notification code must not query CheckAccess.");

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource();
            _queue.Enqueue(() =>
            {
                try
                {
                    callback();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });
            return new ValueTask(completion.Task);
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<T>();
            _queue.Enqueue(() =>
            {
                try
                {
                    completion.TrySetResult(callback());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });
            return new ValueTask<T>(completion.Task);
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource();
            _queue.Enqueue(() => CompletePostedCallback(callback, cancellationToken, completion));
            return new ValueTask(completion.Task);
        }

        public void Drain()
        {
            while (_queue.TryDequeue(out var callback))
            {
                callback();
            }
        }

        private static void CompletePostedCallback(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken,
            TaskCompletionSource completion)
        {
            try
            {
                var operation = callback(cancellationToken);
                if (operation.IsCompletedSuccessfully)
                {
                    completion.TrySetResult();
                    return;
                }

                _ = CompletePostedCallbackAsync(operation, completion);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private static async Task CompletePostedCallbackAsync(
            ValueTask operation,
            TaskCompletionSource completion)
        {
            try
            {
                await operation.ConfigureAwait(false);
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }
    }

    private sealed class InlineUiDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public bool CheckAccess() =>
            throw new InvalidOperationException("Command notification code must not query CheckAccess.");

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
        {
            InvokeCount++;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                callback();
                return ValueTask.CompletedTask;
            }
            catch (Exception exception)
            {
                return ValueTask.FromException(exception);
            }
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
        {
            InvokeCount++;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(callback());
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<T>(exception);
            }
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return callback(cancellationToken);
            }
            catch (Exception exception)
            {
                return ValueTask.FromException(exception);
            }
        }
    }

    private sealed class ThrowingUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => false;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("dispatcher-stopped");
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("dispatcher-stopped");
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("dispatcher-stopped");
        }
    }

    public sealed class SaveViewModel : ViewModelBase;
}
