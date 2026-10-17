using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Core.Tests;

public sealed class LifecycleMiddlewarePipelineTests
{
    [Fact]
    public void LifecycleContextGeneratesOrPreservesOperationId()
    {
        var generated = new LifecycleContext(LifecycleStages.ApplicationStart);
        var explicitContext = new LifecycleContext(
            LifecycleStages.ApplicationStart,
            operationId: "explicit-operation");

        Assert.Matches("^[0-9a-f]{32}$", generated.OperationId);
        Assert.Equal("explicit-operation", explicitContext.OperationId);
        Assert.Throws<ArgumentException>(() => new LifecycleContext(
            LifecycleStages.ApplicationStart,
            operationId: " "));
    }

    [Fact]
    public void LifecycleBoundariesRejectDefaultStageValues()
    {
        Assert.Throws<ArgumentException>(() => new LifecycleContext(default));

        var builder = new LifecyclePipelineBuilder();
        Assert.Throws<ArgumentException>(() => builder.Use(default, static (_, _, _) => default));
        Assert.Throws<ArgumentException>(() =>
            builder.Use<LifecycleBoundariesRejectDefaultStageValuesMiddleware>(
                default,
                static (_, _, _) => default));
    }

    [Fact]
    public async Task PipelineExecutesMiddlewareInRegistrationOrderAroundTerminalHandler()
    {
        var trace = new List<string>();
        var pipeline = new LifecyclePipelineBuilder()
            .Use(async (context, next, cancellationToken) =>
            {
                trace.Add("one.before");
                await next().ConfigureAwait(false);
                trace.Add("one.after");
            })
            .Use(async (context, next, cancellationToken) =>
            {
                trace.Add("two.before");
                await next().ConfigureAwait(false);
                trace.Add("two.after");
            })
            .Build((context, cancellationToken) =>
            {
                trace.Add("terminal");

                return ValueTask.CompletedTask;
            });

        await pipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));

        Assert.Equal(
            ["one.before", "two.before", "terminal", "two.after", "one.after"],
            trace);
    }

    [Fact]
    public async Task MiddlewareCanReturnNextDirectly()
    {
        var terminalCalls = 0;
        var pipeline = new LifecyclePipelineBuilder()
            .Use(static (_, next, _) => next())
            .Build((_, _) =>
            {
                Interlocked.Increment(ref terminalCalls);
                return ValueTask.CompletedTask;
            });

        await pipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));

        Assert.Equal(1, terminalCalls);
    }

    [Fact]
    public async Task PipelineDrainsAndRejectsFireAndForgetNext()
    {
        var terminalEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTerminal = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = new LifecyclePipelineBuilder()
            .Use(static (context, next, cancellationToken) =>
            {
                _ = next();
                return ValueTask.CompletedTask;
            })
            .Build(async (_, _) =>
            {
                terminalEntered.TrySetResult();
                await releaseTerminal.Task.ConfigureAwait(false);
            });

        var execution = pipeline.ExecuteAsync(
            new LifecycleContext(LifecycleStages.ApplicationStart)).AsTask();
        await terminalEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(execution.IsCompleted);

        releaseTerminal.TrySetResult();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => execution);
        Assert.Contains("must await or return next", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavedNextExpiresWhenMiddlewareReturns()
    {
        LifecycleNext? savedNext = null;
        var terminalCalls = 0;
        var pipeline = new LifecyclePipelineBuilder()
            .Use((_, next, _) =>
            {
                savedNext = next;
                return ValueTask.CompletedTask;
            })
            .Build((_, _) =>
            {
                Interlocked.Increment(ref terminalCalls);
                return ValueTask.CompletedTask;
            });

        await pipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await savedNext!());
        Assert.Contains("after its invocation has completed", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, terminalCalls);
    }

    [Fact]
    public async Task ConcurrentNextCallsHaveOneOwnerAndOneTerminalExecution()
    {
        const int callerCount = 32;
        var terminalCalls = 0;
        var rejectedCalls = 0;
        using var start = new ManualResetEventSlim();
        var pipeline = new LifecyclePipelineBuilder()
            .Use(async (_, next, _) =>
            {
                var callers = Enumerable.Range(0, callerCount)
                    .Select(_ => Task.Run(async () =>
                    {
                        start.Wait();

                        try
                        {
                            await next();
                        }
                        catch (InvalidOperationException exception) when (
                            exception.Message.Contains("only once", StringComparison.Ordinal))
                        {
                            Interlocked.Increment(ref rejectedCalls);
                        }
                    }))
                    .ToArray();

                start.Set();
                await Task.WhenAll(callers);
            })
            .Build((_, _) =>
            {
                Interlocked.Increment(ref terminalCalls);
                return ValueTask.CompletedTask;
            });

        await pipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));

        Assert.Equal(1, terminalCalls);
        Assert.Equal(callerCount - 1, rejectedCalls);
    }

    [Fact]
    public async Task NextExpiresAfterACompletedPipelineTransaction()
    {
        LifecycleNext? savedNext = null;
        var terminalCalls = 0;
        var pipeline = new LifecyclePipelineBuilder()
            .Use(async (_, next, _) =>
            {
                savedNext = next;
                await next();
            })
            .Build((_, _) =>
            {
                Interlocked.Increment(ref terminalCalls);
                return ValueTask.CompletedTask;
            });

        await pipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await savedNext!());
        Assert.Contains("after its invocation has completed", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, terminalCalls);
    }

    [Fact]
    public async Task PipelineRunsStageSpecificMiddlewareOnlyForMatchingStage()
    {
        var trace = new List<string>();
        var pipeline = new LifecyclePipelineBuilder()
            .Use(LifecycleStages.ApplicationStart, async (context, next, cancellationToken) =>
            {
                trace.Add("start");
                await next().ConfigureAwait(false);
            })
            .Use(LifecycleStages.ApplicationStop, async (context, next, cancellationToken) =>
            {
                trace.Add("stop");
                await next().ConfigureAwait(false);
            })
            .Build((context, cancellationToken) =>
            {
                trace.Add("terminal");

                return ValueTask.CompletedTask;
            });

        await pipeline.ExecuteAsync(new LifecycleContext(LifecycleStages.ApplicationStart));

        Assert.Equal(["start", "terminal"], trace);
    }

    [Fact]
    public async Task MiddlewareCanShortCircuitTerminalHandler()
    {
        var trace = new List<string>();
        var pipeline = new LifecyclePipelineBuilder()
            .Use((context, next, cancellationToken) =>
            {
                trace.Add("short");
                context.ShortCircuit();

                return ValueTask.CompletedTask;
            })
            .Build((context, cancellationToken) =>
            {
                trace.Add("terminal");

                return ValueTask.CompletedTask;
            });

        var context = new LifecycleContext(LifecycleStages.ApplicationStart);

        await pipeline.ExecuteAsync(context);

        Assert.True(context.IsShortCircuited);
        Assert.Equal(["short"], trace);
    }

    [Fact]
    public async Task GuaranteedTerminalRunsEvenWhenCleanupTokenIsAlreadyCanceled()
    {
        var terminalCalled = false;
        var pipeline = new LifecyclePipelineBuilder()
            .Build(static (_, _) => ValueTask.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new LifecycleContext(LifecycleStages.ApplicationStop);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await pipeline.ExecuteAsync(
                context,
                (_, _) =>
                {
                    terminalCalled = true;
                    return ValueTask.CompletedTask;
                },
                guaranteeTerminal: true,
                cancellationToken: cancellation.Token));

        Assert.True(terminalCalled);
    }

    [Fact]
    public async Task PipelineAttributesDownstreamFailureToActualMiddlewareOnlyOnce()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var expectedFailure = new InvalidOperationException("middleware failed");
        var pipeline = new LifecyclePipelineBuilder()
            .Use<OuterMiddleware>(static async (_, next, _) => await next())
            .Use<FailingMiddleware>((_, _, _) => ValueTask.FromException(expectedFailure))
            .Build(static (_, _) => ValueTask.CompletedTask);
        var context = new LifecycleContext(
            LifecycleStages.ModuleInitialize,
            operationId: "start-operation");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteAsync(
                context,
                static (_, _) => ValueTask.CompletedTask,
                guaranteeTerminal: false,
                diagnostics: diagnostics));

        Assert.Same(expectedFailure, failure);
        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
        Assert.Equal(LifecycleStages.ModuleInitialize, record.Stage);
        Assert.Equal(typeof(FailingMiddleware).FullName, record.Context["middlewareType"]);
        Assert.Equal("start-operation", record.Context["operationId"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, record.Context["exceptionType"]);
    }

    [Fact]
    public async Task ExistingUseOverloadInfersNamedMiddlewareDeclaringType()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var pipeline = new LifecyclePipelineBuilder()
            .Use(LifecycleStages.ApplicationStart, InferredMiddleware.InvokeAsync)
            .Build(static (_, _) => ValueTask.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteAsync(
                new LifecycleContext(LifecycleStages.ApplicationStart),
                static (_, _) => ValueTask.CompletedTask,
                guaranteeTerminal: false,
                diagnostics: diagnostics));

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
        Assert.Equal(typeof(InferredMiddleware).FullName, record.Context["middlewareType"]);
    }

    [Fact]
    public async Task PipelineDoesNotAttributeTerminalFailureToMiddleware()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var expectedFailure = new InvalidOperationException("terminal failed");
        var pipeline = new LifecyclePipelineBuilder()
            .Use<OuterMiddleware>(static async (_, next, _) => await next())
            .Build((_, _) => ValueTask.FromException(expectedFailure));
        var context = new LifecycleContext(LifecycleStages.ApplicationStart);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteAsync(
                context,
                (_, _) => ValueTask.FromException(expectedFailure),
                guaranteeTerminal: false,
                diagnostics: diagnostics));

        Assert.Same(expectedFailure, failure);
        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
    }

    [Fact]
    public async Task PipelineDoesNotRecordExpectedCancellationAsMiddlewareFailure()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        using var cancellation = new CancellationTokenSource();
        var pipeline = new LifecyclePipelineBuilder()
            .Use<CancelingMiddleware>((_, next, _) =>
            {
                cancellation.Cancel();
                return next();
            })
            .Build(static (_, _) => ValueTask.CompletedTask);
        var context = new LifecycleContext(LifecycleStages.ApplicationStop);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await pipeline.ExecuteAsync(
                context,
                static (_, _) => ValueTask.CompletedTask,
                guaranteeTerminal: false,
                diagnostics: diagnostics,
                cancellationToken: cancellation.Token));

        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == HostDiagnosticIds.LifecycleMiddlewareFailed);
    }

    [Fact]
    public async Task DiagnosticsFailureDoesNotReplaceOriginalMiddlewareFailure()
    {
        var expectedFailure = new InvalidOperationException("original failure");
        var pipeline = new LifecyclePipelineBuilder()
            .Use<FailingMiddleware>((_, _, _) => ValueTask.FromException(expectedFailure))
            .Build(static (_, _) => ValueTask.CompletedTask);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.ExecuteAsync(
                new LifecycleContext(LifecycleStages.ApplicationStart),
                static (_, _) => ValueTask.CompletedTask,
                guaranteeTerminal: false,
                diagnostics: new ThrowingHostDiagnostics()));

        Assert.Same(expectedFailure, failure);
    }

    private sealed class OuterMiddleware;

    private sealed class FailingMiddleware;

    private sealed class CancelingMiddleware;

    private static class InferredMiddleware
    {
        public static ValueTask InvokeAsync(
            LifecycleContext context,
            LifecycleNext next,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException(new InvalidOperationException("inferred middleware failed"));
        }
    }

    private sealed class ThrowingHostDiagnostics : IHostDiagnostics
    {
        public IReadOnlyList<HostDiagnosticRecord> Records => [];

        public void Write(HostDiagnosticRecord record)
        {
            throw new InvalidOperationException("diagnostics failed");
        }

        public void Complete()
        {
        }
    }
}

file sealed class LifecycleBoundariesRejectDefaultStageValuesMiddleware;
