using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Tests;

public sealed class ModuleBaseTests
{
    [Fact]
    public void ServiceConfigurationContractIsSynchronous()
    {
        Assert.Equal(typeof(void), typeof(IModule).GetMethod(nameof(IModule.PreConfigureServices))!.ReturnType);
        Assert.Equal(typeof(void), typeof(IModule).GetMethod(nameof(IModule.ConfigureServices))!.ReturnType);
        Assert.Equal(typeof(void), typeof(IModule).GetMethod(nameof(IModule.PostConfigureServices))!.ReturnType);
        Assert.Null(typeof(IModule).GetMethod("PreConfigureServicesAsync"));
        Assert.Null(typeof(IModule).GetMethod("ConfigureServicesAsync"));
        Assert.Null(typeof(IModule).GetMethod("PostConfigureServicesAsync"));
    }

    [Fact]
    public void AsyncLifecycleCallbacksRequireAnExplicitCancellationToken()
    {
        var callbackNames = new[]
        {
            nameof(IModule.ConfigureContributionsAsync),
            nameof(IModule.OnPreApplicationInitializationAsync),
            nameof(IModule.OnApplicationInitializationAsync),
            nameof(IModule.OnPostApplicationInitializationAsync),
            nameof(IModule.OnApplicationShutdownAsync),
        };

        foreach (var callbackName in callbackNames)
        {
            var tokenParameter = typeof(IModule).GetMethod(callbackName)!.GetParameters()[1];
            Assert.Equal(typeof(CancellationToken), tokenParameter.ParameterType);
            Assert.False(tokenParameter.IsOptional);
        }
    }

    [Fact]
    public async Task LifecycleMethodsRunInOrder()
    {
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();
        var calls = new List<string>();
        var module = new RecordingModule(calls);

        module.PreConfigureServices(new ServiceConfigurationContext(applicationContext, services));
        module.ConfigureServices(new ServiceConfigurationContext(applicationContext, services));
        module.PostConfigureServices(new ServiceConfigurationContext(applicationContext, services));
        await module.ConfigureContributionsAsync(new ContributionConfigurationContext(applicationContext, serviceProvider), CancellationToken.None);
        await module.OnPreApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), CancellationToken.None);
        await module.OnApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), CancellationToken.None);
        await module.OnPostApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), CancellationToken.None);
        await module.OnApplicationShutdownAsync(new ApplicationShutdownContext(applicationContext, serviceProvider), CancellationToken.None);

        Assert.Equal(
            [
                "PreConfigureServices",
                "ConfigureServices",
                "PostConfigureServices",
                "ConfigureContributions",
                "OnPreApplicationInitialization",
                "OnApplicationInitialization",
                "OnPostApplicationInitialization",
                "OnApplicationShutdown",
            ],
            calls);
    }

    [Fact]
    public async Task DefaultLifecycleMethodsComplete()
    {
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        using var serviceProvider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();
        var module = new EmptyModule();

        module.PreConfigureServices(new ServiceConfigurationContext(applicationContext, services));
        module.ConfigureServices(new ServiceConfigurationContext(applicationContext, services));
        module.PostConfigureServices(new ServiceConfigurationContext(applicationContext, services));
        await module.ConfigureContributionsAsync(new ContributionConfigurationContext(applicationContext, serviceProvider), CancellationToken.None);
        await module.OnPreApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), CancellationToken.None);
        await module.OnApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), CancellationToken.None);
        await module.OnPostApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), CancellationToken.None);
        await module.OnApplicationShutdownAsync(new ApplicationShutdownContext(applicationContext, serviceProvider), CancellationToken.None);
    }

    [Fact]
    public async Task LifecycleMethodsRejectNullContext()
    {
        var module = new EmptyModule();

        Assert.Throws<ArgumentNullException>(() => module.PreConfigureServices(null!));
        Assert.Throws<ArgumentNullException>(() => module.ConfigureServices(null!));
        Assert.Throws<ArgumentNullException>(() => module.PostConfigureServices(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await module.ConfigureContributionsAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await module.OnPreApplicationInitializationAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await module.OnApplicationInitializationAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await module.OnPostApplicationInitializationAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await module.OnApplicationShutdownAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task AsyncLifecycleMethodsObserveCancellationBeforeSynchronousConvenienceMethod()
    {
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        var services = new ServiceCollection();
        using var serviceProvider = services.BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();
        using var cancellation = new CancellationTokenSource();
        var calls = new List<string>();
        var module = new RecordingModule(calls);

        cancellation.Cancel();

        await AssertCanceledBeforeSyncCall(
            token => module.ConfigureContributionsAsync(new ContributionConfigurationContext(applicationContext, serviceProvider), token),
            calls,
            cancellation.Token);
        await AssertCanceledBeforeSyncCall(
            token => module.OnPreApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), token),
            calls,
            cancellation.Token);
        await AssertCanceledBeforeSyncCall(
            token => module.OnApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), token),
            calls,
            cancellation.Token);
        await AssertCanceledBeforeSyncCall(
            token => module.OnPostApplicationInitializationAsync(new ApplicationInitializationContext(applicationContext, serviceProvider, applicationScope), token),
            calls,
            cancellation.Token);
        await AssertCanceledBeforeSyncCall(
            token => module.OnApplicationShutdownAsync(new ApplicationShutdownContext(applicationContext, serviceProvider), token),
            calls,
            cancellation.Token);
    }

    [Fact]
    public void ApplicationInitializationContextRequiresAndExposesApplicationScope()
    {
        var applicationContext = ApplicationHostTestBuilder.CreateContext();
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var applicationScope = CreateApplicationScope();

        var context = new ApplicationInitializationContext(
            applicationContext,
            serviceProvider,
            applicationScope);

        Assert.Same(applicationScope, context.ApplicationScope);
        Assert.Throws<ArgumentNullException>(() => new ApplicationInitializationContext(
            applicationContext,
            serviceProvider,
            null!));
    }

    private static LifecycleScope CreateApplicationScope()
    {
        return LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "application");
    }

    private static async Task AssertCanceledBeforeSyncCall(
        Func<CancellationToken, ValueTask> invoke,
        List<string> calls,
        CancellationToken cancellationToken)
    {
        calls.Clear();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await invoke(cancellationToken));

        Assert.Empty(calls);
    }

    private sealed class RecordingModule(List<string> calls) : ModuleBase
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            calls.Add("PreConfigureServices");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            calls.Add("ConfigureServices");
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            calls.Add("PostConfigureServices");
        }

        public override void ConfigureContributions(ContributionConfigurationContext context)
        {
            calls.Add("ConfigureContributions");
        }

        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            calls.Add("OnPreApplicationInitialization");
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            calls.Add("OnApplicationInitialization");
        }

        public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
        {
            calls.Add("OnPostApplicationInitialization");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            calls.Add("OnApplicationShutdown");
        }
    }

    private sealed class EmptyModule : ModuleBase;
}
