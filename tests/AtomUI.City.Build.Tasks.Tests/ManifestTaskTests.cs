using System.IO.Compression;
using System.Text.Json;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AtomUI.City.Build.Tasks.Tests;

public sealed class ManifestTaskTests
{
    [Fact]
    public void PluginManifestIsDeterministicAndSeparatesContracts()
    {
        using var directory = TemporaryDirectory.Create();
        var manifestPath = directory.PathOf("atomui-city", "plugin.json");
        var contractPath = directory.PathOf("atomui-city", "manifests", "contracts.json");
        var capability = new TaskItem("routes");
        capability.SetMetadata("Scope", "/sales/**;/reports/**");
        var dependency = new TaskItem("company.identity");
        dependency.SetMetadata("VersionRange", "[1.0.0,2.0.0)");

        var task = new GeneratePluginManifestTask
        {
            PluginId = "company.sales",
            PackageId = "Company.Sales.Plugin",
            PluginVersion = "1.2.3",
            DisplayNameKey = "Sales.DisplayName",
            MainAssembly = "Company.Sales.Plugin.dll",
            TargetFramework = "net10.0",
            PluginApiVersion = "1.0",
            MinHostVersion = "1.0.0",
            Capabilities = [capability],
            Dependencies = [dependency],
            Contracts = [new TaskItem("company.sales.contract")],
            OutputPath = manifestPath,
            ContractOutputPath = contractPath,
        };

        Assert.True(task.Execute());
        var first = File.ReadAllText(manifestPath);
        Assert.True(task.Execute());
        Assert.Equal(first, File.ReadAllText(manifestPath));

        using var manifest = JsonDocument.Parse(first);
        Assert.Equal("company.sales", manifest.RootElement.GetProperty("pluginId").GetString());
        Assert.Contains(
            manifest.RootElement.GetProperty("contributions").EnumerateArray(),
            value => value.GetProperty("type").GetString() == "contracts" &&
                     value.GetProperty("path").GetString() == "atomui-city/manifests/contracts.json");
        Assert.True(File.Exists(contractPath));
        Assert.Equal(2, task.GeneratedFiles.Length);
    }

    [Fact]
    public void ApplicationManifestCopiesAssetsAndRecordsSha256()
    {
        using var directory = TemporaryDirectory.Create();
        var plugin = directory.PathOf("source", "sales.plugin.nupkg");
        var resources = directory.PathOf("source", "zh-CN.resources.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(plugin)!);
        File.WriteAllText(plugin, "plugin-content");
        File.WriteAllText(resources, "resource-content");
        var pluginItem = new TaskItem(plugin);
        pluginItem.SetMetadata("PluginId", "company.sales");
        var resourceItem = new TaskItem(resources);
        resourceItem.SetMetadata("PackId", "company.zh-CN");
        var publishRoot = directory.PathOf("publish");
        var outputPath = directory.PathOf("publish", "atomui-city", "application.manifest.json");

        var task = new GenerateApplicationManifestTask
        {
            ApplicationId = "company.desktop",
            FrameworkVersion = "1.0.0",
            PluginApiVersion = "1.0",
            PluginProfile = "1.0-stable",
            TargetFramework = "net10.0",
            StaticPlugins = [pluginItem],
            ResourcePacks = [resourceItem],
            PublishRoot = publishRoot,
            OutputPath = outputPath,
        };

        Assert.True(task.Execute());
        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        var entry = document.RootElement.GetProperty("staticPlugins")[0];
        Assert.Equal("company.sales", entry.GetProperty("id").GetString());
        Assert.Equal(64, entry.GetProperty("sha256").GetString()!.Length);
        Assert.True(File.Exists(directory.PathOf("publish", "atomui-city", "static-plugins", "sales.plugin.nupkg")));
    }

    [Fact]
    public void PluginPackageValidatorAcceptsCanonicalLayout()
    {
        using var directory = TemporaryDirectory.Create();
        var packagePath = directory.PathOf("plugin.nupkg");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "atomui-city/plugin.json", "{\"pluginId\":\"company.sales\"}");
            WriteEntry(archive, "lib/net10.0/Company.Sales.Plugin.dll", "assembly");
        }

