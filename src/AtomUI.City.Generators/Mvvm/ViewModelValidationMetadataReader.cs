using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AtomUI.City.Generators.Mvvm;

internal static class ViewModelValidationMetadataReader
{
    private const string ValidatingViewModelBaseName = "AtomUI.City.Mvvm.ValidatingViewModelBase";
    private const string ValidationAttributeName = "System.ComponentModel.DataAnnotations.ValidationAttribute";
    private const string ValidationContextName = "System.ComponentModel.DataAnnotations.ValidationContext";
    private const string ValidationResultName = "System.ComponentModel.DataAnnotations.ValidationResult";
    private const string CompareAttributeName = "System.ComponentModel.DataAnnotations.CompareAttribute";
    private const string CustomValidationAttributeName = "System.ComponentModel.DataAnnotations.CustomValidationAttribute";

    private static readonly HashSet<string> SupportedAttributeNames = new(StringComparer.Ordinal)
    {
        "System.ComponentModel.DataAnnotations.RequiredAttribute",
        "System.ComponentModel.DataAnnotations.RangeAttribute",
        "System.ComponentModel.DataAnnotations.StringLengthAttribute",
        "System.ComponentModel.DataAnnotations.MinLengthAttribute",
        "System.ComponentModel.DataAnnotations.MaxLengthAttribute",
        "System.ComponentModel.DataAnnotations.RegularExpressionAttribute",
        "System.ComponentModel.DataAnnotations.EmailAddressAttribute",
        "System.ComponentModel.DataAnnotations.PhoneAttribute",
        "System.ComponentModel.DataAnnotations.UrlAttribute",
        "System.ComponentModel.DataAnnotations.CreditCardAttribute",
        CompareAttributeName,
        CustomValidationAttributeName,
    };

    public static ViewModelValidationMetadata? TryRead(
        GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax declaration ||
            context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type ||
            !DerivesFrom(type, ValidatingViewModelBaseName))
        {
            return null;
        }

        var properties = new List<ViewModelValidationProperty>();
        var issues = new List<ViewModelValidationIssue>();

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>()
                     .Where(static property => !property.IsStatic && !property.IsIndexer)
                     .OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
            var validationAttributes = property.GetAttributes()
                .Where(static attribute => IsValidationAttribute(attribute.AttributeClass))
                .ToArray();
            if (validationAttributes.Length == 0)
            {
                continue;
            }

            var rules = new List<ViewModelValidationRule>(validationAttributes.Length);
            foreach (var attribute in validationAttributes)
            {
                var attributeName = GetTypeName(attribute.AttributeClass);
                var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
                    property.Locations.FirstOrDefault();
                if (attributeName is null || !SupportedAttributeNames.Contains(attributeName))
                {
                    issues.Add(new ViewModelValidationIssue(
                        $"Validation attribute '{attributeName ?? "<unknown>"}' on '{type.ToDisplayString()}.{property.Name}' is not supported by AOT-first generated validation.",
                        location));
                    continue;
                }

                if (UsesResourceBackedErrorMessage(attribute))
                {
                    issues.Add(new ViewModelValidationIssue(
                        $"Validation attribute '{attributeName}' on '{type.ToDisplayString()}.{property.Name}' uses resource-backed error lookup, which is not supported by the reflection-free validation generator.",
                        location));
                    continue;
                }

                switch (attributeName)
                {
                    case CompareAttributeName:
                        ReadCompareRule(type, property, attribute, location, rules, issues);
                        break;
                    case CustomValidationAttributeName:
                        ReadCustomValidationRule(
                            context.SemanticModel.Compilation,
                            type,
                            property,
                            attribute,
                            location,
                            rules,
                            issues);
                        break;
                    default:
                        rules.Add(new ViewModelValidationRule(attribute, ViewModelValidationRuleKind.Attribute));
                        break;
                }
            }

