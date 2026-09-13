using System.Text.Json;
using System.Xml.Linq;
using AtomUI.City.Templates;
using AtomUI.City.Testing.Processes;

namespace AtomUI.City.TemplateSmokeTests;

public sealed class GenerationTemplateRendererTests
{
    [Theory]
    [InlineData(GenerationTemplateKind.Module, "src/SampleApp/Modules/Sales/SalesModule.cs")]
    [InlineData(GenerationTemplateKind.Page, "src/SampleApp/Routes/Sales/SalesRoute.cs")]
    [InlineData(GenerationTemplateKind.Test, "tests/SampleApp.Tests/Features/Sales/SalesTests.cs")]
    [InlineData(GenerationTemplateKind.Configuration, "src/SampleApp/Configuration/Sales/SalesOptions.cs")]
    [InlineData(GenerationTemplateKind.Localization, "src/SampleApp/Localization/Sales/en-US/Resources.resx")]
    public void CreatePlanCoversEveryGenerationKind(
        GenerationTemplateKind kind,
        string expectedPath)
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();

        var plan = renderer.CreatePlan(CreateOptions(kind, workspace.Root));

        Assert.Contains(plan.Changes, change => change.Path == expectedPath);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src")));
        Assert.Empty(plan.Validate());
    }

    [Fact]
    public void RenderWritesModuleDependenciesAndCompleteModuleSkeleton()
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();
        var options = CreateOptions(
            GenerationTemplateKind.Module,
            workspace.Root,
            moduleDependencies: ["Company.Foundation.FoundationModule"]);

        var result = renderer.Render(options);

        Assert.True(result.Succeeded);
        var content = File.ReadAllText(Path.Combine(
            workspace.Root,
            "src",
            "SampleApp",
            "Modules",
            "Sales",
            "SalesModule.cs"));
        Assert.Contains("[DependsOn(typeof(global::Company.Foundation.FoundationModule))]", content);
        Assert.Contains("context.Services.AddSingleton<SalesService>();", content);
        Assert.True(File.Exists(Path.Combine(
            workspace.Root,
            "src",
            "SampleApp",
            "Modules",
            "Sales",
            "SalesContributions.cs")));
    }

    [Fact]
    public void RenderWritesPageRouteViewModelViewAndTests()
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();

        var result = renderer.Render(CreateOptions(GenerationTemplateKind.Page, workspace.Root));

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.AppliedPaths.Count);
        Assert.Contains(result.AppliedPaths, path => path.EndsWith("SalesView.axaml", StringComparison.Ordinal));
        Assert.Contains(result.AppliedPaths, path => path.EndsWith("SalesViewModelTests.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderWritesConfiguredCulturesAndReloadPolicy()
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();
        var localization = CreateOptions(
            GenerationTemplateKind.Localization,
            workspace.Root,
            cultures: ["fr-FR", "ja-JP"]);
        var configuration = CreateOptions(
            GenerationTemplateKind.Configuration,
            workspace.Root,
            name: "Runtime",
            reloadableConfiguration: true);

        var localizationResult = renderer.Render(localization);
        var configurationResult = renderer.Render(configuration);

        Assert.True(localizationResult.Succeeded);
        Assert.True(configurationResult.Succeeded);
        Assert.True(File.Exists(Path.Combine(
            workspace.Root,
            "src",
            "SampleApp",
            "Localization",
            "Sales",
            "ja-JP",
            "Resources.resx")));
        Assert.Contains(
            "ReloadOnChange = true",
            File.ReadAllText(Path.Combine(
                workspace.Root,
                "src",
                "SampleApp",
                "Configuration",
                "Runtime",
                "RuntimeOptions.cs")));
    }

    [Theory]
    [InlineData(GenerationTemplateKind.Module, "class", "AUCTPL2001")]
    [InlineData(GenerationTemplateKind.Page, "Sales", "AUCTPL2004")]
    [InlineData(GenerationTemplateKind.Localization, "Sales", "AUCTPL2005")]
    public void InvalidOptionsReturnStableDiagnosticsWithoutWriting(
        GenerationTemplateKind kind,
        string name,
        string expectedCode)
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();
        var options = CreateOptions(
            kind,
            workspace.Root,
            name,
            kind == GenerationTemplateKind.Page ? null : "/sales",
            kind == GenerationTemplateKind.Localization ? ["invalid culture!"] : ["en-US"]);

        var result = renderer.Render(options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src")));
    }

    [Fact]
    public void OptionsCaptureInputCollections()
    {
        var cultures = new List<string> { "en-US" };
        var dependencies = new List<string> { "Company.FoundationModule" };

        var options = new GenerationTemplateOptions
        {
            Kind = GenerationTemplateKind.Module,
            Name = "Sales",
            ProjectName = "SampleApp",
            RootNamespace = "Company.Sample",
            OutputPath = Path.GetTempPath(),
            Cultures = cultures,
            ModuleDependencies = dependencies,
        };
        cultures.Add("zh-CN");
        dependencies.Clear();

        Assert.Equal(["en-US"], options.Cultures);
        Assert.Equal(["Company.FoundationModule"], options.ModuleDependencies);
    }

    [Fact]
    public void ConflictPreflightDoesNotWriteAnyOtherPlannedFile()
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();
        var options = CreateOptions(GenerationTemplateKind.Module, workspace.Root);
        var plan = renderer.CreatePlan(options);
        var conflict = Path.Combine([workspace.Root, .. plan.Changes[^1].Path.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(conflict)!);
        File.WriteAllText(conflict, "existing");

        var result = renderer.Render(options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AUCTPL1004");
        Assert.Equal("existing", File.ReadAllText(conflict));
        foreach (var path in plan.Changes.Take(plan.Changes.Count - 1))
        {
            Assert.False(File.Exists(Path.Combine([workspace.Root, .. path.Path.Split('/')])));
        }
    }

    [Fact]
    public void IoFailureRollsBackFilesCreatedByCurrentOperation()
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();
        var options = CreateOptions(GenerationTemplateKind.Module, workspace.Root);
        var plan = renderer.CreatePlan(options);

        var result = renderer.Render(
            options,
            CancellationToken.None,
            (_, index) =>
            {
                if (index == 1)
                {
                    throw new IOException("injected");
                }
            });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AUCTPL1005");
        foreach (var path in plan.Changes)
        {
            Assert.False(File.Exists(Path.Combine([workspace.Root, .. path.Path.Split('/')])));
        }
    }

    [Fact]
    public void CancellationDuringRenderRollsBackEveryCreatedFile()
    {
        using var workspace = new TestWorkspace();
        using var cancellation = new CancellationTokenSource();
        var renderer = new GenerationTemplateRenderer();
        var options = CreateOptions(GenerationTemplateKind.Page, workspace.Root);
        var plan = renderer.CreatePlan(options);

        Assert.Throws<OperationCanceledException>(() => renderer.Render(
            options,
            cancellation.Token,
            (_, index) =>
            {
                if (index == 1)
                {
                    cancellation.Cancel();
                }
            }));

        foreach (var path in plan.Changes)
        {
            Assert.False(File.Exists(Path.Combine([workspace.Root, .. path.Path.Split('/')])));
        }
    }

    [Fact]
    public async Task ConcurrentRendersToSameRootProduceOneCompleteTransaction()
    {
        using var workspace = new TestWorkspace();
        var renderer = new GenerationTemplateRenderer();
        var options = CreateOptions(GenerationTemplateKind.Module, workspace.Root);

        var results = await Task.WhenAll(
            Task.Run(() => renderer.Render(options)),
            Task.Run(() => renderer.Render(options)));

        Assert.Single(results, result => result.Succeeded);
        var failure = Assert.Single(results, result => !result.Succeeded);
        Assert.Contains(failure.Diagnostics, diagnostic => diagnostic.Code == "AUCTPL1004");
        var success = Assert.Single(results, result => result.Succeeded);
        Assert.All(success.AppliedPaths, path =>
            Assert.True(File.Exists(Path.Combine([workspace.Root, .. path.Split('/')]))));
    }

    [Fact]
    public async Task GeneratedFamiliesBuildAndTheirGeneratedTestsPass()
    {
        using var workspace = new TestWorkspace();
        var repositoryRoot = FindRepositoryRoot();
        await CreateBuildableWorkspaceAsync(workspace.Root, repositoryRoot.FullName);
        var processEnvironment = new Dictionary<string, string?>
        {
            ["NUGET_PACKAGES"] = ReadRepositoryPackageCache(repositoryRoot.FullName),
            ["AVALONIA_TELEMETRY_OPTOUT"] = "1",
        };
        var renderer = new GenerationTemplateRenderer();
        var cases = new[]
        {
            CreateOptions(GenerationTemplateKind.Module, workspace.Root, name: "Sales"),
            CreateOptions(GenerationTemplateKind.Page, workspace.Root, name: "Orders", routePath: "/orders"),
            CreateOptions(GenerationTemplateKind.Configuration, workspace.Root, name: "Runtime", reloadableConfiguration: true),
            CreateOptions(GenerationTemplateKind.Localization, workspace.Root, name: "Shell", cultures: ["en-US", "zh-CN"]),
            CreateOptions(GenerationTemplateKind.Test, workspace.Root, name: "Health"),
        };
        foreach (var options in cases)
        {
            Assert.True(renderer.Render(options).Succeeded);
        }

        var restore = await ProcessTestRunner.RunAsync(
            "dotnet",
            workspace.Root,
            TimeSpan.FromMinutes(5),
            processEnvironment,
            "restore",
            "GenerationSmoke.slnx",
            "--ignore-failed-sources",
            "-p:NuGetAudit=false");
        Assert.True(
            restore.ExitCode == 0,
            $"Restore failed.\nSTDOUT:\n{restore.StandardOutput}\nSTDERR:\n{restore.StandardError}");

        var build = await ProcessTestRunner.RunAsync(
            "dotnet",
            workspace.Root,
            TimeSpan.FromMinutes(5),
            processEnvironment,
            "build",
            "GenerationSmoke.slnx",
            "-c",
            "Release",
            "--no-restore",
            "-v:minimal");
        Assert.True(
            build.ExitCode == 0,
            $"Build failed.\nSTDOUT:\n{build.StandardOutput}\nSTDERR:\n{build.StandardError}");

        var test = await ProcessTestRunner.RunAsync(
            "dotnet",
            workspace.Root,
            TimeSpan.FromMinutes(5),
            processEnvironment,
            "test",
            "GenerationSmoke.slnx",
            "-c",
            "Release",
            "--no-build",
            "-v:minimal");
        Assert.True(
            test.ExitCode == 0,
            $"Tests failed.\nSTDOUT:\n{test.StandardOutput}\nSTDERR:\n{test.StandardError}");
    }

    private static GenerationTemplateOptions CreateOptions(
        GenerationTemplateKind kind,
        string outputPath,
        string name = "Sales",
        string? routePath = "/sales",
        IReadOnlyList<string>? cultures = null,
        IReadOnlyList<string>? moduleDependencies = null,
        bool reloadableConfiguration = false)
    {
        return new GenerationTemplateOptions
        {
            Kind = kind,
            Name = name,
            ProjectName = "SampleApp",
            RootNamespace = "Company.Sample",
            OutputPath = outputPath,
            RoutePath = routePath,
            Cultures = cultures ?? ["en-US", "zh-CN"],
            ModuleDependencies = moduleDependencies ?? [],
            ReloadableConfiguration = reloadableConfiguration,
        };
    }

    private static async Task CreateBuildableWorkspaceAsync(string root, string repositoryRoot)
    {
        var sourceDirectory = Path.Combine(root, "src", "SampleApp");
        var testDirectory = Path.Combine(root, "tests", "SampleApp.Tests");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(testDirectory);
        var avaloniaVersion = XDocument
            .Load(Path.Combine(repositoryRoot, "build", "Version.props"))
            .Descendants("AvaloniaVersion")
            .Single()
            .Value;

        await File.WriteAllTextAsync(
            Path.Combine(root, "GenerationSmoke.slnx"),
            """
            <Solution>
              <Project Path="src/SampleApp/SampleApp.csproj" />
              <Project Path="tests/SampleApp.Tests/SampleApp.Tests.csproj" />
            </Solution>
            """);
        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, "SampleApp.csproj"),
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Company.Sample</RootNamespace>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="{{avaloniaVersion}}" />
                <ProjectReference Include="{{Path.Combine(repositoryRoot, "src", "AtomUI.City.Core", "AtomUI.City.Core.csproj")}}" />
                <ProjectReference Include="{{Path.Combine(repositoryRoot, "src", "AtomUI.City.Mvvm", "AtomUI.City.Mvvm.csproj")}}" />
                <ProjectReference Include="{{Path.Combine(repositoryRoot, "src", "AtomUI.City.Routing", "AtomUI.City.Routing.csproj")}}" />
                <ProjectReference Include="{{Path.Combine(repositoryRoot, "src", "AtomUI.City.Localization", "AtomUI.City.Localization.csproj")}}" />
                <ProjectReference Include="{{Path.Combine(repositoryRoot, "src", "AtomUI.City.Presentation", "AtomUI.City.Presentation.csproj")}}" />
                <Analyzer Include="{{Path.Combine(repositoryRoot, "output", "bin", "Release", "AtomUI.City.Generators", "netstandard2.0", "AtomUI.City.Generators.dll")}}" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(
            Path.Combine(testDirectory, "SampleApp.Tests.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsTestProject>true</IsTestProject>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" PrivateAssets="all" />
                <ProjectReference Include="../../src/SampleApp/SampleApp.csproj" />
              </ItemGroup>
              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>
            </Project>
            """);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtomUICity.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private static string ReadRepositoryPackageCache(string repositoryRoot)
    {
        var assetsPath = Path.Combine(
            repositoryRoot,
            "output",
            "AtomUI.City.TemplateSmokeTests",
            "obj",
            "project.assets.json");
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        return document.RootElement
            .GetProperty("packageFolders")
            .EnumerateObject()
            .First()
            .Name;
    }

    private sealed class TestWorkspace : IDisposable
    {
        public TestWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "AtomUICityGenerationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
