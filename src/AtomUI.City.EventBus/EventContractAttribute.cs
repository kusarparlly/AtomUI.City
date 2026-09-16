using AtomUI.City.Core.Modularity;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event contract.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventContractAttribute : Attribute
{
    private int _schemaVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventContractAttribute"/> type.
    /// </summary>
    public EventContractAttribute(string contractId, Type ownerModuleType)
    {
        ContractId = EventAttributeValidation.ValidateName(contractId, nameof(contractId));
        OwnerModuleType = EventAttributeValidation.ValidateOwner(ownerModuleType, nameof(ownerModuleType));
    }

    /// <summary>
    /// Gets contract id.
    /// </summary>
    public string ContractId { get; }

    /// <summary>
    /// Gets owner module type.
    /// </summary>
    public Type OwnerModuleType { get; }

    /// <summary>
    /// Represents the schema version value.
    /// </summary>
    public int SchemaVersion
    {
        get => _schemaVersion;
        init => _schemaVersion = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Schema version must be greater than zero.");
    }
}

internal static class EventAttributeValidation
{
    public static string ValidateName(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("The value must be non-empty, trimmed, and contain no control characters.", parameterName);
        }

        return value;
    }

    public static Type ValidateOwner(Type ownerModuleType, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ownerModuleType, parameterName);
        if (!typeof(IModule).IsAssignableFrom(ownerModuleType) ||
            ownerModuleType.IsAbstract || ownerModuleType.IsInterface || ownerModuleType.ContainsGenericParameters)
        {
            throw new ArgumentException("The owner type must be a closed, concrete IModule implementation.", parameterName);
        }

        return ownerModuleType;
    }
}
