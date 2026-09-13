using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Security;

internal static class SecurityFilePersistence
{
    public const int SchemaVersion = 1;

    public static string ResolveRootPath(
        SecurityPersistenceOptions options,
        IApplicationContext? applicationContext)
    {
        ArgumentNullException.ThrowIfNull(options);
        var root = options.RootPath;
        if (root is null)
        {
            var appDataPath = applicationContext?.AppDataPath;
            if (string.IsNullOrWhiteSpace(appDataPath))
            {
                var applicationName = AppDomain.CurrentDomain.FriendlyName;
                appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    applicationName);
            }

            root = Path.Combine(appDataPath, "security");
        }

        return Path.GetFullPath(root);
    }

    public static string HashResourceName(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resourceName.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task WriteJsonAtomicAsync<T>(
        string path,
        T value,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(path) ??
            throw new InvalidOperationException("A persistence path must have a parent directory.");
        Directory.CreateDirectory(directory);
        ApplyDirectoryPermissions(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, jsonTypeInfo, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            ApplyFilePermissions(temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // A successful atomic move removes the temporary path; cleanup failure is non-authoritative.
            }
        }
    }

    public static async Task<T?> ReadJsonAsync<T>(
        string path,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ApplyDirectoryPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void ApplyFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
