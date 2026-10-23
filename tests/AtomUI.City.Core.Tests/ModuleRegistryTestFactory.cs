using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Core.Tests;

internal static class ModuleRegistryTestFactory
{
    public static ModuleRegistry Create(
        IReadOnlyList<Type> moduleTypes,
        IHostDiagnostics? diagnostics = null,
        TimeSpan? buildCleanupTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(moduleTypes);

        var registrations = moduleTypes
            .Select(moduleType => new ModuleRegistration(
                ModuleDescriptorFactory.CreateFromAttributes(moduleType),
                () => (IModule)Activator.CreateInstance(moduleType)!))
            .ToArray();

        return ModuleRegistry.Create(
            ModuleGraphValidator.Validate(registrations),
            diagnostics,
            buildCleanupTimeout);
    }
}
