namespace AtomUI.City.Security;

/// <summary>
/// Represents command authorization context.
/// </summary>
public sealed class CommandAuthorizationContext
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandAuthorizationContext</c> type.
    /// </summary>
    public CommandAuthorizationContext(
        string commandId,
        string? resourceName = null,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        ValidateOptional(resourceName, nameof(resourceName));
        ValidateOptional(contributionId, nameof(contributionId));

        CommandId = commandId;
        ResourceName = resourceName;
        ContributionId = contributionId;
    }

    /// <summary>
    /// Gets command id.
    /// </summary>
    public string CommandId { get; }

    /// <summary>
    /// Gets resource name.
    /// </summary>
    public string? ResourceName { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }

    private static void ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }
    }
}
