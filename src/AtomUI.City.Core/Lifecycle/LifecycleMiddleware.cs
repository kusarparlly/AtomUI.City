namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents the lifecycle middleware callback.
/// </summary>
public delegate ValueTask LifecycleMiddleware(
    LifecycleContext context,
    LifecycleNext next,
    CancellationToken cancellationToken);
