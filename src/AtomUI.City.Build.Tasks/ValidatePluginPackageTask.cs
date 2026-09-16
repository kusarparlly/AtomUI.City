using System.IO.Compression;
using System.Text.Json;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AtomUI.City.Build.Tasks;

public sealed class ValidatePluginPackageTask : Microsoft.Build.Utilities.Task
{
    [Required] public string PackagePath { get; set; } = null!;
    [Required] public string TargetFramework { get; set; } = null!;
    [Required] public string MainAssembly { get; set; } = null!;

    public override bool Execute()
    {
        try
        {
            var packagePath = Path.GetFullPath(PackagePath);
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException($"Plugin package was not found: {packagePath}", packagePath);
            }

            using var archive = ZipFile.OpenRead(packagePath);
            var entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
            var duplicateEntry = entries
                .GroupBy(entry => entry, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateEntry is not null)
            {
                throw new InvalidDataException($"Plugin package contains duplicate entry '{duplicateEntry.Key}'.");
            }

            foreach (var entry in entries)
            {
                var normalized = entry.TrimEnd('/');
                if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                    normalized.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
                {
                    throw new InvalidDataException($"Plugin package contains unsafe entry '{entry}'.");
                }
            }

            const string manifestPath = "atomui-city/plugin.json";
            var manifestEntry = archive.GetEntry(manifestPath) ??
                throw new InvalidDataException($"Plugin package must contain {manifestPath}.");
            using (var stream = manifestEntry.Open())
            using (var document = JsonDocument.Parse(stream))
            {
                var pluginId = document.RootElement.TryGetProperty("pluginId", out var value) ? value.GetString() : null;
                if (string.IsNullOrWhiteSpace(pluginId))
                {
                    throw new InvalidDataException("Plugin manifest requires pluginId.");
                }

                if (document.RootElement.TryGetProperty("contributions", out var contributions))
                {
                    foreach (var contribution in contributions.EnumerateArray())
                    {
                        var required = !contribution.TryGetProperty("required", out var requiredValue) || requiredValue.GetBoolean();
                        var path = contribution.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            throw new InvalidDataException("Plugin contribution requires a package-relative path.");
                        }

                        var normalizedPath = BuildTaskUtilities.NormalizePackagePath(path, "Contribution.Path");
                        if (required && archive.GetEntry(normalizedPath) is null)
                        {
                            throw new InvalidDataException($"Required plugin contribution is missing: {normalizedPath}.");
                        }
                    }
                }
            }

            var expectedMainAssembly = $"lib/{TargetFramework}/{Path.GetFileName(MainAssembly)}";
            if (!entries.Contains(expectedMainAssembly, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Plugin main assembly is missing: {expectedMainAssembly}.");
            }

            var matchingMainAssemblies = entries.Count(entry =>
                entry.StartsWith($"lib/{TargetFramework}/", StringComparison.Ordinal) &&
                entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(entry), Path.GetFileName(MainAssembly), StringComparison.OrdinalIgnoreCase));
            if (matchingMainAssemblies != 1)
            {
                Log.LogError(null, BuildTaskDiagnosticIds.MultiplePluginMainAssemblies, null, packagePath, 0, 0, 0, 0,
                    $"Plugin package must contain exactly one main assembly '{MainAssembly}'.");
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            BuildTaskUtilities.LogError(Log, BuildTaskDiagnosticIds.InvalidPluginPackageLayout, exception);
            return false;
        }
    }
}
