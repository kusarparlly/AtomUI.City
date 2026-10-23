using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Fixtures;
using AtomUI.City.Localization;
using AtomUI.City.Mvvm;
using AtomUI.City.Routing;
using AtomUI.City.Security;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

[assembly: LanguagePackage(
    "Matrix.en-US",
    "en-US",
    Scope = ResourceScope.Host,
    ResourceBaseName = "AtomUI.City.NativeAot.RuntimeMatrix.Localization.Matrix.en-US.locpack.json")]
[assembly: LocalizedResource(
    "Matrix.Greeting",
    "Matrix.en-US",
    Scope = ResourceScope.Host,
    Culture = "en-US",
    Critical = true)]

namespace AtomUI.City.NativeAot.RuntimeMatrix;

[ApplicationModule]
public sealed class MatrixApplicationModule : ModuleBase
{
    private static int _initializationCount;
    private static int _shutdownCount;

    public static int InitializationCount => Volatile.Read(ref _initializationCount);
    public static int ShutdownCount => Volatile.Read(ref _shutdownCount);

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddState();
        context.Services.AddRouting(global::AtomUI.City.Generated.GeneratedRoutingRouteManifest.CreateDescriptors());
        context.Services.AddSecurity();
        context.Services.AddLocalization();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        Interlocked.Increment(ref _initializationCount);
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        Interlocked.Increment(ref _shutdownCount);
    }
}

public readonly record struct MatrixRouteParameters(int Id);

[RouteMap]
public static partial class MatrixRoutes
{
    [Route("orders/{id:int}", typeof(MatrixRouteTarget), Id = "matrix.orders")]
    public static partial RouteReference<MatrixRouteParameters> Orders();
}

public sealed class MatrixRouteTarget;

public sealed partial class MatrixViewModel : ValidatingViewModelBase
{
    private string? _name = "valid";
    private int _age = 18;

    [Required]
    [StringLength(12, MinimumLength = 3)]
    public string? Name
    {
        get => _name;
        set => SetProperty(ref _name, value, validate: true);
    }

    [Range(18, 120)]
    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value, validate: true);
    }
}

public sealed record MatrixResult(
    bool Success,
    int ModuleInitializationCount,
    int ModuleShutdownCount,
    int StateNotificationCount,
    int RouteCount,
    bool SecurityAllowed,
    bool SecurityChallenge,
    bool MvvmGeneratedValidation,
    bool LocalizationEmbedded,
    bool LocalizationFile,
    bool LocalizationInMemory,
    string FrameworkDescription);

[JsonSerializable(typeof(MatrixResult))]
internal sealed partial class MatrixJsonContext : JsonSerializerContext;

internal static class Program
{
    public static Task<int> Main()
    {
        return ProcessEntryPoint.RunAsync(RunAsync);
    }

