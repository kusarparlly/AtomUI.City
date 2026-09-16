namespace AtomUI.City.Data;

/// <summary>
/// Defines the contract for idata client factory.
/// </summary>
public interface IDataClientFactory
{
    /// <summary>
    /// Executes the get required client&lt;tclient&gt; operation.
    /// </summary>
    TClient GetRequiredClient<TClient>()
        where TClient : class, IDataClient;
}
