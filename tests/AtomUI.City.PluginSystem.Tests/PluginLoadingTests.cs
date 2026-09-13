using AtomUI.City.PluginSystem;
using System.Runtime.CompilerServices;

namespace AtomUI.City.PluginSystem.Tests;

public sealed class PluginLoadingTests
{
    [Fact]
    public void LoaderLoadsMainAssemblyFromPluginRoot()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "AtomUI.City.PluginSystem.dll");
        workspace.CopyMainAssembly("AtomUI.City.PluginSystem.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);

        LoadAssertAndUnload(descriptor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadAssertAndUnload(PluginDescriptor descriptor)
    {
        var loader = new PluginLoader();
        var result = loader.LoadAsync(descriptor).GetAwaiter().GetResult();

        Assert.True(result.Succeeded);
        Assert.Equal(PluginRuntimeState.Loaded, result.State);
        var runtime = Assert.IsType<PluginRuntime>(result.Runtime);
        Assert.Equal(PluginRuntimeState.Loaded, runtime.State);
        Assert.Equal("AtomUI.City.PluginSystem", runtime.MainAssembly.GetName().Name);
        Assert.True(runtime.UnloadAsync().GetAwaiter().GetResult().Succeeded);
    }

    [Fact]
    public async Task RuntimeDeactivateAndUnloadUpdateState()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "AtomUI.City.PluginSystem.dll");
        workspace.CopyMainAssembly("AtomUI.City.PluginSystem.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);
        var loader = new PluginLoader();
        var result = await loader.LoadAsync(descriptor);
        var runtime = Assert.IsType<PluginRuntime>(result.Runtime);

        runtime.Activate();
        await runtime.DeactivateAsync();
        await runtime.UnloadAsync();

        Assert.Equal(PluginRuntimeState.Unloaded, runtime.State);
    }

    [Fact]
    public async Task RuntimeUnloadReportsPendingWhenLeaseRevocationFails()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "AtomUI.City.PluginSystem.dll");
        workspace.CopyMainAssembly("AtomUI.City.PluginSystem.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);
        var loader = new PluginLoader();
        var result = await loader.LoadAsync(descriptor);
        var runtime = Assert.IsType<PluginRuntime>(result.Runtime);
        var revokeAttempts = 0;
        var lease = runtime.RegisterUnloadLease(
            "routes:main",
            "route",
            _ => Interlocked.Increment(ref revokeAttempts) == 1
                ? throw new InvalidOperationException("route still active")
                : ValueTask.CompletedTask);

        var unload = await runtime.UnloadAsync();

        Assert.False(unload.Succeeded);
        Assert.Equal(PluginRuntimeState.UnloadPending, unload.State);
        Assert.Equal(PluginRuntimeState.UnloadPending, runtime.State);
        Assert.Equal(PluginRuntimeLeaseState.RevokeFailed, lease.State);
        Assert.Contains(
            unload.Diagnostics,
            diagnostic => diagnostic.Code == PluginDiagnosticIds.PluginUnloadPending
                && diagnostic.PluginId == "com.company.sales"
                && diagnostic.Field == "route"
                && diagnostic.Path == "routes:main");
        Assert.True((await runtime.UnloadAsync()).Succeeded);
    }

    [Fact]
    public async Task RuntimeUnloadDeactivatesActiveRuntimeAndRevokesLeases()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "AtomUI.City.PluginSystem.dll");
        workspace.CopyMainAssembly("AtomUI.City.PluginSystem.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);
        var loader = new PluginLoader();
        var result = await loader.LoadAsync(descriptor);
        var runtime = Assert.IsType<PluginRuntime>(result.Runtime);
        var revoked = false;
        var lease = runtime.RegisterUnloadLease(
            "command:save",
            "command",
            _ =>
            {
                revoked = true;
                return ValueTask.CompletedTask;
            });
        runtime.Activate();

        var unload = await runtime.UnloadAsync();

        Assert.True(unload.Succeeded);
        Assert.True(revoked);
        Assert.Equal(PluginRuntimeLeaseState.Revoked, lease.State);
        Assert.Equal(PluginRuntimeState.Unloaded, runtime.State);
    }

    [Fact]
    public async Task LoaderRejectsMissingMainAssembly()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "Missing.Plugin.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);
        var loader = new PluginLoader();

        var result = await loader.LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Equal(PluginRuntimeState.Faulted, result.State);
        Assert.Null(result.Runtime);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PluginDiagnosticIds.MainAssemblyNotFound);
    }

    [Fact]
    public async Task DiscoveryScannerReportsInvalidPluginDirectoryEntriesAndContinues()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest();
        workspace.CopyMainAssembly("Company.Sales.Plugin.dll");
        var pluginsRoot = workspace.CreateDirectory("plugins");
        var installer = new PluginPackageInstaller();
        await installer.InstallFromDirectoryAsync(workspace.Root, pluginsRoot);
        var invalidPluginDirectory = Path.Combine(
            pluginsRoot,
            PluginPackagePaths.InstalledDirectoryName,
            "com.company.broken");
        await File.WriteAllTextAsync(invalidPluginDirectory, "not a directory");

        var discovery = PluginDiscoveryScanner.DiscoverInstalled(pluginsRoot);

        Assert.False(discovery.Succeeded);
        Assert.Single(discovery.Plugins);
        Assert.Contains(
            discovery.Diagnostics,
            diagnostic => diagnostic.Code == PluginDiagnosticIds.InvalidPluginDirectory
                && diagnostic.PluginId == "com.company.broken"
                && diagnostic.Field == "pluginDirectory"
                && diagnostic.Path == invalidPluginDirectory);
    }
}
