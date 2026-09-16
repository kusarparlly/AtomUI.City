namespace AtomUI.City.Data;

/// <summary>
/// Defines the contract for irequest response transport.
/// </summary>
public interface IRequestResponseTransport
{
    /// <summary>
    /// Gets kind.
    /// </summary>
    DataTransportKind Kind { get; }

    /// <summary>
    /// Executes the send async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        CancellationToken cancellationToken = default);
}
