namespace AtomUI.City.Generators.Routing;

using Microsoft.CodeAnalysis;

internal sealed class RouteMapMetadata
{
    public RouteMapMetadata(
        string typeName,
        IReadOnlyList<RouteDefinitionMetadata> routes,
        Location? location = null,
        IReadOnlyList<string>? issues = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Route map type name cannot be empty.", nameof(typeName));
        }

        TypeName = typeName;
        Routes = Array.AsReadOnly((routes ?? throw new ArgumentNullException(nameof(routes))).ToArray());
        Location = location;
        Issues = Array.AsReadOnly((issues ?? []).ToArray());
    }

    public string TypeName { get; }

    public IReadOnlyList<RouteDefinitionMetadata> Routes { get; }

    public Location? Location { get; }

    public IReadOnlyList<string> Issues { get; }
}
