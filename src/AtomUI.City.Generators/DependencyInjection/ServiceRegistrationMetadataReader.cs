using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.DependencyInjection;

internal static class ServiceRegistrationMetadataReader
{
    private const string ExposeServicesAttributeName = "AtomUI.City.Core.DependencyInjection.ExposeServicesAttribute";
    private const string ScopedDependencyInterfaceName = "AtomUI.City.Core.DependencyInjection.IScopedDependency";
    private const string ScopedServiceAttributeName = "AtomUI.City.Core.DependencyInjection.ScopedServiceAttribute";
    private const string ServiceAttributeName = "AtomUI.City.Core.DependencyInjection.ServiceAttribute";
    private const string SingletonDependencyInterfaceName = "AtomUI.City.Core.DependencyInjection.ISingletonDependency";
    private const string TransientDependencyInterfaceName = "AtomUI.City.Core.DependencyInjection.ITransientDependency";

    public static ServiceRegistrationMetadata? TryRead(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
        {
            return null;
        }

        var implementationTypeName = GetTypeName(type);
        if (!TryReadServiceDeclaration(type, out var serviceDeclaration))
        {
            return null;
        }

        if (serviceDeclaration is null)
        {
            serviceDeclaration = ReadMarkerInterfaceDeclaration(type);
        }

        if (serviceDeclaration is null)
        {
            return null;
        }

        var exposedAttributeServiceTypeNames = ReadExposedServiceTypeNames(type);
        if (exposedAttributeServiceTypeNames is null)
        {
            return null;
        }

        var exposedServiceTypeNames = exposedAttributeServiceTypeNames
            .Concat(serviceDeclaration.ExposedServiceTypeNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (exposedServiceTypeNames.Length == 0)
        {
            exposedServiceTypeNames = [implementationTypeName];
        }

        return new ServiceRegistrationMetadata(
            implementationTypeName,
            serviceDeclaration.Lifetime,
            exposedServiceTypeNames,
            serviceDeclaration.Replace,
            serviceDeclaration.TryAdd,
            serviceDeclaration.Key,
            type.AllInterfaces.Any(@interface =>
                string.Equals(GetTypeName(@interface), "System.IDisposable", StringComparison.Ordinal) ||
                string.Equals(GetTypeName(@interface), "System.IAsyncDisposable", StringComparison.Ordinal)));
    }

    private static bool TryReadServiceDeclaration(
        INamedTypeSymbol type,
        out ServiceDeclaration? declaration)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attributeName = GetAttributeTypeName(attribute);

            if (string.Equals(attributeName, ServiceAttributeName, StringComparison.Ordinal))
            {
                var lifetime = ReadLifetime(attribute);
                if (lifetime is null)
                {
                    declaration = null;
                    return false;
                }

                declaration = new ServiceDeclaration(
                    lifetime.Value,
                    [],
                    ReadNamedBoolean(attribute, "Replace"),
                    ReadNamedBoolean(attribute, "TryAdd"),
                    ReadNamedString(attribute, "Key"));
                return true;
            }

            if (string.Equals(attributeName, ScopedServiceAttributeName, StringComparison.Ordinal))
            {
                var exposedServiceTypeNames = ReadConstructorServiceTypes(attribute);
                if (exposedServiceTypeNames is null)
                {
                    declaration = null;
                    return false;
                }

                declaration = new ServiceDeclaration(
                    ServiceRegistrationLifetime.Scoped,
                    exposedServiceTypeNames,
                    ReadNamedBoolean(attribute, "Replace"),
                    ReadNamedBoolean(attribute, "TryAdd"),
                    ReadNamedString(attribute, "Key"));
                return true;
            }
        }

