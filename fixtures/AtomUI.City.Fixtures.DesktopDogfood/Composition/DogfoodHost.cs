using System.Globalization;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Localization;
using AtomUI.City.Presentation;
using AtomUI.City.Routing;
using AtomUI.City.Security;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;
using AtomUI.City.Fixtures.StressCli.DataIntegration;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class DogfoodHost
{
    public static IApplicationHostBuilder CreateBuilder(
        string[] args,
        StressDataServer dataServer,
        DogfoodExternalDataServerProcess externalDataServer,
        DogfoodSecurityWorkspace securityWorkspace,
        DogfoodRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(dataServer);
        ArgumentNullException.ThrowIfNull(externalDataServer);
        ArgumentNullException.ThrowIfNull(securityWorkspace);
        ArgumentNullException.ThrowIfNull(options);
        var dataEndpoints = dataServer.Endpoints;
        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.DesktopDogfood";
            options.ApplicationName = "City Operations Workbench";
            options.ApplicationVersion = "0.1.0";
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<DogfoodRunLedger>();
            services.AddSingleton<DogfoodResourceMonitor>();
            services.AddSingleton(dataServer);
            services.AddSingleton(dataEndpoints);
            services.AddSingleton(externalDataServer);
            services.AddHttpClient(DogfoodRemoteOperations.ClientName, client => client.BaseAddress = dataEndpoints.Http)
                .RemoveAllLoggers();
            services.AddHttpClient(
                    DogfoodExternalRemoteOperations.ClientName,
                    client => client.BaseAddress = externalDataServer.Endpoints.Http)
                .RemoveAllLoggers();
            services.AddState();
            services.AddSingleton(securityWorkspace);
            services.AddSingleton<DogfoodRefreshingAccessTokenProvider>();
            services.AddSingleton<IAccessTokenProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<DogfoodRefreshingAccessTokenProvider>());
            services.AddSecurity(new SecurityPersistenceOptions(
                securityWorkspace.RootPath,
                "dogfood-api"));
            services.AddRouting(AtomUI.City.Generated.GeneratedRoutingRouteManifest.CreateDescriptors());
            services.AddLocalization(options =>
            {
                options.DefaultCulture = CultureInfo.GetCultureInfo("en-US");
                options.DefaultUICulture = CultureInfo.GetCultureInfo("en-US");
                DogfoodLocalizationCatalog.AddTo(options);
            });
            services.AddPresentation(new PresentationQueueOptions
            {
                OutletPendingCapacity = 32,
                ModalInteractionPendingCapacity = 8,
            });
            services.AddSingleton<DogfoodWindowFactory>();
            services.AddSingleton<DogfoodInteractionViewModel>();
            services.AddSingleton<DogfoodDesktopCoordinator>();
            services.AddSingleton<DogfoodStateWorkload>();
            services.AddSingleton<DogfoodEventWorkload>();
            services.AddSingleton<DogfoodRoutingWorkload>();
            services.AddSingleton<DogfoodLocalizationWorkload>();
            services.AddSingleton<DogfoodSecurityWorkload>();
            services.AddSingleton<DogfoodRemoteOperations>();
            services.AddSingleton<DogfoodExternalRemoteOperations>();
            services.AddSingleton<DogfoodDataRequestProbe>();
            services.AddSingleton<AtomUI.City.Data.IDataRequestHandler>(provider =>
                provider.GetRequiredService<DogfoodDataRequestProbe>());
            services.AddSingleton<DogfoodDataWorkload>();
            services.AddSingleton<DogfoodDataResilienceWorkload>();
            services.AddSingleton<DogfoodNetworkLabWorkload>();
            services.AddSingleton<DogfoodBusinessWorkload>();
            services.AddSingleton<DogfoodConcurrencyWorkload>();
            services.AddSingleton<DogfoodScenarioOrchestrator>();
            services.AddSingleton<DogfoodContributionWorkload>();
            services.AddSingleton<DogfoodAutomationWorkload>();
            services.AddSingleton<DogfoodHeadlessControlWorkload>();
            services.AddSingleton<DogfoodApiCoverageWorkload>();
            DogfoodViewModelCatalog.Register(services);
        });

        builder
            .UseModule<EventBusModule>()
            .UseModule<RoutingModule>()
            .UseModule<DataModule>()
            .UseModule<PresentationModule>()
            .UseModule<ShellPresentationModule>();

        return builder;
    }
}