        var task = new ValidatePluginPackageTask
        {
            PackagePath = packagePath,
            TargetFramework = "net10.0",
            MainAssembly = "Company.Sales.Plugin.dll",
        };

        Assert.True(task.Execute());
    }

    [Fact]
    public void PluginPackageValidatorRejectsMissingRequiredContribution()
    {
        using var directory = TemporaryDirectory.Create();
        var packagePath = directory.PathOf("plugin.nupkg");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            WriteEntry(
                archive,
                "atomui-city/plugin.json",
                "{\"pluginId\":\"company.sales\",\"contributions\":[{\"type\":\"routes\",\"path\":\"atomui-city/manifests/routes.json\",\"required\":true}]}");
            WriteEntry(archive, "lib/net10.0/Company.Sales.Plugin.dll", "assembly");
        }

        var buildEngine = new RecordingBuildEngine();
        var task = new ValidatePluginPackageTask
        {
            BuildEngine = buildEngine,
            PackagePath = packagePath,
            TargetFramework = "net10.0",
            MainAssembly = "Company.Sales.Plugin.dll",
        };

        Assert.False(task.Execute());
        Assert.Contains(buildEngine.Errors, error => error.Code == "AUCBLD0201");
    }

    [Fact]
    public void PluginManifestRejectsEscapingContributionPathWithStableDiagnostic()
    {
        using var directory = TemporaryDirectory.Create();
        var buildEngine = new RecordingBuildEngine();
        var contribution = new TaskItem("routes.json");
        contribution.SetMetadata("Type", "routes");
        contribution.SetMetadata("TargetPath", "../routes.json");
        var task = new GeneratePluginManifestTask
        {
            BuildEngine = buildEngine,
            PluginId = "company.sales",
            PackageId = "Company.Sales.Plugin",
            PluginVersion = "1.0.0",
            DisplayNameKey = "Sales.DisplayName",
            MainAssembly = "Company.Sales.Plugin.dll",
            TargetFramework = "net10.0",
            PluginApiVersion = "1.0",
            MinHostVersion = "1.0.0",
            Contributions = [contribution],
            OutputPath = directory.PathOf("plugin.json"),
            ContractOutputPath = directory.PathOf("contracts.json"),
        };

        Assert.False(task.Execute());
        Assert.Contains(buildEngine.Errors, error => error.Code == "AUCBLD0101");
        Assert.False(File.Exists(task.OutputPath));
    }

    [Fact]
    public void ApplicationManifestRejectsDuplicateStableIds()
    {
        using var directory = TemporaryDirectory.Create();
        var first = directory.PathOf("first.nupkg");
        var second = directory.PathOf("second.nupkg");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        var firstItem = new TaskItem(first);
        var secondItem = new TaskItem(second);
        firstItem.SetMetadata("PluginId", "duplicate");
        secondItem.SetMetadata("PluginId", "duplicate");
        var buildEngine = new RecordingBuildEngine();
        var task = new GenerateApplicationManifestTask
        {
            BuildEngine = buildEngine,
            ApplicationId = "company.desktop",
            PluginApiVersion = "1.0",
            PluginProfile = "1.0-stable",
            TargetFramework = "net10.0",
            StaticPlugins = [firstItem, secondItem],
            PublishRoot = directory.PathOf("publish"),
            OutputPath = directory.PathOf("publish", "application.manifest.json"),
        };

        Assert.False(task.Execute());
        Assert.Contains(buildEngine.Errors, error => error.Code == "AUCBLD0401");
        Assert.False(Directory.Exists(directory.PathOf("publish", "atomui-city", "static-plugins")));
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open());
        writer.Write(content);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Root = path;
        private string Root { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AtomUI.City.Build.Tasks.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string PathOf(params string[] segments) => segments.Aggregate(Root, System.IO.Path.Combine);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class RecordingBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => string.Empty;

        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) { }
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => false;
    }
}
