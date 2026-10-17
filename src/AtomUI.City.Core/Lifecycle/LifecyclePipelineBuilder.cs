namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents lifecycle pipeline builder.
/// </summary>
public sealed class LifecyclePipelineBuilder
{
    private readonly List<LifecycleMiddlewareRegistration> _middleware = [];

    /// <summary>
    /// Executes the use operation.
    /// </summary>
    public LifecyclePipelineBuilder Use(LifecycleMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage: null, InferMiddlewareType(middleware), middleware);
    }

    /// <summary>
    /// Executes the use operation.
    /// </summary>
    public LifecyclePipelineBuilder Use(LifecycleStage stage, LifecycleMiddleware middleware)
    {
        stage.ThrowIfInvalid(nameof(stage));
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage, InferMiddlewareType(middleware), middleware);
    }

    /// <summary>
    /// Executes the use operation.
    /// </summary>
    public LifecyclePipelineBuilder Use<TMiddleware>(LifecycleMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage: null, typeof(TMiddleware), middleware);
    }

    /// <summary>
    /// Executes the use operation.
    /// </summary>
    public LifecyclePipelineBuilder Use<TMiddleware>(
        LifecycleStage stage,
        LifecycleMiddleware middleware)
    {
        stage.ThrowIfInvalid(nameof(stage));
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage, typeof(TMiddleware), middleware);
    }

    /// <summary>
    /// Executes the build operation.
    /// </summary>
    public LifecyclePipeline Build(
        Func<LifecycleContext, CancellationToken, ValueTask> terminalHandler)
    {
        ArgumentNullException.ThrowIfNull(terminalHandler);

        return new LifecyclePipeline(_middleware.ToArray(), terminalHandler);
    }

    private LifecyclePipelineBuilder UseCore(
        LifecycleStage? stage,
        Type middlewareType,
        LifecycleMiddleware middleware)
    {
        _middleware.Add(new LifecycleMiddlewareRegistration(stage, middlewareType, middleware));

        return this;
    }

    private static Type InferMiddlewareType(LifecycleMiddleware middleware)
    {
        return middleware.Method.DeclaringType
               ?? middleware.Target?.GetType()
               ?? typeof(LifecycleMiddleware);
    }
}
