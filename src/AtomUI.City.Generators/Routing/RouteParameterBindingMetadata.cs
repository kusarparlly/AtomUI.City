namespace AtomUI.City.Generators.Routing;

internal sealed class RouteParameterBindingMetadata
{
    public RouteParameterBindingMetadata(string routeName, string memberName)
    {
        RouteName = routeName ?? throw new ArgumentNullException(nameof(routeName));
        MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
    }

    public string RouteName { get; }

    public string MemberName { get; }
}