        declaration = null;
        return true;
    }

    private static ServiceDeclaration? ReadMarkerInterfaceDeclaration(INamedTypeSymbol type)
    {
        foreach (var @interface in type.AllInterfaces)
        {
            var interfaceName = GetTypeName(@interface);

            if (string.Equals(interfaceName, SingletonDependencyInterfaceName, StringComparison.Ordinal))
            {
                return new ServiceDeclaration(ServiceRegistrationLifetime.Singleton, [], replace: false, tryAdd: false, key: null);
            }

            if (string.Equals(interfaceName, ScopedDependencyInterfaceName, StringComparison.Ordinal))
            {
                return new ServiceDeclaration(ServiceRegistrationLifetime.Scoped, [], replace: false, tryAdd: false, key: null);
            }

            if (string.Equals(interfaceName, TransientDependencyInterfaceName, StringComparison.Ordinal))
            {
                return new ServiceDeclaration(ServiceRegistrationLifetime.Transient, [], replace: false, tryAdd: false, key: null);
            }
        }

        return null;
    }

    private static IReadOnlyList<string>? ReadExposedServiceTypeNames(INamedTypeSymbol type)
    {
        var serviceTypeNames = new List<string>();
        foreach (var attribute in type.GetAttributes().Where(attribute =>
                     string.Equals(
                         GetAttributeTypeName(attribute),
                         ExposeServicesAttributeName,
                         StringComparison.Ordinal)))
        {
            var attributeServiceTypeNames = ReadConstructorServiceTypes(attribute);
            if (attributeServiceTypeNames is null)
            {
                return null;
            }

            serviceTypeNames.AddRange(attributeServiceTypeNames);
        }

        return serviceTypeNames;
    }

    private static IReadOnlyList<string>? ReadConstructorServiceTypes(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
        {
            return [];
        }

        return ReadTypeNames(attribute.ConstructorArguments[0]);
    }

    private static IReadOnlyList<string>? ReadTypeNames(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return null;
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            if (constant.Values.IsDefault)
            {
                return null;
            }

            var typeNames = new List<string>(constant.Values.Length);
            foreach (var value in constant.Values)
            {
                var typeName = ReadTypeName(value);
                if (typeName is null)
                {
                    return null;
                }

                typeNames.Add(typeName);
            }

            return typeNames;
        }

        var singleTypeName = ReadTypeName(constant);

        return singleTypeName is null ? null : [singleTypeName];
    }

    private static string? ReadTypeName(TypedConstant constant)
    {
        return constant.Value is INamedTypeSymbol type ? GetTypeName(type) : null;
    }

    private static ServiceRegistrationLifetime? ReadLifetime(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
        {
            return null;
        }

        var value = attribute.ConstructorArguments[0].Value;

        return value switch
        {
            0 => ServiceRegistrationLifetime.Singleton,
            1 => ServiceRegistrationLifetime.Scoped,
            2 => ServiceRegistrationLifetime.Transient,
            _ => null,
        };
    }

    private static bool ReadNamedBoolean(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
            {
                return argument.Value.Value is true;
            }
        }

        return false;
    }

    private static string? ReadNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
            {
                return argument.Value.Value as string;
            }
        }

        return null;
    }

    private static string? GetAttributeTypeName(AttributeData attribute)
    {
        return attribute.AttributeClass is null ? null : GetTypeName(attribute.AttributeClass);
    }

    private static string GetTypeName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private sealed class ServiceDeclaration
    {
        public ServiceDeclaration(
            ServiceRegistrationLifetime lifetime,
            IReadOnlyList<string> exposedServiceTypeNames,
            bool replace,
            bool tryAdd,
            string? key)
        {
            Lifetime = lifetime;
            ExposedServiceTypeNames = exposedServiceTypeNames;
            Replace = replace;
            TryAdd = tryAdd;
            Key = key;
        }

        public ServiceRegistrationLifetime Lifetime { get; }

        public IReadOnlyList<string> ExposedServiceTypeNames { get; }

        public bool Replace { get; }

        public bool TryAdd { get; }

        public string? Key { get; }
    }
}
