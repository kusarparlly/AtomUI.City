namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents mvvm diagnostic ids.
/// </summary>
public static class MvvmDiagnosticIds
{
    /// <summary>
    /// Represents the activation failed value.
    /// </summary>
    public const string ActivationFailed = "AUCMVVM001";
    /// <summary>
    /// Represents the deactivation failed value.
    /// </summary>
    public const string DeactivationFailed = "AUCMVVM002";
    /// <summary>
    /// Represents the command failed value.
    /// </summary>
    public const string CommandFailed = "AUCMVVM003";
    /// <summary>
    /// Represents the command rejected value.
    /// </summary>
    public const string CommandRejected = "AUCMVVM004";
    /// <summary>
    /// Represents the interaction not handled value.
    /// </summary>
    public const string InteractionNotHandled = "AUCMVVM005";
    /// <summary>
    /// Represents the interaction failed value.
    /// </summary>
    public const string InteractionFailed = "AUCMVVM006";
    /// <summary>
    /// Represents the activation scope dispose failed value.
    /// </summary>
    public const string ActivationScopeDisposeFailed = "AUCMVVM007";
    /// <summary>
    /// Represents the validation failed value.
    /// </summary>
    public const string ValidationFailed = "AUCMVVM008";

    /// <summary>
    /// Activation-scoped task admission was rejected.
    /// </summary>
    public const string ActivationTaskRejected = "AUCMVVM009";

    /// <summary>
    /// Activation-scoped task execution failed.
    /// </summary>
    public const string ActivationTaskFailed = "AUCMVVM010";

    /// <summary>
    /// Activation-scoped tasks did not drain within the configured timeout.
    /// </summary>
    public const string ActivationTaskDrainTimedOut = "AUCMVVM011";

    /// <summary>
    /// Command execution was rejected because its owner was not active.
    /// </summary>
    public const string CommandInactive = "AUCMVVM012";

    /// <summary>
    /// The application command exception handler failed.
    /// </summary>
    public const string CommandExceptionHandlerFailed = "AUCMVVM013";

    /// <summary>
    /// A command notification could not be delivered through its configured dispatcher.
    /// </summary>
    public const string CommandNotificationFailed = "AUCMVVM014";
}
