using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents test host.
/// </summary>
public sealed class TestHost : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    internal TestHost(
        IApplicationContext applicationContext,
        IReadOnlyDictionary<string, object?> properties,
        TestDirectory directory,
        FakeUiDispatcher dispatcher,
        DeterministicScheduler scheduler,
        TestDiagnostics diagnostics)
    {
        ApplicationContext = applicationContext;
        Properties = properties;
        Directory = directory;
        Dispatcher = dispatcher;
        Scheduler = scheduler;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets application context.
    /// </summary>
    public IApplicationContext ApplicationContext { get; }

    /// <summary>
    /// Gets properties.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }

    /// <summary>
    /// Gets directory.
    /// </summary>
    public TestDirectory Directory { get; }

    /// <summary>
    /// Gets dispatcher.
    /// </summary>
    public FakeUiDispatcher Dispatcher { get; }

    /// <summary>
    /// Gets scheduler.
    /// </summary>
    public DeterministicScheduler Scheduler { get; }

    /// <summary>
    /// Gets diagnostics.
    /// </summary>
    public TestDiagnostics Diagnostics { get; }

    /// <summary>
    /// Gets or sets is stopped.
    /// </summary>
    public bool IsStopped { get; private set; }

    /// <summary>
    /// Executes the create builder operation.
    /// </summary>
    public static TestHostBuilder CreateBuilder()
    {
        return new TestHostBuilder();
    }

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    public ValueTask StopAsync()
    {
        IsStopped = true;

        return ValueTask.CompletedTask;
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
        StopAsync().AsTask().GetAwaiter().GetResult();
        Dispatcher.Dispose();
        Scheduler.Dispose();
        Diagnostics.Add("AUCTEST001", $"Test host disposed. Directory: {Directory.RootPath}");
        Directory.Dispose();
        Diagnostics.Freeze();
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
        Dispatcher.Dispose();
        Scheduler.Dispose();
        Diagnostics.Add("AUCTEST001", $"Test host disposed. Directory: {Directory.RootPath}");
        Directory.Dispose();
        Diagnostics.Freeze();
    }
}
