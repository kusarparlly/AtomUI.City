namespace AtomUI.City.Generators.DependencyInjection;

internal sealed class ServiceRegistrationManifest
{
    public ServiceRegistrationManifest(IReadOnlyList<ServiceRegistrationMetadata> registrations)
    {
        Registrations = Array.AsReadOnly((registrations ?? throw new ArgumentNullException(nameof(registrations))).ToArray());
    }

    public IReadOnlyList<ServiceRegistrationMetadata> Registrations { get; }
}
