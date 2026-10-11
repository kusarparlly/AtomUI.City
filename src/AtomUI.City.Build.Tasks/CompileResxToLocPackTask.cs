using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AtomUI.City.Build.Tasks;

public sealed class CompileResxToLocPackTask : Microsoft.Build.Utilities.Task
{
    private const int MaximumPackageSizeBytes = 16 * 1024 * 1024;

    [Required] public ITaskItem[] Resources { get; set; } = [];
    [Required] public string ProjectDirectory { get; set; } = null!;
    [Required] public string RootNamespace { get; set; } = null!;
    [Required] public string OutputDirectory { get; set; } = null!;

    [Output] public ITaskItem[] GeneratedPackages { get; private set; } = [];
    [Output] public ITaskItem? GeneratedSource { get; private set; }

    public override bool Execute()
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ProjectDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(RootNamespace);
            ArgumentException.ThrowIfNullOrWhiteSpace(OutputDirectory);

            var projectDirectory = Path.GetFullPath(ProjectDirectory);
            var outputDirectory = Path.GetFullPath(OutputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var inputs = Resources
                .Select(item => ReadInput(item, projectDirectory))
                .OrderBy(input => input.PackageId, StringComparer.Ordinal)
                .ThenBy(input => input.Culture.Name, StringComparer.Ordinal)
                .ToArray();

            EnsureUnique(inputs, input => $"{input.Culture.Name}\0{input.PackageId}", "culture/package id");
            EnsureUnique(inputs, input => input.ResourceBaseName, "resource base name");

            var generatedPackages = new List<ITaskItem>(inputs.Length);
            foreach (var input in inputs)
            {
                var resources = ReadResources(input.SourcePath);
                var outputPath = Path.Combine(
                    outputDirectory,
                    $"resource.{CreateStableSuffix(input.ResourceBaseName)}.locpack.json");
                var json = CreateLocPack(input, resources);
                if (Encoding.UTF8.GetByteCount(json) > MaximumPackageSizeBytes)
                {
                    throw new InvalidDataException(
                        $"Generated localization pack '{input.PackageId}' exceeds the 16 MiB limit.");
                }

                BuildTaskUtilities.WriteUtf8IfChanged(outputPath, json);
                var generated = new TaskItem(outputPath);
                generated.SetMetadata("LogicalName", input.ResourceBaseName);
                generated.SetMetadata("WithCulture", "false");
                generated.SetMetadata("PackageId", input.PackageId);
                generated.SetMetadata("LanguageCulture", input.Culture.Name);
                generated.SetMetadata("SourceResx", input.SourcePath);
                generatedPackages.Add(generated);
                input.Resources = resources;
            }

            var sourcePath = Path.Combine(outputDirectory, "AtomUICityResxResources.g.cs");
            BuildTaskUtilities.WriteUtf8IfChanged(sourcePath, CreateAttributeSource(inputs));
            GeneratedPackages = generatedPackages.ToArray();
            GeneratedSource = new TaskItem(sourcePath);
            return true;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            BuildTaskUtilities.LogError(Log, BuildTaskDiagnosticIds.ResxCompilationFailed, exception);
            GeneratedPackages = [];
            GeneratedSource = null;
            return false;
        }
    }

    private static ResxInput ParseInput(ITaskItem item, string projectDirectory)
    {
        var sourcePath = BuildTaskUtilities.ResolveExistingFile(item, "RESX");
        if (!sourcePath.StartsWith(
                Path.TrimEndingDirectorySeparator(projectDirectory) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException($"RESX file '{sourcePath}' must stay inside the project directory.");
        }

        if (new FileInfo(sourcePath).Length > MaximumPackageSizeBytes)
        {
            throw new InvalidDataException($"RESX file '{sourcePath}' exceeds the 16 MiB limit.");
        }

        var relativePath = Path.GetRelativePath(projectDirectory, sourcePath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            throw new ArgumentException(
                $"RESX file '{relativePath}' must follow '<area>/<name>/<culture>/Resources.resx' or declare Culture and PackageId metadata.");
        }

        var cultureName = BuildTaskUtilities.GetMetadataOrDefault(item, "Culture", segments[^2]);
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var localizationName = segments[^3];
        var packageId = BuildTaskUtilities.GetMetadataOrDefault(
            item,
            "PackageId",
            $"{localizationName}.{culture.Name}");
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        var resourceBaseName = item.GetMetadata("ResourceBaseName");
        if (string.IsNullOrWhiteSpace(resourceBaseName))
        {
            var resourceSegments = segments[..^1]
                .Concat([Path.GetFileNameWithoutExtension(segments[^1])]);
            resourceBaseName = $"{RootNamespacePlaceholder}.{string.Join('.', resourceSegments)}.locpack.json";
        }

        var scope = BuildTaskUtilities.GetMetadataOrDefault(item, "Scope", "Module");
        if (scope is not ("Host" or "Module" or "Plugin" or "Route" or "Window" or "Presentation"))
        {
            throw new ArgumentException($"RESX metadata Scope '{scope}' is not supported.");
        }

        var scopeId = item.GetMetadata("ScopeId").Trim();
        if (scope is not ("Host" or "Presentation") && string.IsNullOrWhiteSpace(scopeId))
        {
            var localizationIndex = Array.FindIndex(
                segments,
                segment => string.Equals(segment, "Localization", StringComparison.OrdinalIgnoreCase));
            var scopeSegments = localizationIndex >= 0
                ? segments[(localizationIndex + 1)..^2]
                : segments[..^2];
            scopeId = string.Join('.', scopeSegments).ToLowerInvariant();
        }

        return new ResxInput(
            sourcePath,
            packageId,
            culture,
            resourceBaseName,
            scope,
            string.IsNullOrWhiteSpace(scopeId) ? null : scopeId,
            NullIfWhiteSpace(item.GetMetadata("Version")),
            new HashSet<string>(BuildTaskUtilities.SplitMetadata(item, "CriticalKeys"), StringComparer.Ordinal));
    }

    private const string RootNamespacePlaceholder = "{RootNamespace}";

    private ResxInput ReadInput(ITaskItem item, string projectDirectory)
    {
        var input = ParseInput(item, projectDirectory);
        return input with
        {
            ResourceBaseName = input.ResourceBaseName.Replace(
                RootNamespacePlaceholder,
                RootNamespace,
                StringComparison.Ordinal),
        };
    }

    private static SortedDictionary<string, string> ReadResources(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumPackageSizeBytes,
        };
        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        if (document.Root?.Name.LocalName != "root")
        {
            throw new InvalidDataException($"RESX file '{path}' must have a root element.");
        }

        var resources = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in document.Root.Elements().Where(element => element.Name.LocalName == "data"))
        {
            var key = element.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException($"RESX file '{path}' contains an empty resource key.");
            }

            if (element.Attribute("type") is not null || element.Attribute("mimetype") is not null)
            {
                throw new InvalidDataException(
                    $"RESX resource '{key}' is not a string resource; City locpack supports strings only.");
            }

            var valueElement = element.Elements().SingleOrDefault(child => child.Name.LocalName == "value");
            if (valueElement is null || string.IsNullOrWhiteSpace(valueElement.Value))
            {
                throw new InvalidDataException($"RESX resource '{key}' must contain a non-empty value.");
            }

            if (!resources.TryAdd(key, valueElement.Value))
            {
                throw new InvalidDataException($"RESX resource '{key}' is declared more than once.");
            }
        }

        if (resources.Count == 0)
        {
            throw new InvalidDataException($"RESX file '{path}' does not contain string resources.");
        }

        return resources;
    }

    private static string CreateLocPack(ResxInput input, IReadOnlyDictionary<string, string> resources)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("packageId", input.PackageId);
            writer.WriteString("culture", input.Culture.Name);
            if (input.Version is not null)
            {
                writer.WriteString("version", input.Version);
            }

            writer.WriteStartObject("resources");
            foreach (var resource in resources)
            {
                writer.WriteString(resource.Key, resource.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateAttributeSource(IReadOnlyList<ResxInput> inputs)
    {
        var builder = new StringBuilder()
            .AppendLine("// <auto-generated />")
            .AppendLine("using AtomUI.City.Localization;")
            .AppendLine();
        foreach (var input in inputs)
        {
            foreach (var key in input.Resources!.Keys)
            {
                builder.Append("[assembly: LocalizedResource(\"")
                    .Append(EscapeCSharp(key))
                    .Append("\", \"")
                    .Append(EscapeCSharp(input.PackageId))
                    .Append("\", Scope = ResourceScope.")
                    .Append(input.Scope)
                    .Append(", Culture = \"")
                    .Append(EscapeCSharp(input.Culture.Name))
                    .Append('"');
                if (input.ScopeId is not null)
                {
                    builder.Append(", ScopeId = \"").Append(EscapeCSharp(input.ScopeId)).Append('"');
                }

                if (input.Version is not null)
                {
                    builder.Append(", Version = \"").Append(EscapeCSharp(input.Version)).Append('"');
                }

                if (input.CriticalKeys.Contains(key))
                {
                    builder.Append(", Critical = true");
                }

                builder.AppendLine(")]");
            }
        }

        return builder.ToString();
    }

    private static string EscapeCSharp(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void EnsureUnique<T>(IEnumerable<T> values, Func<T, string> selector, string field)
    {
        var duplicate = values.GroupBy(selector, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate RESX {field} '{duplicate.Key.Replace("\0", "/", StringComparison.Ordinal)}'.");
        }
    }

    private static string CreateStableSuffix(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 6)).ToLowerInvariant();
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ResxInput(
        string SourcePath,
        string PackageId,
        CultureInfo Culture,
        string ResourceBaseName,
        string Scope,
        string? ScopeId,
        string? Version,
        HashSet<string> CriticalKeys)
    {
        public SortedDictionary<string, string>? Resources { get; set; }
    }
}