            if (rules.Count > 0)
            {
                properties.Add(new ViewModelValidationProperty(property, rules));
            }
        }

        if (properties.Count == 0 && issues.Count == 0)
        {
            return null;
        }

        var declarationLocation = declaration.Identifier.GetLocation();
        if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            issues.Add(new ViewModelValidationIssue(
                $"Validation ViewModel '{type.ToDisplayString()}' must be declared partial so City can generate its validators.",
                declarationLocation));
        }
        if (type.ContainingType is not null)
        {
            issues.Add(new ViewModelValidationIssue(
                $"Nested validation ViewModel '{type.ToDisplayString()}' is not supported; declare it as a top-level partial class.",
                declarationLocation));
        }
        if (type.TypeParameters.Length > 0)
        {
            issues.Add(new ViewModelValidationIssue(
                $"Generic validation ViewModel '{type.ToDisplayString()}' is not supported by the generated validation contract.",
                declarationLocation));
        }

        return new ViewModelValidationMetadata(type, properties, issues, declarationLocation);
    }

    private static void ReadCompareRule(
        INamedTypeSymbol ownerType,
        IPropertySymbol property,
        AttributeData attribute,
        Location? location,
        ICollection<ViewModelValidationRule> rules,
        ICollection<ViewModelValidationIssue> issues)
    {
        var comparedName = attribute.ConstructorArguments.FirstOrDefault().Value as string;
        var comparedProperty = string.IsNullOrWhiteSpace(comparedName)
            ? null
            : FindProperty(ownerType, comparedName!);
        if (comparedProperty is null || comparedProperty.IsStatic || comparedProperty.IsIndexer)
        {
            issues.Add(new ViewModelValidationIssue(
                $"Compare validation on '{ownerType.ToDisplayString()}.{property.Name}' references missing or unsupported property '{comparedName}'.",
                location));
            return;
        }

        rules.Add(new ViewModelValidationRule(
            attribute,
            ViewModelValidationRuleKind.Compare,
            comparedProperty: comparedProperty));
    }

    private static void ReadCustomValidationRule(
        Compilation compilation,
        INamedTypeSymbol ownerType,
        IPropertySymbol property,
        AttributeData attribute,
        Location? location,
        ICollection<ViewModelValidationRule> rules,
        ICollection<ViewModelValidationIssue> issues)
    {
        if (attribute.ConstructorArguments.Length < 2 ||
            attribute.ConstructorArguments[0].Value is not INamedTypeSymbol validatorType ||
            attribute.ConstructorArguments[1].Value is not string methodName ||
            string.IsNullOrWhiteSpace(methodName))
        {
            issues.Add(new ViewModelValidationIssue(
                $"CustomValidation on '{ownerType.ToDisplayString()}.{property.Name}' has invalid validator metadata.",
                location));
            return;
        }

        var validationResult = compilation.GetTypeByMetadataName(ValidationResultName);
        var candidates = validatorType.GetMembers(methodName)
            .OfType<IMethodSymbol>()
            .Where(method => method.IsStatic && !method.IsGenericMethod)
            .Where(method => compilation.IsSymbolAccessibleWithin(method, ownerType))
            .Where(method => validationResult is not null &&
                compilation.ClassifyConversion(method.ReturnType, validationResult).IsImplicit)
            .Where(method => IsSupportedCustomValidationSignature(compilation, property.Type, method))
            .OrderByDescending(static method => method.Parameters.Length)
            .ToArray();

        if (candidates.Length != 1)
        {
            issues.Add(new ViewModelValidationIssue(
                $"CustomValidation method '{validatorType.ToDisplayString()}.{methodName}' for '{ownerType.ToDisplayString()}.{property.Name}' must resolve to exactly one accessible static ValidationResult method with (value) or (value, ValidationContext) parameters.",
                location));
            return;
        }

        rules.Add(new ViewModelValidationRule(
            attribute,
            ViewModelValidationRuleKind.CustomValidation,
            customMethod: candidates[0]));
    }

    private static bool IsSupportedCustomValidationSignature(
        Compilation compilation,
        ITypeSymbol propertyType,
        IMethodSymbol method)
    {
        if (method.Parameters.Length is < 1 or > 2 ||
            !compilation.ClassifyConversion(propertyType, method.Parameters[0].Type).IsImplicit)
        {
            return false;
        }

        return method.Parameters.Length == 1 ||
            string.Equals(GetTypeName(method.Parameters[1].Type), ValidationContextName, StringComparison.Ordinal);
    }

    private static bool UsesResourceBackedErrorMessage(AttributeData attribute)
    {
        return attribute.NamedArguments.Any(argument =>
            (string.Equals(argument.Key, "ErrorMessageResourceType", StringComparison.Ordinal) &&
             argument.Value.Value is not null) ||
            (string.Equals(argument.Key, "ErrorMessageResourceName", StringComparison.Ordinal) &&
             argument.Value.Value is string value && !string.IsNullOrWhiteSpace(value)));
    }

    private static IPropertySymbol? FindProperty(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault();
            if (property is not null)
            {
                return property;
            }
        }
        return null;
    }

    private static bool DerivesFrom(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(GetTypeName(current), metadataName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsValidationAttribute(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(GetTypeName(current), ValidationAttributeName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string? GetTypeName(ITypeSymbol? type)
    {
        return type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }
}
