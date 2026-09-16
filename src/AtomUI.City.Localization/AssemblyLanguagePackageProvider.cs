using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents assembly language package provider.
/// </summary>
public sealed class AssemblyLanguagePackageProvider : ILanguagePackageProvider
{
    /// <summary>
    /// Gets kind.
    /// </summary>
    public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.Assembly;

    /// <summary>
    /// Executes the discover operation.
    /// </summary>
    public IReadOnlyList<LanguagePackageDescriptor> Discover(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return Array.AsReadOnly(assembly
            .GetCustomAttributes<LanguagePackageAttribute>()
            .Select(attribute => CreateDescriptor(assembly, attribute))
            .ToArray());
    }

    /// <summary>
    /// Executes the load async operation.
    /// </summary>
    public ValueTask<LanguagePackageLoadResult> LoadAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.ProviderKind != Kind)
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.InvalidDescriptor,
                        $"Language package '{descriptor.PackageId}' is not an assembly package.")));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Cancelled(cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Location))
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        "Assembly language package location is required.")));
        }

        if (string.IsNullOrWhiteSpace(descriptor.ResourceBaseName))
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        "Assembly language package resource name is required.")));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(descriptor.Location);
            var loadContext = descriptor.LoadContext ?? AssemblyLoadContext.Default;
            var assembly = loadContext.Assemblies.FirstOrDefault(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.Location)
                    && string.Equals(
                        Path.GetFullPath(candidate.Location),
                        fullPath,
                        StringComparison.OrdinalIgnoreCase))
                ?? loadContext.LoadFromAssemblyPath(fullPath);
            cancellationToken.ThrowIfCancellationRequested();
            var resourceName = ResolveResourceName(assembly, descriptor.ResourceBaseName);

            if (resourceName is null)
            {
                return ValueTask.FromResult(
                    LanguagePackageLoadResult.Failed(
                        new LocalizationError(
                            LocalizationErrorKind.PackageNotFound,
                            $"Embedded localization resource '{descriptor.ResourceBaseName}' was not found.")));
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            cancellationToken.ThrowIfCancellationRequested();
            if (stream is null)
            {
                return ValueTask.FromResult(
                    LanguagePackageLoadResult.Failed(
                        new LocalizationError(
                            LocalizationErrorKind.PackageNotFound,
                            $"Embedded localization resource '{resourceName}' was not found.")));
            }

            return ValueTask.FromResult(LocPackReader.Read(stream, descriptor, cancellationToken));
        }
        catch (OperationCanceledException exception)
        {
            return ValueTask.FromResult(Cancelled(cancellationToken, exception));
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageLoadFailed,
                        exception.Message,
                        Exception: exception)));
        }
    }

    private static LanguagePackageDescriptor CreateDescriptor(
        Assembly assembly,
        LanguagePackageAttribute attribute)
    {
        return new LanguagePackageDescriptor(
            attribute.PackageId,
            CultureInfo.GetCultureInfo(attribute.Culture),
            attribute.Scope)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            ScopeId = attribute.ScopeId,
            LoadContext = AssemblyLoadContext.GetLoadContext(assembly),
            FallbackCulture = string.IsNullOrWhiteSpace(attribute.FallbackCulture)
                ? null
                : CultureInfo.GetCultureInfo(attribute.FallbackCulture),
            Location = string.IsNullOrWhiteSpace(assembly.Location) ? null : assembly.Location,
            ResourceBaseName = attribute.ResourceBaseName,
            Version = attribute.Version,
            Checksum = attribute.Checksum,
            ContributionId = attribute.ContributionId,
        };
    }

    private static string? ResolveResourceName(Assembly assembly, string resourceBaseName)
    {
        var names = assembly.GetManifestResourceNames();
        var exact = names.FirstOrDefault(name => string.Equals(name, resourceBaseName, StringComparison.Ordinal));
        if (exact is not null)
        {
            return exact;
        }

        var suffix = "." + resourceBaseName;
        var matches = names
            .Where(name => name.EndsWith(suffix, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    private static LanguagePackageLoadResult Cancelled(
        CancellationToken cancellationToken,
        OperationCanceledException? exception = null)
    {
        return LanguagePackageLoadResult.Failed(
            new LocalizationError(
                LocalizationErrorKind.Cancelled,
                "Language package load was cancelled.",
                exception ?? new OperationCanceledException(cancellationToken)));
    }
}
