using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.Mvvm;

internal sealed class ViewModelValidationMetadata
{
    public ViewModelValidationMetadata(
        INamedTypeSymbol type,
        IReadOnlyList<ViewModelValidationProperty> properties,
        IReadOnlyList<ViewModelValidationIssue> issues,
        Location? location)
    {
        Type = type;
        Properties = properties;
        Issues = issues;
        Location = location;
    }

    public INamedTypeSymbol Type { get; }
    public IReadOnlyList<ViewModelValidationProperty> Properties { get; }
    public IReadOnlyList<ViewModelValidationIssue> Issues { get; }
    public Location? Location { get; }
}

internal sealed class ViewModelValidationProperty
{
    public ViewModelValidationProperty(
        IPropertySymbol property,
        IReadOnlyList<ViewModelValidationRule> rules)
    {
        Property = property;
        Rules = rules;
    }

    public IPropertySymbol Property { get; }
    public IReadOnlyList<ViewModelValidationRule> Rules { get; }
}

internal sealed class ViewModelValidationRule
{
    public ViewModelValidationRule(
        AttributeData attribute,
        ViewModelValidationRuleKind kind,
        IPropertySymbol? comparedProperty = null,
        IMethodSymbol? customMethod = null)
    {
        Attribute = attribute;
        Kind = kind;
        ComparedProperty = comparedProperty;
        CustomMethod = customMethod;
    }

    public AttributeData Attribute { get; }
    public ViewModelValidationRuleKind Kind { get; }
    public IPropertySymbol? ComparedProperty { get; }
    public IMethodSymbol? CustomMethod { get; }
}

internal sealed class ViewModelValidationIssue
{
    public ViewModelValidationIssue(string message, Location? location)
    {
        Message = message;
        Location = location;
    }

    public string Message { get; }
    public Location? Location { get; }
}

internal enum ViewModelValidationRuleKind
{
    Attribute,
    Compare,
    CustomValidation,
}
