namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iactive plugin view lease.
/// </summary>
public interface IActivePluginViewLease : IDisposable
{
    /// <summary>
    /// Gets view.
    /// </summary>
    ActivePluginView View { get; }
}
