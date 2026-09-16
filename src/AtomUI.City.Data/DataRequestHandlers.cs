namespace AtomUI.City.Data;

/// <summary>
/// Represents the operation used to data request handler delegate&lt;tresponse&gt;.
/// </summary>
public delegate ValueTask<DataResult<TResponse>> DataRequestHandlerDelegate<TResponse>(
    CancellationToken cancellationToken);

/// <summary>
/// Defines the contract for idata request handler.
/// </summary>
public interface IDataRequestHandler
{
    /// <summary>
    /// Gets order.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Executes the invoke async&lt;tresponse&gt; operation.
    /// </summary>
    ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for idata request handler source.
/// </summary>
public interface IDataRequestHandlerSource
{
    /// <summary>
    /// Executes the get handlers&lt;tresponse&gt; operation.
    /// </summary>
    IReadOnlyList<IDataRequestHandler> GetHandlers<TResponse>(DataRequest<TResponse> request);
}

internal static class DataRequestHandlerPipeline
{
    public static ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        IReadOnlyList<IDataRequestHandler> handlers,
        DataRequest<TResponse> request,
        DataRequestContext context,
        Func<CancellationToken, ValueTask<DataResult<TResponse>>> terminal,
        CancellationToken cancellationToken)
    {
        return InvokeAt(0, cancellationToken);

        ValueTask<DataResult<TResponse>> InvokeAt(
            int index,
            CancellationToken currentCancellationToken)
        {
            currentCancellationToken.ThrowIfCancellationRequested();
            if (index == handlers.Count)
            {
                return terminal(currentCancellationToken);
            }

            var nextInvoked = 0;
            ValueTask<DataResult<TResponse>> Next(CancellationToken nextCancellationToken)
            {
                if (Interlocked.Exchange(ref nextInvoked, 1) != 0)
                {
                    throw new InvalidOperationException("A data request handler can invoke its continuation only once.");
                }

                return InvokeAt(index + 1, nextCancellationToken);
            }

            return handlers[index].InvokeAsync(request, context, Next, currentCancellationToken);
        }
    }
}
