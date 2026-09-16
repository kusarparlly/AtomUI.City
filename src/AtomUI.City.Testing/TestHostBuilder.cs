using System.Collections.ObjectModel;
using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents test host builder.
/// </summary>
public sealed class TestHostBuilder
{
    private readonly Dictionary<string, object?> _properties = new(StringComparer.Ordinal);
    private string? _directoryName;
    private bool _keepDirectoryOnDispose;
    private bool _built;

    /// <summary>
    /// Executes the use property operation.
    /// </summary>
    public TestHostBuilder UseProperty(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfBuilt();

        _properties[key] = value;

        return this;
    }

    /// <summary>
    /// Executes the use directory name operation.
    /// </summary>
    public TestHostBuilder UseDirectoryName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ThrowIfBuilt();

        _directoryName = name;

        return this;
    }

    /// <summary>
    /// Executes the keep directory on dispose operation.
    /// </summary>
    public TestHostBuilder KeepDirectoryOnDispose()
    {
        ThrowIfBuilt();

        _keepDirectoryOnDispose = true;

        return this;
    }

    /// <summary>
    /// Executes the build operation.
    /// </summary>
    public TestHost Build()
    {
        ThrowIfBuilt();

        var diagnostics = new TestDiagnostics();
        var directory = TestDirectory.Create(_directoryName ?? "host", _keepDirectoryOnDispose);
        var applicationContext = new TestApplicationContext(directory);
        var properties = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(_properties, StringComparer.Ordinal));

        _built = true;

        return new TestHost(
            applicationContext,
            properties,
            directory,
            new FakeUiDispatcher(diagnostics),
            new DeterministicScheduler(diagnostics),
            diagnostics);
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The test host builder has already built a host and is frozen.");
        }
    }
}
