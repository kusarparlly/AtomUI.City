using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using AtomUI.City.Testing.Processes;
using AtomUI.City.Templates;

namespace AtomUI.City.TemplateSmokeTests;

public sealed class ApplicationTemplateDesktopProcessTests
{
    private const uint SemNoGpFaultErrorBox = 0x0002;

    [Fact]
    [Trait("Category", "DesktopIntegration")]
    public async Task GeneratedWindowsApplicationOpensAndClosesThroughNativeDesktopLifetime()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DesktopTemplateWorkspace();
        var renderer = new ApplicationTemplateRenderer();
        var result = renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "DesktopTemplateProbe",
            RootNamespace = "Company.DesktopTemplateProbe",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = false,
        });
        Assert.True(result.Succeeded);

        var repositoryRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Name ?? "Debug";
        var packageSource = Environment.GetEnvironmentVariable("ATOMUI_CITY_PACKAGE_SOURCE");
        if (string.IsNullOrWhiteSpace(packageSource))
        {
            packageSource = Path.Combine(repositoryRoot.FullName, "output", "NuGet", configuration);
        }

        var presentationPackage = Path.Combine(packageSource, "AtomUI.City.Presentation.1.0.0.nupkg");
        Assert.True(File.Exists(presentationPackage), $"Expected Presentation package at {presentationPackage}.");
        var packageCacheKey = File.GetLastWriteTimeUtc(presentationPackage)
            .Ticks
            .ToString(CultureInfo.InvariantCulture);
        var nugetPackagesPath = Path.Combine(
            Path.GetTempPath(),
            "AtomUICityDesktopTemplatePackages",
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
                    new XElement(
                        "add",
                        new XAttribute("key", "AtomUICityLocal"),
                        new XAttribute("value", packageSource)),
                    new XElement(
                        "add",
                        new XAttribute("key", "LocalPackageCache"),
                        new XAttribute("value", globalPackagesSource)),
                    new XElement(
                        "add",
                        new XAttribute("key", "nuget.org"),
                        new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
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

        var projectPath = Path.Combine(
            workspace.Root,
            "src",
            "DesktopTemplateProbe",
            "DesktopTemplateProbe.csproj");
        await RunDotnetAsync(
            workspace.Root,
            nugetPackagesPath,
            "restore",
            projectPath,
            "--configfile",
            nugetConfigPath,
            "--ignore-failed-sources");
        await RunDotnetAsync(
            workspace.Root,
            nugetPackagesPath,
            "build",
            projectPath,
            "--no-restore");

        var executable = Path.Combine(
            workspace.Root,
            "src",
            "DesktopTemplateProbe",
            "bin",
            "Debug",
            "net10.0",
            "DesktopTemplateProbe.exe");
        Assert.True(File.Exists(executable), $"Expected generated desktop executable at {executable}.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                ErrorDialog = false,
            },
        };
        process.StartInfo.Environment["AVALONIA_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        var previousMode = GetErrorMode();
        try
        {
            SetErrorMode(previousMode | SemNoGpFaultErrorBox);
            Assert.True(process.Start());
        }
        finally
        {
            SetErrorMode(previousMode);
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        try
        {
            var windowDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (!process.HasExited && DateTimeOffset.UtcNow < windowDeadline)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    break;
                }

                await Task.Delay(50);
            }

            if (process.HasExited)
            {
                Assert.Fail(
                    $"Generated desktop process exited before opening a Window.{Environment.NewLine}{await standardError}");
            }
            Assert.NotEqual(IntPtr.Zero, process.MainWindowHandle);
            Assert.Equal("DesktopTemplateProbe", process.MainWindowTitle);
            Assert.True(process.CloseMainWindow(), "The generated main Window did not accept WM_CLOSE.");

            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(shutdownTimeout.Token);
            Assert.Equal(
                0,
                process.ExitCode);
            Assert.DoesNotContain(
                "Unhandled exception",
                await standardError,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            await standardOutput;
            await standardError;
        }
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

    [DllImport("kernel32.dll")]
    private static extern uint GetErrorMode();

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint errorMode);

    private sealed class DesktopTemplateWorkspace : IDisposable
    {
        public DesktopTemplateWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "AtomUICityDesktopTemplateTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            const int cleanupAttempts = 10;
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
                    Trace.WriteLine(
                        $"Could not fully remove desktop template workspace '{Root}': {exception.Message}");
                }
            }
        }
    }
}
