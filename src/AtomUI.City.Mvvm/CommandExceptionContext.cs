namespace AtomUI.City.Mvvm;

/// <summary>
/// Provides the original exception and non-secret command identity to an application handler.
/// </summary>
/// <param name="Exception">The original command exception.</param>
/// <param name="CommandName">The configured command name.</param>
/// <param name="OwnerType">The command owner type.</param>
/// <param name="OperationId">The operation identifier.</param>
public sealed record CommandExceptionContext(
    Exception Exception,
    string? CommandName,
    Type? OwnerType,
    Guid OperationId);
