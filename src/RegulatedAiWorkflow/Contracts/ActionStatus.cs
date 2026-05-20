namespace RegulatedAiWorkflow.Contracts;

public enum ActionStatus
{
    NoActionRequested,
    BlockedPendingApproval,
    BlockedUnauthorizedRole,
    Executed,
}
