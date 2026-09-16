using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data module.
/// </summary>
[Module("AtomUI.City.Data", Version = "1.0.0", Description = "Provides the City data access runtime.")]
public sealed class DataModule : ModuleBase
{
    /// <summary>
    /// Executes the configure services operation.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddData();
    }

    /// <summary>
    /// Executes the on application shutdown async operation.
    /// </summary>
    public override async ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var failures = new List<Exception>();
        try
        {
            await context.Services
                .GetRequiredService<DataRuntimeGate>()
                .StopAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await context.Services
                .GetRequiredService<DataConnectionManager>()
                .StopAllAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("Data runtime shutdown failed.", failures);
        }
    }
}
