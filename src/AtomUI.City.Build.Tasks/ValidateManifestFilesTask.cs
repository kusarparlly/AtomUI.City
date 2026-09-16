using System.Text.Json;

using Microsoft.Build.Framework;

namespace AtomUI.City.Build.Tasks;

public sealed class ValidateManifestFilesTask : Microsoft.Build.Utilities.Task
{
    public ITaskItem[] Manifests { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            foreach (var manifest in Manifests.OrderBy(item => item.ItemSpec, StringComparer.Ordinal))
            {
                var path = BuildTaskUtilities.ResolveExistingFile(manifest, "Manifest");
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"Manifest root must be an object: {path}");
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            BuildTaskUtilities.LogError(Log, BuildTaskDiagnosticIds.ManifestValidationFailed, exception);
            return false;
        }
    }
}
