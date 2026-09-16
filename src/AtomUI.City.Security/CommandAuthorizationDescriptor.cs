namespace AtomUI.City.Security;

/// <summary>
/// Represents command authorization descriptor.
/// </summary>
public sealed class CommandAuthorizationDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandAuthorizationDescriptor</c> type.
    /// </summary>
    public CommandAuthorizationDescriptor(
        string commandId,
        AuthorizationPolicy policy,
        CommandUnauthorizedBehavior unauthorizedBehavior = CommandUnauthorizedBehavior.Disable,
        string? deniedMessageKey = null,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(policy);

        if (!Enum.IsDefined(unauthorizedBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unauthorizedBehavior),
                unauthorizedBehavior,
                "Command unauthorized behavior must be defined.");
        }

        if (contributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        }

        if (contributionId is not null
            && policy.ContributionId is not null
            && !string.Equals(contributionId, policy.ContributionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A command descriptor and its policy cannot belong to different contributions.",
                nameof(contributionId));
        }

        if (deniedMessageKey is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(deniedMessageKey);
        }

        CommandId = commandId;
        Policy = policy;
        UnauthorizedBehavior = unauthorizedBehavior;
        DeniedMessageKey = deniedMessageKey;
        ContributionId = contributionId ?? policy.ContributionId;
    }

    /// <summary>
    /// Gets command id.
    /// </summary>
    public string CommandId { get; }

    /// <summary>
    /// Gets policy.
    /// </summary>
    public AuthorizationPolicy Policy { get; }

    /// <summary>
    /// Gets unauthorized behavior.
    /// </summary>
    public CommandUnauthorizedBehavior UnauthorizedBehavior { get; }

    /// <summary>
    /// Gets denied message key.
    /// </summary>
    public string? DeniedMessageKey { get; }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string? ContributionId { get; }
}
