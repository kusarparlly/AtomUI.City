using System.Windows.Input;
using System.Globalization;
using AtomUI.City.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodRoutingWorkload : IDisposable
{
    private readonly IServiceScope _navigationScope;
    private readonly IRouter _router;
    private readonly IRouteGraphProvider _graphProvider;
    private int _disposed;

    public DogfoodRoutingWorkload(
        IServiceScopeFactory scopeFactory,
        IRouteGraphProvider graphProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _graphProvider = graphProvider ?? throw new ArgumentNullException(nameof(graphProvider));
        _navigationScope = scopeFactory.CreateScope();
        _router = _navigationScope.ServiceProvider.GetRequiredService<IRouter>();
    }

    public async Task InitializeAsync(
        Func<NavigationResult, Task> presentAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presentAsync);
        VerifyCatalogs();

        await NavigateAndPresentAsync(
            () => _router.NavigateAsync(
                DogfoodRoutes.DashboardOverview(),
                new NavigationOptions
                {
                    Mode = NavigationMode.Reset,
                    HistoryBehavior = NavigationHistoryBehavior.Record,
                    ConcurrencyPolicy = NavigationConcurrencyPolicy.CancelPrevious,
                    JournalCapacity = 32,
                },
                cancellationToken),
            presentAsync);

        await NavigateAndPresentAsync(
            () => _router.NavigateAsync(
                DogfoodRoutes.Workspace(),
                new WorkspaceRouteParameters(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                new NavigationOptions
                {
                    Mode = NavigationMode.Push,
                    ConcurrencyPolicy = NavigationConcurrencyPolicy.Queue,
                },
                cancellationToken),
            presentAsync);

        await NavigateAndPresentAsync(
            () => _router.NavigateAsync(
                DogfoodRoutes.Search(),
                new SearchRouteParameters { Term = "warehouse", Page = 3, Selection = "SKU-0001" },
                new NavigationOptions
                {
                    Mode = NavigationMode.Replace,
                    HistoryBehavior = NavigationHistoryBehavior.ReplaceCurrent,
                    ForceReload = true,
                    Timeout = TimeSpan.FromSeconds(5),
                },
                cancellationToken),
            presentAsync);

        await NavigateAndPresentAsync(
            () => _router.NavigateByPathAsync(
                "inventory/stock",
                new NavigationOptions { HistoryBehavior = NavigationHistoryBehavior.Record },
                cancellationToken),
            presentAsync);

        await NavigateAndPresentAsync(
            () => _router.NavigateByUriAsync(
                new Uri("city://dogfood/catalog/products?source=deep-link#top"),
                new NavigationOptions { ConcurrencyPolicy = NavigationConcurrencyPolicy.RejectIfBusy },
                cancellationToken),
            presentAsync);

        await NavigateAndPresentAsync(
            () => _router.NavigateByPathAsync(
                "home",
                new NavigationOptions { AllowRedirect = true },
                cancellationToken),
            presentAsync);

        await NavigateAndPresentAsync(() => _router.BackAsync(cancellationToken), presentAsync);
        await NavigateAndPresentAsync(() => _router.ForwardAsync(cancellationToken), presentAsync);

        Console.WriteLine(
            "DESKTOP_DOGFOOD_ROUTING routes=96 layouts=6 groups=8 indexes=10 regular=60 redirects=6 extensions=6 viewModels=64 commands=96 entries=8");
    }

    public async Task<NavigationResult> NavigateSectionAsync(
        string section,
        CancellationToken cancellationToken)
    {
        var route = section switch
        {
            "Dashboard" => DogfoodRoutes.DashboardOverview(),
            "Commerce" => DogfoodRoutes.Products(),
            "Fulfillment" => DogfoodRoutes.Shipments(),
            "Customers" => DogfoodRoutes.Customers(),
            "Operations" => DogfoodRoutes.Reports(),
            "Administration" => DogfoodRoutes.Users(),
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown workspace section."),
        };

        return EnsureNavigable(await _router.NavigateAsync(
            route,
            new NavigationOptions
            {
                Mode = NavigationMode.Push,
                HistoryBehavior = NavigationHistoryBehavior.Record,
                ConcurrencyPolicy = NavigationConcurrencyPolicy.CancelPrevious,
            },
            cancellationToken));
    }

    public async Task<RouteTraversalSummary> TraverseCatalogAsync(
        Func<NavigationResult, Task> presentAsync,
        DogfoodRunLedger ledger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presentAsync);
        ArgumentNullException.ThrowIfNull(ledger);

        var snapshot = _graphProvider.CurrentSnapshot;
        var navigated = 0;
        var redirected = 0;
        var indexed = 0;
        foreach (var route in snapshot.Routes.OrderBy(static route => route.RouteId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ledger.Record("route-definition", route.RouteId);

            if (route.Kind is RouteDefinitionKind.Layout or RouteDefinitionKind.Group or RouteDefinitionKind.ExtensionPoint)
            {
                VerifyStructuralRoute(snapshot, route);
                continue;
            }

            var path = CreateSamplePath(snapshot, route);
            if (path is null)
            {
                VerifyStructuralRoute(snapshot, route);
                continue;
            }

            var result = EnsureNavigable(await _router.NavigateByPathAsync(
                path,
                new NavigationOptions
                {
                    OutletName = route.OutletName,
                    Mode = NavigationMode.Replace,
                    HistoryBehavior = NavigationHistoryBehavior.Record,
                    ConcurrencyPolicy = NavigationConcurrencyPolicy.Queue,
                    JournalCapacity = 128,
                },
                cancellationToken));
            await presentAsync(result);
            ledger.Record("navigation", route.RouteId);
            navigated++;
            redirected += result.Status == NavigationResultStatus.Redirected ? 1 : 0;
            indexed += route.Kind == RouteDefinitionKind.Index ? 1 : 0;
        }

        if (ledger.Covered("route-definition") != 96 || navigated < 66 || redirected != 6)
        {
            throw new InvalidOperationException(
                $"Route traversal was incomplete: definitions={ledger.Covered("route-definition")}, navigated={navigated}, indexes={indexed}, redirects={redirected}.");
        }

        Console.WriteLine(
            $"DESKTOP_DOGFOOD_ROUTE_TRAVERSAL definitions=96 navigated={navigated} indexes={indexed} redirects={redirected}");
        return new RouteTraversalSummary(snapshot.Routes.Count, navigated, indexed, redirected);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _navigationScope.Dispose();
        }
    }

    private void VerifyCatalogs()
    {
        var routes = _graphProvider.CurrentSnapshot.Routes;
        var expectedKinds = new Dictionary<RouteDefinitionKind, int>
        {
            [RouteDefinitionKind.Layout] = 6,
            [RouteDefinitionKind.Group] = 8,
            [RouteDefinitionKind.Index] = 10,
            [RouteDefinitionKind.Route] = 60,
            [RouteDefinitionKind.Redirect] = 6,
            [RouteDefinitionKind.ExtensionPoint] = 6,
        };

        if (routes.Count != 96 || expectedKinds.Any(pair => routes.Count(route => route.Kind == pair.Key) != pair.Value))
        {
            throw new InvalidOperationException("Generated Router manifest does not match the 96-route design ledger.");
        }

        var viewModels = DogfoodViewModelCatalog.Types;
        if (viewModels.Count != 64 || viewModels.Distinct().Count() != 64)
        {
            throw new InvalidOperationException("MVVM catalog must contain exactly 64 unique ViewModel types.");
        }

        var commandCount = viewModels.Sum(type => type
            .GetProperties()
            .Count(property => typeof(ICommand).IsAssignableFrom(property.PropertyType)));
        if (commandCount != 96)
        {
            throw new InvalidOperationException($"MVVM catalog expected 96 commands; observed {commandCount}.");
        }
    }

    private static void VerifyStructuralRoute(RouteGraphSnapshot snapshot, RouteDescriptor route)
    {
        if (route.ParentRouteId is not null)
        {
            _ = snapshot.GetRequiredRoute(route.ParentRouteId);
        }

        switch (route.Kind)
        {
            case RouteDefinitionKind.Layout when route.ViewModelTarget is null:
                throw new InvalidOperationException($"Layout route '{route.RouteId}' has no ViewModel target.");
            case RouteDefinitionKind.Group when route.Template is null:
                throw new InvalidOperationException($"Group route '{route.RouteId}' has no path template.");
            case RouteDefinitionKind.ExtensionPoint when string.IsNullOrWhiteSpace(route.ExtensionPoint):
                throw new InvalidOperationException($"Extension point '{route.RouteId}' has no contribution id.");
        }
    }

    private static string? CreateSamplePath(RouteGraphSnapshot snapshot, RouteDescriptor route)
    {
        var segments = new Stack<RouteTemplateSegment>();
        for (var current = route; current is not null; current = current.ParentRouteId is null
                 ? null
                 : snapshot.GetRequiredRoute(current.ParentRouteId))
        {
            if (current.Template is null)
            {
                continue;
            }

            for (var index = current.Template.Segments.Count - 1; index >= 0; index--)
            {
                segments.Push(current.Template.Segments[index]);
            }
        }

        if (segments.Count == 0)
        {
            return route.Kind == RouteDefinitionKind.Index ? string.Empty : null;
        }

        var path = new List<string>(segments.Count);
        foreach (var segment in segments)
        {
            if (segment.Kind == RouteTemplateSegmentKind.Literal)
            {
                path.Add(segment.Literal!);
                continue;
            }

            if (segment.IsOptional && segment.DefaultValue is null)
            {
                continue;
            }

            var value = CreateSampleValue(segment);
            if (segment.Kind == RouteTemplateSegmentKind.CatchAll)
            {
                path.AddRange(value.Split('/', StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                path.Add(Uri.EscapeDataString(value));
            }
        }

        return string.Join('/', path);
    }

    private static string CreateSampleValue(RouteTemplateSegment segment)
    {
        if (segment.DefaultValue is not null)
        {
            return segment.DefaultValue;
        }

        var constraints = segment.Constraints;
        if (constraints.Any(static value => value.StartsWith("regex(", StringComparison.OrdinalIgnoreCase)))
        {
            return "SKU-0001";
        }

        if (constraints.Contains("guid", StringComparer.OrdinalIgnoreCase))
        {
            return "11111111-1111-1111-1111-111111111111";
        }

        if (constraints.Contains("bool", StringComparer.OrdinalIgnoreCase))
        {
            return "true";
        }

        if (constraints.Contains("datetime", StringComparer.OrdinalIgnoreCase))
        {
            return "2026-09-11";
        }

        if (constraints.Contains("alpha", StringComparer.OrdinalIgnoreCase))
        {
            var length = ReadIntegerConstraint(constraints, "length") ?? 3;
            return new string('a', Math.Max(1, length));
        }

        var exactLength = ReadIntegerConstraint(constraints, "length");
        if (exactLength is not null)
        {
            return new string('x', exactLength.Value);
        }

        if (constraints.Any(static value => value is "int" or "long" or "decimal" or "double" or "float") ||
            constraints.Any(static value => value.StartsWith("min(", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("max(", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("range(", StringComparison.OrdinalIgnoreCase)))
        {
            return "1";
        }

        var minimumLength = ReadIntegerConstraint(constraints, "minlength") ?? 0;
        var valueLength = Math.Max(4, minimumLength);
        var value = segment.Kind == RouteTemplateSegmentKind.CatchAll
            ? "sample/path"
            : new string('x', valueLength);
        var maximumLength = ReadIntegerConstraint(constraints, "maxlength");
        return maximumLength is null || value.Length <= maximumLength
            ? value
            : value[..maximumLength.Value];
    }

    private static int? ReadIntegerConstraint(IReadOnlyList<string> constraints, string name)
    {
        var prefix = name + "(";
        var constraint = constraints.FirstOrDefault(value =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'));
        return constraint is not null &&
               int.TryParse(constraint[prefix.Length..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static async Task NavigateAndPresentAsync(
        Func<ValueTask<NavigationResult>> navigate,
        Func<NavigationResult, Task> presentAsync)
    {
        var result = EnsureNavigable(await navigate());
        await presentAsync(result);
    }

    private static NavigationResult EnsureNavigable(NavigationResult result)
    {
        if (result.Status is NavigationResultStatus.Success or NavigationResultStatus.Redirected &&
            result.ActiveRoute?.ViewModelTarget is not null)
        {
            return result;
        }

        throw new InvalidOperationException(
            $"Navigation failed: status={result.Status}, code={result.Error?.Code}, message={result.Error?.Message}");
    }
}

internal sealed record RouteTraversalSummary(
    int DefinitionCount,
    int NavigationCount,
    int IndexCount,
    int RedirectCount);
