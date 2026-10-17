using AtomUI.City.CoreEventBus.DogfoodApp.Contracts;
using AtomUI.City.CoreEventBus.DogfoodApp.Modules;
using AtomUI.City.CoreEventBus.DogfoodApp.Services;
using AtomUI.City.CoreEventBus.DogfoodApp.Verification;
using AtomUI.City.EventBus;

namespace AtomUI.City.CoreEventBus.DogfoodApp.Handlers;

public abstract class CountingHandler<TEvent>(IDogfoodLedger ledger, string name) : IEventHandler<TEvent>
{
    protected IDogfoodLedger Ledger { get; } = ledger;

    public virtual ValueTask HandleAsync(EventContext<TEvent> context, CancellationToken cancellationToken)
    {
        Ledger.Record(name, GetBusinessId(context.Event));
        return ValueTask.CompletedTask;
    }

    protected abstract string GetBusinessId(TEvent eventData);
}

[EventHandler(typeof(IdentityModule))]
public sealed class IdentityAuditHandler(IDogfoodLedger ledger) : CountingHandler<UserAuthenticated>(ledger, nameof(IdentityAuditHandler))
{ protected override string GetBusinessId(UserAuthenticated e) => e.UserId; }

[EventHandler(typeof(WorkspaceModule))]
public sealed class WorkspaceAuditHandler(IDogfoodLedger ledger) : CountingHandler<WorkspaceOpened>(ledger, nameof(WorkspaceAuditHandler))
{ protected override string GetBusinessId(WorkspaceOpened e) => e.WorkspaceId; }

[EventHandler(typeof(WorkspaceModule))]
public sealed class WorkspaceCacheHandler(IDogfoodLedger ledger) : CountingHandler<WorkspaceOpened>(ledger, nameof(WorkspaceCacheHandler))
{ protected override string GetBusinessId(WorkspaceOpened e) => e.WorkspaceId; }

[EventHandler(typeof(JobsModule), ChannelName = "commands")]
public sealed class JobSubmissionAuditHandler(IDogfoodLedger ledger) : CountingHandler<JobSubmitted>(ledger, nameof(JobSubmissionAuditHandler))
{ protected override string GetBusinessId(JobSubmitted e) => e.JobId; }

[EventHandler(typeof(JobsModule), ChannelName = "commands")]
public sealed class JobSubmissionMetricsHandler(IDogfoodLedger ledger) : CountingHandler<JobSubmitted>(ledger, nameof(JobSubmissionMetricsHandler))
{ protected override string GetBusinessId(JobSubmitted e) => e.JobId; }

[EventHandler(typeof(JobsModule))]
public sealed class JobAcceptedAuditHandler(IDogfoodLedger ledger, IAuditSink audit) : CountingHandler<JobAccepted>(ledger, nameof(JobAcceptedAuditHandler))
{
    public override ValueTask HandleAsync(EventContext<JobAccepted> context, CancellationToken cancellationToken)
    {
        audit.Write(context.Event.JobId);
        return base.HandleAsync(context, cancellationToken);
    }
    protected override string GetBusinessId(JobAccepted e) => e.JobId;
}

[EventHandler(typeof(JobsModule))]
public sealed class JobAcceptedMetricsHandler(IDogfoodLedger ledger) : CountingHandler<JobAccepted>(ledger, nameof(JobAcceptedMetricsHandler))
{ protected override string GetBusinessId(JobAccepted e) => e.JobId; }

[EventHandler(typeof(JobsModule))]
public sealed class JobQueueHandler(IDogfoodLedger ledger) : CountingHandler<JobQueued>(ledger, nameof(JobQueueHandler))
{ protected override string GetBusinessId(JobQueued e) => e.JobId; }

[EventHandler(typeof(ExecutionModule))]
public sealed class ExecutionStartHandler(IDogfoodLedger ledger, IExecutionEngine engine) : CountingHandler<ExecutionStarted>(ledger, nameof(ExecutionStartHandler))
{
    public override ValueTask HandleAsync(EventContext<ExecutionStarted> context, CancellationToken cancellationToken)
    {
        _ = engine.Execute(context.Event.JobId);
        return base.HandleAsync(context, cancellationToken);
    }
    protected override string GetBusinessId(ExecutionStarted e) => e.JobId;
}

