using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.Presentation;

internal sealed class PresentationViewMetadata
{
    public PresentationViewMetadata(
        string viewTypeName,
        string viewModelTypeName,
        string? viewKey,
        string? pluginId,
        string? contributionId,
        Location? location = null,
        IReadOnlyList<PresentationViewConstructorParameter>? constructorParameters = null,
        bool hasAmbiguousConstructors = false)
    {
        if (string.IsNullOrWhiteSpace(viewTypeName))
        {
            throw new ArgumentException("View type name cannot be empty.", nameof(viewTypeName));
        }

        if (string.IsNullOrWhiteSpace(viewModelTypeName))
        {
            throw new ArgumentException("View model type name cannot be empty.", nameof(viewModelTypeName));
        }

        ViewTypeName = viewTypeName;
        ViewModelTypeName = viewModelTypeName;
        ViewKey = string.IsNullOrWhiteSpace(viewKey) ? null : viewKey;
        PluginId = string.IsNullOrWhiteSpace(pluginId) ? null : pluginId;
        ContributionId = string.IsNullOrWhiteSpace(contributionId) ? null : contributionId;
        Location = location;
        ConstructorParameters = Array.AsReadOnly(constructorParameters?.ToArray() ?? []);
        HasAmbiguousConstructors = hasAmbiguousConstructors;
    }

    public string ViewTypeName { get; }

    public string ViewModelTypeName { get; }

    public string? ViewKey { get; }

    public string? PluginId { get; }

    public string? ContributionId { get; }

    public Location? Location { get; }

    public IReadOnlyList<PresentationViewConstructorParameter> ConstructorParameters { get; }

    public bool HasAmbiguousConstructors { get; }
}
