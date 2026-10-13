namespace AtomUI.City.State;

/// <summary>
/// Creates application-state writers constrained by an explicit write authority.
/// </summary>
public interface IApplicationStateWriterFactory
{
    /// <summary>
    /// Creates a writer constrained by the supplied authority.
    /// </summary>
    IApplicationStateWriter CreateWriter(StateWriteAuthority authority);
}
