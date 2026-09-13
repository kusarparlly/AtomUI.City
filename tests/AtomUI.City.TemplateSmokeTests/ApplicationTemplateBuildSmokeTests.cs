using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using AtomUI.City.Testing.Processes;
using AtomUI.City.Templates;

namespace AtomUI.City.TemplateSmokeTests;

public sealed class ApplicationTemplateBuildSmokeTests
{
    [Fact]
    public void TemplatePlanCollectionsRejectExternalMutation()
    {
        var plan = new TemplatePlan(
            "new-app-SalesClient",
            "atomui city new app",
            new Dictionary<string, object?> { ["appName"] = "SalesClient" },
            [TemplateChange.Create("src/SalesClient/SalesClient.csproj")]);
        var inputs = Assert.IsAssignableFrom<IDictionary<string, object?>>(plan.Inputs);
        var changes = Assert.IsAssignableFrom<IList<TemplateChange>>(plan.Changes);

        Assert.Throws<NotSupportedException>(() => inputs["appName"] = "Changed");
        Assert.Throws<NotSupportedException>(() => changes[0] = TemplateChange.Create("changed"));
        Assert.Equal("SalesClient", plan.Inputs["appName"]);
        Assert.Equal("src/SalesClient/SalesClient.csproj", plan.Changes[0].Path);
    }

    [Fact]
    public void TemplateRenderDiagnosticsRejectExternalListMutation()
    {
        var result = TemplateRenderResult.Failed(
            new TemplateDiagnostic("AUCTPL0001", "First"),
            new TemplateDiagnostic("AUCTPL0002", "Second"));
        var diagnostics = Assert.IsAssignableFrom<IList<TemplateDiagnostic>>(result.Diagnostics);

        Assert.Throws<NotSupportedException>(() => diagnostics[0] = new TemplateDiagnostic("AUCTPL9999", "Changed"));
        Assert.Equal("AUCTPL0001", result.Diagnostics[0].Code);
        Assert.Equal("AUCTPL0002", result.Diagnostics[1].Code);
    }

    [Fact]
    public void ApplicationTemplateGeneratesBuildAndTestProjectFiles()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        var appProjectPath = Path.Combine(workspace.Root, "src", "SalesClient", "SalesClient.csproj");
        var testProjectPath = Path.Combine(workspace.Root, "tests", "SalesClient.Tests", "SalesClient.Tests.csproj");
        var programPath = Path.Combine(workspace.Root, "src", "SalesClient", "Program.cs");
        var appXamlPath = Path.Combine(workspace.Root, "src", "SalesClient", "App.axaml");
        var appCodeBehindPath = Path.Combine(workspace.Root, "src", "SalesClient", "App.axaml.cs");
        var bootstrapPath = Path.Combine(workspace.Root, "src", "SalesClient", "DesktopBootstrap.cs");
        var mainWindowPath = Path.Combine(workspace.Root, "src", "SalesClient", "MainWindow.axaml");
        var mainWindowCodeBehindPath = Path.Combine(workspace.Root, "src", "SalesClient", "MainWindow.axaml.cs");

        Assert.True(File.Exists(appProjectPath), $"Expected application project at {appProjectPath}.");
        Assert.True(File.Exists(testProjectPath), $"Expected test project at {testProjectPath}.");
        Assert.True(File.Exists(appXamlPath), $"Expected Avalonia Application at {appXamlPath}.");
        Assert.True(File.Exists(appCodeBehindPath), $"Expected Avalonia Application code at {appCodeBehindPath}.");
        Assert.True(File.Exists(bootstrapPath), $"Expected desktop bootstrap at {bootstrapPath}.");
        Assert.True(File.Exists(mainWindowPath), $"Expected main Window at {mainWindowPath}.");
        Assert.True(File.Exists(mainWindowCodeBehindPath), $"Expected main Window code at {mainWindowCodeBehindPath}.");

