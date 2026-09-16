using System.Globalization;
using System.Collections.ObjectModel;
using System.Runtime.Loader;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents language package descriptor.
/// </summary>
public sealed class LanguagePackageDescriptor
{
    private CultureInfo? _fallbackCulture;
    private IReadOnlyDictionary<string, string>? _inMemoryResources;
    private IReadOnlyList<string> _criticalResourceKeys = Array.Empty<string>();

    /// <summary>
    /// Initializes a new instance of the <c>LanguagePackageDescriptor</c> type.
    /// </summary>
    public LanguagePackageDescriptor(
        string packageId,
        CultureInfo culture,
        ResourceScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown localization resource scope.");
        }

        PackageId = packageId;
        Culture = CultureInfoSnapshot.Create(culture);
        Scope = scope;
    }

    /// <summary>
    /// Gets package id.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    /// Gets culture.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets scope.
    /// </summary>
    public ResourceScope Scope { get; }

    /// <summary>
    /// Gets or sets scope id.
    /// </summary>
    public string? ScopeId { get; init; }

    /// <summary>
    /// Gets or sets provider kind.
    /// </summary>
    public LanguagePackageProviderKind ProviderKind { get; init; } =
        LanguagePackageProviderKind.InMemory;

    /// <summary>
    /// Represents the fallback culture value.
    /// </summary>
    public CultureInfo? FallbackCulture
    {
        get => _fallbackCulture;
        init => _fallbackCulture = value is null ? null : CultureInfoSnapshot.Create(value);
    }

    /// <summary>
    /// Gets or sets location.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Gets or sets allowed root path.
    /// </summary>
    public string? AllowedRootPath { get; init; }

    /// <summary>
    /// Gets or sets resource base name.
    /// </summary>
    public string? ResourceBaseName { get; init; }

    /// <summary>
    /// Gets or sets version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets or sets checksum.
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>
    /// Gets or sets contribution id.
    /// </summary>
    public string? ContributionId { get; init; }

    /// <summary>
    /// Gets or sets load context.
    /// </summary>
    public AssemblyLoadContext? LoadContext { get; init; }

    /// <summary>
    /// Represents the in memory resources value.
    /// </summary>
    public IReadOnlyDictionary<string, string>? InMemoryResources
    {
        get => _inMemoryResources;
        init
        {
            if (value is null)
            {
                _inMemoryResources = null;
                return;
            }

            if (value.Any(resource => string.IsNullOrWhiteSpace(resource.Key) || resource.Value is null))
            {
                throw new ArgumentException(
                    "In-memory resources require non-empty keys and non-null values.",
                    nameof(value));
            }

            _inMemoryResources = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(value, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Represents the critical resource keys value.
    /// </summary>
    public IReadOnlyList<string> CriticalResourceKeys
    {
        get => _criticalResourceKeys;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Critical resource keys cannot contain empty values.", nameof(value));
            }

            _criticalResourceKeys = Array.AsReadOnly(
                value.Distinct(StringComparer.Ordinal).ToArray());
        }
    }
}
