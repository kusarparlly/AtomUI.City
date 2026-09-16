namespace AtomUI.City.Testing;

/// <summary>
/// Represents test directory.
/// </summary>
public sealed class TestDirectory : IDisposable
{
    private bool _disposed;

    private TestDirectory(string rootPath, bool keepOnDispose)
    {
        RootPath = rootPath;
        KeepOnDispose = keepOnDispose;

        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Gets root path.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets keep on dispose.
    /// </summary>
    public bool KeepOnDispose { get; }

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static TestDirectory Create(string? name = null, bool keepOnDispose = false)
    {
        var safeName = string.IsNullOrWhiteSpace(name) ? "test" : name;
        var rootPath = Path.Combine(Path.GetTempPath(), "AtomUI.City.Tests", $"{safeName}-{Guid.NewGuid():N}");

        return new TestDirectory(rootPath, keepOnDispose);
    }

    /// <summary>
    /// Executes the get path operation.
    /// </summary>
    public string GetPath(params string[] segments)
    {
        var path = segments.Length == 0
            ? RootPath
            : Path.Combine([RootPath, .. segments]);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return path;
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!KeepOnDispose && Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
