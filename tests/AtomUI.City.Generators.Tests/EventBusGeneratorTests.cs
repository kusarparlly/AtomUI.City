using AtomUI.City.Generators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Generators.Tests;

public sealed class EventBusGeneratorTests
{
    [Fact]
    public void EmitsOwnedContractsChannelsAndStronglyTypedHandlersIntoTheCoreRegistrar()
    {
        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            using System.Threading.Tasks;

            namespace Generated.Sample;

            public sealed class AppModule : ModuleBase;

            [EventContract("sample.workspace-changed", typeof(AppModule), SchemaVersion = 2)]
            [EventChannel("ui", Capacity = 32, BackpressurePolicy = EventChannelBackpressurePolicy.Reject)]
            public sealed record WorkspaceChanged(string Name);

            public sealed class HandlerDependency;

            [EventHandler(typeof(AppModule), ChannelName = "ui", DispatchPolicy = EventDispatchPolicy.Background)]
            public sealed class WorkspaceHandler(HandlerDependency dependency) : IEventHandler<WorkspaceChanged>
            {
                public ValueTask HandleAsync(EventContext<WorkspaceChanged> context, System.Threading.CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """), out var output);

        var generated = Assert.Single(result.GeneratedSources, source =>
            source.HintName.Contains("DependencyInjection", StringComparison.Ordinal));
        var source = generated.SourceText.ToString();
        Assert.Empty(result.Diagnostics);
        Assert.Contains("GeneratedEventManifestAttribute", source, StringComparison.Ordinal);
        Assert.Contains("context.Register(typeof(global::Generated.Sample.AppModule)", source, StringComparison.Ordinal);
        Assert.Contains("EventContractDescriptor.GeneratedShared<global::Generated.Sample.WorkspaceChanged>", source, StringComparison.Ordinal);
        Assert.Contains(".Assembly, 2,", source, StringComparison.Ordinal);
        Assert.Contains("new global::Generated.Sample.WorkspaceHandler(", source, StringComparison.Ordinal);
        Assert.Contains("GeneratedEventHandlerDescriptor.Create<global::Generated.Sample.WorkspaceChanged, global::Generated.Sample.WorkspaceHandler>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MethodInfo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Activator", source, StringComparison.Ordinal);
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void RejectsDuplicateContractIds()
    {
        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;

            public sealed class AppModule : ModuleBase;
            [EventContract("duplicate", typeof(AppModule))] public sealed class FirstEvent;
            [EventContract("duplicate", typeof(AppModule))] public sealed class SecondEvent;
            """), out _);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Contains("more than one event type", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidHandlerAndChannelBoundaries()
    {
        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            using System.Threading.Tasks;

            public sealed class AppModule : ModuleBase;
            [EventContract("event", typeof(AppModule))]
            [EventChannel("bad", Capacity = 0)]
            public sealed class SampleEvent;

            [EventHandler(typeof(AppModule), DisableSubscriptionAfterFailures = 0)]
            public abstract class BadHandler : IEventHandler<SampleEvent>
            {
                public ValueTask HandleAsync(EventContext<SampleEvent> context, System.Threading.CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """), out _);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("positive Capacity", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("concrete", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsHandlerWhoseEventTypeIsNotAGeneratedContract()
    {
        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            using System.Threading.Tasks;

            public sealed class AppModule : ModuleBase;
            public sealed record MissingContractEvent(int Value);

            [EventHandler(typeof(AppModule))]
            public sealed class MissingContractHandler : IEventHandler<MissingContractEvent>
            {
                public ValueTask HandleAsync(EventContext<MissingContractEvent> context, System.Threading.CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """), out _);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Contains("MissingContractHandler", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("MissingContractEvent", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("reachable generated Shared event contract catalog", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(result.GeneratedSources,
            source => source.SourceText.ToString().Contains("GeneratedEventHandlerDescriptor.Create", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptsHandlerWhoseContractComesFromAReferencedGeneratedCatalog()
    {
        var contractLibrary = CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            namespace Events.Contracts;
            public sealed class ContractsModule : ModuleBase;
            [EventContract("events.shared", typeof(ContractsModule))]
            public sealed record SharedEvent(int Value);
            """, "Events.Contracts");
        var contractReference = EmitReference(contractLibrary, runGenerator: true);

        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            using Events.Contracts;
            using System.Threading.Tasks;
            public sealed class AppModule : ModuleBase;
            [EventHandler(typeof(AppModule))]
            public sealed class SharedEventHandler : IEventHandler<SharedEvent>
            {
                public ValueTask HandleAsync(EventContext<SharedEvent> context, System.Threading.CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """, "Events.Application", [contractReference]), out _);

        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.GeneratedSources,
            source => source.SourceText.ToString().Contains(
                "GeneratedEventHandlerDescriptor.Create<global::Events.Contracts.SharedEvent",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsReferencedAttributedContractWithoutGeneratedCatalogManifest()
    {
        var attributedLibrary = CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            namespace Events.Unmanifested;
            public sealed class ContractsModule : ModuleBase;
            [EventContract("events.unmanifested", typeof(ContractsModule))]
            public sealed record SharedEvent(int Value);
            """, "Events.Unmanifested");
        var contractReference = EmitReference(attributedLibrary, runGenerator: false);

        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            using Events.Unmanifested;
            using System.Threading.Tasks;
            public sealed class AppModule : ModuleBase;
            [EventHandler(typeof(AppModule))]
            public sealed class SharedEventHandler : IEventHandler<SharedEvent>
            {
                public ValueTask HandleAsync(EventContext<SharedEvent> context, System.Threading.CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """, "Events.Application", [contractReference]), out _);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Contains("not present in the reachable generated Shared event contract catalog",
            diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOpenOrExternalSharedContractObjectGraphs()
    {
        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public sealed class AppModule : ModuleBase;

            [EventContract("object", typeof(AppModule))]
            public sealed record ObjectEvent(object Value);

            [EventContract("interface", typeof(AppModule))]
            public sealed record InterfaceEvent(IReadOnlyList<string> Value);

            [EventContract("task", typeof(AppModule))]
            public sealed record TaskEvent(Task Value);

            [EventContract("exception", typeof(AppModule))]
            public sealed record ExceptionEvent(Exception Value);

            [EventContract("array", typeof(AppModule))]
            public sealed record ArrayEvent(string[] Value);

            [EventContract("external-enum", typeof(AppModule))]
            public sealed record ExternalEnumEvent(DayOfWeek Value);
            """), out _);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("ObjectEvent", StringComparison.Ordinal)
            && diagnostic.GetMessage().Contains("'object'", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("IReadOnlyList", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("System.Threading.Tasks.Task", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("System.Exception", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("string[]", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("ExternalEnumEvent", StringComparison.Ordinal)
            && diagnostic.GetMessage().Contains("DayOfWeek", StringComparison.Ordinal));
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id));
        Assert.DoesNotContain(
            result.GeneratedSources,
            source => source.SourceText.ToString().Contains("EventContractDescriptor.GeneratedShared", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMutableOrExtensibleContractLocalTypes()
    {
        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;

            public sealed class AppModule : ModuleBase;

            public class ExtensiblePayload;
            public sealed class MutablePayload { public string Value { get; set; } = string.Empty; }
            public struct MutableValue { public int Value; }

            [EventContract("extensible", typeof(AppModule))]
            public sealed record ExtensibleEvent(ExtensiblePayload Value);

            [EventContract("mutable-class", typeof(AppModule))]
            public sealed record MutableClassEvent(MutablePayload Value);

            [EventContract("mutable-struct", typeof(AppModule))]
            public sealed record MutableStructEvent(MutableValue Value);
            """), out _);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("sealed, non-abstract", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("mutable or indexed property", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.GetMessage().Contains("readonly contract-local struct", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.GeneratedSources,
            source => source.SourceText.ToString().Contains("EventContractDescriptor.GeneratedShared", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptsClosedImmutableSharedContractObjectGraph()
    {
        var result = Run(CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;

            public sealed class AppModule : ModuleBase;
            public enum ChangeKind { Added, Removed }
            public sealed record WorkspaceItem(Guid Id, string Name, ChangeKind Kind);

            [EventContract("closed", typeof(AppModule))]
            public sealed record ClosedEvent(
                Guid EventId,
                DateTimeOffset Timestamp,
                ImmutableArray<WorkspaceItem> Items,
                ImmutableArray<KeyValuePair<string, string>> Metadata);
            """), out var output);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(
            result.GeneratedSources,
            source => source.SourceText.ToString().Contains(
                "EventContractDescriptor.GeneratedShared<global::ClosedEvent>",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ReferencedEventRegistrarIsAggregatedOnceByStableRegistrarType()
    {
        var library = CreateCompilation("""
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.EventBus;
            namespace Events.Library;
            public sealed class LibraryModule : ModuleBase;
            [EventContract("library.event", typeof(LibraryModule))]
            public sealed record LibraryEvent(int Value);
            """, "Events.Library");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(library, out var generatedLibrary, out _);
        using var image = new MemoryStream();
        var emit = generatedLibrary.Emit(image);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        var app = CreateCompilation("public sealed class AppMarker;", "Events.App",
            [MetadataReference.CreateFromImage(image.ToArray())]);
        var result = Run(app, out var output);
        var generated = Assert.Single(result.GeneratedSources, source =>
            source.HintName.Contains("DependencyInjection", StringComparison.Ordinal)).SourceText.ToString();

        Assert.Equal(1, Count(generated, "context.RegisterRegistrar(typeof(global::AtomUI.City.Generated.GeneratedServiceRegistrar_Events_Library_"));
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    private static GeneratorRunResult Run(CSharpCompilation compilation, out Compilation output)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _);
        return Assert.Single(driver.GetRunResult().Results);
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "EventBus.Generated.Sample",
        IReadOnlyList<MetadataReference>? additionalReferences = null)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat([
                MetadataReference.CreateFromFile(typeof(AtomUI.City.Core.Modularity.ModuleBase).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(AtomUI.City.EventBus.IEventBus).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            ])
            .DistinctBy(reference => reference.Display)
            .Concat(additionalReferences ?? [])
            .ToArray();
        return CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static MetadataReference EmitReference(CSharpCompilation compilation, bool runGenerator)
    {
        Compilation output = compilation;
        if (runGenerator)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _);
        }

        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    private static int Count(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
