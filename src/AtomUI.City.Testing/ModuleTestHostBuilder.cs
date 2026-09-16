using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents module test host builder.
/// </summary>
public sealed class ModuleTestHostBuilder
{
    private readonly List<ModuleTestRecord> _modules = [];
    private readonly TestHostBuilder _hostBuilder = TestHost.CreateBuilder();
    private bool _built;

    /// <summary>
    /// Executes the use module operation.
    /// </summary>
    public ModuleTestHostBuilder UseModule(string name, IModule module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(module);
        ThrowIfBuilt();

        _modules.Add(new ModuleTestRecord(name, module));

        return this;
    }

    /// <summary>
    /// Executes the use host property operation.
    /// </summary>
    public ModuleTestHostBuilder UseHostProperty(string key, object? value)
    {
        ThrowIfBuilt();

        _hostBuilder.UseProperty(key, value);

        return this;
    }

    /// <summary>
    /// Executes the build operation.
    /// </summary>
    public ModuleTestHost Build()
    {
        ThrowIfBuilt();

        var orderedModules = OrderByDependencies(_modules);
        var host = _hostBuilder.Build();
        _built = true;

        return new ModuleTestHost(host, orderedModules);
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The module test host builder has already built a host and is frozen.");
        }
    }

    private static IReadOnlyList<ModuleTestRecord> OrderByDependencies(IReadOnlyList<ModuleTestRecord> modules)
    {
        var duplicateName = modules
            .GroupBy(module => module.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateName is not null)
        {
            throw new InvalidOperationException($"Duplicate module name '{duplicateName.Key}'.");
        }

        var entries = modules
            .Select((module, index) => new ModuleGraphEntry(module, index, module.Module.GetType()))
            .ToArray();
        var modulesByType = entries
            .GroupBy(entry => entry.ModuleType)
            .ToDictionary(group => group.Key, group => group.First());
        var ordered = new List<ModuleTestRecord>();
        var visitStates = new Dictionary<ModuleGraphEntry, ModuleVisitState>();
        var path = new Stack<ModuleGraphEntry>();

        foreach (var entry in entries)
        {
            Visit(entry, modulesByType, visitStates, ordered, path);
        }

        return ordered;
    }

    private static void Visit(
        ModuleGraphEntry entry,
        IReadOnlyDictionary<Type, ModuleGraphEntry> modulesByType,
        IDictionary<ModuleGraphEntry, ModuleVisitState> visitStates,
        ICollection<ModuleTestRecord> ordered,
        Stack<ModuleGraphEntry> path)
    {
        if (visitStates.TryGetValue(entry, out var state))
        {
            if (state == ModuleVisitState.Visited)
            {
                return;
            }

            var cyclePath = path
                .Reverse()
                .SkipWhile(pathEntry => pathEntry != entry)
                .Append(entry)
                .Select(pathEntry => pathEntry.ModuleType.FullName);

            throw new InvalidOperationException(
                $"Module dependency graph contains a cycle: {string.Join(" -> ", cyclePath)}.");
        }

        visitStates.Add(entry, ModuleVisitState.Visiting);
        path.Push(entry);

        foreach (var dependency in GetDependencies(entry.ModuleType))
        {
            if (modulesByType.TryGetValue(dependency.ModuleType, out var dependencyModule))
            {
                Visit(dependencyModule, modulesByType, visitStates, ordered, path);
                continue;
            }

            if (!dependency.Optional)
            {
                throw new InvalidOperationException(
                    $"Module '{entry.ModuleType.FullName}' depends on missing module '{dependency.ModuleType.FullName}'.");
            }
        }

        visitStates[entry] = ModuleVisitState.Visited;
        path.Pop();
        ordered.Add(entry.Module);
    }

    private static IEnumerable<DependsOnAttribute> GetDependencies(Type moduleType)
    {
        return moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .OfType<DependsOnAttribute>();
    }

    private enum ModuleVisitState
    {
        Visiting,
        Visited,
    }

    private sealed record ModuleGraphEntry(ModuleTestRecord Module, int Index, Type ModuleType);
}
