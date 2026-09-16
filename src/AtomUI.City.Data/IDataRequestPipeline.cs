namespace AtomUI.City.Data;

/// <summary>
/// Defines the contract for idata request pipeline.
/// </summary>
public interface IDataRequestPipeline
{
    /// <summary>
    /// Executes the send async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
        DataRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
