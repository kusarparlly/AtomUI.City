using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Tests;

public sealed class GeneratedServiceRegistrationCatalogTests
{
    [Fact]
    public void SelectAppliesOnlyRegistrationsOwnedBySelectedModules()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();
        catalog.RegisterRegistrar(typeof(SelectedRegistrar), static () => new SelectedRegistrar());
        catalog.RegisterRegistrar(typeof(UnselectedRegistrar), static () => new UnselectedRegistrar());
        var selected = new[]
        {
            new ModuleRegistration(
                ModuleDescriptorFactory.CreateFromAttributes(typeof(SelectedModule)),
                static () => new SelectedModule()),
        };
        var services = new ServiceCollection();

        catalog.Select(selected)(services);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<SelectedService>());
        Assert.Null(provider.GetService<UnselectedService>());
    }

    [Fact]
    public void SelectDoesNotApplyPluginRegistrationsToRootCollection()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();
        catalog.RegisterRegistrar(typeof(UnselectedRegistrar), static () => new UnselectedRegistrar());
        var plugin = new ModuleRegistration(
            new ModuleDescriptor(
                "plugin",
                typeof(UnselectedModule),
                version: null,
                description: null,
                dependencies: [],
                ModuleOrigin.Plugin,
                "sample-plugin"),
            static () => new UnselectedModule());
        var services = new ServiceCollection();

        catalog.Select([plugin])(services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(UnselectedService));
    }

    [Fact]
    public void RegisterIsIdempotentForDiamondRegistrarAggregation()
    {
        SharedRegistrar.Reset();
        var catalog = new GeneratedServiceRegistrationCatalog();
        catalog.RegisterRegistrar(typeof(DiamondRootRegistrar), static () => new DiamondRootRegistrar());
        var selected = new[]
        {
            new ModuleRegistration(
                ModuleDescriptorFactory.CreateFromAttributes(typeof(SelectedModule)),
                static () => new SelectedModule()),
        };
        var services = new ServiceCollection();

        catalog.Select(selected)(services);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(SelectedService));
        Assert.Equal(1, SharedRegistrar.RegisterCount);
    }

    [Fact]
    public void DifferentRegistrarsCannotClaimTheSameOwner()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();
        catalog.RegisterRegistrar(typeof(SelectedRegistrar), static () => new SelectedRegistrar());

        var failure = Assert.Throws<InvalidOperationException>(() =>
            catalog.RegisterRegistrar(
                typeof(ConflictingSelectedRegistrar),
                static () => new ConflictingSelectedRegistrar()));

        Assert.Contains(typeof(SelectedModule).FullName!, failure.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(SelectedRegistrar).FullName!, failure.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(ConflictingSelectedRegistrar).FullName!, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrarCannotClaimOwnerFromAnotherAssembly()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();

        var failure = Assert.Throws<InvalidOperationException>(() =>
            catalog.RegisterRegistrar(typeof(ForeignOwnerRegistrar), static () => new ForeignOwnerRegistrar()));

        Assert.Contains(typeof(ForeignOwnerRegistrar).Assembly.GetName().Name!, failure.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(ModuleBase).Assembly.GetName().Name!, failure.Message, StringComparison.Ordinal);
        Assert.Contains("DependsOn", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OneRegistrarCanSubmitMultipleContributionsForItsLocalOwner()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();
        catalog.RegisterRegistrar(
            typeof(MultipleContributionRegistrar),
            static () => new MultipleContributionRegistrar());
        var selected = new[]
        {
            new ModuleRegistration(
                ModuleDescriptorFactory.CreateFromAttributes(typeof(SelectedModule)),
                static () => new SelectedModule()),
        };
        var services = new ServiceCollection();

        catalog.Select(selected)(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SelectedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SecondSelectedService));
    }

    [Fact]
    public void RegistrarContextCannotEscapeItsRegistrationTransaction()
    {
        EscapingRegistrar.Reset();
        var catalog = new GeneratedServiceRegistrationCatalog();
        catalog.RegisterRegistrar(typeof(EscapingRegistrar), static () => new EscapingRegistrar());

        var failure = Assert.Throws<InvalidOperationException>(() =>
            EscapingRegistrar.CapturedContext!.Register(
                typeof(SelectedModule),
                services => services.AddSingleton<SelectedService>()));

        Assert.Contains("no longer active", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateRegistrarDoesNotInvokeItsFactoryAgain()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();
        catalog.RegisterRegistrar(typeof(SelectedRegistrar), static () => new SelectedRegistrar());
        var duplicateFactoryCount = 0;

        catalog.RegisterRegistrar(typeof(SelectedRegistrar), () =>
        {
            duplicateFactoryCount++;
            return new SelectedRegistrar();
        });

        Assert.Equal(0, duplicateFactoryCount);
    }

    [Fact]
    public void RegistrarFactoryCannotSubstituteAnotherRegistrarIdentity()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();

        var failure = Assert.Throws<InvalidOperationException>(() =>
            catalog.RegisterRegistrar(typeof(SelectedRegistrar), static () => new UnselectedRegistrar()));

        Assert.Contains(typeof(SelectedRegistrar).FullName!, failure.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(UnselectedRegistrar).FullName!, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalOwnerMustBeAConcreteClosedModuleClass()
    {
        var catalog = new GeneratedServiceRegistrationCatalog();

        var failure = Assert.Throws<ArgumentException>(() =>
            catalog.RegisterRegistrar(typeof(AbstractOwnerRegistrar), static () => new AbstractOwnerRegistrar()));

        Assert.Contains("non-abstract, closed class", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedServicesRunBetweenPreConfigureAndConfigure()
    {
        var registry = ModuleRegistryTestFactory.Create([typeof(OrderModule)]);
        var services = new ServiceCollection();
        var order = new List<string>();
        OrderModule.Order = order;

        registry.ConfigureServices(
            ApplicationHostTestBuilder.CreateContext(),
            services,
            new AtomUI.City.Core.Diagnostics.InMemoryHostDiagnostics(),
            generatedServices: collection =>
            {
                order.Add("Generated");
                collection.AddSingleton<SelectedService>();
            });

        Assert.Equal(["Pre", "Generated", "Configure", "Post"], order);
        await registry.DisposeAsync();
    }

    private sealed class SelectedModule : ModuleBase;
    private sealed class UnselectedModule : ModuleBase;
    private sealed class SelectedService;
    private sealed class SecondSelectedService;
    private sealed class UnselectedService;

    private sealed class SelectedRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context) =>
            context.Register(
                typeof(SelectedModule),
                services => services.AddSingleton<SelectedService>());
    }

    private sealed class UnselectedRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context) =>
            context.Register(
                typeof(UnselectedModule),
                services => services.AddSingleton<UnselectedService>());
    }

    private sealed class ConflictingSelectedRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context) =>
            context.Register(
                typeof(SelectedModule),
                services => services.AddSingleton<SecondSelectedService>());
    }

    private sealed class ForeignOwnerRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context) =>
            context.Register(typeof(ModuleBase), static _ => { });
    }

    private sealed class MultipleContributionRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context)
        {
            context.Register(
                typeof(SelectedModule),
                services => services.AddSingleton<SelectedService>());
            context.Register(
                typeof(SelectedModule),
                services => services.AddSingleton<SecondSelectedService>());
        }
    }

    private sealed class DiamondRootRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context)
        {
            context.RegisterRegistrar(typeof(LeftRegistrar), static () => new LeftRegistrar());
            context.RegisterRegistrar(typeof(RightRegistrar), static () => new RightRegistrar());
        }
    }

    private sealed class LeftRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context) =>
            context.RegisterRegistrar(typeof(SharedRegistrar), static () => new SharedRegistrar());
    }

    private sealed class RightRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context) =>
            context.RegisterRegistrar(typeof(SharedRegistrar), static () => new SharedRegistrar());
    }

    private sealed class SharedRegistrar : IServiceRegistrar
    {
        public static int RegisterCount { get; private set; }

        public static void Reset() => RegisterCount = 0;

        public void Register(IServiceRegistrarContext context)
        {
            RegisterCount++;
            context.Register(
                typeof(SelectedModule),
                services => services.AddSingleton<SelectedService>());
        }
    }

    private sealed class EscapingRegistrar : IServiceRegistrar
    {
        public static IServiceRegistrarContext? CapturedContext { get; private set; }

        public static void Reset() => CapturedContext = null;

        public void Register(IServiceRegistrarContext context) => CapturedContext = context;
    }

    private abstract class AbstractOwnerModule : ModuleBase;

    private sealed class AbstractOwnerRegistrar : IServiceRegistrar
    {
        public void Register(IServiceRegistrarContext context) =>
            context.Register(typeof(AbstractOwnerModule), static _ => { });
    }

    private sealed class OrderModule : ModuleBase
    {
        public static List<string> Order { get; set; } = [];

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            Order.Add("Pre");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Assert.Contains(context.Services, descriptor => descriptor.ServiceType == typeof(SelectedService));
            Order.Add("Configure");
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            Order.Add("Post");
        }
    }
}