        var appProject = File.ReadAllText(appProjectPath);
        Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", appProject, StringComparison.Ordinal);
        Assert.Contains("<Nullable>enable</Nullable>", appProject, StringComparison.Ordinal);
        Assert.Contains("<OutputType>WinExe</OutputType>", appProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="AtomUI.City.Core" Version="1.0.0" />""", appProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="AtomUI.City.Build" Version="1.0.0" PrivateAssets="all" />""", appProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="AtomUI.City.Presentation" Version="1.0.0" />""", appProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="Avalonia.Desktop" Version="12.0.4" />""", appProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.4" />""", appProject, StringComparison.Ordinal);

        var testProject = File.ReadAllText(testProjectPath);
        Assert.Contains("Avalonia.Headless", testProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="Microsoft.NET.Test.Sdk" Version=""", testProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="xunit" Version=""", testProject, StringComparison.Ordinal);
        Assert.Contains("""<ProjectReference Include="../../src/SalesClient/SalesClient.csproj" />""", testProject, StringComparison.Ordinal);

        var program = File.ReadAllText(programPath);
        Assert.Contains("using AtomUI.City.Core.Hosting;", program, StringComparison.Ordinal);
        Assert.Contains("ApplicationHost.CreateBuilder(args)", program, StringComparison.Ordinal);
        Assert.Contains("options.ApplicationId = \"Company.SalesClient\";", program, StringComparison.Ordinal);
        Assert.Contains("options.ApplicationName = \"SalesClient\";", program, StringComparison.Ordinal);
        Assert.Contains("[STAThread]", program, StringComparison.Ordinal);
        Assert.Contains("builder.UseModule<PresentationModule>();", program, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<MainWindow>()", program, StringComparison.Ordinal);
        Assert.Contains("host.StartAsync().GetAwaiter().GetResult();", program, StringComparison.Ordinal);
        Assert.Contains("StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)", program, StringComparison.Ordinal);
        Assert.Contains("Task.Run(async () => await host.StopAsync().ConfigureAwait(false))", program, StringComparison.Ordinal);

        var appCodeBehind = File.ReadAllText(appCodeBehindPath);
        Assert.Contains("DesktopBootstrap.Initialize(desktopLifetime);", appCodeBehind, StringComparison.Ordinal);
        var bootstrap = File.ReadAllText(bootstrapPath);
        Assert.Contains("runtime.Attach(lifetime, host.HostScope);", bootstrap, StringComparison.Ordinal);
        Assert.Contains("runtime.RegisterWindow(mainWindow, \"main\");", bootstrap, StringComparison.Ordinal);
        Assert.Contains("mainWindow.Closed", bootstrap, StringComparison.Ordinal);
        Assert.Contains("WindowSessionState.Closed", bootstrap, StringComparison.Ordinal);
        Assert.Contains("lifetime.Shutdown(exitCode)", bootstrap, StringComparison.Ordinal);
        Assert.Contains("host.Services.GetRequiredService<MainWindow>()", bootstrap, StringComparison.Ordinal);
        Assert.Contains("presentation:RouteOutletProperties.Name=\"main\"", File.ReadAllText(mainWindowPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationTemplateGeneratesLayeredTestingProjectContract()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        var appProjectPath = Path.Combine(workspace.Root, "src", "SalesClient", "SalesClient.csproj");
        var testProjectPath = Path.Combine(workspace.Root, "tests", "SalesClient.Tests", "SalesClient.Tests.csproj");
        var testSourcePath = Path.Combine(workspace.Root, "tests", "SalesClient.Tests", "ApplicationSmokeTests.cs");

        Assert.True(File.Exists(testProjectPath), $"Expected test project at {testProjectPath}.");
        Assert.True(File.Exists(testSourcePath), $"Expected generated smoke test at {testSourcePath}.");

        var appPackageReferences = ReadPackageReferences(appProjectPath);
        var testPackageReferences = ReadPackageReferences(testProjectPath);
        var testProject = XDocument.Load(testProjectPath);
        var rootNamespace = testProject.Descendants("RootNamespace").Single().Value;
        var projectReferences = testProject.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .ToArray();

        Assert.Equal("Company.SalesClient.Tests", rootNamespace);
        Assert.DoesNotContain("AtomUI.City.Testing", appPackageReferences);
        Assert.DoesNotContain("AtomUI.City.Testing", testPackageReferences);
        Assert.Contains("../../src/SalesClient/SalesClient.csproj", projectReferences);

        var smokeTest = File.ReadAllText(testSourcePath);
        Assert.DoesNotContain("using AtomUI.City.Testing;", smokeTest, StringComparison.Ordinal);
        Assert.DoesNotContain("[TestLayer(", smokeTest, StringComparison.Ordinal);
        Assert.Contains("host.HostScope.State", smokeTest, StringComparison.Ordinal);
        Assert.Contains("UseHeadless", smokeTest, StringComparison.Ordinal);
        Assert.Contains("DesktopBootstrap.Initialize(lifetime);", smokeTest, StringComparison.Ordinal);
        Assert.Contains("namespace Company.SalesClient.Tests;", smokeTest, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationTemplateGeneratesSolutionBuildPropsAndDocsEntry()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        var solutionPath = Path.Combine(workspace.Root, "SalesClient.slnx");
        var directoryBuildPropsPath = Path.Combine(workspace.Root, "Directory.Build.props");
        var directoryPackagesPropsPath = Path.Combine(workspace.Root, "Directory.Packages.props");
        var docsEntryPath = Path.Combine(workspace.Root, "docs", "SalesClient.md");

        Assert.True(File.Exists(solutionPath), $"Expected generated solution at {solutionPath}.");
        Assert.True(File.Exists(directoryBuildPropsPath), $"Expected Directory.Build.props at {directoryBuildPropsPath}.");
        Assert.True(File.Exists(directoryPackagesPropsPath), $"Expected Directory.Packages.props at {directoryPackagesPropsPath}.");
        Assert.True(File.Exists(docsEntryPath), $"Expected docs entry at {docsEntryPath}.");

        var solution = File.ReadAllText(solutionPath);
        Assert.Contains("""<Project Path="src/SalesClient/SalesClient.csproj" />""", solution, StringComparison.Ordinal);
        Assert.Contains("""<Project Path="tests/SalesClient.Tests/SalesClient.Tests.csproj" />""", solution, StringComparison.Ordinal);

        var directoryBuildProps = File.ReadAllText(directoryBuildPropsPath);
        Assert.Contains("<Nullable>enable</Nullable>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains("<LangVersion>latest</LangVersion>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains(
            "<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>",
            File.ReadAllText(directoryPackagesPropsPath),
            StringComparison.Ordinal);

        var docsEntry = File.ReadAllText(docsEntryPath);
        Assert.Contains("Company.SalesClient", docsEntry, StringComparison.Ordinal);
        Assert.Contains("atomui city new app", docsEntry, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationTemplateGeneratedFilesDoNotContainAbsoluteWorkspacePaths()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        foreach (var file in Directory.EnumerateFiles(workspace.Root, "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain(workspace.Root, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ApplicationTemplateGeneratedSolutionCanRestoreBuildAndTest()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        var solutionPath = Path.Combine(workspace.Root, "SalesClient.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected generated solution at {solutionPath}.");

        var repositoryRoot = FindRepositoryRoot();
        var configuration = Environment.GetEnvironmentVariable("CONFIGURATION");
        if (string.IsNullOrWhiteSpace(configuration))
        {
            configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Name ?? "Debug";
        }

        var packageSource = Environment.GetEnvironmentVariable("ATOMUI_CITY_PACKAGE_SOURCE");
        if (string.IsNullOrWhiteSpace(packageSource))
        {
            packageSource = Path.Combine(repositoryRoot.FullName, "output", "NuGet", configuration);
        }
        Assert.True(Directory.Exists(packageSource), $"Expected local package source at {packageSource}.");
        var corePackagePath = Path.Combine(packageSource, "AtomUI.City.Core.1.0.0.nupkg");
        Assert.True(File.Exists(corePackagePath), $"Expected Core package at {corePackagePath}.");
        var presentationPackagePath = Path.Combine(packageSource, "AtomUI.City.Presentation.1.0.0.nupkg");
        Assert.True(
            File.Exists(presentationPackagePath),
            $"Expected Presentation package at {presentationPackagePath}.");
        var packageCacheKey = File.GetLastWriteTimeUtc(corePackagePath).Ticks.ToString(CultureInfo.InvariantCulture);
        var nugetPackagesPath = Path.Combine(
            Path.GetTempPath(),
            "AtomUICityTemplateSmokePackages",
            packageCacheKey);
        var globalPackagesSource = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(globalPackagesSource))
        {
            globalPackagesSource = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages");
        }
        var nugetConfigPath = Path.Combine(workspace.Root, "NuGet.Config");
        new XDocument(
            new XElement(
                "configuration",
                new XElement(
                    "packageSources",
                    new XElement("clear"),
                    new XElement("add", new XAttribute("key", "AtomUICityLocal"), new XAttribute("value", packageSource)),
                    new XElement("add", new XAttribute("key", "LocalPackageCache"), new XAttribute("value", globalPackagesSource)),
                    new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
                new XElement(
                    "packageSourceMapping",
                    new XElement(
                        "packageSource",
                        new XAttribute("key", "AtomUICityLocal"),
                        new XElement("package", new XAttribute("pattern", "AtomUI.City.*"))),
                    new XElement(
                        "packageSource",
                        new XAttribute("key", "LocalPackageCache"),
                        new XElement("package", new XAttribute("pattern", "*"))),
                    new XElement(
                        "packageSource",
                        new XAttribute("key", "nuget.org"),
                        new XElement("package", new XAttribute("pattern", "*"))))))
            .Save(nugetConfigPath);

        await RunDotnetAsync(
            workspace.Root,
            nugetPackagesPath,
            "restore",
            "SalesClient.slnx",
            "--configfile",
            nugetConfigPath,
            "--ignore-failed-sources");
        await RunDotnetAsync(workspace.Root, nugetPackagesPath, "build", "SalesClient.slnx", "--no-restore");
        await RunDotnetAsync(workspace.Root, nugetPackagesPath, "test", "SalesClient.slnx", "--no-build");
        await RunDotnetAsync(workspace.Root, nugetPackagesPath, "build-server", "shutdown");
    }

    [Fact]
    public void ApplicationTemplateOptionsExposeStableDefaults()
    {
        var options = new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = string.Empty,
            OutputPath = "out",
        };

        Assert.Equal("net10.0", options.TargetFramework);
        Assert.True(options.IncludeTests);
        Assert.False(options.IncludeSample);
        Assert.False(options.UseAot);
        Assert.False(options.UseDynamicPlugins);
        Assert.Equal("SalesClient", options.EffectiveRootNamespace);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void ApplicationTemplateRenderRejectsInvalidAppNameWithoutWritingFiles()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "Sales Client",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.Succeeded);
        Assert.Null(result.Plan);
        Assert.Equal("AUCTPL0001", diagnostic.Code);
        Assert.Equal("appName", diagnostic.Context["variable"]);
        Assert.Equal("Sales Client", diagnostic.Context["rawValue"]);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src")));
    }

    [Fact]
    public void ApplicationTemplateRenderRejectsFrameworkRootNamespace()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "AtomUI.City.SalesClient",
            OutputPath = workspace.Root,
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("AUCTPL0002", diagnostic.Code);
        Assert.Equal("rootNamespace", diagnostic.Context["variable"]);
        Assert.Equal("AtomUI.City.SalesClient", diagnostic.Context["rawValue"]);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src")));
    }

    [Fact]
    public void ApplicationTemplateRenderRejectsInvalidTargetFramework()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "../net10.0",
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("AUCTPL0001", diagnostic.Code);
        Assert.Equal("targetFramework", diagnostic.Context["variable"]);
        Assert.Equal("../net10.0", diagnostic.Context["rawValue"]);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src")));
    }

    [Fact]
    public void ApplicationTemplateRenderRejectsAotDynamicPluginConflict()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            UseAot = true,
            UseDynamicPlugins = true,
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("AUCTPL0301", diagnostic.Code);
        Assert.Equal("useDynamicPlugins", diagnostic.Context["variable"]);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src")));
    }

    [Fact]
    public void BlankRootNamespaceUsesAppNameForGeneratedNamespace()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = string.Empty,
            OutputPath = workspace.Root,
        });

