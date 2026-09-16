using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AtomUI.City.Build.Tasks;

public sealed class GenerateApplicationManifestTask : Microsoft.Build.Utilities.Task
{
    [Required] public string ApplicationId { get; set; } = null!;
    public string? FrameworkVersion { get; set; }
    [Required] public string PluginApiVersion { get; set; } = null!;
    [Required] public string PluginProfile { get; set; } = null!;
    [Required] public string TargetFramework { get; set; } = null!;
    public string? RuntimeIdentifier { get; set; }
    public bool AotMode { get; set; }
    public ITaskItem[] StaticPlugins { get; set; } = [];
    public ITaskItem[] ResourcePacks { get; set; } = [];
    [Required] public string PublishRoot { get; set; } = null!;
    [Required] public string OutputPath { get; set; } = null!;

    [Output] public ITaskItem[] CopiedFiles { get; private set; } = [];

    public override bool Execute()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ApplicationId) || string.IsNullOrWhiteSpace(PublishRoot) || string.IsNullOrWhiteSpace(OutputPath))
            {
                throw new ArgumentException("ApplicationId, PublishRoot and OutputPath are required.");
            }

            var publishRoot = Path.GetFullPath(PublishRoot);
            Directory.CreateDirectory(publishRoot);
            var copied = new List<ITaskItem>();
            var staticPlugins = CopyEntries(StaticPlugins, publishRoot, "atomui-city/static-plugins", "PluginId", copied);
            var resourcePacks = CopyEntries(ResourcePacks, publishRoot, "atomui-city/resources", "PackId", copied);
            var manifest = new ApplicationManifest(
                "1.0",
                ApplicationId.Trim(),
                ResolveFrameworkVersion(),
                PluginApiVersion.Trim(),
                PluginProfile.Trim(),
                TargetFramework.Trim(),
                string.IsNullOrWhiteSpace(RuntimeIdentifier) ? null : RuntimeIdentifier.Trim(),
                AotMode ? "NativeAot" : "CoreClr",
                staticPlugins,
                resourcePacks);
            BuildTaskUtilities.WriteUtf8IfChanged(
                OutputPath,
                JsonSerializer.Serialize(manifest, JsonOptions));
            copied.Add(new TaskItem(Path.GetFullPath(OutputPath)));
            CopiedFiles = copied.ToArray();
            return true;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            BuildTaskUtilities.LogError(Log, BuildTaskDiagnosticIds.InvalidApplicationPublishLayout, exception);
            return false;
        }
    }

    private static AssetEntry[] CopyEntries(
        IEnumerable<ITaskItem> items,
        string publishRoot,
        string relativeRoot,
        string idMetadata,
        List<ITaskItem> copied)
    {
        var pending = items
            .OrderBy(value => value.ItemSpec, StringComparer.Ordinal)
            .Select(item =>
            {
                var sourcePath = BuildTaskUtilities.ResolveExistingFile(item, idMetadata);
                var id = BuildTaskUtilities.GetMetadataOrDefault(item, idMetadata, Path.GetFileNameWithoutExtension(sourcePath));
                var targetPath = BuildTaskUtilities.NormalizePackagePath(
                    $"{relativeRoot}/{Path.GetFileName(sourcePath)}",
                    "TargetPath");
                return new PendingAsset(item.ItemSpec, sourcePath, id, targetPath);
            })
            .ToArray();

        EnsureUnique(pending, value => value.Id, "id");
        EnsureUnique(pending, value => value.TargetPath, "target path");

        var entries = new List<AssetEntry>(pending.Length);
        foreach (var asset in pending)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(
                publishRoot,
                asset.TargetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(publishRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Asset '{asset.ItemSpec}' escapes the publish root.");
            }

            BuildTaskUtilities.CopyIfChanged(asset.SourcePath, destinationPath);
            copied.Add(new TaskItem(destinationPath));
            entries.Add(new AssetEntry(asset.Id, asset.TargetPath, BuildTaskUtilities.ComputeSha256(destinationPath)));
        }

        return entries.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
    }

    private static void EnsureUnique(
        IEnumerable<PendingAsset> assets,
        Func<PendingAsset, string> selector,
        string field)
    {
        var duplicate = assets.GroupBy(selector, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate published asset {field} '{duplicate.Key}'.");
        }
    }

    private string ResolveFrameworkVersion()
    {
        if (!string.IsNullOrWhiteSpace(FrameworkVersion))
        {
            return FrameworkVersion.Trim();
        }

        return typeof(GenerateApplicationManifestTask).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "0.0.0";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private sealed record AssetEntry(string Id, string Path, string Sha256);
    private sealed record PendingAsset(string ItemSpec, string SourcePath, string Id, string TargetPath);
    private sealed record ApplicationManifest(
        string SchemaVersion,
        string AppId,
        string FrameworkVersion,
        string PluginApiVersion,
        string PluginProfile,
        string TargetFramework,
        string? RuntimeIdentifier,
        string AotMode,
        AssetEntry[] StaticPlugins,
        AssetEntry[] ResourcePacks);
}
