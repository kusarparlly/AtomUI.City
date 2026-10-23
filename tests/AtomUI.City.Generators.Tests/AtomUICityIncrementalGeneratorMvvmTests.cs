using System.ComponentModel.DataAnnotations;
using AtomUI.City.Generators;
using AtomUI.City.Generators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AtomUI.City.Generators.Tests;

public sealed class AtomUICityIncrementalGeneratorMvvmTests
{
    private const string RuntimeContract = """
        namespace AtomUI.City.Mvvm
        {
            public abstract class ValidatingViewModelBase
            {
                protected static void ValidateAttribute(
                    System.Collections.Generic.ICollection<string> errors,
                    object? value,
                    string displayName,
                    System.ComponentModel.DataAnnotations.ValidationAttribute attribute) { }

                protected static void ValidateResult(
                    System.Collections.Generic.ICollection<string> errors,
                    System.ComponentModel.DataAnnotations.ValidationResult? result) { }

                protected void ValidateProperty(string propertyName, object? value) { }

                protected virtual void ValidateGeneratedProperty(
                    string propertyName,
                    object? value,
                    System.Collections.Generic.ICollection<string> errors) { }

                protected virtual void ValidateAllGeneratedProperties() { }
            }
        }
        """;

    [Fact]
    public void GeneratesStaticRulesCompareAndCustomValidation()
    {
        var compilation = CreateCompilation(RuntimeContract + """

            namespace Sample.App
            {
                public static class AccountRules
                {
                    public static System.ComponentModel.DataAnnotations.ValidationResult? ValidateName(
                        string? value,
                        System.ComponentModel.DataAnnotations.ValidationContext context)
                    {
                        return value == "blocked"
                            ? new System.ComponentModel.DataAnnotations.ValidationResult("Blocked")
                            : System.ComponentModel.DataAnnotations.ValidationResult.Success;
                    }
                }

                public sealed partial class AccountViewModel : AtomUI.City.Mvvm.ValidatingViewModelBase
                {
                    [System.ComponentModel.DataAnnotations.Required]
                    [System.ComponentModel.DataAnnotations.StringLength(12, MinimumLength = 3)]
                    [System.ComponentModel.DataAnnotations.CustomValidation(typeof(AccountRules), nameof(AccountRules.ValidateName))]
                    public string? Name { get; set; }

                    public string? Confirmation { get; set; }

                    [System.ComponentModel.DataAnnotations.Compare(nameof(Confirmation))]
                    public string? RepeatedConfirmation { get; set; }
                }
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new AtomUICityIncrementalGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        var result = driver.GetRunResult();
        var generated = Assert.Single(
                Assert.Single(result.Results).GeneratedSources,
                source => source.SourceText.ToString().Contains("partial class AccountViewModel", StringComparison.Ordinal))
            .SourceText
            .ToString();

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain(
            outputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Contains("new global::System.ComponentModel.DataAnnotations.RequiredAttribute()", generated, StringComparison.Ordinal);
        Assert.Contains("new global::System.ComponentModel.DataAnnotations.StringLengthAttribute(12)", generated, StringComparison.Ordinal);
        Assert.Contains("MinimumLength = 3", generated, StringComparison.Ordinal);
        Assert.Contains("global::System.Object.Equals(this.RepeatedConfirmation, this.Confirmation)", generated, StringComparison.Ordinal);
        Assert.Contains("global::Sample.App.AccountRules.ValidateName(this.Name", generated, StringComparison.Ordinal);
        Assert.Contains("ValidateProperty(nameof(Name), this.Name)", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownValidationAttributeProducesAucgen009()
    {
        var compilation = CreateCompilation(RuntimeContract + """

            namespace Sample.App
            {
                public sealed class UnknownRuleAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
                {
                }

                public sealed partial class AccountViewModel : AtomUI.City.Mvvm.ValidatingViewModelBase
                {
                    [UnknownRule]
                    public string? Name { get; set; }
                }
            }
            """);
        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        var diagnostic = Assert.Single(Assert.Single(result.Results).Diagnostics);
        Assert.Equal(GeneratorDiagnosticIds.InvalidViewModelValidation, diagnostic.Id);
        Assert.Contains("UnknownRuleAttribute", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.True(diagnostic.Location.IsInSource);
    }

    [Fact]
    public void NonPartialValidationViewModelProducesAucgen009()
    {
        var compilation = CreateCompilation(RuntimeContract + """

            namespace Sample.App
            {
                public sealed class AccountViewModel : AtomUI.City.Mvvm.ValidatingViewModelBase
                {
                    [System.ComponentModel.DataAnnotations.Required]
                    public string? Name { get; set; }
                }
            }
            """);
        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        var diagnostic = Assert.Single(Assert.Single(result.Results).Diagnostics);
        Assert.Equal(GeneratorDiagnosticIds.InvalidViewModelValidation, diagnostic.Id);
        Assert.Contains("partial", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(ValidationAttribute).Assembly.Location))
            .DistinctBy(reference => reference.Display)
            .ToArray();

        return CSharpCompilation.Create(
            "Sample.App",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}
