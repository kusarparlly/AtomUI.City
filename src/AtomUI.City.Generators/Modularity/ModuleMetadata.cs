namespace AtomUI.City.Generators.Modularity;

internal sealed class ModuleMetadata
{
    public ModuleMetadata(
        string name,
        string typeName,
        string? version,
        string? description,
        IReadOnlyList<ModuleDependencyMetadata> dependencies,
        bool isApplicationRoot = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Module name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Module type name cannot be empty.", nameof(typeName));
        }

        Name = name;
        TypeName = typeName;
        Version = version;
        Description = description;
        Dependencies = Array.AsReadOnly((dependencies ?? throw new ArgumentNullException(nameof(dependencies))).ToArray());
        IsApplicationRoot = isApplicationRoot;
    }

    public string Name { get; }

    public string TypeName { get; }

    public string? Version { get; }

    public string? Description { get; }

    public IReadOnlyList<ModuleDependencyMetadata> Dependencies { get; }

    public bool IsApplicationRoot { get; }
}
