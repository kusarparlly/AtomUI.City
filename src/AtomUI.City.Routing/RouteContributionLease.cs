namespace AtomUI.City.Routing;

/// <summary>
/// Represents route contribution lease.
/// </summary>
public sealed class RouteContributionLease : IDisposable, IAsyncDisposable
{
    private readonly Action<string> _release;
    private int _disposed;

    internal RouteContributionLease(string contributionId, Action<string> release)
    {
        ContributionId = contributionId;
        _release = release;
    }

    /// <summary>
    /// Gets contribution id.
    /// </summary>
    public string ContributionId { get; }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                _release(ContributionId);
            }
            catch
            {
                Volatile.Write(ref _disposed, 0);
                throw;
            }
        }
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
