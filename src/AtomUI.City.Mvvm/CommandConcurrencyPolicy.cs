namespace AtomUI.City.Mvvm;

/// <summary>
/// Defines atomic admission policies for asynchronous commands.
/// </summary>
public enum CommandConcurrencyPolicy
{
    /// <summary>
    /// Reject a new execution while any execution is active.
    /// </summary>
    Reject = 0,

    /// <summary>
    /// Allow independent executions to run concurrently.
    /// </summary>
    Allow = 1,

    /// <summary>
    /// Reject a new execution only when the same developer-defined key is active.
    /// </summary>
    RejectByKey = 2,
}
