using System.Collections.Immutable;
using AtomUI.City.Generators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AtomUI.City.Generators.Analyzers;

/// <summary>
/// Represents build service provider usage analyzer.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BuildServiceProviderUsageAnalyzer : DiagnosticAnalyzer
{
    private const string MicrosoftExtensionType =
        "Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions";
    private const string CityGuardExtensionType =
        "AtomUI.City.Core.Modularity.ModuleServiceCollectionBuildGuardExtensions";
    private const string ServiceProviderFactoryType =
        "Microsoft.Extensions.DependencyInjection.IServiceProviderFactory`1";
    private const string HostApplicationBuilderType =
        "Microsoft.Extensions.Hosting.HostApplicationBuilder";
    private const string HostBuilderInterfaceType =
        "Microsoft.Extensions.Hosting.IHostBuilder";
    private const string HostingAbstractionsHostBuilderExtensionsType =
        "Microsoft.Extensions.Hosting.HostingAbstractionsHostBuilderExtensions";
    private const string HostingHostBuilderExtensionsType =
        "Microsoft.Extensions.Hosting.HostingHostBuilderExtensions";

    private static readonly DiagnosticDescriptor Rule = new(
        AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed,
        "ApplicationHost must build the root service provider",
        "Do not create a service provider in a City production project; register services and let ApplicationHost build the root provider",
        "AtomUI.City.Analyzers.Modularity",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets supported diagnostics.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>
    /// Initializes analyzer actions for the compilation.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            if (IsTestProject(compilationContext.Options))
            {
                return;
            }

            var forbiddenSymbols = new ForbiddenProviderCreationSymbols(
                compilationContext.Compilation.GetTypeByMetadataName(MicrosoftExtensionType),
                compilationContext.Compilation.GetTypeByMetadataName(CityGuardExtensionType),
                compilationContext.Compilation.GetTypeByMetadataName(ServiceProviderFactoryType),
                compilationContext.Compilation.GetTypeByMetadataName(HostApplicationBuilderType),
                compilationContext.Compilation.GetTypeByMetadataName(HostBuilderInterfaceType),
                compilationContext.Compilation.GetTypeByMetadataName(
                    HostingAbstractionsHostBuilderExtensionsType),
                compilationContext.Compilation.GetTypeByMetadataName(HostingHostBuilderExtensionsType));

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, forbiddenSymbols),
                OperationKind.Invocation);
            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeMethodReference(operationContext, forbiddenSymbols),
                OperationKind.MethodReference);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        ForbiddenProviderCreationSymbols forbiddenSymbols)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (IsForbiddenProviderCreation(invocation.TargetMethod, forbiddenSymbols))
        {
            ReportDiagnostic(context, invocation.Syntax.GetLocation());
        }
    }

    private static void AnalyzeMethodReference(
        OperationAnalysisContext context,
        ForbiddenProviderCreationSymbols forbiddenSymbols)
    {
        var methodReference = (IMethodReferenceOperation)context.Operation;

        if (IsForbiddenProviderCreation(methodReference.Method, forbiddenSymbols))
        {
            ReportDiagnostic(context, methodReference.Syntax.GetLocation());
        }
    }

    private static bool IsForbiddenProviderCreation(
        IMethodSymbol targetMethod,
        ForbiddenProviderCreationSymbols forbiddenSymbols)
    {
        var method = targetMethod.ReducedFrom ?? targetMethod;

        return IsBuildServiceProvider(method, forbiddenSymbols) ||
               IsServiceProviderFactoryCreation(method, forbiddenSymbols.ServiceProviderFactory) ||
               IsGenericHostCreation(method, forbiddenSymbols);
    }

    private static bool IsBuildServiceProvider(
        IMethodSymbol method,
        ForbiddenProviderCreationSymbols forbiddenSymbols)
    {
        if (!string.Equals(method.Name, "BuildServiceProvider", StringComparison.Ordinal))
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   forbiddenSymbols.MicrosoftBuildExtensions) ||
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   forbiddenSymbols.CityBuildGuardExtensions);
    }

    private static bool IsServiceProviderFactoryCreation(
        IMethodSymbol method,
        INamedTypeSymbol? serviceProviderFactory)
    {
        return IsInterfaceMethodOrImplementation(
            method,
            serviceProviderFactory,
            "CreateServiceProvider");
    }

    private static bool IsGenericHostCreation(
        IMethodSymbol method,
        ForbiddenProviderCreationSymbols forbiddenSymbols)
    {
        if (string.Equals(method.Name, "Build", StringComparison.Ordinal))
        {
            return SymbolEqualityComparer.Default.Equals(
                       method.ContainingType,
                       forbiddenSymbols.HostApplicationBuilder) ||
                   IsInterfaceMethodOrImplementation(
                       method,
                       forbiddenSymbols.HostBuilderInterface,
                       "Build");
        }

        if ((string.Equals(method.Name, "Start", StringComparison.Ordinal) ||
             string.Equals(method.Name, "StartAsync", StringComparison.Ordinal)) &&
            SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                forbiddenSymbols.HostingAbstractionsHostBuilderExtensions))
        {
            return true;
        }

        return string.Equals(method.Name, "RunConsoleAsync", StringComparison.Ordinal) &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   forbiddenSymbols.HostingHostBuilderExtensions);
    }

    private static bool IsInterfaceMethodOrImplementation(
        IMethodSymbol method,
        INamedTypeSymbol? interfaceType,
        string memberName)
    {
        if (interfaceType is null ||
            !string.Equals(method.Name, memberName, StringComparison.Ordinal))
        {
            return false;
        }

        if (method.ContainingType.TypeKind == TypeKind.Interface &&
            SymbolEqualityComparer.Default.Equals(
                method.ContainingType.OriginalDefinition,
                interfaceType))
        {
            return true;
        }

        foreach (var implementedInterface in method.ContainingType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    implementedInterface.OriginalDefinition,
                    interfaceType))
            {
                continue;
            }

            foreach (var interfaceMethod in implementedInterface
                         .GetMembers(memberName)
                         .OfType<IMethodSymbol>())
            {
                var implementation = method.ContainingType
                    .FindImplementationForInterfaceMember(interfaceMethod) as IMethodSymbol;

                if (implementation is not null && MethodsMatch(implementation, method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MethodsMatch(IMethodSymbol left, IMethodSymbol right)
    {
        return SymbolEqualityComparer.Default.Equals(left, right) ||
               SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition);
    }

    private static void ReportDiagnostic(OperationAnalysisContext context, Location location)
    {
        context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static bool IsTestProject(AnalyzerOptions options)
    {
        return options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                   "build_property.IsTestProject",
                   out var value) &&
               bool.TryParse(value, out var isTestProject) &&
               isTestProject;
    }

    private sealed class ForbiddenProviderCreationSymbols
    {
        public ForbiddenProviderCreationSymbols(
            INamedTypeSymbol? microsoftBuildExtensions,
            INamedTypeSymbol? cityBuildGuardExtensions,
            INamedTypeSymbol? serviceProviderFactory,
            INamedTypeSymbol? hostApplicationBuilder,
            INamedTypeSymbol? hostBuilderInterface,
            INamedTypeSymbol? hostingAbstractionsHostBuilderExtensions,
            INamedTypeSymbol? hostingHostBuilderExtensions)
        {
            MicrosoftBuildExtensions = microsoftBuildExtensions;
            CityBuildGuardExtensions = cityBuildGuardExtensions;
            ServiceProviderFactory = serviceProviderFactory;
            HostApplicationBuilder = hostApplicationBuilder;
            HostBuilderInterface = hostBuilderInterface;
            HostingAbstractionsHostBuilderExtensions = hostingAbstractionsHostBuilderExtensions;
            HostingHostBuilderExtensions = hostingHostBuilderExtensions;
        }

        public INamedTypeSymbol? MicrosoftBuildExtensions { get; }

        public INamedTypeSymbol? CityBuildGuardExtensions { get; }

        public INamedTypeSymbol? ServiceProviderFactory { get; }

        public INamedTypeSymbol? HostApplicationBuilder { get; }

        public INamedTypeSymbol? HostBuilderInterface { get; }

        public INamedTypeSymbol? HostingAbstractionsHostBuilderExtensions { get; }

        public INamedTypeSymbol? HostingHostBuilderExtensions { get; }
    }
}
