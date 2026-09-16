using System.Collections.Concurrent;
using AtomUI.City.Data;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents scripted data credential provider.
/// </summary>
public sealed class ScriptedDataCredentialProvider : IDataCredentialProvider
{
    private readonly ConcurrentQueue<Func<DataAuthenticationContext, CancellationToken, ValueTask<DataCredentialResult>>> _responses = new();
    private readonly ConcurrentQueue<DataAuthenticationContext> _requests = new();

    /// <summary>
    /// Gets requests.
    /// </summary>
    public IReadOnlyList<DataAuthenticationContext> Requests => _requests.ToArray();

    /// <summary>
    /// Executes the enqueue operation.
    /// </summary>
    public void Enqueue(DataCredentialResult response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responses.Enqueue((_, _) => ValueTask.FromResult(response));
    }

    /// <summary>
    /// Executes the enqueue operation.
    /// </summary>
    public void Enqueue(
        Func<DataAuthenticationContext, CancellationToken, ValueTask<DataCredentialResult>> response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Executes the get credential async operation.
    /// </summary>
    public ValueTask<DataCredentialResult> GetCredentialAsync(
        DataAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Enqueue(context);
        return _responses.TryDequeue(out var response)
            ? response(context, cancellationToken)
            : ValueTask.FromResult(DataCredentialResult.Unavailable("No scripted credential response is available."));
    }
}