    private static async Task<int> RunAsync()
    {
        var builder = ApplicationHost.CreateBuilder([]);
        builder.UseModule<MatrixApplicationModule>();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.NativeAot.RuntimeMatrix";
            options.ApplicationName = "AtomUI.City NativeAOT Runtime Matrix";
        });

        await using var host = builder.Build();
        await host.StartAsync();
        Ensure(MatrixApplicationModule.InitializationCount == 1, "Core module initialization did not run exactly once.");

        var stateNotificationCount = VerifyState(host.Services);
        var routeCount = VerifyRouting(host.Services);
        var (securityAllowed, securityChallenge) = await VerifySecurityAsync(host.Services);
        var mvvmGeneratedValidation = VerifyMvvm();
        var (embedded, file, inMemory) = await VerifyLocalizationAsync(host.Services);

        await host.StopAsync();
        Ensure(MatrixApplicationModule.ShutdownCount == 1, "Core module shutdown did not run exactly once.");

        var result = new MatrixResult(
            Success: true,
            ModuleInitializationCount: MatrixApplicationModule.InitializationCount,
            ModuleShutdownCount: MatrixApplicationModule.ShutdownCount,
            StateNotificationCount: stateNotificationCount,
            RouteCount: routeCount,
            SecurityAllowed: securityAllowed,
            SecurityChallenge: securityChallenge,
            MvvmGeneratedValidation: mvvmGeneratedValidation,
            LocalizationEmbedded: embedded,
            LocalizationFile: file,
            LocalizationInMemory: inMemory,
            FrameworkDescription: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

        Console.WriteLine("CITY_NATIVEAOT_RUNTIME_MATRIX_OK");
        Console.WriteLine(JsonSerializer.Serialize(result, MatrixJsonContext.Default.MatrixResult));
        return 0;
    }

    private static int VerifyState(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IStateRegistry>();
        var state = services.GetRequiredService<IApplicationState>();
        var writer = services.GetRequiredService<IApplicationStateWriter>();
        var key = new StateKey<int>("matrix.counter");
        registry.Add(StateDefinition.Create(key, 20));
        var notifications = new List<StateChangedEventArgs<int>>();
        using var subscription = state.Observe(key, notifications.Add);
        Ensure(writer.Set(key, 21), "State mutation was not committed.");
        using var computed = new ComputedState<int>(() => state.Get(key).Value * 2, state.Get(key));
        Ensure(computed.Value == 42, "Computed state returned the wrong value.");
        Ensure(notifications.Count == 2, "State did not emit initial and changed notifications.");
        return notifications.Count;
    }

    private static int VerifyRouting(IServiceProvider services)
    {
        var graph = services.GetRequiredService<IRouteGraphProvider>().CurrentSnapshot;
        var route = MatrixRoutes.Orders();
        var match = graph.Matcher.Match("orders/42");
        Ensure(route.Id == "matrix.orders", "Generated route reference has the wrong identity.");
        Ensure(match.Status == RouteMatchStatus.Success, "Generated route did not match.");
        Ensure(match.Route.RouteId == route.Id && match.Parameters["id"] == "42", "Route match metadata is incorrect.");
        return graph.Routes.Count;
    }

    private static async Task<(bool Allowed, bool Challenge)> VerifySecurityAsync(IServiceProvider services)
    {
        var registry = services.GetRequiredService<PermissionRegistry>();
        registry.Add(new PermissionDescriptor("matrix.read"));
        var checker = services.GetRequiredService<IPermissionChecker>();
        var identity = new ClaimsIdentity(authenticationType: "Matrix");
        identity.AddClaim(new Claim(SecurityClaimTypes.Permission, "matrix.read"));
        var allowed = await checker.CheckAsync(new ClaimsPrincipal(identity), "matrix.read");
        var challenge = await checker.CheckAsync(new ClaimsPrincipal(new ClaimsIdentity()), "matrix.read");
        Ensure(allowed.Status == AuthorizationResultStatus.Allowed, "Registered permission was not allowed.");
        Ensure(challenge.Status == AuthorizationResultStatus.Challenge, "Anonymous permission check did not challenge.");
        return (true, true);
    }

    private static bool VerifyMvvm()
    {
        using var viewModel = new MatrixViewModel();
        var propertyChanges = new List<string?>();
        viewModel.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);
        viewModel.Name = null;
        viewModel.Age = 17;
        Ensure(viewModel.HasErrors, "Generated validation did not report invalid values.");
        Ensure(viewModel.GetErrors(nameof(MatrixViewModel.Name)).Cast<string>().Any(), "Required validation was not generated.");
        Ensure(viewModel.GetErrors(nameof(MatrixViewModel.Age)).Cast<string>().Any(), "Range validation was not generated.");
        viewModel.Name = "matrix";
        viewModel.Age = 42;
        Ensure(!viewModel.HasErrors, "Generated validation did not clear repaired values.");
        Ensure(propertyChanges.Contains(nameof(MatrixViewModel.Name), StringComparer.Ordinal), "Observable property notification was not raised.");
        return true;
    }

    private static async Task<(bool Embedded, bool File, bool InMemory)> VerifyLocalizationAsync(IServiceProvider services)
    {
        var registry = services.GetRequiredService<LanguagePackageRegistry>();
        global::AtomUI.City.Generated.GeneratedLocalizationManifest.RegisterPackages(registry, "matrix.generated");

        var filePath = Path.Combine(AppContext.BaseDirectory, "Localization", "Matrix.fr-FR.locpack.json");
        Ensure(registry.Register(
            new LanguagePackageDescriptor("Matrix.fr-FR", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), ResourceScope.Host)
            {
                ProviderKind = LanguagePackageProviderKind.File,
                Location = filePath,
                AllowedRootPath = AppContext.BaseDirectory,
            },
            "matrix.file").Succeeded,
            "File localization descriptor registration failed.");
        Ensure(registry.Register(
            new LanguagePackageDescriptor("Matrix.zh-CN", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), ResourceScope.Host)
            {
                ProviderKind = LanguagePackageProviderKind.InMemory,
                InMemoryResources = new Dictionary<string, string> { ["Matrix.Greeting"] = "memory" },
            },
            "matrix.memory").Succeeded,
            "In-memory localization descriptor registration failed.");

        var localization = services.GetRequiredService<ILocalizationService>();
        var embedded = await ReadLocalizedValueAsync(localization, "en-US", "embedded");
        var file = await ReadLocalizedValueAsync(localization, "fr-FR", "file");
        var inMemory = await ReadLocalizedValueAsync(localization, "zh-CN", "memory");
        return (embedded, file, inMemory);
    }

    private static async Task<bool> ReadLocalizedValueAsync(
        ILocalizationService localization,
        string culture,
        string expected)
    {
        var cultureResult = await localization.SetCultureAsync(culture);
        Ensure(cultureResult.Succeeded, $"Culture '{culture}' failed to load: {cultureResult.Error?.Message}");
        var value = await localization.GetStringAsync("Matrix.Greeting");
        Ensure(!value.IsMissing && value.Value == expected, $"Culture '{culture}' returned '{value.Value}'.");
        return true;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
