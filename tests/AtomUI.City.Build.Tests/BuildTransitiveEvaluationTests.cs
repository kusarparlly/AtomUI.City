using System.Xml.Linq;
using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Build.Tests;

public sealed class BuildTransitiveEvaluationTests
{
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
}
