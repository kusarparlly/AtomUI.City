namespace AtomUI.City.Security;

/// <summary>
/// Defines the supported command authorization change reason values.
/// </summary>
public enum CommandAuthorizationChangeReason
{
    /// <summary>
    /// Represents the authentication state changed value.
    /// </summary>
    AuthenticationStateChanged,
    /// <summary>
    /// Represents the permission changed value.
    /// </summary>
    PermissionChanged,
    /// <summary>
    /// Represents the descriptor changed value.
    /// </summary>
    DescriptorChanged,
}
