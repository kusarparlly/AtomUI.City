using System.Diagnostics;
using System.IO.Compression;
using AtomUI.City.Testing.Processes;

namespace AtomUI.City.TemplateSmokeTests;

public sealed class DotnetNewTemplateIntegrationTests
{
    [Fact]
    public async Task PackedTemplatesInstallAndInstantiateWithoutTokenCorruption()
    {
        using var workspace = new IntegrationWorkspace();
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Name ?? "Debug";
        var templateProject = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "AtomUI.City.Templates",
            "AtomUI.City.Templates.csproj");

        await RunDotnetAsync(
            repositoryRoot.FullName,
            workspace.DotnetHome,
            "pack",
            templateProject,
            "--configuration",
            configuration,
            "--no-build",
            "--no-restore",
            "--output",
            workspace.PackageRoot);

        var packagePath = Assert.Single(Directory.EnumerateFiles(
            workspace.PackageRoot,
            "AtomUI.City.Templates.*.nupkg",
            SearchOption.TopDirectoryOnly));
        using (var package = ZipFile.OpenRead(packagePath))
        {
            Assert.Contains(
                package.Entries,
                entry => entry.FullName.Equals(
                    $"lib/net10.0/AtomUI.City.Templates.dll",
                    StringComparison.Ordinal));
        }

        await RunDotnetAsync(
            repositoryRoot.FullName,
            workspace.DotnetHome,
            "new",
            "install",
            packagePath,
            "--debug:custom-hive",
            workspace.HiveRoot);
        await RunDotnetAsync(
            repositoryRoot.FullName,
            workspace.DotnetHome,
            "new",
            "atomui-city-app",
            "--name",
            "SalesDesk",
            "--output",
            workspace.ApplicationRoot,
            "--TargetFramework",
            "net10.0",
            "--IncludeSample",
            "true",
            "--debug:custom-hive",
            workspace.HiveRoot);
        await RunDotnetAsync(
            repositoryRoot.FullName,
            workspace.DotnetHome,
            "new",
            "atomui-city-app",
            "--name",
            "MinimalDesk",
            "--output",
            workspace.MinimalApplicationRoot,
            "--debug:custom-hive",
            workspace.HiveRoot);
        await RunDotnetAsync(
            repositoryRoot.FullName,
            workspace.DotnetHome,
            "new",
            "atomui-city-plugin",
            "--name",
            "EnginePlugin",
            "--output",
            workspace.PluginRoot,
            "--PluginId",
            "com.company.engine",
            "--TargetFramework",
            "net10.0",
            "--debug:custom-hive",
            workspace.HiveRoot);

