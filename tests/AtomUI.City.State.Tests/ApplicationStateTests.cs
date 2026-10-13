using AtomUI.City.Core.Diagnostics;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.State.Tests;

public sealed class ApplicationStateTests
{
    [Fact]
    public void AddStateRegistersAuthorityBoundWriterFactory()
    {
        var services = new ServiceCollection();
        services.AddState();
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IStateRegistry>();
        var factory = provider.GetRequiredService<IApplicationStateWriterFactory>();
        var key = new StateKey<int>("AtomUI.City.Tests.FactoryOwner");
        registry.Add(StateDefinition.Create(
            key,
            1,
            access: StateAccessPolicy.OwnerWrite,
            ownerModule: "Boxer.Documents"));

        var owner = factory.CreateWriter(StateWriteAuthority.Module("Boxer.Documents"));
        var other = factory.CreateWriter(StateWriteAuthority.Module("Boxer.Settings"));

        Assert.True(owner.Set(key, 2));
        Assert.Throws<StateAccessDeniedException>(() => other.Set(key, 3));
    }

    [Fact]
    public void ApplicationStateObserveEmitsCurrentValueThenChanges()
    {
        var key = new StateKey<int>("AtomUI.City.Tests.Observe");
        var registry = new ApplicationStateRegistry();
        registry.Add(StateDefinition.Create(key, 1));
        var notifications = new List<StateChangedEventArgs<int>>();

        using var subscription = registry.Observe(key, notifications.Add);
        registry.Set(key, 2);

        Assert.Equal(2, notifications.Count);
        Assert.True(notifications[0].IsInitial);
        Assert.False(notifications[1].IsInitial);
        Assert.Equal([0L, 1L], notifications.Select(notification => notification.Version));
    }

    [Fact]
    public void RegistryReadsRegisteredApplicationStateByKey()
    {
        var key = new StateKey<string>("AtomUI.City.Tests.Theme");
        var registry = new ApplicationStateRegistry();

        registry.Add(StateDefinition.Create(key, "light"));

        IApplicationState state = registry;
        var theme = state.Get(key);

        Assert.Equal("light", theme.Value);
        Assert.Equal(0, theme.Version);
    }

    [Fact]
    public void WriterUpdatesRegisteredApplicationState()
    {
        var key = new StateKey<string>("AtomUI.City.Tests.Theme");
        var registry = new ApplicationStateRegistry();
        registry.Add(StateDefinition.Create(key, "light"));

        IApplicationStateWriter writer = registry;

        var changed = writer.Set(key, "dark");

        Assert.True(changed);
        Assert.Equal("dark", registry.Get(key).Value);
    }

    [Fact]
    public void WriterUpdateRejectsNullUpdaterBeforeRegistryLookup()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ApplicationStateRegistry(diagnostics);
        var key = new StateKey<string>("AtomUI.City.Tests.Missing");

        var exception = Assert.Throws<ArgumentNullException>(
            () => registry.Update(key, null!));

