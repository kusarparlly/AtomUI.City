using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Mvvm.Tests;

public sealed class GeneratedValidationTests
{
    [Fact]
    public void GeneratedRulesReportAndClearPropertyErrors()
    {
        var viewModel = new GeneratedValidationViewModel();
        var changed = new List<string?>();
        viewModel.ErrorsChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Name = null;
        viewModel.Age = 9;
        viewModel.Code = "x";
        viewModel.Minimum = "x";
        viewModel.Maximum = "toolong";
        viewModel.Pattern = "abc";
        viewModel.Email = "invalid";
        viewModel.Phone = "not-a-phone";
        viewModel.Url = "not-a-url";
        viewModel.CreditCard = "123";

        Assert.True(viewModel.HasErrors);
        Assert.Equal(ValidationStatus.Invalid, viewModel.ValidationStatus);
        Assert.All(
            new[] { "Name", "Age", "Code", "Minimum", "Maximum", "Pattern", "Email", "Phone", "Url", "CreditCard" },
            property => Assert.NotEmpty(viewModel.GetErrors(property).Cast<string>()));

        viewModel.Name = "Ada";
        viewModel.Age = 42;
        viewModel.Code = "valid";
        viewModel.Minimum = "four";
        viewModel.Maximum = "short";
        viewModel.Pattern = "123";
        viewModel.Email = "ada@example.com";
        viewModel.Phone = "+1 425 555 0100";
        viewModel.Url = "https://example.com";
        viewModel.CreditCard = "4111111111111111";

        Assert.False(viewModel.HasErrors);
        Assert.Equal(ValidationStatus.Valid, viewModel.ValidationStatus);
        Assert.Contains(nameof(GeneratedValidationViewModel.Name), changed);
    }

    [Fact]
    public void ValidateAllPropertiesUsesCompareAndDirectCustomValidator()
    {
        var viewModel = new GeneratedValidationViewModel
        {
            Password = "one",
            Confirmation = "two",
            UserName = "blocked"
        };

        viewModel.ValidateAllProperties();

        Assert.NotEmpty(viewModel.GetErrors(nameof(GeneratedValidationViewModel.Confirmation)).Cast<string>());
        Assert.Equal(
            new[] { "The user name is blocked." },
            viewModel.GetErrors(nameof(GeneratedValidationViewModel.UserName)).Cast<string>());

        viewModel.Confirmation = "one";
        viewModel.UserName = "allowed";

        Assert.Empty(viewModel.GetErrors(nameof(GeneratedValidationViewModel.Confirmation)).Cast<string>());
        Assert.Empty(viewModel.GetErrors(nameof(GeneratedValidationViewModel.UserName)).Cast<string>());
    }

    [Fact]
    public void ValidationRuleFailureIsObservableAndDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var viewModel = new GeneratedValidationViewModel(diagnostics);

        viewModel.UserName = "throw";

        Assert.Equal(ValidationStatus.Failed, viewModel.ValidationStatus);
        Assert.IsType<InvalidOperationException>(viewModel.ValidationException);
        Assert.False(viewModel.HasErrors);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == MvvmDiagnosticIds.ValidationFailed &&
            record.Context["propertyName"] == nameof(GeneratedValidationViewModel.UserName));
    }

    [Fact]
    public void CommittedErrorsRemainReadableButMutationFailsAfterDispose()
    {
        var viewModel = new GeneratedValidationViewModel { Name = null };
        viewModel.Dispose();

        Assert.NotEmpty(viewModel.GetErrors(nameof(GeneratedValidationViewModel.Name)).Cast<string>());
        Assert.Throws<ObjectDisposedException>(() => viewModel.Name = "Ada");
        Assert.Throws<ObjectDisposedException>(() => viewModel.ValidateAllProperties());
    }
}

internal sealed partial class GeneratedValidationViewModel : ValidatingViewModelBase
{
    private string? _name = "initial";
    private int _age = 18;
    private string? _code = "valid";
    private string? _minimum = "four";
    private string? _maximum = "short";
    private string? _pattern = "123";
    private string? _email = "ada@example.com";
    private string? _phone = "+1 425 555 0100";
    private string? _url = "https://example.com";
    private string? _creditCard = "4111111111111111";
    private string? _password;
    private string? _confirmation;
    private string? _userName = "allowed";

    public GeneratedValidationViewModel(IHostDiagnostics? diagnostics = null)
        : base(diagnostics)
    {
    }

    [Required]
    public string? Name
    {
        get => _name;
        set => SetProperty(ref _name, value, validate: true);
    }

    [Range(18, 120)]
    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value, validate: true);
    }

    [StringLength(8, MinimumLength = 3)]
    public string? Code
    {
        get => _code;
        set => SetProperty(ref _code, value, validate: true);
    }

    [MinLength(3)]
    public string? Minimum
    {
        get => _minimum;
        set => SetProperty(ref _minimum, value, validate: true);
    }

    [MaxLength(5)]
    public string? Maximum
    {
        get => _maximum;
        set => SetProperty(ref _maximum, value, validate: true);
    }

    [RegularExpression("^[0-9]+$")]
    public string? Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value, validate: true);
    }

    [EmailAddress]
    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value, validate: true);
    }

    [Phone]
    public string? Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value, validate: true);
    }

    [Url]
    public string? Url
    {
        get => _url;
        set => SetProperty(ref _url, value, validate: true);
    }

    [CreditCard]
    public string? CreditCard
    {
        get => _creditCard;
        set => SetProperty(ref _creditCard, value, validate: true);
    }

    public string? Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    [Compare(nameof(Password))]
    public string? Confirmation
    {
        get => _confirmation;
        set => SetProperty(ref _confirmation, value, validate: true);
    }

    [CustomValidation(typeof(GeneratedValidationRules), nameof(GeneratedValidationRules.ValidateUserName))]
    public string? UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value, validate: true);
    }
}

internal static class GeneratedValidationRules
{
    public static ValidationResult? ValidateUserName(
        string? value,
        ValidationContext context)
    {
        if (value == "throw")
        {
            throw new InvalidOperationException("validator failed");
        }

        return value == "blocked"
            ? new ValidationResult("The user name is blocked.", [context.MemberName!])
            : ValidationResult.Success;
    }
}
