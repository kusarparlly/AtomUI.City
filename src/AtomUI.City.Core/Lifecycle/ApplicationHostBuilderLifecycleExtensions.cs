using System.Runtime.CompilerServices;
using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents application host builder lifecycle extensions.
/// </summary>
public static class ApplicationHostBuilderLifecycleExtensions
{
    /// <summary>
    /// Executes the configure lifecycle operation.
    /// </summary>
    public static IApplicationHostBuilder ConfigureLifecycle(
        this IApplicationHostBuilder builder,
        Action<LifecyclePipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        LifecycleConfigurationStore.Add(builder, configure);

        return builder;
    }
}

internal static class LifecycleConfigurationStore
{
    private static readonly ConditionalWeakTable<
        IApplicationHostBuilder,
        FreezableApplicationHostBuilderCollection<Action<LifecyclePipelineBuilder>>> Stores = new();

    public static void Add(
        IApplicationHostBuilder builder,
        Action<LifecyclePipelineBuilder> configure)
    {
        Stores.GetOrCreateValue(builder).Add(configure);
    }

    public static IReadOnlyList<Action<LifecyclePipelineBuilder>> FreezeAndSnapshot(
        IApplicationHostBuilder builder)
    {
        return Stores.GetOrCreateValue(builder).FreezeAndSnapshot();
    }

    public static LifecyclePipeline Build(
        IReadOnlyList<Action<LifecyclePipelineBuilder>> configurations)
    {
        var pipelineBuilder = new LifecyclePipelineBuilder();

        foreach (var configure in configurations)
        {
            configure(pipelineBuilder);
        }

        return pipelineBuilder.Build(static (_, _) => ValueTask.CompletedTask);
    }
}
