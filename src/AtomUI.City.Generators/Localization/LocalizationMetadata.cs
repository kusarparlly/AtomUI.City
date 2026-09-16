using AtomUI.City.Generators.Diagnostics;

namespace AtomUI.City.Generators.Localization;

internal sealed class LocalizationMetadata
{
    public LocalizationMetadata(
        IReadOnlyList<LanguagePackageMetadata> packages,
        IReadOnlyList<LocalizedResourceMetadata> resources,
        IReadOnlyList<GeneratorDiagnostic>? diagnostics = null)
    {
        Packages = Array.AsReadOnly((packages ?? throw new ArgumentNullException(nameof(packages))).ToArray());
        Resources = Array.AsReadOnly((resources ?? throw new ArgumentNullException(nameof(resources))).ToArray());
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public IReadOnlyList<LanguagePackageMetadata> Packages { get; }

    public IReadOnlyList<LocalizedResourceMetadata> Resources { get; }

    public IReadOnlyList<GeneratorDiagnostic> Diagnostics { get; }
}
