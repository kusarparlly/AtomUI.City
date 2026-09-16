using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents localization service collection extensions.
/// </summary>
public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Executes the add localization operation.
    /// </summary>
    public static IServiceCollection AddLocalization(
        this IServiceCollection services,
        Action<LocalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddState();

        services.TryAddSingleton(_ =>
        {
            var options = new LocalizationOptions();
            configure?.Invoke(options);

            return options;
        });
        services.TryAddSingleton<ILocalizationDiagnostics, InMemoryLocalizationDiagnostics>();
        services.TryAddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<LocalizationOptions>();

            return LanguagePackageRegistry.CreateWithHostDescriptors(options.LanguagePackages);
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguagePackageProvider, FileLanguagePackageProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguagePackageProvider, AssemblyLanguagePackageProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguagePackageProvider, InMemoryLanguagePackageProvider>());
        services.TryAddSingleton<ILocalizationService>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<LocalizationOptions>();
            var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();

            return new LocalizationService(
                options,
                registry,
                serviceProvider.GetServices<ILanguagePackageProvider>(),
                serviceProvider.GetService<IPresentationLocalizationBridge>(),
                serviceProvider.GetService<ILocalizationDiagnostics>(),
                serviceProvider.GetService<IStateFactory>());
        });

        return services;
    }
}
