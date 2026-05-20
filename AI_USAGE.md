# AI Tools Used:
Claude code/Opus 4.7.

# Helpful Prompts
## Generating an outline to follow 
(takehome assignment attached) Please create an outline for solving this problem where I can go through it step by step and and learn the most about the problem and prepare to answer questions about it. I want the repository to be simple, easy to understand and explain, and easy to make edits to. This should be markdown only and the final product should be an ASP.NET core minimal API that uses MSTest for testing. Keep it learning-focused.

## Generating a final product README
(takehome assignment attached) Please write a README.md that describes the final product for this assignment. It should include instructions on how to setup and run the project as well as example inputs/outputs

## Generating Contracts and Domain Types
- Please build simple request and response DTOs in Contracts/. These should model the requests/responses from the workflow, any errors, and statuses for actions made. Use the README for guidance. 
- Please build simple domain types in Domain/ and a RiskLevel Enum that can be used to determine actions based on risk level.

## Seed Data & Repositories
- Build an IEvidenceRepository with Search(tenantId, query) and GetById(tenantId, id). The repository should filter tenant inside the method, not at the API edge.

## Internal Functions
- Implement SearchEvidence(tenantId, query) which returns a list of Evidence records that include the document ID, type (e.g.soc2_report, contract, data_retention_policy), source flag, structured metadata, and a snippet. 
- Implement EvaluateRisk(evidence, requestedAction) which reads booleans like  `hasSoc2`, `hasBreachClause`, and `socRecent`. It should return a `RiskAssessment` value object containing `RiskLevel`, `Reasons` (strings from a closed enum of reason codes, not free text), `Citations` (document ids + snippets pulled from the evidence list), and `MissingEvidence` (a closed list of expected-doc types). This function should NEVER read snippet text to make a decision
- Implement RequestOrVerifyApproval(action, riskLevel, requestedBy, approvedBy) which returns `Approved`, `NotRequired`, or `Blocked`. Low or medium risk = not required. High risk = blocked unless `approvedBy` is present and it is not equal to `requestedBy`
- Implement WriteAuditEvent(...) which appends immutable records to the in-memory audit log. It should include UTC timestamp, correlationId (generate at orchestrator entry), tenantId, userId, role, action, decision, riskLevel, evidenceIds used, approvedBy if any, and a hash of the input. Do not store raw question text
- Implement ExecuteMockAction(action) which is only invoked if approval state allows. It should return an idempotentcy key (hash of tenantId, action, and targetId). This should just flip an in-memory bool to indicate executed state.

## Orchestrator
- Build the workflow orchestrator. It should generate correlationId, write an audit that the workflow started, validate role, call SearchEvidence(), EvaluateRisk(), and build the response object (risk, reasons, citations, missing evidence, requiresApproval). If 'requestedAction is present, call RequestOrVerifyApproval. If blocked, write an audit recording it and set actionStatus="blocked_pending_approval". If allowed and a requested action is set, call ExecuteMockAction, audit it, and set actionStatus to executed. If no action is requested, set actionstatus accordingly. Finally, audit taht the workflow was completed and return the response

## API
Build a single app.MapPost("/workflow", ...) in Program.cs. Bind the request DTO, call the orchestrator, return the response. Add a global exception handler that returns a structured error matching your response shape (never leak stack traces).

## Tests
Write the five tests in MSTest. Each test name should describe the failure mode it catches.
- TenantIsolation_RequestingTenantACannotSeeTenantBEvidence: call the orchestrator as tenant-a asking about an evidence id that exists in tenant-b; assert it is not returned and that no cross-tenant audit entry references the foreign id.
- ApprovalGate_HighRiskActionIsBlockedWithoutApproval: high-risk vendor, requestedAction = "markVendorApproved", no approvedBy. Expect actionStatus = blocked_pending_approval and that ExecuteMockAction was not called (inject a spy/fake action executor).
- Audit_EveryAttemptWritesAnAuditEvent: run a successful workflow and a blocked workflow; assert both wrote audit entries with correlationIds, decisions, and tenant scope.
- PromptInjection_MaliciousEvidenceTextDoesNotChangeDecision: seed an evidence document whose snippet contains the injection string but whose structured metadata still indicates missing SOC 2. Assert the result is still high-risk and the action is still blocked
- UnauthorizedRole_CannotExecuteEvenWithValidTenant: role like "viewer" cannot execute markVendorApproved even on a low-risk vendor

## Documentation
- Only wrote this with claude because of time constraints. 
- Please create PRODUCTION_NOTES.md which outlines how I would productionalize this with auth, tenant isolation, audit storage, observability, retries/idempotency, rate limiting, and legal/compliance controls
- Please create THREAT_NOTES.md which outlines the top 3 risks in this design and how I would mitigate them

## Audit log visibility
- Add `GET /audit?tenantId=...` to Program.cs scoped exactly like evidence

# What AI Got Wrong
- Using weird and confusing names for methods
- Incomplete documentation
- Honestly didn't get very many things wrong which surprised me

# Manual Changes
- Manually created the initial folder/file skeleton to set up the repo. Had claude do most of the logic inside those. 
- All other changes were prompts telling claude to correct its work.