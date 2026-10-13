namespace AtomUI.City.Mvvm;

internal static class ActivationTaskRejection
{
    private const string ScopeIdKey = "AtomUI.City.Mvvm.ActivationTaskRejection.ScopeId";
    private const string ReasonKey = "AtomUI.City.Mvvm.ActivationTaskRejection.Reason";

    public static InvalidOperationException Create(
        Guid scopeId,
        string reason,
        string message)
    {
        var exception = new InvalidOperationException(message);
        exception.Data[ScopeIdKey] = scopeId;
        exception.Data[ReasonKey] = reason;
        return exception;
    }

    public static bool IsScopeRejection(Exception exception, Guid scopeId)
    {
        return exception.Data[ScopeIdKey] is Guid rejectedScopeId
            && rejectedScopeId == scopeId;
    }
}
