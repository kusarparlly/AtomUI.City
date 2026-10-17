namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents module base.
/// </summary>
public abstract class ModuleBase : IModule
{
    /// <summary>
    /// Executes the pre configure services operation.
    /// </summary>
    public virtual void PreConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Executes the configure services operation.
    /// </summary>
    public virtual void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Executes the post configure services operation.
    /// </summary>
    public virtual void PostConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Executes the configure contributions async operation.
    /// </summary>
    public virtual ValueTask ConfigureContributionsAsync(
        ContributionConfigurationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        ConfigureContributions(context);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the configure contributions operation.
    /// </summary>
    public virtual void ConfigureContributions(ContributionConfigurationContext context)
    {
    }

    /// <summary>
    /// Executes the on pre application initialization async operation.
    /// </summary>
    public virtual ValueTask OnPreApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        OnPreApplicationInitialization(context);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the on pre application initialization operation.
    /// </summary>
    public virtual void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
    }

    /// <summary>
    /// Executes the on application initialization async operation.
    /// </summary>
    public virtual ValueTask OnApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        OnApplicationInitialization(context);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the on application initialization operation.
    /// </summary>
    public virtual void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }

    /// <summary>
    /// Executes the on post application initialization async operation.
    /// </summary>
    public virtual ValueTask OnPostApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        OnPostApplicationInitialization(context);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the on post application initialization operation.
    /// </summary>
    public virtual void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
    }

    /// <summary>
    /// Executes the on application shutdown async operation.
    /// </summary>
    public virtual ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        OnApplicationShutdown(context);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the on application shutdown operation.
    /// </summary>
    public virtual void OnApplicationShutdown(ApplicationShutdownContext context)
    {
    }
}
