namespace RegulatedAiWorkflow.Workflow;

public static class RoleAuthorizer
{
    // Roles permitted to invoke a requestedAction. Read-only requests (no action) are open
    // to any caller; the action layer is what is gated. Unknown roles are read-only by
    // default — the allow-list, not a deny-list, is the safe shape for an authorization
    // check. Real systems would resolve permissions from a per-tenant role mapping rather
    // than a hardcoded set.
    private static readonly HashSet<string> ActionAuthorizedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "compliance_officer",
        "compliance_admin",
    };

    public static bool CanExecuteActions(string role)
    {
        return !string.IsNullOrWhiteSpace(role) && ActionAuthorizedRoles.Contains(role);
    }
}
