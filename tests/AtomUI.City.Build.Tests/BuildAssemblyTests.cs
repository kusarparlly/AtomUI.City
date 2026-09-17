using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AtomUI.City.Build.Tests;

public sealed partial class BuildAssemblyTests
{
    private static readonly string[] AssetFileNames =
    [
        "AtomUI.City.Application.targets",
        "AtomUI.City.Build.props",
        "AtomUI.City.Build.targets",
        "AtomUI.City.Core.Diagnostics.targets",
        "AtomUI.City.Plugin.targets",
    ];

    [Fact]
    public void BuildAssemblyHasNoPublicRuntimeSurface()
    {
        var assembly = Assembly.Load("AtomUI.City.Build");

        Assert.Equal("AtomUI.City.Build", assembly.GetName().Name);
        Assert.Empty(assembly.GetExportedTypes());
    }

    [Fact]
    public void MachineReadableContractExactlyMatchesBuildAssets()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var buildRoot = Path.Combine(repositoryRoot, "src", "AtomUI.City.Build");
        var assetsRoot = Path.Combine(buildRoot, "buildTransitive");
        var contractPath = Path.Combine(assetsRoot, "AtomUI.City.Build.contract.json");
        using var contract = JsonDocument.Parse(File.ReadAllText(contractPath));
        var root = contract.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Preview", root.GetProperty("stability").GetString());
        Assert.False(root.GetProperty("runtimeAssembly").GetBoolean());

        var assetDocuments = AssetFileNames
            .Select(fileName => XDocument.Load(Path.Combine(assetsRoot, fileName)))
            .ToArray();
        var assetText = string.Join(
            Environment.NewLine,
            AssetFileNames.Select(fileName => File.ReadAllText(Path.Combine(assetsRoot, fileName))));

        var propertyContracts = ReadContracts(root, "properties");
        var actualProperties = PropertyReferenceRegex()
            .Matches(assetText)
            .Select(match => match.Groups[1].Value)
            .Concat(assetDocuments.SelectMany(document => document
                .Descendants("PropertyGroup")
                .Elements()
                .Select(element => element.Name.LocalName)
                .Where(static name => name.StartsWith("AtomUICity", StringComparison.Ordinal))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(propertyContracts.Keys.Order(StringComparer.Ordinal), actualProperties);

        var actualDefaults = assetDocuments
            .SelectMany(document => document.Descendants("PropertyGroup").Elements())
            .Where(static element => element.Name.LocalName.StartsWith("AtomUICity", StringComparison.Ordinal))
            .ToDictionary(
                static element => element.Name.LocalName,
                static element => element.Value,
                StringComparer.Ordinal);
        foreach (var (name, descriptor) in propertyContracts)
        {
            var declaredDefault = descriptor.GetProperty("default");
            if (declaredDefault.ValueKind is JsonValueKind.Null)
            {
                Assert.False(actualDefaults.ContainsKey(name), $"Property '{name}' unexpectedly acquired a default value.");
            }
            else
            {
                Assert.Equal(declaredDefault.GetString(), actualDefaults[name]);
            }
        }

        var itemContracts = ReadContracts(root, "items");
        var actualItems = ItemReferenceRegex()
            .Matches(assetText)
            .Select(match => match.Groups[1].Value)
            .Concat(assetDocuments.SelectMany(document => document
                .Descendants("ItemGroup")
                .Elements()
                .Select(element => element.Name.LocalName)
                .Where(static name => name.StartsWith("AtomUICity", StringComparison.Ordinal))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(itemContracts.Keys.Order(StringComparer.Ordinal), actualItems);

        var targetContracts = ReadContracts(root, "targets");
        var actualTargets = assetDocuments
            .SelectMany(document => document.Descendants("Target"))
            .Select(target => target.Attribute("Name")?.Value)
            .Where(static name => name is not null)
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(targetContracts.Keys.Order(StringComparer.Ordinal), actualTargets);

        Assert.All(propertyContracts.Values.Concat(itemContracts.Values).Concat(targetContracts.Values), descriptor =>
        {
            var visibility = descriptor.GetProperty("visibility").GetString();
            Assert.True(visibility is "public" or "infrastructure", $"Unknown contract visibility '{visibility}'.");
        });

        var project = XDocument.Load(Path.Combine(buildRoot, "AtomUI.City.Build.csproj"));
        var projectProperties = project
            .Descendants("PropertyGroup")
            .Elements()
            .ToDictionary(static property => property.Name.LocalName, static property => property.Value, StringComparer.Ordinal);
        var actualPackageAssets = project
            .Descendants("None")
            .Where(item => item.Attribute("Pack")?.Value == "true")
            .Select(item => ResolvePackageAsset(item, projectProperties))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var contractedPackageAssets = root.GetProperty("packageAssets")
            .EnumerateArray()
            .Select(static asset => asset.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(contractedPackageAssets, actualPackageAssets);
    }

    [Fact]
    public void BuildPackageIsAssetOnlyAndPacksTheContractBaseline()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            repositoryRoot,
            "src",
            "AtomUI.City.Build",
            "AtomUI.City.Build.csproj"));
        var properties = project
            .Descendants("PropertyGroup")
            .Elements()
            .ToDictionary(static property => property.Name.LocalName, static property => property.Value, StringComparer.Ordinal);

        Assert.Equal("false", properties["IncludeBuildOutput"]);
        Assert.Equal("true", properties["SuppressDependenciesWhenPacking"]);
        Assert.Equal("false", properties["IncludeSymbols"]);
        Assert.Equal("false", properties["GenerateDocumentationFile"]);

        var contractAsset = project
            .Descendants("None")
            .Single(item => item.Attribute("Include")?.Value == "buildTransitive/AtomUI.City.Build.contract.json");
        Assert.Equal("true", contractAsset.Attribute("Pack")?.Value);
        Assert.Equal("buildTransitive/", contractAsset.Attribute("PackagePath")?.Value);
    }

    private static Dictionary<string, JsonElement> ReadContracts(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName)
            .EnumerateArray()
            .ToDictionary(
                static descriptor => descriptor.GetProperty("name").GetString()!,
                static descriptor => descriptor,
                StringComparer.Ordinal);

    private static string ResolvePackageAsset(
        XElement item,
        IReadOnlyDictionary<string, string> projectProperties)
    {
        var include = item.Attribute("Include")?.Value ?? throw new InvalidOperationException("Packed item has no Include.");
        var propertyMatch = ExactPropertyReferenceRegex().Match(include);
        if (propertyMatch.Success)
        {
            include = projectProperties[propertyMatch.Groups[1].Value];
        }

        var fileName = Path.GetFileName(include.Replace('\\', '/'));
        var packagePath = item.Attribute("PackagePath")?.Value?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;
        return string.IsNullOrEmpty(packagePath) ? fileName : $"{packagePath}/{fileName}";
    }

    [GeneratedRegex(@"\$\((AtomUICity[A-Za-z0-9_]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex PropertyReferenceRegex();

    [GeneratedRegex(@"@\((AtomUICity[A-Za-z0-9_]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex ItemReferenceRegex();

    [GeneratedRegex(@"^\$\(([A-Za-z0-9_]+)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactPropertyReferenceRegex();
}
