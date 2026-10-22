using System.Xml.Linq;

namespace AtomUI.City.Cli.Tests;

public sealed class CliToolPackageTests
{
    [Fact]
    public void CliProjectDeclaresDotnetToolContractWithoutBuildRuntimeReference()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "AtomUI.City.Cli", "AtomUI.City.Cli.csproj");
        var document = XDocument.Load(projectPath);
        var properties = document.Descendants("PropertyGroup").Elements().ToDictionary(
            static element => element.Name.LocalName,
            static element => element.Value,
            StringComparer.Ordinal);
        var references = document.Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => value is not null)
            .ToArray();

        Assert.Equal("true", properties["PackAsTool"]);
        Assert.Equal("atomui", properties["ToolCommandName"]);
        Assert.Equal("Exe", properties["OutputType"]);
        Assert.DoesNotContain(references, static reference => reference!.Contains("AtomUI.City.Build", StringComparison.Ordinal));
        Assert.Contains(references, static reference => reference!.Contains("AtomUI.City.Core", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AtomUICity.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
