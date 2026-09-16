namespace AtomUI.City.Security;

/// <summary>
/// Represents account switch options.
/// </summary>
public sealed class AccountSwitchOptions
{
    /// <summary>
    /// Initializes a new instance of the <c>AccountSwitchOptions</c> type.
    /// </summary>
    public AccountSwitchOptions(
        bool allowOffline = false,
        string? credentialResourceName = null)
    {
        if (credentialResourceName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(credentialResourceName);
        }

        AllowOffline = allowOffline;
        CredentialResourceName = credentialResourceName?.Trim();
    }

    /// <summary>
    /// Gets allow offline.
    /// </summary>
    public bool AllowOffline { get; }

    /// <summary>
    /// Gets credential resource name.
    /// </summary>
    public string? CredentialResourceName { get; }
}
