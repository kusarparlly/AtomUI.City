using System.Text;

namespace AtomUI.City.Templates;

public sealed class GenerationTemplateRenderer
{
    private static readonly object RenderGatesSyncRoot = new();
    private static readonly Dictionary<string, RenderGate> RenderGates = new(GetPathComparer());

    public TemplatePlan CreatePlan(GenerationTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = options.Validate();
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Generation template options are invalid: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Code))}.",
                nameof(options));
        }

        return CreatePlan(options, CreateFiles(options));
    }

    public TemplateRenderResult Render(GenerationTemplateOptions options)
    {
        return Render(options, CancellationToken.None);
    }

    public TemplateRenderResult Render(
        GenerationTemplateOptions options,
        CancellationToken cancellationToken)
    {
        return Render(options, cancellationToken, beforeWrite: null);
    }

    internal TemplateRenderResult Render(
        GenerationTemplateOptions options,
        CancellationToken cancellationToken,
        Action<string, int>? beforeWrite)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var optionDiagnostics = options.Validate();
        if (optionDiagnostics.Count > 0)
        {
            return TemplateRenderResult.Failed([.. optionDiagnostics]);
        }

        var files = CreateFiles(options);
        var plan = CreatePlan(options, files);
        var planDiagnostics = plan.Validate();
        if (planDiagnostics.Count > 0)
        {
            return TemplateRenderResult.Failed(plan, [.. planDiagnostics]);
        }

        var rootPath = Path.GetFullPath(options.OutputPath);
        var gate = AcquireRenderGate(rootPath);
        try
        {
            lock (gate.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in files)
                {
                    string destination;
                    try
                    {
                        destination = ResolvePath(rootPath, file.Change.Path);
                        ValidateExistingAncestors(rootPath, destination);
                    }
                    catch (Exception exception) when (
                        exception is IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        return TemplateRenderResult.Failed(
                            plan,
                            CreateOutputDiagnostic(
                                "AUCTPL1005",
                                $"Generation output preflight failed: {exception.GetType().Name}.",
                                options,
                                file.Change.Path,
                                rootPath,
                                exception));
                    }

                    if (File.Exists(destination) || Directory.Exists(destination))
                    {
                        return TemplateRenderResult.Failed(
                            plan,
                            CreateOutputDiagnostic(
                                "AUCTPL1004",
                                "Generation output already exists.",
                                options,
                                file.Change.Path,
                                destination));
                    }
                }

                var createdFiles = new List<string>();
                var createdDirectories = new List<string>();
                string? currentRelativePath = null;
                try
                {
                    for (var index = 0; index < files.Count; index++)
                    {
                        var file = files[index];
                        currentRelativePath = file.Change.Path;
                        cancellationToken.ThrowIfCancellationRequested();
                        beforeWrite?.Invoke(currentRelativePath, index);
                        var destination = ResolvePath(rootPath, currentRelativePath);
                        EnsureDirectory(rootPath, Path.GetDirectoryName(destination)!, createdDirectories);
                        WriteNewFile(destination, file.Content, createdFiles);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    return TemplateRenderResult.Success(
                        plan,
                        files.Select(static file => file.Change.Path).ToArray());
                }
                catch (OperationCanceledException)
                {
                    Rollback(createdFiles, createdDirectories, options);
                    throw;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    var diagnostics = new List<TemplateDiagnostic>
                    {
                        CreateOutputDiagnostic(
                            "AUCTPL1005",
                            $"Generation output failed: {exception.GetType().Name}.",
                            options,
                            currentRelativePath,
                            rootPath,
                            exception),
                    };
                    diagnostics.AddRange(Rollback(createdFiles, createdDirectories, options));
                    return TemplateRenderResult.Failed(plan, [.. diagnostics]);
                }
            }
        }
        finally
        {
            ReleaseRenderGate(rootPath, gate);
        }
    }

    private static TemplatePlan CreatePlan(
        GenerationTemplateOptions options,
        IReadOnlyList<TemplateFile> files)
    {
        var kind = options.Kind.ToString().ToLowerInvariant();
        var name = string.Join('/', options.GetNameSegments());
        var buildTarget = $"src/{options.ProjectName}/{options.ProjectName}.csproj";
        var testTarget = $"tests/{options.ProjectName}.Tests/{options.ProjectName}.Tests.csproj";

        return new TemplatePlan(
            operationId: $"generate-{kind}-{name.Replace('/', '-').ToLowerInvariant()}",
            command: $"atomui city generate {kind}",
            inputs: new Dictionary<string, object?>
            {
                ["kind"] = options.Kind.ToString(),
                ["name"] = name,
                ["projectName"] = options.ProjectName,
                ["rootNamespace"] = options.RootNamespace,
                ["includeTests"] = options.IncludeTests,
                ["routePath"] = options.RoutePath,
                ["cultures"] = options.Cultures.ToArray(),
                ["moduleDependencies"] = options.ModuleDependencies.ToArray(),
                ["reloadableConfiguration"] = options.ReloadableConfiguration,
            },
            changes: files.Select(static file => file.Change).ToArray(),
            buildTargets: [buildTarget],
            testTargets: options.IncludeTests ? [testTarget] : [],
            docsRequired: [],
            risks: [],
            rollback: files.Select(static file => file.Change.Path).Reverse().ToArray());
    }

    private static IReadOnlyList<TemplateFile> CreateFiles(GenerationTemplateOptions options)
    {
        return options.Kind switch
        {
            GenerationTemplateKind.Module => CreateModuleFiles(options),
            GenerationTemplateKind.Page => CreatePageFiles(options),
            GenerationTemplateKind.Test => CreateTestFiles(options),
            GenerationTemplateKind.Configuration => CreateConfigurationFiles(options),
            GenerationTemplateKind.Localization => CreateLocalizationFiles(options),
            _ => throw new InvalidOperationException("Generation kind was not validated."),
        };
    }

    private static IReadOnlyList<TemplateFile> CreateModuleFiles(GenerationTemplateOptions options)
    {
        var context = TemplateContext.Create(options, "Modules");
        var files = new List<TemplateFile>
        {
            CreateFile($"{context.SourceDirectory}/{context.TypeName}Module.cs", CreateModule(context, options)),
            CreateFile($"{context.SourceDirectory}/{context.TypeName}Options.cs", CreateModuleOptions(context)),
            CreateFile($"{context.SourceDirectory}/{context.TypeName}Contributions.cs", CreateModuleContributions(context)),
        };
        if (options.IncludeTests)
        {
            files.Add(CreateFile(
                $"{context.TestDirectory}/{context.TypeName}ModuleTests.cs",
                CreateModuleTests(context)));
            files.Add(CreateFile(
                $"tests/{options.ProjectName}.Tests/FeatureTestMatrix/Modules-{string.Join('-', options.GetNameSegments())}.md",
                CreateAreaTestMatrixFragment(context, "Module")));
        }

        return files.AsReadOnly();
    }

    private static IReadOnlyList<TemplateFile> CreatePageFiles(GenerationTemplateOptions options)
    {
        var context = TemplateContext.Create(options, "Routes");
        var files = new List<TemplateFile>
        {
            CreateFile($"{context.SourceDirectory}/{context.TypeName}Route.cs", CreateRoute(context, options)),
            CreateFile($"{context.SourceDirectory}/{context.TypeName}ViewModel.cs", CreateViewModel(context)),
            CreateFile($"{context.SourceDirectory}/{context.TypeName}View.axaml", CreateViewMarkup(context)),
            CreateFile($"{context.SourceDirectory}/{context.TypeName}View.axaml.cs", CreateViewCodeBehind(context)),
        };
        if (options.IncludeTests)
        {
            files.Add(CreateFile(
                $"{context.TestDirectory}/{context.TypeName}RouteTests.cs",
                CreateRouteTests(context, options)));
            files.Add(CreateFile(
                $"{context.TestDirectory}/{context.TypeName}ViewModelTests.cs",
                CreateViewModelTests(context)));
            files.Add(CreateFile(
                $"tests/{options.ProjectName}.Tests/FeatureTestMatrix/Routes-{string.Join('-', options.GetNameSegments())}.md",
                CreateAreaTestMatrixFragment(context, "Page")));
        }

        return files.AsReadOnly();
    }

    private static IReadOnlyList<TemplateFile> CreateTestFiles(GenerationTemplateOptions options)
    {
        var context = TemplateContext.Create(options, "Features");
        return Array.AsReadOnly<TemplateFile>(
        [
            CreateFile($"{context.TestDirectory}/{context.TypeName}Tests.cs", CreateStandaloneTests(context)),
            CreateFile(
                $"tests/{options.ProjectName}.Tests/FeatureTestMatrix/{string.Join('-', options.GetNameSegments())}.md",
                CreateFeatureTestMatrixFragment(context)),
        ]);
    }

    private static IReadOnlyList<TemplateFile> CreateConfigurationFiles(GenerationTemplateOptions options)
    {
        var context = TemplateContext.Create(options, "Configuration");
        var files = new List<TemplateFile>
        {
            CreateFile($"{context.SourceDirectory}/{context.TypeName}Options.cs", CreateConfigurationOptions(context, options)),
            CreateFile($"{context.SourceDirectory}/{context.TypeName}OptionsValidator.cs", CreateConfigurationValidator(context)),
        };
        if (options.IncludeTests)
        {
            files.Add(CreateFile(
                $"{context.TestDirectory}/{context.TypeName}OptionsTests.cs",
                CreateConfigurationTests(context, options)));
            files.Add(CreateFile(
                $"tests/{options.ProjectName}.Tests/FeatureTestMatrix/Configuration-{string.Join('-', options.GetNameSegments())}.md",
                CreateAreaTestMatrixFragment(context, "Configuration")));
        }

        return files.AsReadOnly();
    }

    private static IReadOnlyList<TemplateFile> CreateLocalizationFiles(GenerationTemplateOptions options)
    {
        var context = TemplateContext.Create(options, "Localization");
        var files = new List<TemplateFile>
        {
            CreateFile($"{context.SourceDirectory}/{context.TypeName}Localization.cs", CreateLocalizationDeclarations(context, options)),
            CreateFile($"{context.SourceDirectory}/{context.TypeName}Keys.cs", CreateLocalizationKeys(context)),
        };
        foreach (var culture in options.Cultures)
        {
            files.Add(CreateFile(
                $"{context.SourceDirectory}/{culture}/Resources.resx",
                CreateResources(context, culture)));
        }

        if (options.IncludeTests)
        {
            files.Add(CreateFile(
                $"{context.TestDirectory}/{context.TypeName}LocalizationTests.cs",
                CreateLocalizationTests(context, options)));
            files.Add(CreateFile(
                $"tests/{options.ProjectName}.Tests/FeatureTestMatrix/Localization-{string.Join('-', options.GetNameSegments())}.md",
                CreateAreaTestMatrixFragment(context, "Localization")));
        }

        return files.AsReadOnly();
    }

    private static TemplateFile CreateFile(string path, string content)
    {
        return new TemplateFile(TemplateChange.Create(path), content);
    }

    private static string CreateModule(TemplateContext context, GenerationTemplateOptions options)
    {
        var dependencies = options.ModuleDependencies.Count == 0
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                options.ModuleDependencies.Select(static dependency => $"[DependsOn(typeof(global::{dependency}))]")) +
                Environment.NewLine;
        return $$"""
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{context.Namespace}};

            {{dependencies}}public sealed class {{context.TypeName}}Module : ModuleBase
            {
                public override void ConfigureServices(ServiceConfigurationContext context)
                {
                    context.Services.AddSingleton<{{context.TypeName}}Service>();
                }
            }

            public sealed class {{context.TypeName}}Service;
            """;
    }

    private static string CreateModuleOptions(TemplateContext context)
    {
        return $$"""
            namespace {{context.Namespace}};

            public sealed class {{context.TypeName}}Options
            {
                public const string SectionName = "{{context.TypeName}}";
            }
            """;
    }

    private static string CreateModuleContributions(TemplateContext context)
    {
        return $$"""
            namespace {{context.Namespace}};

            public static class {{context.TypeName}}Contributions
            {
                public const string ContributionId = "{{context.StableId}}";
            }
            """;
    }

    private static string CreateModuleTests(TemplateContext context)
    {
        return $$"""
            using AtomUI.City.Core.Modularity;

            namespace {{context.TestNamespace}};

            public sealed class {{context.TypeName}}ModuleTests
            {
                [Fact]
                public void ModuleImplementsCityContract()
                {
                    Assert.IsAssignableFrom<IModule>(new {{context.Namespace}}.{{context.TypeName}}Module());
                }
            }
            """;
    }

    private static string CreateRoute(TemplateContext context, GenerationTemplateOptions options)
    {
        var route = options.GetNormalizedRoutePath();
        return $$"""
            using AtomUI.City.Routing;

            namespace {{context.Namespace}};

            [RouteMap]
            public static partial class {{context.TypeName}}Routes
            {
                [Route("{{route}}", typeof({{context.TypeName}}ViewModel), Id = "{{context.StableId}}")]
                public static partial RouteReference {{context.TypeName}}();
            }
            """;
    }

    private static string CreateViewModel(TemplateContext context)
    {
        return $$"""
            using AtomUI.City.Mvvm;

            namespace {{context.Namespace}};

            public sealed class {{context.TypeName}}ViewModel : ViewModelBase
            {
                protected override ValueTask OnActivatedAsync(
                    ActivationContext context,
                    CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return ValueTask.CompletedTask;
                }
            }
            """;
    }

    private static string CreateViewMarkup(TemplateContext context)
    {
        return $$"""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="{{context.Namespace}}.{{context.TypeName}}View">
              <TextBlock Text="{{context.TypeName}}" />
            </UserControl>
            """;
    }

    private static string CreateViewCodeBehind(TemplateContext context)
    {
        return $$"""
            using Avalonia.Controls;
            using Avalonia.Markup.Xaml;
            using AtomUI.City.Presentation;

            namespace {{context.Namespace}};

            [ViewFor(typeof({{context.TypeName}}ViewModel))]
            public sealed partial class {{context.TypeName}}View : UserControl
            {
                public {{context.TypeName}}View()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """;
    }

    private static string CreateRouteTests(TemplateContext context, GenerationTemplateOptions options)
    {
        return $$"""
            using AtomUI.City.Routing;

            namespace {{context.TestNamespace}};

            public sealed class {{context.TypeName}}RouteTests
            {
                [Fact]
                public void RouteDeclarationTargetsExpectedViewModel()
                {
                    var method = typeof({{context.Namespace}}.{{context.TypeName}}Routes)
                        .GetMethod(nameof({{context.Namespace}}.{{context.TypeName}}Routes.{{context.TypeName}}));
                    var route = Assert.Single(method!.GetCustomAttributes(typeof(RouteAttribute), inherit: false));
                    Assert.Equal(typeof({{context.Namespace}}.{{context.TypeName}}ViewModel), ((RouteAttribute)route).ViewModelType);
                    Assert.Equal("{{options.GetNormalizedRoutePath()}}", ((RouteAttribute)route).Template);
                }

                [Fact]
                public void ViewDeclaresPresentationMapping()
                {
                    var mapping = Assert.Single(typeof({{context.Namespace}}.{{context.TypeName}}View)
                        .GetCustomAttributes(typeof(AtomUI.City.Presentation.ViewForAttribute), inherit: false));
                    Assert.Equal(
                        typeof({{context.Namespace}}.{{context.TypeName}}ViewModel),
                        ((AtomUI.City.Presentation.ViewForAttribute)mapping).ViewModelType);
                }
            }
            """;
    }

    private static string CreateViewModelTests(TemplateContext context)
    {
        return $$"""
            namespace {{context.TestNamespace}};

            public sealed class {{context.TypeName}}ViewModelTests
            {
                [Fact]
                public async Task ViewModelActivatesAndDeactivates()
                {
                    using var viewModel = new {{context.Namespace}}.{{context.TypeName}}ViewModel();
                    using var scope = new AtomUI.City.Mvvm.ActivationScope();

                    await viewModel.ActivateAsync(scope);
                    Assert.True(viewModel.IsActive);

                    await viewModel.DeactivateAsync();
                    Assert.False(viewModel.IsActive);
                    Assert.True(scope.IsDisposed);
                }

                [Fact]
                public async Task CancelledActivationDoesNotBecomeActive()
                {
                    using var viewModel = new {{context.Namespace}}.{{context.TypeName}}ViewModel();
                    using var scope = new AtomUI.City.Mvvm.ActivationScope();
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                        await viewModel.ActivateAsync(scope, cancellation.Token));
                    Assert.False(viewModel.IsActive);
                }
            }
            """;
    }

    private static string CreateStandaloneTests(TemplateContext context)
    {
        return $$"""
            namespace {{context.TestNamespace}};

            public sealed class {{context.TypeName}}Tests
            {
                [Fact]
                public void FeatureContractIsRepresented()
                {
                    Assert.True(true);
                }
            }
            """;
    }

    private static string CreateFeatureTestMatrixFragment(TemplateContext context)
    {
        return $$"""
            # {{context.TypeName}} Test Matrix

            | Scenario | Layer | Status |
            | --- | --- | --- |
            | contract | Unit | Required |
            | cancellation | Unit | Required |
            | integration | Integration | Required |
            """;
    }

    private static string CreateAreaTestMatrixFragment(TemplateContext context, string area)
    {
        return $$"""
            # {{context.TypeName}} {{area}} Test Matrix

            | Scenario | Layer | Status |
            | --- | --- | --- |
            | declaration and generated metadata | Contract | Required |
            | cancellation and failure | Unit | Required |
            | generated output build | TemplateSmoke | Required |
            """;
    }

    private static string CreateConfigurationOptions(
        TemplateContext context,
        GenerationTemplateOptions options)
    {
        return $$"""
            namespace {{context.Namespace}};

            public sealed class {{context.TypeName}}Options
            {
                public const string SectionName = "{{context.TypeName}}";

                public const bool ReloadOnChange = {{options.ReloadableConfiguration.ToString().ToLowerInvariant()}};

                public string Value { get; set; } = string.Empty;
            }
            """;
    }

    private static string CreateConfigurationValidator(TemplateContext context)
    {
        return $$"""
            using Microsoft.Extensions.Options;

            namespace {{context.Namespace}};

            public sealed class {{context.TypeName}}OptionsValidator : IValidateOptions<{{context.TypeName}}Options>
            {
                public ValidateOptionsResult Validate(string? name, {{context.TypeName}}Options options)
                {
                    return string.IsNullOrWhiteSpace(options.Value)
                        ? ValidateOptionsResult.Fail("Value is required.")
                        : ValidateOptionsResult.Success;
                }
            }
            """;
    }

    private static string CreateConfigurationTests(
        TemplateContext context,
        GenerationTemplateOptions options)
    {
        return $$"""
            namespace {{context.TestNamespace}};

            public sealed class {{context.TypeName}}OptionsTests
            {
                [Fact]
                public void ValidatorAcceptsConfiguredValue()
                {
                    var validator = new {{context.Namespace}}.{{context.TypeName}}OptionsValidator();
                    var result = validator.Validate(null, new {{context.Namespace}}.{{context.TypeName}}Options { Value = "configured" });
                    Assert.True(result.Succeeded);
                }

                [Fact]
                public void ValidatorRejectsMissingValue()
                {
                    var validator = new {{context.Namespace}}.{{context.TypeName}}OptionsValidator();
                    var result = validator.Validate(null, new {{context.Namespace}}.{{context.TypeName}}Options());
                    Assert.False(result.Succeeded);
                }

                [Fact]
                public void ReloadPolicyIsExplicit()
                {
                    Assert.Equal({{options.ReloadableConfiguration.ToString().ToLowerInvariant()}}, {{context.Namespace}}.{{context.TypeName}}Options.ReloadOnChange);
                }
            }
            """;
    }

    private static string CreateLocalizationDeclarations(
        TemplateContext context,
        GenerationTemplateOptions options)
    {
        var fallbackCulture = options.Cultures[0];
        var attributes = string.Join(
            Environment.NewLine,
            options.Cultures.Select(culture =>
            {
                var fallback = culture.Equals(fallbackCulture, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : $", FallbackCulture = \"{fallbackCulture}\"";
                return $"[assembly: LanguagePackage(\"{context.TypeName}.{culture}\", \"{culture}\", Scope = ResourceScope.Module, ScopeId = \"{context.StableId}\", ResourceBaseName = \"{context.Namespace}.{culture}.Resources\"{fallback})]";
            }));
        return $$"""
            using AtomUI.City.Localization;

            {{attributes}}

            namespace {{context.Namespace}};

            public static class {{context.TypeName}}Localization
            {
                public const string ScopeId = "{{context.StableId}}";
            }
            """;
    }

    private static string CreateLocalizationKeys(TemplateContext context)
    {
        return $$"""
            namespace {{context.Namespace}};

            public static class {{context.TypeName}}Keys
            {
                public const string Title = "{{context.TypeName}}.Title";
            }
            """;
    }

    private static string CreateResources(TemplateContext context, string culture)
    {
        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
              <resheader name="version"><value>2.0</value></resheader>
              <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms</value></resheader>
              <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms</value></resheader>
              <data name="{{context.TypeName}}.Title" xml:space="preserve"><value>{{context.TypeName}} ({{culture}})</value></data>
            </root>
            """;
    }

    private static string CreateLocalizationTests(
        TemplateContext context,
        GenerationTemplateOptions options)
    {
        return $$"""
            using AtomUI.City.Localization;

            namespace {{context.TestNamespace}};

            public sealed class {{context.TypeName}}LocalizationTests
            {
                [Fact]
                public void AssemblyDeclaresEveryCulture()
                {
                    var cultures = typeof({{context.Namespace}}.{{context.TypeName}}Localization)
                        .Assembly
                        .GetCustomAttributes(typeof(LanguagePackageAttribute), inherit: false)
                        .Cast<LanguagePackageAttribute>()
                        .Where(attribute => attribute.ScopeId == {{context.Namespace}}.{{context.TypeName}}Localization.ScopeId)
                        .Select(attribute => attribute.Culture)
                        .OrderBy(static culture => culture)
                        .ToArray();
                    Assert.Equal(new[] { {{string.Join(", ", options.Cultures.Order().Select(static culture => $"\"{culture}\""))}} }, cultures);
                }

                [Fact]
                public void TitleKeyIsStable()
                {
                    Assert.Equal("{{context.TypeName}}.Title", {{context.Namespace}}.{{context.TypeName}}Keys.Title);
                }

                [Fact]
                public void SecondaryCulturesFallbackToPrimaryCulture()
                {
                    var packages = typeof({{context.Namespace}}.{{context.TypeName}}Localization)
                        .Assembly
                        .GetCustomAttributes(typeof(LanguagePackageAttribute), inherit: false)
                        .Cast<LanguagePackageAttribute>()
                        .Where(attribute => attribute.ScopeId == {{context.Namespace}}.{{context.TypeName}}Localization.ScopeId)
                        .ToArray();
                    Assert.All(
                        packages.Where(package => package.Culture != "{{options.Cultures[0]}}"),
                        package => Assert.Equal("{{options.Cultures[0]}}", package.FallbackCulture));
                }
            }
            """;
    }

    private static string ResolvePath(string rootPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine([rootPath, .. relativePath.Split('/')]));
        var rootPrefix = Path.TrimEndingDirectorySeparator(rootPath) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, GetPathComparison()))
        {
            throw new IOException("Generation output escaped its target root.");
        }

        return path;
    }

    private static void EnsureDirectory(
        string rootPath,
        string directory,
        List<string> createdDirectories)
    {
        var missing = new Stack<string>();
        for (var current = directory;
             !Directory.Exists(current);
             current = Path.GetDirectoryName(current) ?? throw new IOException("Generation directory has no parent."))
        {
            if (File.Exists(current))
            {
                throw new IOException("A file blocks a generation output directory.");
            }

            missing.Push(current);
            if (string.Equals(current, rootPath, GetPathComparison()))
            {
                break;
            }
        }

        while (missing.TryPop(out var path))
        {
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
        }
    }

    private static void ValidateExistingAncestors(string rootPath, string destination)
    {
        for (var current = Path.GetDirectoryName(destination);
             current is not null && !string.Equals(current, rootPath, GetPathComparison());
             current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current))
            {
                throw new IOException("A file blocks a generation output directory.");
            }

            if (!Directory.Exists(current))
            {
                continue;
            }

            var directory = new DirectoryInfo(current);
            if (directory.LinkTarget is not null ||
                directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException("Generation output cannot traverse a symbolic link or reparse point.");
            }
        }
    }

    private static void WriteNewFile(string path, string content, List<string> createdFiles)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        createdFiles.Add(path);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static IReadOnlyList<TemplateDiagnostic> Rollback(
        IReadOnlyList<string> createdFiles,
        IReadOnlyList<string> createdDirectories,
        GenerationTemplateOptions options)
    {
        var diagnostics = new List<TemplateDiagnostic>();
        foreach (var path in createdFiles.Reverse())
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(CreateRollbackDiagnostic(path, exception, options));
            }
        }

        foreach (var path in createdDirectories.Reverse())
        {
            try
            {
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                {
                    Directory.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(CreateRollbackDiagnostic(path, exception, options));
            }
        }

        return diagnostics;
    }

    private static TemplateDiagnostic CreateRollbackDiagnostic(
        string path,
        Exception exception,
        GenerationTemplateOptions options)
    {
        return new TemplateDiagnostic(
            "AUCTPL1006",
            $"Generation rollback failed: {exception.GetType().Name}.",
            new Dictionary<string, object?>
            {
                ["templateId"] = options.GetTemplateId(),
                ["path"] = path,
                ["errorType"] = exception.GetType().FullName,
            });
    }

    private static TemplateDiagnostic CreateOutputDiagnostic(
        string code,
        string message,
        GenerationTemplateOptions options,
        string? relativePath,
        string targetPath,
        Exception? exception = null)
    {
        return new TemplateDiagnostic(
            code,
            message,
            new Dictionary<string, object?>
            {
                ["templateId"] = options.GetTemplateId(),
                ["targetPath"] = targetPath,
                ["path"] = relativePath,
                ["operationId"] = $"generate-{options.Kind.ToString().ToLowerInvariant()}",
                ["errorType"] = exception?.GetType().FullName,
            });
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    private static RenderGate AcquireRenderGate(string rootPath)
    {
        lock (RenderGatesSyncRoot)
        {
            if (!RenderGates.TryGetValue(rootPath, out var gate))
            {
                gate = new RenderGate();
                RenderGates.Add(rootPath, gate);
            }

            gate.ReferenceCount++;
            return gate;
        }
    }

    private static void ReleaseRenderGate(string rootPath, RenderGate gate)
    {
        lock (RenderGatesSyncRoot)
        {
            gate.ReferenceCount--;
            if (gate.ReferenceCount == 0 &&
                RenderGates.TryGetValue(rootPath, out var registered) &&
                ReferenceEquals(registered, gate))
            {
                RenderGates.Remove(rootPath);
            }
        }
    }

    private sealed class RenderGate
    {
        public object SyncRoot { get; } = new();

        public int ReferenceCount { get; set; }
    }

    private sealed record TemplateFile(TemplateChange Change, string Content);

    private sealed record TemplateContext(
        string TypeName,
        string Namespace,
        string TestNamespace,
        string SourceDirectory,
        string TestDirectory,
        string StableId)
    {
        public static TemplateContext Create(GenerationTemplateOptions options, string area)
        {
            var segments = options.GetNameSegments();
            var typeName = segments[^1];
            var directories = string.Join('/', segments);
            var namespaceSuffix = string.Join('.', segments);
            var sourceDirectory = $"src/{options.ProjectName}/{area}/{directories}";
            var testDirectory = $"tests/{options.ProjectName}.Tests/{area}/{directories}";
            var targetNamespace = $"{options.RootNamespace}.{area}.{namespaceSuffix}";
            var stableId = string.Join('.', segments).ToLowerInvariant();
            return new TemplateContext(
                typeName,
                targetNamespace,
                $"{options.RootNamespace}.Tests.{area}.{namespaceSuffix}",
                sourceDirectory,
                testDirectory,
                stableId);
        }
    }
}
