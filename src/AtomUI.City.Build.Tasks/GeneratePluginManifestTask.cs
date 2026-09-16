using System.Text.Json;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AtomUI.City.Build.Tasks;

public sealed class GeneratePluginManifestTask : Microsoft.Build.Utilities.Task
{
    [Required] public string PluginId { get; set; } = null!;
    [Required] public string PackageId { get; set; } = null!;
    [Required] public string PluginVersion { get; set; } = null!;
    [Required] public string DisplayNameKey { get; set; } = null!;
    public string? DescriptionKey { get; set; }
    public string? Publisher { get; set; }
    [Required] public string MainAssembly { get; set; } = null!;
    [Required] public string TargetFramework { get; set; } = null!;
    [Required] public string PluginApiVersion { get; set; } = null!;
    [Required] public string MinHostVersion { get; set; } = null!;
    public string? MaxHostVersion { get; set; }
    public bool Unloadable { get; set; } = true;
    public bool AotCompatible { get; set; }
    public ITaskItem[] Capabilities { get; set; } = [];
    public ITaskItem[] Dependencies { get; set; } = [];
    public ITaskItem[] Contributions { get; set; } = [];
    public ITaskItem[] Contracts { get; set; } = [];
    [Required] public string OutputPath { get; set; } = null!;
    [Required] public string ContractOutputPath { get; set; } = null!;

    [Output] public ITaskItem[] GeneratedFiles { get; private set; } = [];

    public override bool Execute()
    {
        try
        {
            ValidateRequiredValues();
            var capabilities = Capabilities
                .Select(item => new Capability(item.ItemSpec.Trim(), BuildTaskUtilities.SplitMetadata(item, "Scope")))
                .OrderBy(value => value.Name, StringComparer.Ordinal)
                .ToArray();
            EnsureUnique(capabilities.Select(value => value.Name), "capability");

            var dependencies = Dependencies
                .Select(item => new Dependency(item.ItemSpec.Trim(), EmptyToNull(item.GetMetadata("VersionRange"))))
                .OrderBy(value => value.PluginId, StringComparer.Ordinal)
                .ToArray();
            EnsureUnique(dependencies.Select(value => value.PluginId), "dependency");

            var contributions = Contributions
                .Select(CreateContribution)
                .ToList();
            var contracts = Contracts
                .Select(item => new Contract(item.ItemSpec.Trim(), EmptyToNull(item.GetMetadata("VersionRange"))))
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .ToArray();
            EnsureUnique(contracts.Select(value => value.Id), "contract");
            if (contracts.Length > 0)
            {
                contributions.Add(new Contribution("contracts", "atomui-city/manifests/contracts.json", true));
            }

            var manifest = new PluginManifest(
                "1.0",
                PluginId.Trim(),
                PackageId.Trim(),
                PluginVersion.Trim(),
                DisplayNameKey.Trim(),
                EmptyToNull(DescriptionKey),
                EmptyToNull(Publisher),
                Path.GetFileName(MainAssembly),
                TargetFramework.Trim(),
                PluginApiVersion.Trim(),
                MinHostVersion.Trim(),
                EmptyToNull(MaxHostVersion),
                Unloadable,
                AotCompatible,
                capabilities,
                contributions.OrderBy(value => value.Type, StringComparer.Ordinal).ThenBy(value => value.Path, StringComparer.Ordinal).ToArray(),
                dependencies,
                Array.Empty<object>());

            BuildTaskUtilities.WriteUtf8IfChanged(OutputPath, Serialize(manifest));
            var generatedFiles = new List<ITaskItem> { new TaskItem(Path.GetFullPath(OutputPath)) };
            if (contracts.Length > 0)
            {
                BuildTaskUtilities.WriteUtf8IfChanged(
                    ContractOutputPath,
                    Serialize(new ContractManifest("1.0", contracts)));
                generatedFiles.Add(new TaskItem(Path.GetFullPath(ContractOutputPath)));
            }

            GeneratedFiles = generatedFiles.ToArray();
            return true;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            BuildTaskUtilities.LogError(Log, BuildTaskDiagnosticIds.ManifestGenerationFailed, exception);
            return false;
        }
    }

    private void ValidateRequiredValues()
    {
        var values = new Dictionary<string, string?>
        {
            [nameof(PluginId)] = PluginId,
            [nameof(PackageId)] = PackageId,
            [nameof(PluginVersion)] = PluginVersion,
            [nameof(DisplayNameKey)] = DisplayNameKey,
            [nameof(MainAssembly)] = MainAssembly,
            [nameof(TargetFramework)] = TargetFramework,
            [nameof(PluginApiVersion)] = PluginApiVersion,
            [nameof(MinHostVersion)] = MinHostVersion,
            [nameof(OutputPath)] = OutputPath,
            [nameof(ContractOutputPath)] = ContractOutputPath,
        };
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                throw new ArgumentException($"{pair.Key} is required.", pair.Key);
            }
        }

        if (PluginId.Contains('/') || PluginId.Contains('\\') || Path.IsPathRooted(PluginId))
        {
            throw new ArgumentException("PluginId must be a stable identifier, not a path.", nameof(PluginId));
        }

        if (Path.GetFileName(MainAssembly) != MainAssembly)
        {
            throw new ArgumentException("MainAssembly must be a file name.", nameof(MainAssembly));
        }
    }

    private static Contribution CreateContribution(ITaskItem item)
    {
        var type = item.GetMetadata("Type");
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException($"Contribution '{item.ItemSpec}' requires Type metadata.");
        }

        var defaultPath = $"atomui-city/manifests/{Path.GetFileName(item.ItemSpec)}";
        var path = BuildTaskUtilities.NormalizePackagePath(
            BuildTaskUtilities.GetMetadataOrDefault(item, "TargetPath", defaultPath),
            "TargetPath");
        return new Contribution(type.Trim(), path, BuildTaskUtilities.GetBooleanMetadata(item, "Required", true));
    }

    private static void EnsureUnique(IEnumerable<string> values, string kind)
    {
        var duplicate = values.GroupBy(value => value, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate plugin {kind} '{duplicate.Key}'.");
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private sealed record Capability(string Name, string[] Scope);
    private sealed record Dependency(string PluginId, string? VersionRange);
    private sealed record Contribution(string Type, string Path, bool Required);
    private sealed record Contract(string Id, string? VersionRange);
    private sealed record ContractManifest(string SchemaVersion, Contract[] Contracts);
    private sealed record PluginManifest(
        string SchemaVersion,
        string PluginId,
        string PackageId,
        string Version,
        string DisplayNameKey,
        string? DescriptionKey,
        string? Publisher,
        string MainAssembly,
        string TargetFramework,
        string PluginApiVersion,
        string MinHostVersion,
        string? MaxHostVersion,
        bool Unloadable,
        bool AotCompatible,
        Capability[] Capabilities,
        Contribution[] Contributions,
        Dependency[] Dependencies,
        object[] Modules);
}
