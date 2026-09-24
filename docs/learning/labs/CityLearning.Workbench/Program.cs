using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using CityLearning.Workbench.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityLearning.Workbench;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["--verify-host"])
            {
                return Task.Run(VerifyHostAsync).GetAwaiter().GetResult();
            }

            return Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    internal static IApplicationHost CreateHost(string[] args, string? dataFilePath = null)
    {
        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "CityLearning.Workbench";
            options.ApplicationName = "City Learning Workbench";
        });
        builder.UseModule<WorkbenchApplicationModule>();
        builder.ConfigureServices(services =>
            services.AddSingleton(new WorkbenchOptions(dataFilePath ?? ResolveDataFilePath())));
        return builder.Build();
    }

    internal static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();

    private static int Run(string[] args)
    {
        using var host = CreateHost(args);
        host.StartAsync().GetAwaiter().GetResult();
        DesktopBootstrap.Attach(host);
        try
        {
            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
        }
        finally
        {
            DesktopBootstrap.Detach(host);
            Task.Run(async () => await host.StopAsync().ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();
        }
    }

    private static async Task<int> VerifyHostAsync()
    {
        await using var host = CreateHost([]);
        await host.StartAsync().ConfigureAwait(false);
        var service = host.Services.GetRequiredService<Services.WorkItemService>();
        var created = await service.AddAsync("City Host verification").ConfigureAwait(false);
        var completed = await service.CompleteAsync(created.Id).ConfigureAwait(false);
        await host.StopAsync().ConfigureAwait(false);
        return completed ? 0 : 1;
    }

    private static string ResolveDataFilePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("CITY_LEARNING_DATA_FILE");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtomUI.City.Learning",
            "Workbench",
            "work-items.json");
    }
}