        Assert.True(result.Succeeded);
        var project = File.ReadAllText(Path.Combine(workspace.Root, "src", "SalesClient", "SalesClient.csproj"));
        var program = File.ReadAllText(Path.Combine(workspace.Root, "src", "SalesClient", "Program.cs"));
        Assert.Contains("<RootNamespace>SalesClient</RootNamespace>", project, StringComparison.Ordinal);
        Assert.Contains("namespace SalesClient;", program, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationTemplateRenderObservesPreCancelledTokenBeforeWriting()
    {
        using var workspace = new TemplateSmokeWorkspace();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var renderer = new ApplicationTemplateRenderer();

        Assert.Throws<OperationCanceledException>(() => renderer.Render(
            new ApplicationTemplateOptions
            {
                AppName = "SalesClient",
                RootNamespace = "Company.SalesClient",
                OutputPath = workspace.Root,
                TargetFramework = "net10.0",
                IncludeTests = true,
            },
            cancellation.Token));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src", "SalesClient")));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "tests", "SalesClient.Tests")));
    }

    [Fact]
    public void ApplicationTemplatePlanAndAppliedPathsUseTheSameFileSet()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();
        var options = new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            IncludeTests = true,
            IncludeSample = true,
        };

        var plan = renderer.CreatePlan(options);
        var result = renderer.Render(options);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Plan);
        Assert.Equal(plan.Changes.Select(change => change.Path), result.AppliedPaths);
        Assert.Equal(plan.Changes.Select(change => change.Path), result.Plan.Changes.Select(change => change.Path));
        Assert.Equal(["src/SalesClient/SalesClient.csproj"], plan.BuildTargets);
        Assert.Equal(["tests/SalesClient.Tests/SalesClient.Tests.csproj"], plan.TestTargets);
        Assert.Equal(["docs/SalesClient.md"], plan.DocsRequired);
        Assert.Contains("src/SalesClient/Samples/WelcomeViewModel.cs", result.AppliedPaths);
    }

    [Fact]
    public void ApplicationTemplateRenderDoesNotOverwriteExistingFile()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var existingPath = Path.Combine(workspace.Root, "SalesClient.slnx");
        File.WriteAllText(existingPath, "existing");
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(CreateOptions(workspace.Root));

        Assert.False(result.Succeeded);
        Assert.Empty(result.AppliedPaths);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("AUCTPL1004", diagnostic.Code);
        Assert.Equal("SalesClient.slnx", diagnostic.Context["path"]);
        Assert.Equal("existing", File.ReadAllText(existingPath));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src")));
    }

    [Fact]
    public void ApplicationTemplateRenderRollsBackFilesWhenParentPathIsBlocked()
    {
        using var workspace = new TemplateSmokeWorkspace();
        File.WriteAllText(Path.Combine(workspace.Root, "src"), "blocked");
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(CreateOptions(workspace.Root));

        Assert.False(result.Succeeded);
        Assert.Empty(result.AppliedPaths);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("AUCTPL1005", diagnostic.Code);
        Assert.Equal("src/SalesClient/SalesClient.csproj", diagnostic.Context["path"]);
        Assert.False(File.Exists(Path.Combine(workspace.Root, "SalesClient.slnx")));
        Assert.False(File.Exists(Path.Combine(workspace.Root, "Directory.Build.props")));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "docs")));
        Assert.Equal("blocked", File.ReadAllText(Path.Combine(workspace.Root, "src")));
    }

    [Fact]
    public async Task ConcurrentRendersToSameTargetProduceOneCompleteResult()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();
        var options = CreateOptions(workspace.Root);

        var results = await Task.WhenAll(
            Task.Run(() => renderer.Render(options)),
            Task.Run(() => renderer.Render(options)));

        var success = Assert.Single(results, result => result.Succeeded);
        var conflict = Assert.Single(results, result => !result.Succeeded);
        Assert.Equal(success.Plan!.Changes.Count, success.AppliedPaths.Count);
        Assert.Equal("AUCTPL1004", Assert.Single(conflict.Diagnostics).Code);
        Assert.All(success.AppliedPaths, path => Assert.True(File.Exists(Path.Combine(workspace.Root, path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    [Theory]
    [InlineData("class")]
    [InlineData("9Client")]
    [InlineData("Sales-Client")]
    public void ApplicationTemplateRejectsInvalidCSharpIdentifiers(string appName)
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();
        var options = CreateOptions(workspace.Root, appName);

        var result = renderer.Render(options);

        Assert.False(result.Succeeded);
        Assert.Equal("AUCTPL0001", Assert.Single(result.Diagnostics).Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
        Assert.Throws<ArgumentException>(() => renderer.CreatePlan(options));
    }

    [Fact]
    public void FrameworkLikeButNonReservedNamespaceIsAccepted()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "Cityscape",
            RootNamespace = "AtomUI.Cityscape",
            OutputPath = workspace.Root,
        });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("net")]
    [InlineData("net10")]
    [InlineData("net10.x")]
    [InlineData("net10.0-")]
    [InlineData("net10.0-windows-")]
    public void ApplicationTemplateRejectsMalformedTargetFramework(string targetFramework)
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = targetFramework,
        });

        Assert.False(result.Succeeded);
        Assert.Equal("targetFramework", Assert.Single(result.Diagnostics).Context["variable"]);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    [Fact]
    public void FailedResultRequiresDiagnosticAndNeverReportsSuccess()
    {
        Assert.Throws<ArgumentException>(() => TemplateRenderResult.Failed());

        var result = TemplateRenderResult.Failed(new TemplateDiagnostic("AUCTPL9999", "Failure."));

        Assert.False(result.Succeeded);
        Assert.Null(result.Plan);
        Assert.Empty(result.AppliedPaths);
    }

    private static ApplicationTemplateOptions CreateOptions(string outputPath, string appName = "SalesClient")
    {
        return new ApplicationTemplateOptions
        {
            AppName = appName,
            RootNamespace = "Company.SalesClient",
            OutputPath = outputPath,
            TargetFramework = "net10.0",
            IncludeTests = true,
        };
    }

    private static async Task RunDotnetAsync(
        string workingDirectory,
        string nugetPackagesPath,
        params string[] arguments)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AVALONIA_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["NUGET_PACKAGES"] = nugetPackagesPath,
            ["NuGetAudit"] = "false",
            ["UseSharedCompilation"] = "false",
        };

        var result = await ProcessTestRunner.RunAsync(
            "dotnet",
            workingDirectory,
            TimeSpan.FromMinutes(5),
            environment,
            arguments);
        Assert.True(
            result.ExitCode == 0,
            $"""
            dotnet {string.Join(' ', arguments)} failed with exit code {result.ExitCode}.
            STDOUT:
            {result.StandardOutput}
            STDERR:
            {result.StandardError}
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

    private static string[] ReadPackageReferences(string projectPath)
    {
        return XDocument.Load(projectPath)
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();
    }

    private sealed class TemplateSmokeWorkspace : IDisposable
    {
        public TemplateSmokeWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "AtomUICityTemplateSmokeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            const int cleanupAttempts = 5;

            for (var attempt = 1; attempt <= cleanupAttempts; attempt++)
            {
                if (!Directory.Exists(Root))
                {
                    return;
                }

                try
                {
                    Directory.Delete(Root, recursive: true);
                    return;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException &&
                    attempt < cleanupAttempts)
                {
                    Thread.Sleep(200);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    Trace.WriteLine($"Could not fully remove template smoke workspace '{Root}': {exception.Message}");
                }
            }
        }
    }
}
