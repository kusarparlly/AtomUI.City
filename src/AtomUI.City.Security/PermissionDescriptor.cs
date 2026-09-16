namespace AtomUI.City.Security;

/// <summary>
/// Represents permission descriptor.
/// </summary>
public sealed class PermissionDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>PermissionDescriptor</c> type.
    /// </summary>
    public PermissionDescriptor(
        string name,
        string? displayNameKey = null,
        string? descriptionKey = null,
        string? category = null,
        string? contributionId = null,
        string? defaultPolicy = null,
        bool isHostOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ValidateOptionalIdentifier(displayNameKey, nameof(displayNameKey));
        ValidateOptionalIdentifier(descriptionKey, nameof(descriptionKey));
        ValidateOptionalIdentifier(category, nameof(category));
        ValidateOptionalIdentifier(contributionId, nameof(contributionId));
        ValidateOptionalIdentifier(defaultPolicy, nameof(defaultPolicy));

        Name = name;
        DisplayNameKey = displayNameKey;
        DescriptionKey = descriptionKey;
        Category = category;
        ContributionId = contributionId;
        DefaultPolicy = defaultPolicy;
        IsHostOnly = isHostOnly;
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets display name key.
    /// </summary>
    public string? DisplayNameKey { get; }

    /// <summary>
    /// Gets description key.
    /// </summary>
    public string? DescriptionKey { get; }

    /// <summary>
    /// Gets category.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    /// <summary>
    /// Gets default policy.
    /// </summary>
    public string? DefaultPolicy { get; }

    /// <summary>
    /// Gets a value indicating whether is host only.
    /// </summary>
    public bool IsHostOnly { get; }

    private static void ValidateOptionalIdentifier(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }
    }
}
