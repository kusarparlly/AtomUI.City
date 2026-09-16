using AtomUI.City.Core.Lifecycle;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for ipresentation runtime.
/// </summary>
public interface IPresentationRuntime
{
    /// <summary>
    /// Gets state.
    /// </summary>
    PresentationRuntimeState State { get; }

    /// <summary>
    /// Gets a value indicating whether is ready.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets presentation scope.
    /// </summary>
    LifecycleScope? PresentationScope { get; }

    /// <summary>
    /// Gets application lifetime.
    /// </summary>
    IApplicationLifetime? ApplicationLifetime { get; }

    /// <summary>
    /// Gets windows.
    /// </summary>
    IReadOnlyCollection<WindowSession> Windows { get; }

    /// <summary>
    /// Executes the attach operation.
    /// </summary>
    void Attach(
        IApplicationLifetime applicationLifetime,
        LifecycleScope hostScope,
        string presentationScopeId = "presentation");

    /// <summary>
    /// Executes the register window operation.
    /// </summary>
    WindowSession RegisterWindow(Window window, string windowId);

    /// <summary>
    /// Executes the start async operation.
    /// </summary>
    ValueTask StartAsync(
        LifecycleScope applicationScope,
        string presentationScopeId = "presentation",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the create window scope operation.
    /// </summary>
    LifecycleScope CreateWindowScope(string windowScopeId);

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
