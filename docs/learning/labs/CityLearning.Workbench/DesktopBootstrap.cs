using Avalonia.Controls.ApplicationLifetimes;
using AtomUI.City.Core.Hosting;
using CityLearning.Workbench.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CityLearning.Workbench;

internal static class DesktopBootstrap
{
    private static IApplicationHost? _host;

    internal static void Attach(IApplicationHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (Interlocked.CompareExchange(ref _host, host, null) is not null)
        {
            throw new InvalidOperationException("A City Host is already attached.");
        }
    }

    internal static void Initialize(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        var host = Volatile.Read(ref _host)
            ?? throw new InvalidOperationException("The City Host has not been attached.");
        lifetime.MainWindow = host.Services.GetRequiredService<MainWindow>();
    }

    internal static void Detach(IApplicationHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _host, null, host), host))
        {
            throw new InvalidOperationException("The supplied City Host is not attached.");
        }
    }
}
