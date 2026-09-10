using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Presentation;

[Module(
    "AtomUI.City.Presentation",
    Version = "1.0.0",
    Description = "Connects City application services to the Avalonia presentation runtime.")]
public sealed class PresentationModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddPresentation();
    }

    public override ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<IPresentationRuntime>()
            .StopAsync(cancellationToken);
    }
}