[EventHandler(typeof(ExecutionModule))]
public sealed class ExecutionStartMetricsHandler(IDogfoodLedger ledger) : CountingHandler<ExecutionStarted>(ledger, nameof(ExecutionStartMetricsHandler))
{ protected override string GetBusinessId(ExecutionStarted e) => e.JobId; }

[EventHandler(typeof(ExecutionModule), ChannelName = "jobs")]
public sealed class ProgressProjectionHandler(IDogfoodLedger ledger) : CountingHandler<JobProgressed>(ledger, nameof(ProgressProjectionHandler))
{ protected override string GetBusinessId(JobProgressed e) => e.JobId; }

[EventHandler(typeof(ExecutionModule), ChannelName = "jobs")]
public sealed class ProgressMetricsHandler(IDogfoodLedger ledger) : CountingHandler<JobProgressed>(ledger, nameof(ProgressMetricsHandler))
{ protected override string GetBusinessId(JobProgressed e) => e.JobId; }

[EventHandler(typeof(ExecutionModule))]
public sealed class CompletionAuditHandler(IDogfoodLedger ledger, IAuditSink audit) : CountingHandler<JobCompleted>(ledger, nameof(CompletionAuditHandler))
{
    public override ValueTask HandleAsync(EventContext<JobCompleted> context, CancellationToken cancellationToken)
    {
        audit.Write($"completed:{context.Event.JobId}");
        return base.HandleAsync(context, cancellationToken);
    }
    protected override string GetBusinessId(JobCompleted e) => e.JobId;
}

[EventHandler(typeof(NotificationModule))]
public sealed class CompletionNotificationHandler(IDogfoodLedger ledger, INotificationGateway notifications) : CountingHandler<JobCompleted>(ledger, nameof(CompletionNotificationHandler))
{
    public override ValueTask HandleAsync(EventContext<JobCompleted> context, CancellationToken cancellationToken)
    {
        notifications.Send(context.Event.JobId);
        return base.HandleAsync(context, cancellationToken);
    }
    protected override string GetBusinessId(JobCompleted e) => e.JobId;
}

[EventHandler(typeof(AuditModule))]
public sealed class AuditProjectionHandler(IDogfoodLedger ledger) : CountingHandler<AuditRecorded>(ledger, nameof(AuditProjectionHandler))
{ protected override string GetBusinessId(AuditRecorded e) => e.JobId; }

[EventHandler(typeof(AuditModule))]
public sealed class AuditMetricsHandler(IDogfoodLedger ledger) : CountingHandler<AuditRecorded>(ledger, nameof(AuditMetricsHandler))
{ protected override string GetBusinessId(AuditRecorded e) => e.JobId; }

[EventHandler(typeof(ReportingModule))]
public sealed class ReportIndexHandler(IDogfoodLedger ledger) : CountingHandler<ReportGenerated>(ledger, nameof(ReportIndexHandler))
{ protected override string GetBusinessId(ReportGenerated e) => e.JobId; }

[EventHandler(typeof(NotificationModule), ChannelName = "telemetry")]
public sealed class NotificationMetricsHandler(IDogfoodLedger ledger) : CountingHandler<NotificationSent>(ledger, nameof(NotificationMetricsHandler))
{ protected override string GetBusinessId(NotificationSent e) => e.JobId; }

[EventHandler(typeof(MaintenanceModule), ChannelName = "failures")]
public sealed class MaintenanceAuditHandler(IDogfoodLedger ledger) : CountingHandler<MaintenanceCycle>(ledger, nameof(MaintenanceAuditHandler))
{ protected override string GetBusinessId(MaintenanceCycle e) => e.CycleId; }

[EventHandler(typeof(MaintenanceModule), ChannelName = "failures")]
public sealed class MaintenanceMetricsHandler(IDogfoodLedger ledger) : CountingHandler<MaintenanceCycle>(ledger, nameof(MaintenanceMetricsHandler))
{ protected override string GetBusinessId(MaintenanceCycle e) => e.CycleId; }
