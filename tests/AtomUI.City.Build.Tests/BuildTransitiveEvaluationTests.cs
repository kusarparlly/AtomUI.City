using System.Xml.Linq;
using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Build.Tests;

public sealed class BuildTransitiveEvaluationTests
{
    [Fact]
    public async Task StrictAotFailsClosedRejectsProjectSuppressionAndLeavesToolingUnchanged()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var buildTransitiveRoot = Path.Combine(
            repositoryRoot,
            "src",
            "AtomUI.City.Build",
            "buildTransitive");
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "AtomUI.City.Build.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var tasksAssembly = Path.Combine(temporaryRoot, "AtomUI.City.Build.Tasks.dll");
            await File.WriteAllBytesAsync(tasksAssembly, []);
            await File.WriteAllTextAsync(
                Path.Combine(temporaryRoot, "Program.cs"),
                """
                using System.Diagnostics.CodeAnalysis;

                internal static class Program
                {
                    [RequiresUnreferencedCode("strict-aot-sentinel")]
                    private static void Risk() { }

                    private static void Main() => Risk();
                }
                """);

            var runtimeProject = CreateStrictAotProject(
                buildTransitiveRoot,
                tasksAssembly,
                runtimeProject: true,
                noWarn: null);
            var runtimePath = Path.Combine(temporaryRoot, "Runtime.csproj");
            runtimeProject.Save(runtimePath);

            var runtimeResult = await ProcessTestRunner.RunAsync(
                "dotnet",
                temporaryRoot,
                TimeSpan.FromSeconds(60),
                "build",
                runtimePath,
                "--nologo",
                "-v:minimal");

            Assert.NotEqual(0, runtimeResult.ExitCode);
            Assert.Contains(
                "IL2026",
                runtimeResult.StandardOutput + runtimeResult.StandardError,
                StringComparison.Ordinal);

            var suppressedProject = CreateStrictAotProject(
                buildTransitiveRoot,
                tasksAssembly,
                runtimeProject: true,
                noWarn: "IL2026");
            var suppressedPath = Path.Combine(temporaryRoot, "Suppressed.csproj");
            suppressedProject.Save(suppressedPath);

            var suppressedResult = await ProcessTestRunner.RunAsync(
                "dotnet",
                temporaryRoot,
                TimeSpan.FromSeconds(60),
                "build",
                suppressedPath,
                "--nologo",
                "-v:minimal");

            Assert.NotEqual(0, suppressedResult.ExitCode);
            Assert.Contains(
                "AUCBLD0302",
                suppressedResult.StandardOutput + suppressedResult.StandardError,
                StringComparison.Ordinal);

            var toolingProject = CreateStrictAotProject(
                buildTransitiveRoot,
                tasksAssembly,
                runtimeProject: false,
                noWarn: null);
            var toolingPath = Path.Combine(temporaryRoot, "Tooling.csproj");
            toolingProject.Save(toolingPath);

            var toolingResult = await ProcessTestRunner.RunAsync(
                "dotnet",
                temporaryRoot,
                TimeSpan.FromSeconds(60),
                "msbuild",
                toolingPath,
                "-getProperty:IsAotCompatible,EnableAotAnalyzer,TreatWarningsAsErrors");

            Assert.Equal(0, toolingResult.ExitCode);
            Assert.DoesNotContain("true", toolingResult.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BuildTargetsSupportEvaluationOnlyPropertyAndItemQueries()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var buildTransitiveRoot = Path.Combine(
            repositoryRoot,
            "src",
            "AtomUI.City.Build",
            "buildTransitive");
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "AtomUI.City.Build.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var projectPath = Path.Combine(temporaryRoot, "EvaluationConsumer.csproj");
            var project = new XDocument(
                new XElement(
                    "Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement(
                        "Import",
                        new XAttribute(
                            "Project",
                            Path.Combine(buildTransitiveRoot, "AtomUI.City.Build.props"))),
                    new XElement(
                        "PropertyGroup",
                        new XElement("TargetFramework", "net10.0")),
                    new XElement(
                        "ItemGroup",
                        new XElement(
                            "Analyzer",
                            new XAttribute(
                                "Include",
                                Path.Combine(temporaryRoot, "AtomUI.City.Generators.dll"))),
                        new XElement(
                            "Analyzer",
                            new XAttribute(
                                "Include",
                                Path.Combine(temporaryRoot, "Sentinel.Analyzer.dll")))),
                    new XElement(
                        "Import",
                        new XAttribute(
                            "Project",
                            Path.Combine(buildTransitiveRoot, "AtomUI.City.Build.targets")))));
            project.Save(projectPath);

            var result = await ProcessTestRunner.RunAsync(
                "dotnet",
                temporaryRoot,
                TimeSpan.FromSeconds(30),
                "msbuild",
                projectPath,
                "-p:AtomUICityEnableAnalyzers=false",
                "-getProperty:AtomUICityEnableAnalyzers",
                "-getItem:Analyzer");

            Assert.True(
                result.ExitCode == 0,
                $"MSBuild evaluation failed.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{result.StandardError}");
            Assert.DoesNotContain("MSB4190", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("MSB4190", result.StandardError, StringComparison.Ordinal);
            Assert.Contains("Sentinel.Analyzer.dll", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("AtomUI.City.Generators.dll", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static XDocument CreateStrictAotProject(
        string buildTransitiveRoot,
        string tasksAssembly,
        bool runtimeProject,
        string? noWarn)
    {
        return new XDocument(
            new XElement(
                "Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement(
                    "Import",
                    new XAttribute(
                        "Project",
                        Path.Combine(buildTransitiveRoot, "AtomUI.City.Build.props"))),
                new XElement(
                    "PropertyGroup",
                    new XElement("OutputType", "Exe"),
                    new XElement("TargetFramework", "net10.0"),
                    new XElement("BaseIntermediateOutputPath", "obj/$(MSBuildProjectName)/"),
                    new XElement("OutputPath", "bin/$(MSBuildProjectName)/"),
                    new XElement("AtomUICityStrictAot", "true"),
                    new XElement("AtomUICityRuntimeProject", runtimeProject ? "true" : "false"),
                    new XElement("AtomUICityBuildTasksAssembly", tasksAssembly),
                    new XElement("AtomUICityCompileResx", "false"),
                    new XElement("AtomUICityGenerateManifests", "false"),
                    new XElement("AtomUICityValidateManifests", "false"),
                    noWarn is null ? null : new XElement("NoWarn", noWarn)),
                new XElement(
                    "Import",
                    new XAttribute(
                        "Project",
                        Path.Combine(buildTransitiveRoot, "AtomUI.City.Build.targets")))));
    }
}