        Assert.Equal("updater", exception.ParamName);
        Assert.Empty(diagnostics.Records);
    }

    [Fact]
    public void ReadOnlyStateDefinitionRejectsApplicationWriter()
    {
        var key = new StateKey<string>("AtomUI.City.Tests.ReadOnly");
        var registry = new ApplicationStateRegistry();
        registry.Add(StateDefinition.Create(key, "fixed", access: StateAccessPolicy.ReadOnly));

        var exception = Assert.Throws<StateAccessDeniedException>(
            () => registry.Set(key, "changed"));

        Assert.Equal(key.Name, exception.StateName);
        Assert.Equal("fixed", registry.Get(key).Value);
    }

    [Fact]
    public void ReadOnlyStateDefinitionRecordsWriteDeniedDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var key = new StateKey<string>("AtomUI.City.Tests.ReadOnly");
        var registry = new ApplicationStateRegistry(diagnostics);
        registry.Add(StateDefinition.Create(key, "fixed", access: StateAccessPolicy.ReadOnly));

        var exception = Assert.Throws<StateAccessDeniedException>(
            () => registry.Set(key, "changed"));

        Assert.Equal(key.Name, exception.StateName);
        Assert.Equal("fixed", registry.Get(key).Value);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.ApplicationStateWriteDenied, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Warning, record.Severity);
        Assert.Contains(key.Name, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingApplicationStateDoesNotCreateImplicitState()
    {
        var registry = new ApplicationStateRegistry();
        var key = new StateKey<int>("AtomUI.City.Tests.Missing");

        var exception = Assert.Throws<StateNotRegisteredException>(
            () => registry.Get(key));

        Assert.Equal(key.Name, exception.StateName);
    }

    [Fact]
    public void MissingApplicationStateRecordsDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ApplicationStateRegistry(diagnostics);
        var key = new StateKey<int>("AtomUI.City.Tests.Missing");

        var exception = Assert.Throws<StateNotRegisteredException>(
            () => registry.Get(key));

        Assert.Equal(key.Name, exception.StateName);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.ApplicationStateNotRegistered, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Warning, record.Severity);
        Assert.Contains(key.Name, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingApplicationStateRejectsDefaultKeyBeforeDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ApplicationStateRegistry(diagnostics);

        var exception = Assert.Throws<ArgumentException>(
            () => registry.Get(default(StateKey<int>)));

        Assert.Equal("key", exception.ParamName);
        Assert.Empty(diagnostics.Records);
    }

    [Fact]
    public void ApplicationStateTypeMismatchRecordsDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ApplicationStateRegistry(diagnostics);
        var stringKey = new StateKey<string>("AtomUI.City.Tests.Theme");
        var intKey = new StateKey<int>(stringKey.Name);
        registry.Add(StateDefinition.Create(stringKey, "light"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Get(intKey));

        Assert.Contains(stringKey.Name, exception.Message, StringComparison.Ordinal);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(StateDiagnosticIds.ApplicationStateNotRegistered, record.Code);
        Assert.Equal(HostDiagnosticSeverity.Warning, record.Severity);
        Assert.Contains(stringKey.Name, record.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(int).FullName!, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateApplicationStateRegistrationRecordsDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new ApplicationStateRegistry(diagnostics);
        var key = new StateKey<string>("AtomUI.City.Tests.Theme");
        registry.Add(StateDefinition.Create(key, "light"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Add(StateDefinition.Create(key, "dark")));

        Assert.Contains(key.Name, exception.Message, StringComparison.Ordinal);
        var record = Assert.Single(diagnostics.Records);
        Assert.Equal("AUCSTA008", record.Code);
        Assert.Equal(HostDiagnosticSeverity.Warning, record.Severity);
        Assert.Contains(key.Name, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerWriterOnlyWritesStatesOwnedByItsModule()
    {
        var key = new StateKey<int>("AtomUI.City.Tests.Owner");
        var registry = new ApplicationStateRegistry();
        registry.Add(StateDefinition.Create(
            key,
            1,
            access: StateAccessPolicy.OwnerWrite,
            ownerModule: "Boxer.Documents"));

        var owner = registry.CreateWriter(StateWriteAuthority.Module("Boxer.Documents"));
        var other = registry.CreateWriter(StateWriteAuthority.Module("Boxer.Settings"));

        Assert.True(owner.Set(key, 2));
        Assert.Throws<StateAccessDeniedException>(() => other.Set(key, 3));
        Assert.Throws<StateAccessDeniedException>(() => registry.Set(key, 3));
        Assert.Equal(2, registry.Get(key).Value);
    }

    [Fact]
    public void HostWriterOnlyWritesHostWriteStates()
    {
        var key = new StateKey<int>("AtomUI.City.Tests.Host");
        var registry = new ApplicationStateRegistry();
        registry.Add(StateDefinition.Create(key, 1, access: StateAccessPolicy.HostWrite));

        Assert.True(registry.Set(key, 2));
        Assert.Throws<StateAccessDeniedException>(
            () => registry.CreateWriter(StateWriteAuthority.Module("Boxer.Documents")).Set(key, 3));
    }

    [Fact]
    public void AuthorizedWriterRequiresDeclaredCapability()
    {
        var key = new StateKey<int>("AtomUI.City.Tests.Authorized");
        var registry = new ApplicationStateRegistry();
        registry.Add(StateDefinition.Create(
            key,
            1,
            access: StateAccessPolicy.AuthorizedWrite,
            writeCapability: "state.documents.write"));

        var authorized = registry.CreateWriter(StateWriteAuthority.Module(
            "Boxer.Documents",
            ["state.documents.write"]));
        var unauthorized = registry.CreateWriter(StateWriteAuthority.Module("Boxer.Documents"));

        Assert.True(authorized.Set(key, 2));
        Assert.Throws<StateAccessDeniedException>(() => unauthorized.Set(key, 3));
        Assert.Throws<StateAccessDeniedException>(() => registry.Set(key, 3));
    }

    [Fact]
    public void PluginWriterOnlyWritesItsIsolatedState()
    {
        var key = new StateKey<int>("AtomUI.City.Tests.Plugin");
        var registry = new ApplicationStateRegistry();
        registry.Add(StateDefinition.Create(
            key,
            1,
            access: StateAccessPolicy.PluginIsolated,
            pluginId: "boxer.catalog"));

        var owner = registry.CreateWriter(StateWriteAuthority.Plugin("boxer.catalog"));
        var other = registry.CreateWriter(StateWriteAuthority.Plugin("boxer.export"));

        Assert.True(owner.Set(key, 2));
        Assert.Throws<StateAccessDeniedException>(() => other.Set(key, 3));
        Assert.Throws<StateAccessDeniedException>(() => registry.Set(key, 3));
    }

    [Fact]
    public async Task RegistrySupportsConcurrentRegistrationAndLookup()
    {
        var registry = new ApplicationStateRegistry();
        var keys = Enumerable.Range(0, 100)
            .Select(index => new StateKey<int>($"AtomUI.City.Tests.Concurrent.{index}"))
            .ToArray();

        await Task.WhenAll(keys.Select((key, index) => Task.Run(() =>
        {
            registry.Add(StateDefinition.Create(key, index));
            Assert.Equal(index, registry.Get(key).Value);
            _ = registry.CreateSnapshot();
        })));

        Assert.All(keys, key => Assert.Equal(0, registry.Get(key).Version));
    }
}
