using System.Collections.Concurrent;
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
    public void SyncCommandCapturesFailureWithoutThrowing()
    {
        var exception = new InvalidOperationException("boom");
        var state = new CommandExecutionState("save", typeof(SaveViewModel));
        var command = CommandFactory.Create(
            () => throw exception,
            state: state);

        command.Execute(null);

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
    public async Task AsyncCommandCapturesFailureWithoutThrowing()
    {
        var state = new CommandExecutionState();
        var command = CommandFactory.CreateAsync(
            _ => throw new InvalidOperationException("boom"),
            state);

        await command.ExecuteAsync(null);

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
        await execution;

        Assert.Equal(OperationStatus.Canceled, state.LastResult?.Status);
        Assert.Null(state.LastError);
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

    private sealed class SaveViewModel;
}
