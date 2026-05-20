# Production Notes

What this slice fakes and what a real deployment would require. The orchestrator and the repository interfaces are shaped so that none of the changes below touch `WorkflowOrchestrator` — they are all edge or adapter work.

---

## 1. Authentication

Today: `tenantId`, `userId`, and `role` arrive in the request body. `Program.cs` has a comment flagging this as a deliberate slice simplification.

Production:

- **OIDC / OAuth 2.0** in front of the API (Azure AD / Entra ID for the interviewer's stack). The gateway validates the JWT; the API trusts the validated principal.
- Bind `tenantId`, `userId`, and `role` from token claims (`tid`, `oid`, `roles`) onto an `ICallerContext` resolved per request. Remove those fields from `WorkflowRequest`.
- **No header smuggling.** Anything client-supplied that names a tenant (`X-Tenant-Id`, query strings, body fields) is either ignored or rejected. The only source of truth is the validated claim.
- **Service-to-service** callers get their own client credentials flow with a scoped audience; they cannot impersonate end users.
- **Role hardening.** `RoleAuthorizer` becomes a policy check over claim-derived roles. Add `audit_reader` as a distinct role from `analyst` / `approver` — workflow callers and audit readers are not the same population.
- **Approval tokens.** `approvedBy` today is a string. Production: the approver signs a short-lived JWT that binds `(actionType, vendorId, riskAssessmentHash, approverId, nonce)`. The orchestrator verifies the signature, the binding, and the nonce (single-use) before treating the approval as valid. This kills replay and cross-action reuse.

---

## 2. Tenant isolation

Today: `IEvidenceRepository`, `IPolicyRepository`, `IVendorRepository`, and `IAuditLog` all require `tenantId` as a method parameter and filter inside the in-memory implementations. Cross-tenant access is architecturally impossible, not guarded by an `if` at the edge.

Production keeps that contract and adds defense in depth:

- **Postgres / SQL Server row-level security.** Every tenant-scoped table has `tenant_id NOT NULL` and an RLS policy keyed off a session variable set from the principal at connection check-out. A buggy query without a `WHERE tenant_id = ...` clause still returns nothing.
- **EF Core global query filter** on every tenant-scoped entity as a second layer, so an ORM-level mistake (raw SQL, missing filter) still fails closed.
- **Connection-pool hygiene.** Reset the session tenant variable on connection return so a pooled connection cannot leak the previous request's tenant context.
- **Tests stay.** `TenantIsolationTests` runs against the repository contract, so the SQL implementation must pass the same test.
- **Tenant-id provenance.** The repository's `tenantId` argument comes from `ICallerContext` (claim-derived), not from anything in the request body — see auth above.

---

## 3. Audit storage

Today: `InMemoryAuditLog` is an append-only `List<AuditEvent>` keyed by tenant. `AuditWriter` records every workflow attempt, including blocked ones, before the action runs.

Production:

- **Append-only table** with no `UPDATE` or `DELETE` grant for the application role. Schema migrations that drop the table require a separate operator role.
- **Hash chaining.** Each row stores `prev_hash` + `row_hash = H(prev_hash || canonical_row_bytes)`. A tamper-evident chain per tenant. A nightly job verifies the chain head and alerts on divergence.
- **WORM-grade storage** for the regulated copy: ship audit events to S3 Object Lock (Compliance mode) or Azure Blob immutability policies, with retention aligned to the regulator's window (typically 7 years for financial-services vendor management).
- **Separate read path.** `GET /audit` becomes its own service or at minimum its own RBAC scope (`audit:read`). Workflow callers cannot read audit; audit readers cannot run workflows.
- **What goes in.** Already correct in the slice: UTC timestamp, correlationId, tenantId, userId, role, action, decision, riskLevel, evidenceIds, approvedBy, and a hash of the question — never the raw question text. Production adds: source IP, JWT `jti`, and the approval-token `jti` if present.
- **PII minimization.** Audit records do not store evidence snippets or free-text user input. The evidenceIds let an auditor reconstruct what was seen, scoped through the same RBAC.
- **Retention and legal hold.** Configurable per-tenant retention with a legal-hold override that suspends deletion for events tagged by an investigation.

---

## 4. Observability

Today: structured `ErrorResponse` envelope with `traceId`, `correlationId` propagates through every audit event. No metrics, no traces.

Production:

- **Structured logs** (Serilog → JSON sink) with `correlationId`, `tenantId`, `userId`, `role`, `riskLevel`, `actionStatus`, `decision` as first-class fields. Never log `question`, evidence snippets, or token contents.
- **OpenTelemetry tracing.** One root span per `/workflow` request; child spans for `SearchEvidence`, `EvaluateRisk`, `ApprovalGate`, `ExecuteAction`, `WriteAudit`. The five-function decomposition in the orchestrator maps one-to-one to spans, which is most of the value of keeping them separate.
- **Metrics worth alerting on:**
  - `workflow_decisions_total{risk_level, action_status}` — sudden jumps in `blocked_pending_approval` rate, or in `executed` for `high` risk, are both incidents.
  - `approval_rejections_total{reason}` — spike in four-eyes rejections or signature failures is a credential-abuse signal.
  - `evidence_search_latency_p99` — degradation here cascades into every workflow.
  - `audit_write_failures_total` — should be zero; any non-zero rate pages immediately because we cannot execute actions without a durable audit.
- **Correlation across services.** `correlationId` is generated at the edge if absent and propagated as `traceparent` to any downstream call (real RAG retrieval, real action executor).
- **Sampling.** Trace 100% of `high` risk and `blocked` decisions, sample the rest at 10%.

---

## 5. Retries and idempotency

Today: `InMemoryActionExecutor` computes an idempotency key `H(tenantId|action|targetId)` and short-circuits duplicates.

Production:

- **Idempotency table** keyed by `(tenant_id, idempotency_key)` with the response payload and a TTL (24h is enough for retry storms; longer if the action has external side effects). Inserts use `INSERT ... ON CONFLICT DO NOTHING` so the first writer wins.
- **Client-supplied `Idempotency-Key` header** on `POST /workflow` for end-to-end retry safety, in addition to the server-side action key. Same `(tenant, key)` returns the cached response.
- **Retry policy is asymmetric.** Read calls (evidence search, policy lookup) get bounded exponential backoff with jitter. Action execution is **retry-on-network-only**: if the executor returned any HTTP response, we do not retry — we look up the idempotency record. This avoids the classic "timeout caused a double-execution" bug.
- **Audit-then-act, with reconciliation.** The audit write for `workflow_completed` happens after the action returns. If the process crashes between action and audit, a reconciliation job replays from the action executor's own log to close the audit gap. The `correlationId` is the join key.
- **Approval tokens are single-use** (see auth). The nonce store sits in the same idempotency table; replay attempts fail at signature check before reaching the action.

---

## 6. Rate limiting

Today: none.

Production, layered:

- **Per-user, per-tenant** on `POST /workflow`. Token bucket: e.g., 60 requests/minute steady, 120 burst. The user dimension prevents one compromised account from exhausting the tenant's budget.
- **Per-tenant ceiling** on action-executing calls (`requestedAction != null`), independent of the read budget. Tighter than the read limit because actions have side effects.
- **Global ceiling on high-risk actions** across all tenants, enforced as a kill-switch — protects downstream systems (the real action executor, the approval-notification service) from a coordinated burst.
- **Per-tenant ceiling on audit reads** with a separate, lower budget. Audit endpoints are an exfiltration target.
- **Implementation:** ASP.NET Core's built-in `RateLimiter` middleware (fixed-window or sliding-window partitioner keyed by `tenantId + userId`) backed by Redis for multi-instance coordination.
- **429 envelope** uses the same `ErrorResponse` shape as today's errors, with `Retry-After` populated.
- **Bypass for break-glass roles** is logged with a dedicated audit event type so it cannot be used quietly.

---

## 7. Legal and compliance controls

The interviewer's domain (regulated AI, vendor risk) is where this section earns its weight.

- **Data residency.** Tenant data — evidence, audit, action records — is pinned to a region per tenant config. Cross-region replication is opt-in and gated by a separate contract. The repository layer enforces region affinity; a misconfigured node cannot serve out-of-region data.
- **PII minimization.** No PII in logs, no raw user questions in audit (only a hash), no evidence snippets in metrics. The trust boundary doc ([`THREAT_NOTES.md`](./THREAT_NOTES.md)) already keeps snippet text out of decision branches; this extends the same rule to telemetry.
- **Evidence retention.** Driven by the strictest applicable framework per tenant (SOC 2, GDPR, HIPAA, sector-specific). Configurable per evidence type with a default deny-delete during legal hold.
- **Right-to-erasure (GDPR Art. 17).** Erasure operates on PII fields in user/principal tables. Audit records are exempt under the legal-obligation basis but are pseudonymized: the user identifier becomes a tombstoned reference. The hash chain stays intact.
- **Data processing records (GDPR Art. 30).** Every action type is registered with its lawful basis, retention, and processors. New actions cannot be deployed without this registration — enforced as a CI check on the action registry.
- **Model governance.** Even though this slice uses deterministic rules, the architecture leaves room for an LLM-assisted evaluator. When that lands: model version, prompt version, and structured-output schema version are recorded on every decision so an auditor can reproduce the reasoning. The deterministic risk rules remain the system of record — the LLM proposes, code disposes.
- **Vendor due diligence on the AI provider itself.** If a real LLM is added (Anthropic, OpenAI, Azure OpenAI), the provider goes through the same vendor-evidence process this system evaluates: SOC 2, DPA, sub-processor list, breach-notification SLA. Eating our own dogfood.
- **Approval audit for regulators.** The four-eyes record (requester, approver, signed token, evidence snapshot hash) is the artifact a regulator asks for. The hash-chained audit log is the chain of custody.
- **Export controls and content review.** Actions that emit data outside the tenant boundary (the brief's hinted `exportPrivilegedSummary`) get their own classification check and a second approver tier. The action registry carries the classification; the approval gate reads it.

---

## What does not change

The slice was structured so productionalizing it is mostly adapter work:

- `WorkflowOrchestrator` and its five collaborators stay as-is.
- `IEvidenceRepository`, `IPolicyRepository`, `IVendorRepository`, `IAuditLog`, `IActionExecutor` get production implementations; their contracts do not move.
- The five existing tests continue to pass against the new implementations. They are the regression net for everything above.