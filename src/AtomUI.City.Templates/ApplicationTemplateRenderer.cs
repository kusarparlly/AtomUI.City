using System.Text;

namespace AtomUI.City.Templates;

public sealed class ApplicationTemplateRenderer
{
    private const string AtomUICityPackageVersion = "1.0.0";
    private const string AvaloniaVersion = "12.0.4";
    private const string MicrosoftNetTestSdkVersion = "17.14.1";
    private const string XUnitVersion = "2.9.3";
    private const string XUnitRunnerVisualStudioVersion = "3.1.4";
    private static readonly object RenderGatesSyncRoot = new();
    private static readonly Dictionary<string, RenderGate> RenderGates = new(GetPathComparer());

    public TemplatePlan CreatePlan(ApplicationTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = options.Validate();
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Template options are invalid: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Code))}.",
                nameof(options));
        }

        var files = CreateFiles(options);
        return CreatePlan(options, files);
    }

    private static TemplatePlan CreatePlan(
        ApplicationTemplateOptions options,
        IReadOnlyList<TemplateFile> files)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        var testTargets = options.IncludeTests
            ? new[] { $"tests/{options.AppName}.Tests/{options.AppName}.Tests.csproj" }
            : [];

        return new TemplatePlan(
            operationId: $"new-app-{options.AppName}",
            command: "atomui city new app",
            inputs: new Dictionary<string, object?>
            {
                ["appName"] = options.AppName,
                ["rootNamespace"] = rootNamespace,
                ["targetFramework"] = options.TargetFramework,
                ["includeTests"] = options.IncludeTests,
                ["useAot"] = options.UseAot,
                ["useDynamicPlugins"] = options.UseDynamicPlugins,
                ["includeSample"] = options.IncludeSample,
            },
            changes: files.Select(static file => file.Change).ToArray(),
            buildTargets: [$"src/{options.AppName}/{options.AppName}.csproj"],
            testTargets: testTargets,
            docsRequired: [$"docs/{options.AppName}.md"],
            risks: options.UseDynamicPlugins ? ["dynamic-plugin-runtime"] : [],
            rollback: files.Select(static file => file.Change.Path).Reverse().ToArray());
    }

    public TemplateRenderResult Render(ApplicationTemplateOptions options)
    {
        return Render(options, CancellationToken.None);
    }

    public TemplateRenderResult Render(ApplicationTemplateOptions options, CancellationToken cancellationToken)
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
                                $"Template output preflight failed: {exception.GetType().Name}.",
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
                                "Template output already exists.",
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
                    foreach (var file in files)
                    {
                        currentRelativePath = file.Change.Path;
                        cancellationToken.ThrowIfCancellationRequested();
                        var destination = ResolvePath(rootPath, file.Change.Path);
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
                    Rollback(createdFiles, createdDirectories);
                    throw;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    var rollbackFailures = Rollback(createdFiles, createdDirectories);
                    var diagnostics = new List<TemplateDiagnostic>
                    {
                        CreateOutputDiagnostic(
                            "AUCTPL1005",
                            $"Template output failed: {exception.GetType().Name}.",
                            options,
                            currentRelativePath,
                            rootPath,
                            exception),
                    };
                    diagnostics.AddRange(rollbackFailures);
                    return TemplateRenderResult.Failed(plan, [.. diagnostics]);
                }
            }
        }
        finally
        {
            ReleaseRenderGate(rootPath, gate);
        }
    }

    private static IReadOnlyList<TemplateFile> CreateFiles(ApplicationTemplateOptions options)
    {
        var files = new List<TemplateFile>
        {
            CreateFile($"{options.AppName}.slnx", CreateSolution(options)),
            CreateFile("Directory.Build.props", CreateDirectoryBuildProps()),
            CreateFile("Directory.Packages.props", CreateDirectoryPackagesProps()),
            CreateFile($"docs/{options.AppName}.md", CreateDocsEntry(options)),
            CreateFile($"src/{options.AppName}/{options.AppName}.csproj", CreateApplicationProject(options)),
            CreateFile($"src/{options.AppName}/Program.cs", CreateProgram(options)),
            CreateFile($"src/{options.AppName}/App.axaml", CreateApplicationXaml(options)),
            CreateFile($"src/{options.AppName}/App.axaml.cs", CreateApplicationCodeBehind(options)),
            CreateFile($"src/{options.AppName}/DesktopBootstrap.cs", CreateDesktopBootstrap(options)),
            CreateFile($"src/{options.AppName}/MainWindow.axaml", CreateMainWindowXaml(options)),
            CreateFile($"src/{options.AppName}/MainWindow.axaml.cs", CreateMainWindowCodeBehind(options)),
            CreateFile($"src/{options.AppName}/Properties/AssemblyInfo.cs", CreateAssemblyInfo(options)),
            CreateFile($"src/{options.AppName}/Modules/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Routes/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Resources/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Configuration/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Localization/.gitkeep", string.Empty),
        };

        if (options.IncludeTests)
        {
            files.Add(CreateFile(
                $"tests/{options.AppName}.Tests/{options.AppName}.Tests.csproj",
                CreateTestProject(options)));
            files.Add(CreateFile(
                $"tests/{options.AppName}.Tests/FeatureTestMatrix.md",
                CreateFeatureTestMatrix(options)));
            files.Add(CreateFile(
                $"tests/{options.AppName}.Tests/ApplicationSmokeTests.cs",
                CreateApplicationSmokeTests(options)));
        }

        if (options.IncludeSample)
        {
            files.Add(CreateFile(
                $"src/{options.AppName}/Samples/WelcomeViewModel.cs",
                CreateWelcomeViewModel(options)));
        }

        return files.AsReadOnly();
    }

    private static TemplateFile CreateFile(string path, string content)
    {
        return new TemplateFile(TemplateChange.Create(path), content);
    }

    private static string ResolvePath(string rootPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine([rootPath, .. relativePath.Split('/')]));
        var rootPrefix = Path.TrimEndingDirectorySeparator(rootPath) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, GetPathComparison()))
        {
            throw new IOException("Template output escaped its target root.");
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
             current = Path.GetDirectoryName(current) ?? throw new IOException("Template directory has no parent."))
        {
            if (File.Exists(current))
            {
                throw new IOException("A file blocks a template output directory.");
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
                throw new IOException("A file blocks a template output directory.");
            }

            if (!Directory.Exists(current))
            {
                continue;
            }

            var directory = new DirectoryInfo(current);
            if (directory.LinkTarget is not null ||
                directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException("Template output cannot traverse a symbolic link or reparse point.");
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
        IReadOnlyList<string> createdDirectories)
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
                diagnostics.Add(CreateRollbackDiagnostic(path, exception));
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
                diagnostics.Add(CreateRollbackDiagnostic(path, exception));
            }
        }

        return diagnostics;
    }

    private static TemplateDiagnostic CreateRollbackDiagnostic(string path, Exception exception)
    {
        return new TemplateDiagnostic(
            "AUCTPL1006",
            $"Template rollback failed: {exception.GetType().Name}.",
            new Dictionary<string, object?>
            {
                ["templateId"] = "atomui-city-app",
                ["path"] = path,
                ["errorType"] = exception.GetType().FullName,
            });
    }

    private static TemplateDiagnostic CreateOutputDiagnostic(
        string code,
        string message,
        ApplicationTemplateOptions options,
        string? relativePath,
        string targetPath,
        Exception? exception = null)
    {
        return new TemplateDiagnostic(
            code,
            message,
            new Dictionary<string, object?>
            {
                ["templateId"] = "atomui-city-app",
                ["targetPath"] = targetPath,
                ["path"] = relativePath,
                ["operationId"] = $"new-app-{options.AppName}",
                ["errorType"] = exception?.GetType().FullName,
            });
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
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

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    private static string CreateSolution(ApplicationTemplateOptions options)
    {
        var testProject = options.IncludeTests
            ? $$"""
                  <Folder Name="/tests/">
                    <Project Path="tests/{{options.AppName}}.Tests/{{options.AppName}}.Tests.csproj" />
                  </Folder>
            """
            : string.Empty;

        return $$"""
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/{{options.AppName}}/{{options.AppName}}.csproj" />
              </Folder>
            {{testProject}}
            </Solution>
            """;
    }

    private static string CreateDirectoryBuildProps()
    {
        return """
            <Project>

              <PropertyGroup>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <LangVersion>latest</LangVersion>
                <WarningsAsErrors>Nullable</WarningsAsErrors>
              </PropertyGroup>

            </Project>
            """;
    }

    private static string CreateDirectoryPackagesProps()
    {
        return """
            <Project>

              <PropertyGroup>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>

            </Project>
            """;
    }

    private static string CreateDocsEntry(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            # {{options.AppName}}

            Generated by `atomui city new app`.

            | Field | Value |
            | --- | --- |
            | Root namespace | `{{rootNamespace}}` |
            | Target framework | `{{options.TargetFramework}}` |
            | Tests included | `{{options.IncludeTests.ToString().ToLowerInvariant()}}` |

            ## Restore, Build, Test

            ```bash
            dotnet restore {{options.AppName}}.slnx
            dotnet build {{options.AppName}}.slnx --no-restore
            dotnet test {{options.AppName}}.slnx --no-build
            ```

            Run the desktop application with:

            ```bash
            dotnet run --project src/{{options.AppName}}/{{options.AppName}}.csproj
            ```
            """;
    }

    private static string CreateApplicationProject(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        var dynamicPlugins = options.UseDynamicPlugins
            ? """
                <PackageReference Include="AtomUI.City.PluginSystem" Version="{{AtomUICityPackageVersion}}" />
            """
            : string.Empty;

        return $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>{{options.TargetFramework}}</TargetFramework>
                <RootNamespace>{{rootNamespace}}</RootNamespace>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <AtomUICityManifestGeneration>true</AtomUICityManifestGeneration>
                <AtomUICityAotFriendly>{{options.UseAot.ToString().ToLowerInvariant()}}</AtomUICityAotFriendly>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="AtomUI.City.Build" Version="{{AtomUICityPackageVersion}}" PrivateAssets="all" />
                <PackageReference Include="AtomUI.City.Core" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="AtomUI.City.Mvvm" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="AtomUI.City.Routing" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="AtomUI.City.Localization" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="AtomUI.City.Presentation" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="Avalonia.Desktop" Version="{{AvaloniaVersion}}" />
                <PackageReference Include="Avalonia.Themes.Fluent" Version="{{AvaloniaVersion}}" />
            {{dynamicPlugins}}
              </ItemGroup>

            </Project>
            """;
    }

    private static string CreateProgram(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            using Avalonia;
            using Avalonia.Controls;
            using Avalonia.Controls.ApplicationLifetimes;
            using AtomUI.City.Core.Hosting;
            using AtomUI.City.Core.Modularity;
            using AtomUI.City.Presentation;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{rootNamespace}};

            internal static class Program
            {
                [STAThread]
                public static int Main(string[] args)
                {
                    try
                    {
                        return Run(args);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine(exception);
                        return 1;
                    }
                }

                internal static IApplicationHost CreateHost(string[] args)
                {
                    var builder = ApplicationHost.CreateBuilder(args);
                    builder.ConfigureHost(options =>
                    {
                        options.ApplicationId = "{{rootNamespace}}";
                        options.ApplicationName = "{{options.AppName}}";
                    });
                    builder.UseModule<PresentationModule>();
                    builder.ConfigureServices(services => services.AddSingleton<MainWindow>());

                    return builder.Build();
                }

                internal static AppBuilder BuildAvaloniaApp() =>
                    AppBuilder.Configure<App>()
                        .UsePlatformDetect();

                private static int Run(string[] args)
                {
                    using var host = CreateHost(args);
                    host.StartAsync().GetAwaiter().GetResult();

                    var attached = false;
                    try
                    {
                        DesktopBootstrap.Attach(host);
                        attached = true;
                        return BuildAvaloniaApp()
                            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
                    }
                    finally
                    {
                        if (attached)
                        {
                            DesktopBootstrap.Detach(host);
                        }

                        Task.Run(async () => await host.StopAsync().ConfigureAwait(false))
                            .GetAwaiter()
                            .GetResult();
                    }
                }
            }
            """;
    }

    private static string CreateApplicationXaml(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        return $$"""
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="{{rootNamespace}}.App"
                         RequestedThemeVariant="Default">
              <Application.Styles>
                <FluentTheme />
              </Application.Styles>
            </Application>
            """;
    }

    private static string CreateApplicationCodeBehind(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        return $$"""
            using Avalonia;
            using Avalonia.Controls;
            using Avalonia.Controls.ApplicationLifetimes;
            using Avalonia.Markup.Xaml;

            namespace {{rootNamespace}};

            public sealed partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }

                public override void OnFrameworkInitializationCompleted()
                {
                    if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        throw new InvalidOperationException(
                            "This application requires the Avalonia classic desktop lifetime.");
                    }

                    DesktopBootstrap.Initialize(desktopLifetime);
                    base.OnFrameworkInitializationCompleted();
                }
            }
            """;
    }

    private static string CreateDesktopBootstrap(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        return $$"""
            using Avalonia.Controls.ApplicationLifetimes;
            using Avalonia.Threading;
            using AtomUI.City.Core.Hosting;
            using AtomUI.City.Presentation;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{rootNamespace}};

            internal static class DesktopBootstrap
            {
                private static IApplicationHost? _host;

                internal static void Attach(IApplicationHost host)
                {
                    ArgumentNullException.ThrowIfNull(host);
                    if (Interlocked.CompareExchange(ref _host, host, null) is not null)
                    {
                        throw new InvalidOperationException("A desktop application Host is already attached.");
                    }
                }

                internal static void Initialize(IClassicDesktopStyleApplicationLifetime lifetime)
                {
                    ArgumentNullException.ThrowIfNull(lifetime);
                    var host = Volatile.Read(ref _host)
                        ?? throw new InvalidOperationException("The desktop application Host is not attached.");
                    if (lifetime.MainWindow is not null)
                    {
                        throw new InvalidOperationException("The desktop lifetime already has a main window.");
                    }

                    var runtime = host.Services.GetRequiredService<IPresentationRuntime>();
                    runtime.Attach(lifetime, host.HostScope);
                    var mainWindow = host.Services.GetRequiredService<MainWindow>();
                    var session = runtime.RegisterWindow(mainWindow, "main");
                    mainWindow.Closed += (_, _) => _ = ShutdownWhenSessionCompletesAsync(session, lifetime);
                    lifetime.MainWindow = mainWindow;
                }

                private static async Task ShutdownWhenSessionCompletesAsync(
                    WindowSession session,
                    IClassicDesktopStyleApplicationLifetime lifetime)
                {
                    while (session.State is not (WindowSessionState.Closed or WindowSessionState.Faulted))
                    {
                        await Task.Delay(10).ConfigureAwait(false);
                    }

                    var exitCode = session.State == WindowSessionState.Closed ? 0 : 1;
                    Dispatcher.UIThread.Post(() => lifetime.Shutdown(exitCode));
                }

                internal static void Detach(IApplicationHost host)
                {
                    ArgumentNullException.ThrowIfNull(host);
                    if (!ReferenceEquals(Interlocked.CompareExchange(ref _host, null, host), host))
                    {
                        throw new InvalidOperationException("The supplied desktop application Host is not attached.");
                    }
                }
            }
            """;
    }

    private static string CreateMainWindowXaml(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        return $$"""
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:presentation="clr-namespace:AtomUI.City.Presentation;assembly=AtomUI.City.Presentation"
                    x:Class="{{rootNamespace}}.MainWindow"
                    Title="{{options.AppName}}"
                    Width="960"
                    Height="640"
                    MinWidth="640"
                    MinHeight="420">
              <Grid Margin="24">
                <ContentControl presentation:RouteOutletProperties.Name="main">
                  <StackPanel HorizontalAlignment="Center"
                              VerticalAlignment="Center"
                              Spacing="8">
                    <TextBlock Text="{{options.AppName}}"
                               FontSize="28"
                               FontWeight="SemiBold"
                               HorizontalAlignment="Center" />
                    <TextBlock Text="Ready"
                               Opacity="0.7"
                               HorizontalAlignment="Center" />
                  </StackPanel>
                </ContentControl>
              </Grid>
            </Window>
            """;
    }


    private static string CreateMainWindowCodeBehind(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        return $$"""
            using Avalonia.Controls;
            using Avalonia.Markup.Xaml;

            namespace {{rootNamespace}};

            public sealed partial class MainWindow : Window
            {
                public MainWindow()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """;
    }

    private static string CreateAssemblyInfo(ApplicationTemplateOptions options)
    {
        return $$"""
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo("{{options.AppName}}.Tests")]
            """;
    }

    private static string CreateTestProject(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{{options.TargetFramework}}</TargetFramework>
                <RootNamespace>{{rootNamespace}}.Tests</RootNamespace>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="{{MicrosoftNetTestSdkVersion}}" />
                <PackageReference Include="Avalonia.Headless" Version="{{AvaloniaVersion}}" />
                <PackageReference Include="xunit" Version="{{XUnitVersion}}" />
                <PackageReference Include="xunit.runner.visualstudio" Version="{{XUnitRunnerVisualStudioVersion}}" PrivateAssets="all" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="../../src/{{options.AppName}}/{{options.AppName}}.csproj" />
              </ItemGroup>

              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>

            </Project>
            """;
    }

    private static string CreateFeatureTestMatrix(ApplicationTemplateOptions options)
    {
        return $$"""
            # {{options.AppName}} Feature Test Matrix

            | Feature | Unit Tests | Integration Tests | Notes |
            |---|---|---|---|
            | City Host lifecycle | ApplicationSmokeTests | Host start/stop | Generated by AtomUI.City template. |
            | Avalonia desktop bootstrap | ApplicationSmokeTests | Headless Presentation attach and WindowSession registration | Generated by AtomUI.City template. |
            """;
    }

    private static string CreateApplicationSmokeTests(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            using Avalonia;
            using Avalonia.Controls;
            using Avalonia.Controls.ApplicationLifetimes;
            using Avalonia.Headless;
            using Avalonia.Threading;
            using AtomUI.City.Core.Lifecycle;
            using AtomUI.City.Presentation;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{rootNamespace}}.Tests;

            public sealed class ApplicationSmokeTests
            {
                [Fact]
                public async Task ApplicationHostStartsAndStops()
                {
                    using var host = Program.CreateHost([]);
                    await host.StartAsync();
                    Assert.Equal(LifecycleScopeState.Running, host.HostScope.State);

                    await host.StopAsync();
                    Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
                }

                [Fact]
                public async Task DesktopBootstrapAttachesPresentationAndRegistersMainWindow()
                {
                    using var host = Program.CreateHost([]);
                    await host.StartAsync();
                    AppBuilder.Configure<Application>()
                        .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                        .SetupWithoutStarting();
                    var lifetime = new ClassicDesktopStyleApplicationLifetime
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown,
                    };

                    Assert.Throws<InvalidOperationException>(() => DesktopBootstrap.Initialize(lifetime));
                    using var duplicateHost = Program.CreateHost([]);
                    DesktopBootstrap.Attach(host);
                    try
                    {
                        Assert.Throws<InvalidOperationException>(() => DesktopBootstrap.Attach(duplicateHost));
                        DesktopBootstrap.Initialize(lifetime);
                        var runtime = host.Services.GetRequiredService<IPresentationRuntime>();
                        Assert.True(runtime.IsReady);
                        Assert.IsType<MainWindow>(lifetime.MainWindow);
                        Assert.Equal("main", Assert.Single(runtime.Windows).Id);
                    }
                    finally
                    {
                        DesktopBootstrap.Detach(host);
                        PumpUntil(host.StopAsync());
                    }

                    Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
                }

                private static void PumpUntil(Task task)
                {
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
                    while (!task.IsCompleted && DateTimeOffset.UtcNow < deadline)
                    {
                        Dispatcher.UIThread.RunJobs();
                        Thread.Yield();
                    }

                    task.WaitAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
                    Dispatcher.UIThread.RunJobs();
                }
            }
            """;
    }

    private static string CreateWelcomeViewModel(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        return $$"""
            using AtomUI.City.Mvvm;

            namespace {{rootNamespace}}.Samples;

            public sealed class WelcomeViewModel : ViewModelBase
            {
                private string _message = "AtomUI.City";

                public string Message
                {
                    get => _message;
                    set => SetProperty(ref _message, value);
                }
            }
            """;
    }

    private sealed record TemplateFile(TemplateChange Change, string Content);

    private sealed class RenderGate
    {
        public object SyncRoot { get; } = new();

        public int ReferenceCount { get; set; }
    }
}
