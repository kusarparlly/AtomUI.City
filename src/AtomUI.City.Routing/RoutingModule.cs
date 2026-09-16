using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Routing;

/// <summary>
/// Represents routing module.
/// </summary>
public sealed class RoutingModule : ModuleBase
{
    /// <summary>
    /// Executes the configure services operation.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddRouting();
    }
}
