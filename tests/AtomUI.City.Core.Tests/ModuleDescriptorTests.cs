using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Core.Tests;

public sealed class ModuleDescriptorTests
{
    [Fact]
    public void ModuleGraphFailureRecordsMissingDependencyPath()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistryTestFactory.Create([typeof(DependsOnMissingModule)]));

        Assert.Contains(typeof(DependsOnMissingModule).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(MissingModule).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleGraphFailureRecordsCyclePath()
    {
        CycleStartModule.CreatedCount = 0;
        CycleEndModule.CreatedCount = 0;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistryTestFactory.Create([typeof(CycleStartModule), typeof(CycleEndModule)]));

        Assert.Contains(typeof(CycleStartModule).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(CycleEndModule).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, CycleStartModule.CreatedCount);
        Assert.Equal(0, CycleEndModule.CreatedCount);
    }

    [Fact]
    public void HostBuildRejectsIndirectCycleBeforeAnyModuleFactoryRuns()
    {
        IndirectCycleOne.CreatedCount = 0;
        IndirectCycleTwo.CreatedCount = 0;
        IndirectCycleThree.CreatedCount = 0;
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<IndirectCycleThree>();
        builder.UseModule<IndirectCycleOne>();
        builder.UseModule<IndirectCycleTwo>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(typeof(IndirectCycleOne).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(IndirectCycleTwo).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(IndirectCycleThree).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, IndirectCycleOne.CreatedCount);
        Assert.Equal(0, IndirectCycleTwo.CreatedCount);
        Assert.Equal(0, IndirectCycleThree.CreatedCount);
    }

    [Fact]
    public async Task ValidatedGraphOrdersDiamondBeforeCreatingAnyModule()
    {
        DiamondRecorder.Reset();
        var registrations = new ModuleRegistration[]
        {
            Registration<DiamondRoot>(),
            Registration<DiamondRight>(),
            Registration<DiamondFoundation>(),
            Registration<DiamondLeft>(),
        };

        var graph = ModuleGraphValidator.Validate(registrations);

        Assert.Empty(DiamondRecorder.Created);
        Assert.Equal(
            [typeof(DiamondFoundation), typeof(DiamondLeft), typeof(DiamondRight), typeof(DiamondRoot)],
            graph.OrderedRegistrations.Select(registration => registration.ModuleType));
        var exposed = Assert.IsAssignableFrom<IList<ModuleRegistration>>(graph.OrderedRegistrations);
        Assert.Throws<NotSupportedException>(() => exposed[0] = registrations[0]);

        await using var registry = ModuleRegistry.Create(graph);
        Assert.Equal(
            ["foundation", "left", "right", "root"],
            DiamondRecorder.Created);
    }

    [Fact]
    public void ModuleGraphFailureRecordsDuplicateModuleId()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistryTestFactory.Create([typeof(DuplicateModuleA), typeof(DuplicateModuleB)]));

        Assert.Contains("DuplicateModule", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DependenciesRejectExternalListMutation()
    {
        var sourceDependencies = new List<ModuleDependencyDescriptor>
        {
            new(typeof(DependencyModule), optional: false),
        };
        var descriptor = new ModuleDescriptor(
            "TestModule",
            typeof(TestModule),
            version: null,
            description: null,
            sourceDependencies);
        var dependencies = Assert.IsAssignableFrom<IList<ModuleDependencyDescriptor>>(descriptor.Dependencies);

        Assert.Throws<NotSupportedException>(() => dependencies[0] = new ModuleDependencyDescriptor(
            typeof(ReplacementModule),
            optional: true));
        Assert.Equal(typeof(DependencyModule), descriptor.Dependencies[0].ModuleType);
        Assert.False(descriptor.Dependencies[0].Optional);
    }

    [Fact]
    public void DependenciesRejectNullEntriesAtConstructionBoundary()
    {
        var dependencies = new ModuleDependencyDescriptor[]
        {
            new(typeof(DependencyModule), optional: false),
            null!,
        };

        var exception = Assert.Throws<ArgumentException>(() => new ModuleDescriptor(
            "TestModule",
            typeof(TestModule),
            version: null,
            description: null,
            dependencies));

        Assert.Equal("dependencies", exception.ParamName);
    }

    [Fact]
    public void ModuleDescriptorRejectsNonModuleType()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ModuleDescriptor(
            "Invalid",
            typeof(string),
            version: null,
            description: null,
            []));

        Assert.Contains(nameof(IModule), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleDependencyDescriptorRejectsNonModuleType()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ModuleDependencyDescriptor(typeof(string), optional: false));

        Assert.Contains(nameof(IModule), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleDescriptorDefaultsToApplicationOrigin()
    {
        var descriptor = new ModuleDescriptor(
            "TestModule",
            typeof(TestModule),
            version: null,
            description: null,
            []);

        Assert.Equal(ModuleOrigin.Application, descriptor.Origin);
        Assert.Null(descriptor.PluginId);
    }

    [Fact]
    public void ModuleDescriptorCanDescribePluginOrigin()
    {
        var descriptor = new ModuleDescriptor(
            "PluginModule",
            typeof(TestModule),
            version: "1.0.0",
            description: "Plugin module",
            [],
            ModuleOrigin.Plugin,
            "sales-plugin");

        Assert.Equal(ModuleOrigin.Plugin, descriptor.Origin);
        Assert.Equal("sales-plugin", descriptor.PluginId);
    }

    [Fact]
    public void PluginModuleDescriptorRequiresPluginId()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new ModuleDescriptor(
            "PluginModule",
            typeof(TestModule),
            version: null,
            description: null,
            [],
            ModuleOrigin.Plugin,
            pluginId: null));

        Assert.Contains("plugin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationModuleDescriptorRejectsPluginId()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ModuleDescriptor(
            "ApplicationModule",
            typeof(TestModule),
            version: null,
            description: null,
            [],
            ModuleOrigin.Application,
            "sales-plugin"));

        Assert.Contains("plugin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestModule : ModuleBase;

    private sealed class DependencyModule : ModuleBase;

    private sealed class ReplacementModule : ModuleBase;

    private static ModuleRegistration Registration<TModule>()
        where TModule : IModule, new()
    {
        return new ModuleRegistration(
            ModuleDescriptorFactory.CreateFromAttributes(typeof(TModule)),
            static () => new TModule());
    }

    [DependsOn(typeof(MissingModule))]
    private sealed class DependsOnMissingModule : ModuleBase;

    private sealed class MissingModule : ModuleBase;

    [DependsOn(typeof(CycleEndModule))]
    private sealed class CycleStartModule : ModuleBase
    {
        public static int CreatedCount;

        public CycleStartModule() => CreatedCount++;
    }

    [DependsOn(typeof(CycleStartModule))]
    private sealed class CycleEndModule : ModuleBase
    {
        public static int CreatedCount;

        public CycleEndModule() => CreatedCount++;
    }

    [DependsOn(typeof(IndirectCycleTwo))]
    private sealed class IndirectCycleOne : ModuleBase
    {
        public static int CreatedCount;

        public IndirectCycleOne() => CreatedCount++;
    }

    [DependsOn(typeof(IndirectCycleThree))]
    private sealed class IndirectCycleTwo : ModuleBase
    {
        public static int CreatedCount;

        public IndirectCycleTwo() => CreatedCount++;
    }

    [DependsOn(typeof(IndirectCycleOne))]
    private sealed class IndirectCycleThree : ModuleBase
    {
        public static int CreatedCount;

        public IndirectCycleThree() => CreatedCount++;
    }

    private static class DiamondRecorder
    {
        public static List<string> Created { get; } = [];

        public static void Reset() => Created.Clear();
    }

    private sealed class DiamondFoundation : ModuleBase
    {
        public DiamondFoundation() => DiamondRecorder.Created.Add("foundation");
    }

    [DependsOn(typeof(DiamondFoundation))]
    private sealed class DiamondLeft : ModuleBase
    {
        public DiamondLeft() => DiamondRecorder.Created.Add("left");
    }

    [DependsOn(typeof(DiamondFoundation))]
    private sealed class DiamondRight : ModuleBase
    {
        public DiamondRight() => DiamondRecorder.Created.Add("right");
    }

    [DependsOn(typeof(DiamondLeft))]
    [DependsOn(typeof(DiamondRight))]
    private sealed class DiamondRoot : ModuleBase
    {
        public DiamondRoot() => DiamondRecorder.Created.Add("root");
    }

    [Module("DuplicateModule")]
    private sealed class DuplicateModuleA : ModuleBase;

    [Module("DuplicateModule")]
    private sealed class DuplicateModuleB : ModuleBase;
}
