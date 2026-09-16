using System.Collections.Concurrent;
using AtomUI.City.Data;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents scripted data response.
/// </summary>
/// <param name="Status">The status value.</param>
/// <param name="Value">The value value.</param>
/// <param name="Error">The error value.</param>
public sealed record ScriptedDataResponse(
    DataResultStatus Status,
    object? Value = null,
    DataError? Error = null)
{
    /// <summary>
    /// Gets success.
    /// </summary>
    public static ScriptedDataResponse Success(object? value = null) =>
        new(DataResultStatus.Success, value);

    /// <summary>
    /// Gets failed.
    /// </summary>
    public static ScriptedDataResponse Failed(DataError error) =>
        new(DataResultStatus.Failed, Error: error ?? throw new ArgumentNullException(nameof(error)));

    /// <summary>
    /// Gets a value indicating whether cancelled.
    /// </summary>
    public static ScriptedDataResponse Cancelled(string? message = null) =>
        new(DataResultStatus.Cancelled, Error: new DataError(DataErrorKind.Cancelled, message ?? "Scripted request cancelled."));
}

/// <summary>
/// Represents scripted data invocation.
/// </summary>
/// <param name="Request">The request value.</param>
/// <param name="Context">The context value.</param>
/// <param name="CancellationToken">The cancellation token value.</param>
public sealed record ScriptedDataInvocation(
    object Request,
    DataRequestContext Context,
    CancellationToken CancellationToken);

/// <summary>
/// Represents scripted data transport.
/// </summary>
public sealed class ScriptedDataTransport : IRequestResponseTransport
{
    private readonly ConcurrentQueue<Func<ScriptedDataInvocation, ValueTask<ScriptedDataResponse>>> _responses = new();
    private readonly ConcurrentQueue<ScriptedDataInvocation> _invocations = new();

    /// <summary>
    /// Initializes a new instance of the <c>ScriptedDataTransport</c> type.
    /// </summary>
    public ScriptedDataTransport(DataTransportKind kind = DataTransportKind.Http)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Data transport kind is not supported.");
        }

        Kind = kind;
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public DataTransportKind Kind { get; }

    /// <summary>
    /// Gets invocations.
    /// </summary>
    public IReadOnlyList<ScriptedDataInvocation> Invocations => _invocations.ToArray();

    /// <summary>
    /// Executes the enqueue operation.
    /// </summary>
    public void Enqueue(ScriptedDataResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responses.Enqueue(_ => ValueTask.FromResult(response));
    }

    /// <summary>
    /// Executes the enqueue operation.
    /// </summary>
    public void Enqueue(Func<ScriptedDataInvocation, ValueTask<ScriptedDataResponse>> response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Executes the send async&lt;tresponse&gt; operation.
    /// </summary>
    public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_responses.TryDequeue(out var responder))
        {
            return DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.Unknown,
                "No scripted data response is available."));
        }

        var invocation = new ScriptedDataInvocation(request, context, cancellationToken);
        _invocations.Enqueue(invocation);
        var response = await responder(invocation).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return response.Status switch
        {
            DataResultStatus.Success when response.Value is TResponse value => DataResult<TResponse>.Success(value),
            DataResultStatus.Success when response.Value is null && default(TResponse) is null => DataResult<TResponse>.Success(default!),
            DataResultStatus.Success => DataResult<TResponse>.Failed(new DataError(
                DataErrorKind.SerializationError,
                "Scripted response value does not match the requested response type.")),
            DataResultStatus.Cancelled => DataResult<TResponse>.Cancelled(response.Error?.Message),
            DataResultStatus.StaleSuppressed => DataResult<TResponse>.StaleSuppressed(response.Error?.Message),
            _ => DataResult<TResponse>.Failed(response.Error ?? new DataError(
                DataErrorKind.Unknown,
                "Scripted failure did not provide an error.")),
        };
    }
}
