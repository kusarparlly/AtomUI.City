using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.Data;

internal static class DataClientMetadataReader
{
    private const string ClientAttribute = "AtomUI.City.Data.DataClientAttribute";
    private const string OperationAttribute = "AtomUI.City.Data.DataOperationAttribute";
    private const string CancellationTokenType = "System.Threading.CancellationToken";

    public static DataClientGenerationMetadata? TryRead(INamedTypeSymbol symbol)
    {
        if (symbol is null)
        {
            throw new ArgumentNullException(nameof(symbol));
        }
        var clientAttribute = symbol.GetAttributes().FirstOrDefault(attribute => Is(attribute, ClientAttribute));
        if (clientAttribute is null)
        {
            return null;
        }

        var issues = new List<string>();
        var diagnosticTypeName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var typeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!IsPublicNonGeneric(symbol))
        {
            issues.Add($"Data client '{diagnosticTypeName}' and all of its containing types must be public and non-generic.");
        }

        var clientId = ReadString(clientAttribute, 0);
        var transportKind = ReadInt(clientAttribute, 1, -1);
        var version = ReadNamedString(clientAttribute, "Version") ?? "1";
        if (string.IsNullOrWhiteSpace(clientId))
        {
            issues.Add($"Data client '{diagnosticTypeName}' must declare a non-empty client id.");
        }

        if (transportKind is < 0 or > 2)
        {
            issues.Add($"Data client '{diagnosticTypeName}' declares an unsupported transport kind.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            issues.Add($"Data client '{diagnosticTypeName}' must declare a non-empty version.");
        }

        var operations = new List<DataOperationGenerationMetadata>();
        foreach (var method in GetOperationMethods(symbol))
        {
            var operationAttribute = method.GetAttributes().FirstOrDefault(attribute => Is(attribute, OperationAttribute));
            if (operationAttribute is null)
            {
                continue;
            }

            var operationName = ReadString(operationAttribute, 0);
            var accessMode = ReadInt(operationAttribute, 1, 0);
            var concurrencyPolicy = ReadNamedInt(operationAttribute, "ConcurrencyPolicy", 0);
            var timeout = ReadNamedInt(operationAttribute, "TimeoutMilliseconds", 0);
            var retries = ReadNamedInt(operationAttribute, "MaxRetryAttempts", 0);
            var cacheEnabled = ReadNamedBool(operationAttribute, "CacheEnabled");
            var auth = ReadNamedString(operationAttribute, "AuthenticationPolicy") ?? "Anonymous";
            var payloadParameters = method.Parameters
                .Where(parameter => !string.Equals(
                    parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    CancellationTokenType,
                    StringComparison.Ordinal))
                .ToArray();
            var cancellationParameters = method.Parameters
                .Where(parameter => string.Equals(
                    parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    CancellationTokenType,
                    StringComparison.Ordinal))
                .ToArray();
            var requestType = payloadParameters.FirstOrDefault()
                ?.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                ?? "global::System.ValueTuple";
            var responseType = UnwrapResponseType(method.ReturnType);

            if (method.MethodKind != MethodKind.Ordinary
                || method.DeclaredAccessibility != Accessibility.Public
                || method.IsStatic
                || method.Arity != 0)
            {
                issues.Add($"Data operation '{diagnosticTypeName}.{method.Name}' must be a public, instance, non-generic ordinary method.");
            }

            if (payloadParameters.Length > 1
                || cancellationParameters.Length > 1
                || (cancellationParameters.Length == 1
                    && !SymbolEqualityComparer.Default.Equals(method.Parameters.Last().Type, cancellationParameters[0].Type)))
            {
                issues.Add($"Data operation '{diagnosticTypeName}.{method.Name}' may have one payload parameter and one trailing CancellationToken.");
            }

            if (string.IsNullOrWhiteSpace(operationName))
            {
                issues.Add($"Data operation '{diagnosticTypeName}.{method.Name}' must declare a non-empty operation name.");
            }

            if (responseType is null)
            {
                issues.Add($"Data operation '{diagnosticTypeName}.{method.Name}' must return DataResult<T>, Task<DataResult<T>>, ValueTask<DataResult<T>>, or IDataStream<T>.");
                continue;
            }

            if (accessMode is < 0 or > 4 || concurrencyPolicy is < 0 or > 5 || timeout < 0 || retries < 0)
            {
                issues.Add($"Data operation '{diagnosticTypeName}.{method.Name}' declares invalid policy metadata.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(auth))
            {
                issues.Add($"Data operation '{diagnosticTypeName}.{method.Name}' must declare a non-empty authentication policy.");
            }

            if (cacheEnabled && accessMode != 0)
            {
                issues.Add($"Data operation '{diagnosticTypeName}.{method.Name}' can enable request caching only for query access mode.");
            }

            operations.Add(new DataOperationGenerationMetadata(
                operationName!,
                requestType,
                responseType,
                accessMode,
                concurrencyPolicy,
                timeout,
                retries,
                cacheEnabled,
                auth));
        }

        if (operations.GroupBy(static operation => operation.OperationName, StringComparer.Ordinal).Any(static group => group.Count() > 1))
        {
            issues.Add($"Data client '{diagnosticTypeName}' contains duplicate operation names.");
        }

        return new DataClientGenerationMetadata(
            typeName,
            clientId ?? string.Empty,
            transportKind,
            version,
            operations,
            issues,
            symbol.Locations.FirstOrDefault());
    }

    private static IEnumerable<IMethodSymbol> GetOperationMethods(INamedTypeSymbol symbol)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (seen.Add(method))
            {
                yield return method;
            }
        }

        if (symbol.TypeKind != TypeKind.Interface)
        {
            yield break;
        }

        foreach (var inheritedInterface in symbol.AllInterfaces.OrderBy(
                     static item => item.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                     StringComparer.Ordinal))
        {
            foreach (var method in inheritedInterface.GetMembers().OfType<IMethodSymbol>())
            {
                if (seen.Add(method))
                {
                    yield return method;
                }
            }
        }
    }

    private static string? UnwrapResponseType(ITypeSymbol returnType)
    {
        var current = returnType;
        if (current is INamedTypeSymbol asyncType
            && asyncType.TypeArguments.Length == 1
            && asyncType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) is
                "System.Threading.Tasks.Task<TResult>" or
                "System.Threading.Tasks.ValueTask<TResult>")
        {
            current = asyncType.TypeArguments[0];
        }

        if (current is not INamedTypeSymbol resultType || resultType.TypeArguments.Length != 1)
        {
            return null;
        }

        var definition = resultType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return definition is "AtomUI.City.Data.DataResult<T>" or "AtomUI.City.Data.IDataStream<T>"
            ? resultType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;
    }

    private static bool IsPublicNonGeneric(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0 || current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Is(AttributeData attribute, string name) =>
        string.Equals(
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            name,
            StringComparison.Ordinal);

    private static string? ReadString(AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as string
            : null;

    private static int ReadInt(AttributeData attribute, int index, int fallback) =>
        attribute.ConstructorArguments.Length > index && attribute.ConstructorArguments[index].Value is int value
            ? value
            : fallback;

    private static string? ReadNamedString(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;

    private static int ReadNamedInt(AttributeData attribute, string name, int fallback)
    {
        var value = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value;
        return value is int integer ? integer : fallback;
    }

    private static bool ReadNamedBool(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value is true;
}
