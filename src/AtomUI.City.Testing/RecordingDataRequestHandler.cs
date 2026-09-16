using System.Collections.Concurrent;
using AtomUI.City.Data;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents recorded data request.
/// </summary>
/// <param name="Request">The request value.</param>
/// <param name="Context">The context value.</param>
/// <param name="IsEntering">The is entering value.</param>
public sealed record RecordedDataRequest(
    object Request,
    DataRequestContext Context,
    bool IsEntering);

/// <summary>
/// Represents recording data request handler.
/// </summary>
public sealed class RecordingDataRequestHandler : IDataRequestHandler
{
    private readonly ConcurrentQueue<RecordedDataRequest> _records = new();
    private readonly Func<object, DataRequestContext, CancellationToken, ValueTask>? _before;

    /// <summary>
    /// Initializes a new instance of the <c>RecordingDataRequestHandler</c> type.
    /// </summary>
    public RecordingDataRequestHandler(
        int order = 0,
        Func<object, DataRequestContext, CancellationToken, ValueTask>? before = null)
    {
        Order = order;
        _before = before;
    }

    /// <summary>
    /// Gets order.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Gets records.
    /// </summary>
    public IReadOnlyList<RecordedDataRequest> Records => _records.ToArray();

    /// <summary>
    /// Executes the invoke async&lt;tresponse&gt; operation.
    /// </summary>
    public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        _records.Enqueue(new RecordedDataRequest(request, context, IsEntering: true));
        if (_before is not null)
        {
            await _before(request, context, cancellationToken).ConfigureAwait(false);
        }

        var result = await next(cancellationToken).ConfigureAwait(false);
        _records.Enqueue(new RecordedDataRequest(request, context, IsEntering: false));
        return result;
    }
}
