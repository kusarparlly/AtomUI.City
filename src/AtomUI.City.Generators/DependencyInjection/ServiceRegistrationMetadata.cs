namespace AtomUI.City.Generators.DependencyInjection;

internal sealed class ServiceRegistrationMetadata
{
    public ServiceRegistrationMetadata(
        string implementationTypeName,
        ServiceRegistrationLifetime lifetime,
        IReadOnlyList<string> exposedServiceTypeNames,
        bool replace,
        bool tryAdd,
        string? key,
        bool isDisposable = false)
    {
        if (string.IsNullOrWhiteSpace(implementationTypeName))
        {
            throw new ArgumentException("Implementation type name cannot be empty.", nameof(implementationTypeName));
        }

        if (!Enum.IsDefined(typeof(ServiceRegistrationLifetime), lifetime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "Service registration lifetime must be a defined value.");
        }

        if (exposedServiceTypeNames is null)
        {
            throw new ArgumentNullException(nameof(exposedServiceTypeNames));
        }
        if (exposedServiceTypeNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Exposed service type names cannot contain null or whitespace.",
                nameof(exposedServiceTypeNames));
        }

        ImplementationTypeName = implementationTypeName;
        Lifetime = lifetime;
        ExposedServiceTypeNames = Array.AsReadOnly(exposedServiceTypeNames.ToArray());
        Replace = replace;
        TryAdd = tryAdd;
        Key = key;
        IsDisposable = isDisposable;
    }

    public string ImplementationTypeName { get; }

    public ServiceRegistrationLifetime Lifetime { get; }

    public IReadOnlyList<string> ExposedServiceTypeNames { get; }

    public bool Replace { get; }

    public bool TryAdd { get; }

    public string? Key { get; }

    public bool IsDisposable { get; }
}
