namespace RegulatedAiWorkflow.Domain;

public enum AuditEventType
{
    WorkflowStarted,
    DecisionComputed,
    ActionBlocked,
    ActionExecuted,
    WorkflowCompleted,
}