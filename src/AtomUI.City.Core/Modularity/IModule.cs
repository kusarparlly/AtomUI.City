namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Defines the contract for imodule.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Executes the pre configure services operation.
    /// </summary>
    void PreConfigureServices(ServiceConfigurationContext context);

    /// <summary>
    /// Executes the configure services operation.
    /// </summary>
    void ConfigureServices(ServiceConfigurationContext context);

    /// <summary>
    /// Executes the post configure services operation.
    /// </summary>
    void PostConfigureServices(ServiceConfigurationContext context);

    /// <summary>
    /// Executes the configure contributions async operation.
    /// </summary>
    ValueTask ConfigureContributionsAsync(
        ContributionConfigurationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the on pre application initialization async operation.
    /// </summary>
    ValueTask OnPreApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the on application initialization async operation.
    /// </summary>
    ValueTask OnApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the on post application initialization async operation.
    /// </summary>
    ValueTask OnPostApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the on application shutdown async operation.
    /// </summary>
    ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken);
}
