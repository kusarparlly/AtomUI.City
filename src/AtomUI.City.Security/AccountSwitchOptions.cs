namespace AtomUI.City.Security;

public sealed class AccountSwitchOptions
{
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

    public bool AllowOffline { get; }

    public string? CredentialResourceName { get; }
}
