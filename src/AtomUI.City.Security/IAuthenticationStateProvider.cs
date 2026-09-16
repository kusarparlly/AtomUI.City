namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for iauthentication state provider.
/// </summary>
public interface IAuthenticationStateProvider
{
    /// <summary>
    /// Gets current.
    /// </summary>
    AuthenticationStateSnapshot Current { get; }

    /// <summary>
    /// Occurs when state changed.
    /// </summary>
    event EventHandler<AuthenticationStateChangedEventArgs>? StateChanged;
}
