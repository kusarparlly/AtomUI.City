using AtomUI.City.Generators.Diagnostics;

namespace AtomUI.City.Generators.DependencyInjection;

internal static class ServiceRegistrationManifestBuilder
{
    public static ServiceRegistrationManifestResult Build(IReadOnlyList<ServiceRegistrationMetadata> registrations)
    {
        if (registrations is null)
        {
            throw new ArgumentNullException(nameof(registrations));
        }

        var diagnostics = new List<GeneratorDiagnostic>();
        var exposedServices = new Dictionary<string, ServiceRegistrationMetadata>(StringComparer.Ordinal);

        foreach (var implementationGroup in registrations.GroupBy(registration => registration.ImplementationTypeName, StringComparer.Ordinal))
        {
            var lifetimes = implementationGroup
                .Select(registration => registration.Lifetime)
                .Distinct()
                .ToArray();

            if (lifetimes.Length <= 1)
            {
                continue;
            }

            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidManifestInput,
                $"Service implementation '{implementationGroup.Key}' has conflicting lifetimes: {string.Join(", ", lifetimes)}.",
                implementationGroup.Key));
        }

        foreach (var registration in registrations)
        {
            foreach (var exposedServiceTypeName in registration.ExposedServiceTypeNames)
            {
                var registrationKey = CreateRegistrationKey(exposedServiceTypeName, registration.Key);

                if (!exposedServices.TryGetValue(registrationKey, out var existingRegistration))
                {
                    exposedServices.Add(registrationKey, registration);
                    continue;
                }

                if (registration.Replace || registration.TryAdd || existingRegistration.Replace || existingRegistration.TryAdd)
                {
                    continue;
                }

                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnostics.InvalidManifestInput,
                    $"Service '{exposedServiceTypeName}' is exposed by multiple registrations.",
                    exposedServiceTypeName));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new ServiceRegistrationManifestResult(new ServiceRegistrationManifest([]), diagnostics);
        }

        var manifest = new ServiceRegistrationManifest(
            registrations
                .OrderBy(registration => registration.ImplementationTypeName, StringComparer.Ordinal)
                .ThenBy(registration => registration.Lifetime)
                .ToArray());

        return new ServiceRegistrationManifestResult(manifest, diagnostics);
    }

    private static string CreateRegistrationKey(string exposedServiceTypeName, string? key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? exposedServiceTypeName
            : $"{exposedServiceTypeName}|{key}";
    }
}
