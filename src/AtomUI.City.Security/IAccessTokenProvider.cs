namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for iaccess token provider.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Executes the get token async operation.
    /// </summary>
    ValueTask<AccessTokenResult> GetTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken = default);
}
