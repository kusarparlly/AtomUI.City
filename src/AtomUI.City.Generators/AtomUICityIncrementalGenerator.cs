using System.Text;
using AtomUI.City.Generators.Common;
using AtomUI.City.Generators.DependencyInjection;
using AtomUI.City.Generators.Data;
using AtomUI.City.Generators.Diagnostics;
using AtomUI.City.Generators.EventBus;
using AtomUI.City.Generators.Localization;
using AtomUI.City.Generators.Modularity;
using AtomUI.City.Generators.Presentation;
using AtomUI.City.Generators.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using CityModuleMetadata = AtomUI.City.Generators.Modularity.ModuleMetadata;

namespace AtomUI.City.Generators;

/// <summary>
/// Generates City module, service, EventBus, and presentation metadata at compile time.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AtomUICityIncrementalGenerator : IIncrementalGenerator
{
    private const string ApplicationModuleAttributeName =
        "AtomUI.City.Core.Modularity.ApplicationModuleAttribute";
    private const string GeneratedModuleManifestAttributeName =
        "AtomUI.City.Core.Modularity.GeneratedModuleManifestAttribute";
    private const string GeneratedServiceManifestAttributeName =
        "AtomUI.City.Core.DependencyInjection.GeneratedServiceManifestAttribute";
    private const string GeneratedEventManifestAttributeName =
        "AtomUI.City.EventBus.GeneratedEventManifestAttribute";
    private const string EventContractAttributeName =
        "AtomUI.City.EventBus.EventContractAttribute";
    private const string ServiceRegistrationOwnerAttributeName =
        "AtomUI.City.Core.DependencyInjection.ServiceRegistrationOwnerAttribute";
    private const string ModuleInterfaceName = "AtomUI.City.Core.Modularity.IModule";
    private const string ServiceRegistrarInterfaceName =
        "AtomUI.City.Core.DependencyInjection.IServiceRegistrar";

    /// <summary>
    /// Initializes the incremental source-generation pipeline.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generationEnabled = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                !string.Equals(
                    options.GlobalOptions.TryGetValue(
                        "build_property.AtomUICitySourceGenerationMode",
                        out var mode)
                        ? mode
                        : null,
                    "Off",
                    StringComparison.OrdinalIgnoreCase));

        InitializeDependencyInjection(context, generationEnabled);
        InitializeModularity(context, generationEnabled);
        InitializeRouting(context, generationEnabled);
        InitializePresentation(context, generationEnabled);
        InitializeLocalization(context, generationEnabled);
        InitializeData(context, generationEnabled);
    }

    private static void InitializeData(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<bool> generationEnabled)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax declaration && declaration.AttributeLists.Count > 0,
                static (syntaxContext, _) => syntaxContext.SemanticModel.GetDeclaredSymbol(syntaxContext.Node) is INamedTypeSymbol symbol
                    ? DataClientMetadataReader.TryRead(symbol)
                    : null)
            .Where(static candidate => candidate is not null)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(candidates).Combine(generationEnabled),
            static (sourceContext, input) =>
            {
                if (!input.Right)
                {
                    return;
                }

                var value = input.Left;
                var clients = value.Right.Where(static candidate => candidate is not null).Select(static candidate => candidate!).ToArray();
                if (clients.Length == 0)
                {
                    return;
                }

                var invalid = clients.Where(static client => client.Issues.Count > 0).ToArray();
                foreach (var client in invalid)
                {
                    foreach (var issue in client.Issues)
                    {
                        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Data,
                            new GeneratorDiagnostic(GeneratorDiagnostics.InvalidManifestInput, issue, client.TypeName),
                            client.Location));
                    }
                }

                var duplicateIds = clients.GroupBy(static client => client.ClientId, StringComparer.Ordinal)
                    .Where(static group => group.Count() > 1)
                    .ToArray();
                foreach (var duplicate in duplicateIds)
                {
                    sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                        GeneratorFeature.Data,
                        new GeneratorDiagnostic(
                            GeneratorDiagnostics.InvalidManifestInput,
                            $"Data client id '{duplicate.Key}' is declared more than once.",
                            duplicate.Key),
                        duplicate.First().Location));
                }

                if (invalid.Length > 0 || duplicateIds.Length > 0)
                {
                    return;
                }

                var assemblyName = string.IsNullOrWhiteSpace(value.Left.AssemblyName) ? "Assembly" : value.Left.AssemblyName!;
                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(GeneratorFeature.Data, assemblyName, "Clients"),
                    SourceText.From(DataClientRegistrarSourceBuilder.Build(assemblyName, clients), Encoding.UTF8));
            });
    }

    private static void InitializeLocalization(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<bool> generationEnabled)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(generationEnabled),
            static (sourceContext, input) =>
            {
                if (!input.Right)
                {
                    return;
                }

                var compilation = input.Left;
                var metadata = LocalizationMetadataReader.Read(compilation);
                foreach (var diagnostic in metadata.Diagnostics)
                {
                    sourceContext.ReportDiagnostic(
                        GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Localization,
                            diagnostic));
                }

                if (metadata.Diagnostics.Count > 0)
                {
                    return;
                }

                if (metadata.Packages.Count == 0 && metadata.Resources.Count == 0)
                {
                    return;
                }

                var result = LocalizationManifestBuilder.Build(metadata.Packages, metadata.Resources);
                foreach (var diagnostic in result.Diagnostics)
                {
                    sourceContext.ReportDiagnostic(
                        GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Localization,
                            diagnostic));
                }

                if (result.Diagnostics.Count > 0)
                {
                    return;
                }

                var assemblyName = string.IsNullOrWhiteSpace(compilation.AssemblyName)
                    ? "Assembly"
                    : compilation.AssemblyName!;
                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(
                        GeneratorFeature.Localization,
                        assemblyName,
                        "LocalizationManifest"),
                    SourceText.From(
                        LocalizationRegistrarSourceBuilder.Build(result.Manifest),
                        Encoding.UTF8));
            });
    }

    private static void InitializeDependencyInjection(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<bool> generationEnabled)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) => ReadServiceCandidate(syntaxContext))
            .Where(static candidate => candidate is not null)
            .Collect();
        var eventCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax declaration && declaration.AttributeLists.Count > 0,
                static (syntaxContext, _) => ReadEventCandidate(syntaxContext))
            .Where(static candidate => candidate is not null)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(candidates).Combine(eventCandidates).Combine(generationEnabled),
            static (sourceContext, input) =>
            {
                if (!input.Right)
                {
                    return;
                }

                var value = input.Left;
                var compilation = value.Left.Left;
                var allCandidates = value.Left.Right.Where(candidate => candidate is not null).Select(candidate => candidate!).ToArray();
                var allEventCandidates = value.Right.Where(candidate => candidate is not null).Select(candidate => candidate!).ToArray();
                var services = allCandidates
                    .Where(candidate => candidate.Registration is not null)
                    .GroupBy(candidate => candidate.TypeName, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray();
                var owners = allCandidates
                    .Where(candidate => candidate.IsOwner)
                    .GroupBy(candidate => candidate.TypeName, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray();
                var referencedRegistrars = ReadReferencedServiceRegistrarTypeNames(compilation);
                var invalidDeclarations = allCandidates
                    .Where(candidate => candidate.Registration is null && candidate.Issues.Count > 0)
                    .ToArray();

                if (invalidDeclarations.Length > 0 || allEventCandidates.Any(candidate => candidate.Issues.Count > 0))
                {
                    foreach (var candidate in invalidDeclarations)
                    {
                        foreach (var issue in candidate.Issues)
                        {
                            ReportInvalidService(sourceContext, candidate.Location, issue);
                        }
                    }
                    foreach (var candidate in allEventCandidates)
                    {
                        foreach (var issue in candidate.Issues)
                        {
                            ReportInvalidEvent(sourceContext, candidate.Location, issue);
                        }
                    }
                    return;
                }

                var eventContracts = allEventCandidates.Where(candidate => candidate.Contract is not null)
                    .Select(candidate => candidate.Contract!).OrderBy(value => value.ContractId, StringComparer.Ordinal).ToArray();
                var eventHandlers = allEventCandidates.Where(candidate => candidate.Handler is not null)
                    .Select(candidate => candidate.Handler!).OrderBy(value => value.HandlerTypeName, StringComparer.Ordinal).ToArray();
                var duplicateContractIds = eventContracts.GroupBy(value => value.ContractId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1).ToArray();
                if (duplicateContractIds.Length > 0)
                {
                    foreach (var duplicate in duplicateContractIds)
                    {
                        ReportInvalidEvent(sourceContext, null,
                            $"Event ContractId '{duplicate.Key}' is declared by more than one event type in the same compilation.");
                    }
                    return;
                }

                var localContractTypes = new HashSet<ITypeSymbol>(
                    eventContracts.Select(contract => contract.EventTypeSymbol),
                    SymbolEqualityComparer.Default);
                var invalidHandlerContracts = allEventCandidates
                    .Where(candidate => candidate.Handler is not null)
                    .Where(candidate => !IsReachableGeneratedEventContract(
                        compilation,
                        candidate.Handler!.EventTypeSymbol,
                        localContractTypes))
                    .ToArray();
                if (invalidHandlerContracts.Length > 0)
                {
                    foreach (var candidate in invalidHandlerContracts)
                    {
                        ReportInvalidEvent(
                            sourceContext,
                            candidate.Location,
                            $"Event handler '{candidate.Handler!.HandlerTypeName}' targets event type " +
                            $"'{candidate.Handler.EventTypeName}', which is not present in the reachable generated Shared event contract catalog.");
                    }
                    return;
                }
                var eventManifest = new EventGenerationManifest(eventContracts, eventHandlers);

                if (services.Length == 0 && referencedRegistrars.Count == 0 && eventManifest.IsEmpty)
                {
                    return;
                }

                var invalid = false;
                if (services.Length > 0 && owners.Length != 1)
                {
                    invalid = true;
                    var target = owners.FirstOrDefault()?.Location ?? services.First().Location;
                    ReportInvalidService(sourceContext, target,
                        owners.Length == 0
                            ? "An assembly containing generated services must declare exactly one module with ServiceRegistrationOwner."
                            : "An assembly containing generated services cannot declare more than one ServiceRegistrationOwner.");
                }

                if (owners.Length == 1 && !owners[0].ImplementsModule)
                {
                    invalid = true;
                    ReportInvalidService(sourceContext, owners[0].Location,
                        $"Service registration owner '{owners[0].TypeName}' must implement IModule.");
                }

                foreach (var service in services)
                {
                    var registration = service.Registration!;
                    foreach (var issue in service.Issues)
                    {
                        invalid = true;
                        ReportInvalidService(sourceContext, service.Location, issue);
                    }

                    if (registration.Replace && registration.TryAdd)
                    {
                        invalid = true;
                        ReportInvalidService(sourceContext, service.Location,
                            $"Service '{service.TypeName}' cannot use Replace and TryAdd together.");
                    }

                    if (registration.IsDisposable && registration.ExposedServiceTypeNames.Count > 1)
                    {
                        invalid = true;
                        ReportInvalidService(sourceContext, service.Location,
                            $"Disposable service '{service.TypeName}' cannot expose multiple service contracts because forwarding registrations would make disposal ownership ambiguous.");
                    }
                }

                var manifest = ServiceRegistrationManifestBuilder.Build(services.Select(service => service.Registration!).ToArray());
                foreach (var diagnostic in manifest.Diagnostics)
                {
                    invalid = true;
                    var location = services.FirstOrDefault(service => string.Equals(service.TypeName, diagnostic.Target, StringComparison.Ordinal))?.Location;
                    sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                        GeneratorFeature.DependencyInjection, diagnostic, location));
                }

                if (invalid)
                {
                    return;
                }

                var assemblyName = string.IsNullOrWhiteSpace(compilation.AssemblyName) ? "Assembly" : compilation.AssemblyName!;
                var source = ServiceRegistrarSourceBuilder.Build(
                    assemblyName,
                    owners.SingleOrDefault()?.TypeName,
                    manifest.Manifest.Registrations,
                    referencedRegistrars,
                    eventManifest);
                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(GeneratorFeature.DependencyInjection, assemblyName, "Services"),
                    SourceText.From(source, Encoding.UTF8));
            });
    }

    private static EventGenerationCandidate? ReadEventCandidate(GeneratorSyntaxContext context)
    {
        return context.SemanticModel.GetDeclaredSymbol(context.Node) is INamedTypeSymbol symbol
            ? EventMetadataReader.Read(symbol)
            : null;
    }

    private static void ReportInvalidEvent(SourceProductionContext context, Location? location, string message)
    {
        context.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
            GeneratorFeature.EventBus,
            new GeneratorDiagnostic(GeneratorDiagnostics.InvalidManifestInput, message),
            location));
    }

    private static ServiceGenerationCandidate? ReadServiceCandidate(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        var registration = ServiceRegistrationMetadataReader.TryRead(symbol);
        var attributes = symbol.GetAttributes();
        var isOwner = attributes.Any(attribute => string.Equals(
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            ServiceRegistrationOwnerAttributeName,
            StringComparison.Ordinal));
        var issues = ValidateServiceDeclaration(symbol, attributes, registration);
        if (isOwner && (symbol.IsAbstract || symbol.IsStatic || symbol.Arity > 0 || !IsAccessible(symbol)))
        {
            issues = issues.Concat([$"Service registration owner '{symbol.Name}' must be a non-abstract, non-generic accessible class."]).ToArray();
        }

        if (registration is null && !isOwner && issues.Count == 0)
        {
            return null;
        }

        return new ServiceGenerationCandidate(
            symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            registration,
            isOwner,
            symbol.AllInterfaces.Any(@interface => string.Equals(
                @interface.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), ModuleInterfaceName, StringComparison.Ordinal)),
            symbol.Locations.FirstOrDefault(),
            issues);
    }

    private static IReadOnlyList<string> ValidateServiceDeclaration(
        INamedTypeSymbol symbol,
        IReadOnlyList<AttributeData> attributes,
        ServiceRegistrationMetadata? registration)
    {
        const string serviceAttribute = "AtomUI.City.Core.DependencyInjection.ServiceAttribute";
        const string scopedAttribute = "AtomUI.City.Core.DependencyInjection.ScopedServiceAttribute";
        const string exposeAttribute = "AtomUI.City.Core.DependencyInjection.ExposeServicesAttribute";
        var issues = new List<string>();
        var serviceAttributes = attributes.Where(attribute => HasAttributeName(attribute, serviceAttribute)).ToArray();
        var scopedAttributes = attributes.Where(attribute => HasAttributeName(attribute, scopedAttribute)).ToArray();
        var exposeAttributes = attributes.Where(attribute => HasAttributeName(attribute, exposeAttribute)).ToArray();
        var markerLifetimes = symbol.AllInterfaces
            .Select(@interface => @interface.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
            .Where(name => name is "AtomUI.City.Core.DependencyInjection.ISingletonDependency" or
                "AtomUI.City.Core.DependencyInjection.IScopedDependency" or
                "AtomUI.City.Core.DependencyInjection.ITransientDependency")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (serviceAttributes.Length > 0 && scopedAttributes.Length > 0)
        {
            issues.Add($"Service '{symbol.Name}' cannot use ServiceAttribute and ScopedServiceAttribute together.");
        }

        if (markerLifetimes.Length > 1)
        {
            issues.Add($"Service '{symbol.Name}' implements conflicting dependency lifetime markers.");
        }

        if (registration is null && exposeAttributes.Length > 0 && serviceAttributes.Length == 0 && scopedAttributes.Length == 0 && markerLifetimes.Length == 0)
        {
            issues.Add($"Service '{symbol.Name}' uses ExposeServicesAttribute without a service declaration or dependency marker.");
        }

        foreach (var attribute in serviceAttributes)
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not int lifetime ||
                lifetime is < 0 or > 2)
            {
                issues.Add($"Service '{symbol.Name}' declares an unknown ServiceLifetime value.");
            }
        }

        foreach (var attribute in scopedAttributes.Concat(exposeAttributes))
        {
            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var serviceTypes = attribute.ConstructorArguments[0];
            if (serviceTypes.IsNull ||
                serviceTypes.Kind != TypedConstantKind.Array ||
                serviceTypes.Values.IsDefault)
            {
                issues.Add($"Service '{symbol.Name}' contains a null or invalid exposed service type.");
                continue;
            }

            foreach (var value in serviceTypes.Values)
            {
                if (value.Value is not INamedTypeSymbol exposedType)
                {
                    issues.Add($"Service '{symbol.Name}' contains a null or invalid exposed service type.");
                    continue;
                }

                if (!IsAssignableTo(symbol, exposedType))
                {
                    issues.Add($"Service '{symbol.Name}' is not assignable to exposed contract '{exposedType.Name}'.");
                }
            }
        }

        if (registration is null &&
            issues.Count == 0 &&
            (serviceAttributes.Length > 0 || scopedAttributes.Length > 0 || markerLifetimes.Length > 0))
        {
            issues.Add($"Service '{symbol.Name}' must be a concrete class that can be generated.");
        }

        if (registration is not null)
        {
            if (symbol.IsAbstract || symbol.IsStatic || symbol.Arity > 0 || !IsAccessible(symbol))
            {
                issues.Add($"Service '{symbol.Name}' must be a non-abstract, non-generic accessible class.");
            }

            if (registration.Key is not null && string.IsNullOrWhiteSpace(registration.Key))
            {
                issues.Add($"Service '{symbol.Name}' cannot declare an empty registration key.");
            }
        }

        return issues;
    }

    private static bool HasAttributeName(AttributeData attribute, string expected) => string.Equals(
        attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), expected, StringComparison.Ordinal);

    private static bool IsAssignableTo(INamedTypeSymbol implementation, INamedTypeSymbol contract)
    {
        if (SymbolEqualityComparer.Default.Equals(implementation, contract) ||
            implementation.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, contract)))
        {
            return true;
        }

        for (var current = implementation.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, contract))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ReadReferencedServiceRegistrarTypeNames(Compilation compilation)
    {
        var registrarTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            if (string.Equals(assembly.Identity.Name, compilation.AssemblyName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var attribute in assembly.GetAttributes())
            {
                if (string.Equals(
                        attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        GeneratedServiceManifestAttributeName,
                        StringComparison.Ordinal) &&
                    attribute.ConstructorArguments.Length > 0 &&
                    attribute.ConstructorArguments[0].Value is INamedTypeSymbol registrarType &&
                    registrarType.DeclaredAccessibility == Accessibility.Public)
                {
                    registrarTypes.Add(registrarType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                }
            }
        }

        return registrarTypes.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static bool IsReachableGeneratedEventContract(
        Compilation compilation,
        ITypeSymbol eventType,
        HashSet<ITypeSymbol> localContractTypes)
    {
        if (SymbolEqualityComparer.Default.Equals(eventType.ContainingAssembly, compilation.Assembly))
        {
            return localContractTypes.Contains(eventType);
        }

        if (eventType is not INamedTypeSymbol namedEventType ||
            !namedEventType.GetAttributes().Any(attribute =>
                HasAttributeName(attribute, EventContractAttributeName)))
        {
            return false;
        }

        var eventManifests = eventType.ContainingAssembly.GetAttributes().Where(attribute =>
            HasAttributeName(attribute, GeneratedEventManifestAttributeName)).ToArray();
        var serviceManifests = eventType.ContainingAssembly.GetAttributes().Where(attribute =>
            HasAttributeName(attribute, GeneratedServiceManifestAttributeName)).ToArray();
        if (eventManifests.Length != 1 || serviceManifests.Length != 1)
        {
            return false;
        }

        var eventManifest = eventManifests[0];
        var serviceManifest = serviceManifests[0];
        if (
            eventManifest.ConstructorArguments.Length != 2 ||
            serviceManifest.ConstructorArguments.Length != 1 ||
            eventManifest.ConstructorArguments[0].Value is not INamedTypeSymbol eventRegistrar ||
            serviceManifest.ConstructorArguments[0].Value is not INamedTypeSymbol serviceRegistrar ||
            eventManifest.ConstructorArguments[1].Value is not int version || version != 1)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(eventRegistrar, serviceRegistrar) &&
            SymbolEqualityComparer.Default.Equals(eventRegistrar.ContainingAssembly, eventType.ContainingAssembly) &&
            eventRegistrar.DeclaredAccessibility == Accessibility.Public &&
            !eventRegistrar.IsAbstract &&
            eventRegistrar.Arity == 0 &&
            eventRegistrar.AllInterfaces.Any(@interface => string.Equals(
                @interface.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ServiceRegistrarInterfaceName,
                StringComparison.Ordinal));
    }

    private static void ReportInvalidService(SourceProductionContext context, Location? location, string message)
    {
        context.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
            GeneratorFeature.DependencyInjection,
            new GeneratorDiagnostic(GeneratorDiagnostics.InvalidManifestInput, message),
            location));
    }

    private static void InitializeModularity(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<bool> generationEnabled)
    {
        var moduleCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) => ReadModuleCandidate(syntaxContext))
            .Where(static candidate => candidate is not null)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(moduleCandidates).Combine(generationEnabled),
            static (sourceContext, input) =>
            {
                if (!input.Right)
                {
                    return;
                }

                var value = input.Left;
                var compilation = value.Left;
                var candidates = value.Right
                    .Where(candidate => candidate is not null)
                    .Select(candidate => candidate!)
                    .GroupBy(candidate => candidate.TypeName, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(candidate => candidate.TypeName, StringComparer.Ordinal)
                    .ToArray();
                var validCandidates = new List<ModuleGenerationCandidate>(candidates.Length);
                var hasInvalidCandidate = false;

                foreach (var candidate in candidates)
                {
                    if (candidate.Metadata is null)
                    {
                        hasInvalidCandidate = true;
                        ReportInvalidGeneratedModule(
                            sourceContext,
                            candidate,
                            $"Type '{candidate.TypeName}' is marked with ApplicationModule but does not implement IModule.");
                        continue;
                    }

                    if (candidate.IsApplicationRoot &&
                        !IsExecutableOutput(compilation.Options.OutputKind))
                    {
                        hasInvalidCandidate = true;
                        ReportInvalidGeneratedModule(
                            sourceContext,
                            candidate,
                            $"Application module '{candidate.TypeName}' must be declared by an executable application project.");
                        continue;
                    }

                    if (CanGenerateFactory(candidate.Symbol))
                    {
                        validCandidates.Add(candidate);
                        continue;
                    }

                    if (candidate.IsApplicationRoot)
                    {
                        hasInvalidCandidate = true;
                        ReportInvalidGeneratedModule(
                            sourceContext,
                            candidate,
                            $"Module '{candidate.TypeName}' cannot be emitted with a strong-typed factory.");
                    }
                }

                if (hasInvalidCandidate)
                {
                    return;
                }

                var applicationRoots = validCandidates
                    .Where(candidate => candidate.IsApplicationRoot)
                    .ToArray();

                if (applicationRoots.Length > 1)
                {
                    foreach (var root in applicationRoots)
                    {
                        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Modularity,
                            new GeneratorDiagnostic(
                                GeneratorDiagnostics.MultipleApplicationModules,
                                "An application compilation can declare only one ApplicationModule.",
                                root.TypeName),
                            root.Location));
                    }

                    return;
                }

                var modules = validCandidates
                    .Select(candidate => candidate.Metadata!)
                    .ToArray();
                var availableExternalDependencies = GetAvailableExternalDependencyTypeNames(
                    compilation,
                    modules);
                var graph = ModuleDependencyGraphBuilder.Build(
                    modules,
                    availableExternalDependencies);

                if (graph.Diagnostics.Count > 0)
                {
                    foreach (var diagnostic in graph.Diagnostics)
                    {
                        var location = validCandidates
                            .FirstOrDefault(candidate => string.Equals(
                                candidate.TypeName,
                                diagnostic.Target,
                                StringComparison.Ordinal))
                            ?.Location;
                        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Modularity,
                            diagnostic,
                            location));
                    }

                    return;
                }

                var referencedRegistrars = ReadReferencedRegistrarTypeNames(compilation);

                if (graph.OrderedModules.Count == 0 && referencedRegistrars.Count == 0)
                {
                    return;
                }

                var assemblyName = string.IsNullOrWhiteSpace(compilation.AssemblyName)
                    ? "Assembly"
                    : compilation.AssemblyName!;
                var source = ModuleRegistrarSourceBuilder.Build(
                    assemblyName,
                    graph.OrderedModules,
                    referencedRegistrars);

                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(
                        GeneratorFeature.Modularity,
                        assemblyName,
                        "Modules"),
                    SourceText.From(source, Encoding.UTF8));
            });
    }

    private static void InitializePresentation(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<bool> generationEnabled)
    {
        var presentationViews = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count > 0,
                static (syntaxContext, _) => ReadPresentationViews(syntaxContext))
            .Where(static views => views.Count > 0)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(presentationViews).Combine(generationEnabled),
            static (sourceContext, input) =>
            {
                if (!input.Right)
                {
                    return;
                }

                var value = input.Left;
                var views = value.Right
                    .SelectMany(group => group)
                    .ToArray();

                if (views.Length == 0)
                {
                    return;
                }

                var result = PresentationViewManifestBuilder.Build(views);
                if (result.Diagnostics.Count > 0)
                {
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        sourceContext.ReportDiagnostic(CreatePresentationDiagnostic(diagnostic, views));
                    }

                    return;
                }

                var source = PresentationViewRegistrarSourceBuilder.Build(result.Manifest);
                var assemblyName = string.IsNullOrWhiteSpace(value.Left.AssemblyName)
                    ? "Assembly"
                    : value.Left.AssemblyName!;

                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(GeneratorFeature.Presentation, assemblyName, "Views"),
                    SourceText.From(source, Encoding.UTF8));
            });
    }

    private static void InitializeRouting(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<bool> generationEnabled)
    {
        var routeMaps = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax declaration && declaration.AttributeLists.Count > 0,
                static (syntaxContext, _) => ReadRouteMap(syntaxContext))
            .Where(static routeMap => routeMap is not null)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(routeMaps).Combine(generationEnabled),
            static (sourceContext, input) =>
            {
                if (!input.Right)
                {
                    return;
                }

                var value = input.Left;
                var maps = value.Right
                    .Where(routeMap => routeMap is not null)
                    .Select(routeMap => routeMap!)
                    .GroupBy(routeMap => routeMap.TypeName, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(routeMap => routeMap.TypeName, StringComparer.Ordinal)
                    .ToArray();
                if (maps.Length == 0)
                {
                    return;
                }

                var invalidMaps = maps.Where(map => map.Issues.Count > 0).ToArray();
                if (invalidMaps.Length > 0)
                {
                    foreach (var map in invalidMaps)
                    {
                        foreach (var issue in map.Issues)
                        {
                            sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                                GeneratorFeature.Routing,
                                new GeneratorDiagnostic(GeneratorDiagnostics.InvalidManifestInput, issue, map.TypeName),
                                map.Location));
                        }
                    }

                    return;
                }

                var manifest = RouteManifestBuilder.Build(maps.SelectMany(routeMap => routeMap.Routes).ToArray());
                if (manifest.Diagnostics.Count > 0)
                {
                    foreach (var diagnostic in manifest.Diagnostics)
                    {
                        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Routing,
                            diagnostic,
                            maps.FirstOrDefault(map => map.Routes.Any(route =>
                                string.Equals(route.Id, diagnostic.Target, StringComparison.Ordinal)))?.Location));
                    }

                    return;
                }

                var assemblyName = string.IsNullOrWhiteSpace(value.Left.AssemblyName)
                    ? "Assembly"
                    : value.Left.AssemblyName!;
                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(GeneratorFeature.Routing, assemblyName, "Routes"),
                    SourceText.From(RouteSourceBuilder.Build(maps, manifest.Manifest), Encoding.UTF8));
            });
    }

    private static ModuleGenerationCandidate? ReadModuleCandidate(GeneratorSyntaxContext context)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

        if (symbol is null)
        {
            return null;
        }

        var metadata = ModuleMetadataReader.TryRead(symbol);
        var isApplicationRoot = symbol
            .GetAttributes()
            .Any(attribute => string.Equals(
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ApplicationModuleAttributeName,
                StringComparison.Ordinal));

        return metadata is null && !isApplicationRoot
            ? null
            : new ModuleGenerationCandidate(
                symbol,
                metadata,
                isApplicationRoot,
                symbol.Locations.FirstOrDefault());
    }

    private static bool CanGenerateFactory(INamedTypeSymbol symbol)
    {
        if (symbol.IsAbstract || symbol.IsStatic || symbol.Arity > 0 || !IsAccessible(symbol))
        {
            return false;
        }

        return symbol.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0 &&
            constructor.DeclaredAccessibility is Accessibility.Public or
                Accessibility.Internal or
                Accessibility.ProtectedOrInternal);
    }

    private static bool IsAccessible(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return false;
            }

            if (current.DeclaredAccessibility is not Accessibility.Public and
                not Accessibility.Internal and
                not Accessibility.ProtectedOrInternal)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExecutableOutput(OutputKind outputKind)
    {
        return outputKind is OutputKind.ConsoleApplication or
            OutputKind.WindowsApplication or
            OutputKind.WindowsRuntimeApplication;
    }

    private static void ReportInvalidGeneratedModule(
        SourceProductionContext sourceContext,
        ModuleGenerationCandidate candidate,
        string message)
    {
        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
            GeneratorFeature.Modularity,
            new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidGeneratedModule,
                message,
                candidate.TypeName),
            candidate.Location));
    }

    private static ISet<string> GetAvailableExternalDependencyTypeNames(
        Compilation compilation,
        IReadOnlyList<CityModuleMetadata> modules)
    {
        var available = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dependency in modules.SelectMany(module => module.Dependencies))
        {
            var dependencyType = compilation.GetTypeByMetadataName(dependency.TypeName);

            if (dependencyType is not null &&
                !SymbolEqualityComparer.Default.Equals(
                    dependencyType.ContainingAssembly,
                    compilation.Assembly))
            {
                available.Add(dependency.TypeName);
            }
        }

        return available;
    }

    private static IReadOnlyList<string> ReadReferencedRegistrarTypeNames(Compilation compilation)
    {
        var registrarTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            if (string.Equals(assembly.Identity.Name, compilation.AssemblyName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var attribute in assembly.GetAttributes())
            {
                if (!string.Equals(
                        attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        GeneratedModuleManifestAttributeName,
                        StringComparison.Ordinal) ||
                    attribute.ConstructorArguments.Length == 0 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol registrarType ||
                    registrarType.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                registrarTypes.Add(registrarType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
            }
        }

        return registrarTypes.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<PresentationViewMetadata> ReadPresentationViews(GeneratorSyntaxContext context)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

        return symbol is null ? [] : PresentationViewMetadataReader.Read(symbol);
    }

    private static RouteMapMetadata? ReadRouteMap(GeneratorSyntaxContext context)
    {
        return context.SemanticModel.GetDeclaredSymbol(context.Node) is INamedTypeSymbol symbol
            ? RouteMetadataReader.TryRead(symbol)
            : null;
    }

    private static Diagnostic CreatePresentationDiagnostic(
        GeneratorDiagnostic diagnostic,
        IReadOnlyList<PresentationViewMetadata> views)
    {
        return GeneratorDiagnostics.CreateRoslynDiagnostic(
            GeneratorFeature.Presentation,
            diagnostic,
            FindPresentationDiagnosticLocation(diagnostic, views));
    }

    private static Location? FindPresentationDiagnosticLocation(
        GeneratorDiagnostic diagnostic,
        IReadOnlyList<PresentationViewMetadata> views)
    {
        if (string.IsNullOrWhiteSpace(diagnostic.Target))
        {
            return null;
        }

        return views
            .FirstOrDefault(view =>
                string.Equals(view.ViewTypeName, diagnostic.Target, StringComparison.Ordinal) ||
                string.Equals(view.ViewModelTypeName, diagnostic.Target, StringComparison.Ordinal))
            ?.Location;
    }

    private sealed class ServiceGenerationCandidate
    {
        public ServiceGenerationCandidate(
            string typeName,
            ServiceRegistrationMetadata? registration,
            bool isOwner,
            bool implementsModule,
            Location? location,
            IReadOnlyList<string> issues)
        {
            TypeName = typeName;
            Registration = registration;
            IsOwner = isOwner;
            ImplementsModule = implementsModule;
            Location = location;
            Issues = issues;
        }

        public string TypeName { get; }
        public ServiceRegistrationMetadata? Registration { get; }
        public bool IsOwner { get; }
        public bool ImplementsModule { get; }
        public Location? Location { get; }
        public IReadOnlyList<string> Issues { get; }
    }
}
