namespace AtomUI.City.Data;

/// <summary>
/// Defines the contract for idata credential provider.
/// </summary>
public interface IDataCredentialProvider
{
    /// <summary>
    /// Executes the get credential async operation.
    /// </summary>
    ValueTask<DataCredentialResult> GetCredentialAsync(
        DataAuthenticationContext context,
        CancellationToken cancellationToken = default);
}
