using AtomUI.City.State;

namespace AtomUI.City.State.Tests;

public sealed class StateDefinitionTests
{
    [Fact]
    public void StateDefinitionConstructionHelpersAreNotPublicExtensionPoints()
    {
        Assert.Empty(typeof(StateDefinition).GetConstructors());
        Assert.Null(typeof(StateDefinition<>).GetMethod(
            "Create",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));

        var constructor = Assert.Single(typeof(StateDefinition).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        var genericFactory = typeof(StateDefinition<>).GetMethod(
            "Create",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.True(constructor.IsAssembly);
        Assert.NotNull(genericFactory);
        Assert.True(genericFactory!.IsAssembly);
    }

    [Fact]
    public void StateDefinitionRejectsDefaultKey()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            StateDefinition.Create(default(StateKey<string>), "value"));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void StateDefinitionRejectsUnknownLifetime()
    {
        var key = new StateKey<string>("AtomUI.City.Tests.InvalidLifetime");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StateDefinition.Create(
                key,
                "value",
                lifetime: (StateLifetime)999));
    }

    [Fact]
    public void StateDefinitionRejectsUnknownAccessPolicy()
    {
        var key = new StateKey<string>("AtomUI.City.Tests.InvalidAccess");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StateDefinition.Create(
                key,
                "value",
                access: (StateAccessPolicy)999));
    }

    [Fact]
    public void StateDefinitionRejectsUnknownSnapshotPolicy()
    {
        var key = new StateKey<string>("AtomUI.City.Tests.InvalidSnapshotPolicy");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StateDefinition.Create(
                key,
                "value",
                snapshotPolicy: (StateSnapshotPolicy)999));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StateDefinitionRejectsInvalidSchemaVersion(int schemaVersion)
    {
        var key = new StateKey<string>("AtomUI.City.Tests.InvalidSchema");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StateDefinition.Create(
                key,
                "value",
                schemaVersion: schemaVersion));
    }

    [Fact]
    public void RestrictedAccessPoliciesRequireTheirAuthorizationMetadata()
    {
        var key = new StateKey<string>("AtomUI.City.Tests.Restricted");

        Assert.Throws<ArgumentException>(() => StateDefinition.Create(
            key,
            "value",
            access: StateAccessPolicy.OwnerWrite));
        Assert.Throws<ArgumentException>(() => StateDefinition.Create(
            key,
            "value",
            access: StateAccessPolicy.AuthorizedWrite));
        Assert.Throws<ArgumentException>(() => StateDefinition.Create(
            key,
            "value",
            access: StateAccessPolicy.PluginIsolated));
    }
}
