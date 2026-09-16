namespace AtomUI.City.Routing;

/// <summary>
/// Defines the contract for irouter.
/// </summary>
public interface IRouter
{
    /// <summary>
    /// Executes the navigate async operation.
    /// </summary>
    ValueTask<NavigationResult> NavigateAsync(
        RouteReference route,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the navigate async&lt;tparameters&gt; operation.
    /// </summary>
    ValueTask<NavigationResult> NavigateAsync<TParameters>(
        RouteReference<TParameters> route,
        TParameters parameters,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the navigate by path async operation.
    /// </summary>
    ValueTask<NavigationResult> NavigateByPathAsync(
        string path,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the navigate by uri async operation.
    /// </summary>
    ValueTask<NavigationResult> NavigateByUriAsync(
        Uri uri,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the back async operation.
    /// </summary>
    ValueTask<NavigationResult> BackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the forward async operation.
    /// </summary>
    ValueTask<NavigationResult> ForwardAsync(CancellationToken cancellationToken = default);
}
