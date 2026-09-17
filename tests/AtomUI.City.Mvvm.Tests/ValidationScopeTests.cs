using AtomUI.City.Mvvm;

namespace AtomUI.City.Mvvm.Tests;

public sealed class ValidationScopeTests
{
    [Fact]
    public void ValidationChangedEventArgsCanOnlyBeCreatedByValidationScope()
    {
        Assert.Empty(typeof(ValidationChangedEventArgs).GetConstructors());

        var constructor = Assert.Single(typeof(ValidationChangedEventArgs).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));

        Assert.True(constructor.IsAssembly);
    }

    [Fact]
    public void ValidationScopeStartsValidAndTracksInvalidEntries()
    {
        var scope = new ValidationScope();

        Assert.Equal(ValidationStatus.Valid, scope.Status);

        scope.SetInvalid("Name", "Name is required.");

        Assert.Equal(ValidationStatus.Invalid, scope.Status);
        Assert.Equal("Name is required.", scope.Errors["Name"][0]);
    }

    [Fact]
    public void ValidationScopeTracksLocalizableMessageMetadata()
    {
        var scope = new ValidationScope();

        scope.SetInvalid(
            "Name",
            "Name is required.",
            "Validation.Name.Required",
            ["Name"]);

        var message = scope.Messages["Name"][0];
        Assert.Equal(ValidationStatus.Invalid, scope.Status);
        Assert.Equal("Name is required.", scope.Errors["Name"][0]);
        Assert.Equal("Name", message.Key);
        Assert.Equal("Name is required.", message.Message);
        Assert.Equal("Validation.Name.Required", message.MessageKey);
        Assert.Equal(["Name"], message.MessageArguments);
    }

    [Fact]
    public void SetMessagesReplacesAndClearsFieldMessages()
    {
        var scope = new ValidationScope();
        var first = new ValidationMessage("Name", "Name is required.");
        var second = new ValidationMessage("Name", "Name is too long.");

        scope.SetMessages("Name", [first]);
        scope.SetMessages("Name", [second]);

        Assert.Equal(ValidationStatus.Invalid, scope.Status);
        Assert.Single(scope.Messages["Name"]);
        Assert.Equal("Name is too long.", scope.Errors["Name"][0]);

        scope.SetMessages("Name", []);

        Assert.Equal(ValidationStatus.Valid, scope.Status);
        Assert.False(scope.Errors.ContainsKey("Name"));
        Assert.False(scope.Messages.ContainsKey("Name"));
    }

    [Fact]
    public void SetMessagesDeduplicatesRepeatedMessages()
    {
        var scope = new ValidationScope();
        var message = new ValidationMessage("Name", "Name is required.", "Validation.Name.Required", ["Name"]);

        scope.SetMessages("Name", [message, message]);

        Assert.Single(scope.Messages["Name"]);
        Assert.Single(scope.Errors["Name"]);
    }

    [Fact]
    public void SetMessagesRejectsNullMessages()
    {
        var scope = new ValidationScope();
        ValidationMessage?[] messages = [null];

        Assert.Throws<ArgumentException>(() => scope.SetMessages("Name", messages));
    }

    [Fact]
    public void ValidationChangedEventCarriesPresentationBindingInputs()
    {
        using var activationScope = new ActivationScope();
        var scope = new ValidationScope();
        ValidationChangedEventArgs? changed = null;

        scope.BindTo(activationScope);
        scope.ValidationChanged += (_, args) => changed = args;

        scope.SetMessages("Name", [new ValidationMessage("Name", "Name is required.")]);

        Assert.NotNull(changed);
        Assert.Equal("Name", changed.Key);
        Assert.Equal(ValidationStatus.Invalid, changed.Status);
        Assert.Equal("Name is required.", changed.Errors[0]);
        Assert.Equal("Name is required.", changed.Messages[0].Message);
        Assert.Equal(activationScope.Id, changed.OwnerScopeId);
    }

    [Fact]
    public void DisposeIsIdempotentAndRejectsMutation()
    {
        var scope = new ValidationScope();

        scope.Dispose();
        scope.Dispose();

        Assert.True(scope.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() =>
            scope.SetMessages("Name", [new ValidationMessage("Name", "Name is required.")]));
    }

    [Fact]
    public void ValidationMessageArgumentsRejectExternalMutation()
    {
        var arguments = new List<object?> { "Name" };
        var message = new ValidationMessage("Name", "Name is required.", "Validation.Name.Required", arguments);
        var exposedArguments = Assert.IsAssignableFrom<IList<object?>>(message.MessageArguments);

        arguments[0] = "Changed";

        Assert.Throws<NotSupportedException>(() => exposedArguments[0] = "Changed");
        Assert.Equal("Name", message.MessageArguments![0]);
    }

    [Fact]
    public void ValidationCollectionsRejectExternalMutation()
    {
        var scope = new ValidationScope();

        scope.SetInvalid("Name", "Name is required.");
        var errors = Assert.IsAssignableFrom<IDictionary<string, IReadOnlyList<string>>>(scope.Errors);
        var messages = Assert.IsAssignableFrom<IDictionary<string, IReadOnlyList<ValidationMessage>>>(scope.Messages);
        var errorMessages = Assert.IsAssignableFrom<IList<string>>(scope.Errors["Name"]);
        var validationMessages = Assert.IsAssignableFrom<IList<ValidationMessage>>(scope.Messages["Name"]);

        Assert.Throws<NotSupportedException>(() => errors["Other"] = ["Changed"]);
        Assert.Throws<NotSupportedException>(() => messages["Other"] = [new ValidationMessage("Other", "Changed")]);
        Assert.Throws<NotSupportedException>(() => errorMessages[0] = "Changed");
        Assert.Throws<NotSupportedException>(() => validationMessages[0] = new ValidationMessage("Name", "Changed"));
        Assert.Equal("Name is required.", scope.Errors["Name"][0]);
        Assert.Equal("Name is required.", scope.Messages["Name"][0].Message);
    }

    [Fact]
    public void ValidationFailureIsDistinctFromInvalidState()
    {
        var scope = new ValidationScope();
        var exception = new InvalidOperationException("validator failed");

        scope.SetInvalid(
            "Name",
            "Name is required.",
            "Validation.Name.Required",
            ["Name"]);
        scope.SetFailed(exception);

        Assert.Equal(ValidationStatus.Failed, scope.Status);
        Assert.Same(exception, scope.Exception);
        Assert.Empty(scope.Errors);
        Assert.Empty(scope.Messages);
    }

    [Fact]
    public void ActivationScopeDisposalCancelsValidationScope()
    {
        using var activationScope = new ActivationScope();
        var validationScope = new ValidationScope();

        validationScope.BindTo(activationScope);
        validationScope.SetPending();
        activationScope.Dispose();

        Assert.Equal(ValidationStatus.Canceled, validationScope.Status);
    }
}
