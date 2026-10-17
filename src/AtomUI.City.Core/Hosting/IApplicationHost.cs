using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Core.Hosting;

/// <summary>
/// Defines the contract for iapplication host.
/// </summary>
public interface IApplicationHost : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the services value.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets the context value.
    /// </summary>
    IApplicationContext Context { get; }

    /// <summary>
    /// Gets the host scope value.
    /// </summary>
    LifecycleScope HostScope { get; }

    /// <summary>
    /// Gets the application scope value.
    /// </summary>
    LifecycleScope? ApplicationScope { get; }

    /// <summary>
    /// Executes the start async operation.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    Task StopAsync(CancellationToken waitCancellationToken = default);

    /// <summary>
    /// Executes the run async operation.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