        Assert.True(File.Exists(Path.Combine(workspace.ApplicationRoot, "SalesDesk.slnx")));
        Assert.True(File.Exists(Path.Combine(workspace.ApplicationRoot, "src", "SalesDesk", "App.axaml")));
        Assert.True(File.Exists(Path.Combine(workspace.ApplicationRoot, "src", "SalesDesk", "App.axaml.cs")));
        Assert.True(File.Exists(Path.Combine(workspace.ApplicationRoot, "src", "SalesDesk", "DesktopBootstrap.cs")));
        Assert.True(File.Exists(Path.Combine(workspace.ApplicationRoot, "src", "SalesDesk", "MainWindow.axaml")));
        Assert.True(File.Exists(Path.Combine(workspace.ApplicationRoot, "src", "SalesDesk", "MainWindow.axaml.cs")));
        var generatedProgram = File.ReadAllText(Path.Combine(
            workspace.ApplicationRoot,
            "src",
            "SalesDesk",
            "Program.cs"));
        var generatedBootstrap = File.ReadAllText(Path.Combine(
            workspace.ApplicationRoot,
            "src",
            "SalesDesk",
            "DesktopBootstrap.cs"));
        var generatedApplication = File.ReadAllText(Path.Combine(
            workspace.ApplicationRoot,
            "src",
            "SalesDesk",
            "App.axaml"));
        var generatedMainWindow = File.ReadAllText(Path.Combine(
            workspace.ApplicationRoot,
            "src",
            "SalesDesk",
            "MainWindow.axaml"));
        Assert.Contains("UseModule<PresentationModule>()", generatedProgram, StringComparison.Ordinal);
        Assert.Contains("ShutdownMode.OnExplicitShutdown", generatedProgram, StringComparison.Ordinal);
        Assert.Contains("runtime.Attach(lifetime, host.HostScope)", generatedBootstrap, StringComparison.Ordinal);
        Assert.Contains("WindowSessionState.Closed", generatedBootstrap, StringComparison.Ordinal);
        Assert.Contains("x:Class=\"SalesDesk.App\"", generatedApplication, StringComparison.Ordinal);
        Assert.Contains("x:Class=\"SalesDesk.MainWindow\"", generatedMainWindow, StringComparison.Ordinal);
        Assert.Contains("Title=\"SalesDesk\"", generatedMainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("AtomUICityApplication", generatedProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AtomUICityApplication", generatedApplication, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            workspace.ApplicationRoot,
            "src",
            "SalesDesk",
            "Samples",
            "WelcomeViewModel.cs")));
        Assert.False(Directory.Exists(Path.Combine(
            workspace.MinimalApplicationRoot,
            "src",
            "MinimalDesk",
            "Samples")));

        var pluginProjectPath = Path.Combine(
            workspace.PluginRoot,
            "src",
            "EnginePlugin",
            "EnginePlugin.csproj");
        var manifestPath = Path.Combine(
            workspace.PluginRoot,
            "src",
            "EnginePlugin",
            "atomui-city",
            "plugin.json");
        var pluginProject = File.ReadAllText(pluginProjectPath);
        var manifest = File.ReadAllText(manifestPath);

        Assert.Contains("<AtomUICityPlugin>true</AtomUICityPlugin>", pluginProject, StringComparison.Ordinal);
        Assert.Contains("<AtomUICityPluginId>com.company.engine</AtomUICityPluginId>", pluginProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<EnginePlugin", pluginProject, StringComparison.Ordinal);
        Assert.Contains("\"pluginId\": \"com.company.engine\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"EnginePlugin.EnginePluginModule\"", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("__PLUGIN_ID__", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("__TARGET_FRAMEWORK__", manifest, StringComparison.Ordinal);
    }

    private static async Task RunDotnetAsync(
        string workingDirectory,
        string dotnetHome,
        params string[] arguments)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DOTNET_CLI_HOME"] = dotnetHome,
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
            ["MSBUILDDISABLENODEREUSE"] = "1",
        };

        var result = await ProcessTestRunner.RunAsync(
            "dotnet",
            workingDirectory,
            TimeSpan.FromMinutes(2),
            environment,
            arguments);
        Assert.True(
            result.ExitCode == 0,
            $"dotnet {string.Join(' ', arguments)} failed.\nSTDOUT:\n{result.StandardOutput}\nSTDERR:\n{result.StandardError}");
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

    private sealed class IntegrationWorkspace : IDisposable
    {
        public IntegrationWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "AtomUICityDotnetNewTests", Guid.NewGuid().ToString("N"));
            PackageRoot = Path.Combine(Root, "packages");
            HiveRoot = Path.Combine(Root, "hive");
            DotnetHome = Path.Combine(Root, "dotnet-home");
            ApplicationRoot = Path.Combine(Root, "app");
            MinimalApplicationRoot = Path.Combine(Root, "minimal-app");
            PluginRoot = Path.Combine(Root, "plugin");
            Directory.CreateDirectory(PackageRoot);
            Directory.CreateDirectory(DotnetHome);
        }

        public string Root { get; }

        public string PackageRoot { get; }

        public string HiveRoot { get; }

        public string DotnetHome { get; }

        public string ApplicationRoot { get; }

        public string MinimalApplicationRoot { get; }

        public string PluginRoot { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
