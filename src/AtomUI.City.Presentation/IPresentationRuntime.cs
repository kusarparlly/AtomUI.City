using AtomUI.City.Core.Lifecycle;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace AtomUI.City.Presentation;

public interface IPresentationRuntime
{
    PresentationRuntimeState State { get; }

    bool IsReady { get; }

    LifecycleScope? PresentationScope { get; }

    IApplicationLifetime? ApplicationLifetime { get; }

    IReadOnlyCollection<WindowSession> Windows { get; }

    void Attach(
        IApplicationLifetime applicationLifetime,
        LifecycleScope hostScope,
        string presentationScopeId = "presentation");

    WindowSession RegisterWindow(Window window, string windowId);

    ValueTask StartAsync(
        LifecycleScope applicationScope,
        string presentationScopeId = "presentation",
        CancellationToken cancellationToken = default);

    LifecycleScope CreateWindowScope(string windowScopeId);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
