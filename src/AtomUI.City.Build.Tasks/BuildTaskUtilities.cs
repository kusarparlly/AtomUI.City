using System.Security.Cryptography;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AtomUI.City.Build.Tasks;

internal static class BuildTaskUtilities
{
    public static string NormalizePackagePath(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
        {
            throw new ArgumentException($"{fieldName} must be a non-empty package-relative path.", fieldName);
        }

        var normalized = value.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException($"{fieldName} must stay inside the package.", fieldName);
        }

        return normalized;
    }

    public static string ResolveExistingFile(ITaskItem item, string fieldName)
    {
        var fullPath = Path.GetFullPath(item.ItemSpec);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"{fieldName} file was not found: {fullPath}", fullPath);
        }

        return fullPath;
    }

    public static string GetMetadataOrDefault(ITaskItem item, string name, string defaultValue)
    {
        var value = item.GetMetadata(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    public static bool GetBooleanMetadata(ITaskItem item, string name, bool defaultValue)
    {
        var value = item.GetMetadata(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Metadata '{name}' on '{item.ItemSpec}' must be true or false.", name);
    }

    public static string[] SplitMetadata(ITaskItem item, string name)
    {
        return item.GetMetadata(name)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static void CopyIfChanged(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        if (File.Exists(destinationPath) && FilesEqual(sourcePath, destinationPath))
        {
            return;
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    public static void WriteUtf8IfChanged(string path, string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.EndsWith('\n'))
        {
            normalized += "\n";
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(normalized);
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, bytes);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static void LogError(TaskLoggingHelper log, string code, Exception exception)
    {
        log.LogError(
            subcategory: null,
            errorCode: code,
            helpKeyword: null,
            file: exception is FileNotFoundException fileException ? fileException.FileName : null,
            lineNumber: 0,
            columnNumber: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            message: exception.Message);
    }

    private static bool FilesEqual(string left, string right)
    {
        var leftInfo = new FileInfo(left);
        var rightInfo = new FileInfo(right);
        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        Span<byte> leftBuffer = stackalloc byte[8192];
        Span<byte> rightBuffer = stackalloc byte[8192];
        while (true)
        {
            var leftRead = leftStream.Read(leftBuffer);
            var rightRead = rightStream.Read(rightBuffer);
            if (leftRead != rightRead || !leftBuffer[..leftRead].SequenceEqual(rightBuffer[..rightRead]))
            {
                return false;
            }

            if (leftRead == 0)
            {
                return true;
            }
        }
    }
}
