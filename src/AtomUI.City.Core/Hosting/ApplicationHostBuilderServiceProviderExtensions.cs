using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Hosting;

/// <summary>
/// Provides default service provider configuration for an application host builder.
/// </summary>
public static class ApplicationHostBuilderServiceProviderExtensions
{
    /// <summary>
    /// Configures the default Microsoft dependency injection service provider created by the host.
    /// </summary>
    public static IApplicationHostBuilder UseDefaultServiceProvider(
        this IApplicationHostBuilder builder,
        Action<ServiceProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        DefaultServiceProviderConfigurationStore.Add(builder, configure);

        return builder;
    }
}

internal static class DefaultServiceProviderConfigurationStore
{
    private static readonly ConditionalWeakTable<
        IApplicationHostBuilder,
        FreezableApplicationHostBuilderCollection<Action<ServiceProviderOptions>>> Stores = new();

    public static void Add(
        IApplicationHostBuilder builder,
        Action<ServiceProviderOptions> configure)
    {
        Stores.GetOrCreateValue(builder).Add(configure);
    }

    public static IReadOnlyList<Action<ServiceProviderOptions>> FreezeAndSnapshot(
        IApplicationHostBuilder builder)
    {
        return Stores.GetOrCreateValue(builder).FreezeAndSnapshot();
    }
}
