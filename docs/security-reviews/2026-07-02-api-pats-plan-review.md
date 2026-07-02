---
document_type: security-review
review_type: plan
assessment_date: 2026-07-02
codebase_analyzed: RfcBuddy (specs/002-rest-api-pat-auth/)
total_files_analyzed: 5
total_findings: 5
overall_risk: MODERATE
critical_count: 0
high_count: 0
medium_count: 3
low_count: 1
informational_count: 1
owasp_categories: [A01, A04, A07]
cwe_ids: [CWE-307, CWE-312, CWE-400, CWE-269, CWE-778]
field_summaries:
  document_type: "Always 'security-review'. Allows indexers to skip non-review documents."
  review_type: "Which command generated this document: audit, branch, staged, plan, tasks, or followup."
  assessment_date: "ISO 8601 date the review was performed (YYYY-MM-DD)."
  overall_risk: "Highest severity tier with active findings (CRITICAL, HIGH, MODERATE, LOW, INFORMATIONAL)."
  critical_count: "Number of Critical findings (CVSS 9.0-10.0)."
  high_count: "Number of High findings (CVSS 7.0-8.9)."
  medium_count: "Number of Medium findings (CVSS 4.0-6.9)."
  low_count: "Number of Low findings (CVSS 0.1-3.9)."
  informational_count: "Number of Informational findings."
  owasp_categories: "OWASP Top 10 2025 categories (A01-A10) that have at least one finding."
  cwe_ids: "CWE identifiers referenced in this document."
  finding_id: "Unique finding identifier (SEC-NNN) for cross-referencing and task linkage."
  location: "File path and line number of the vulnerable code (path/to/file.ext:line)."
  owasp_category: "OWASP Top 10 2025 category for this finding (AXX:2025-Name)."
  cwe: "Common Weakness Enumeration identifier with short name (CWE-NNN: Name)."
  cvss_score: "CVSS v3.1 base score (0.0-10.0). 9.0+=Critical, 7.0-8.9=High, 4.0-6.9=Medium, 0.1-3.9=Low."
  spec_kit_task: "Spec-Kit task ID for backlog tracking and remediation follow-up (TASK-SEC-NNN)."
---

# Security Review: REST API, PATs, and Admin Roles — Plan Review

## Executive Summary

This document presents a threat-modeled security review of the design and implementation planning artifacts for Feature `002-rest-api-pat-auth` (REST API with PAT Authentication, User Management, and Admin Roles). The architecture has been analyzed against the Project Constitution, OWASP Top 10, and CWE standards. 

The overall planning risk is evaluated as **MODERATE**. By sticking to a database-free architecture and utilizing process-isolated filesystem Mutex blocks, the design keeps complexity very low. However, custom bearer token parsing, administrative authorization, and shared physical files introduce boundary risks. This review details these findings and prescribes immediate design-level mitigations before code authoring begins.

---

## Plan Artifacts Reviewed

The following design specifications were audited for structural and cryptographic security:
- `specs/002-rest-api-pat-auth/spec.md` — Feature requirements and constraints
- `specs/002-rest-api-pat-auth/plan.md` — Project structure, target configurations, and post-design checks
- `specs/002-rest-api-pat-auth/research.md` — Central JSON storage structure, Mutex handling, and custom authentication handlers
- `specs/002-rest-api-pat-auth/data-model.md` — Persisted structures of `ApiToken` and `UserRecord`
- `specs/002-rest-api-pat-auth/contracts/rest-api.md` — Bearer headers, REST endpoints, and error handling behaviors

---

## Vulnerability Findings

### SEC-001: Denied Rate-Limiting or Brute-Force Gate on Token Lookup
- **Severity**: MODERATE (CVSS v3.1 Score: 5.3)
- **OWASP Category**: [A07:2021-Identification and Authentication Failure](https://owasp.org/Top10/A07_2021-Identification_and_Authentication_Failures/)
- **CWE ID**: [CWE-307: Improper Restriction of Excessive Authentication Attempts](https://cwe.mitre.org/data/definitions/307.html)
- **Location**: `specs/002-rest-api-pat-auth/contracts/rest-api.md:12`
- **Spec-Kit Task**: `TASK-T012`
- **Description**: The API authentication is designed via a custom `"ApiToken"` bearer scheme checking `apitokens.json`. The design does not mandate brute-force throttling or rate-limiting for failed looks. An attacker with a high-speed HTTP client could brute-force guess token suffixes (high entropy but possible if tokens are short or lack adequate length validation), causing persistent SHA-256 computations on the web server or provoking lock contention on the central JSON storage file.
- **Mitigation/Prescription**: Ensure `ApiTokenAuthenticationHandler` enforces a minimum response latency for failed authentications (e.g., cryptographic timing side-channel protection or basic rate limiting via standard ASP.NET Core rate limiting middleware, which must be registered in `Program.cs` before API endpoints are exposed).

### SEC-002: In-Memory Token Registry or Log Leaks of Raw Secret Keys
- **Severity**: MODERATE (CVSS v3.1 Score: 6.8)
- **OWASP Category**: [A04:2021-Insecure Design](https://owasp.org/Top10/A04_2021-Insecure_Design/)
- **CWE ID**: [CWE-312: Cleartext Storage of Sensitive Information](https://cwe.mitre.org/data/definitions/312.html)
- **Location**: `specs/002-rest-api-pat-auth/research.md:32`
- **Spec-Kit Task**: `TASK-T018`
- **Description**: Although `data-model.md` explicitly mandates storing only SHA-256 hashes of tokens, there remains a risk of cleartext token leaking into standard diagnostics, exceptions, or audit logs if the controller parameters or service arguments are poorly typed (e.g., passing raw tokens as string parameters named fields that could be serialized or logged by reflection-driven loggers).
- **Mitigation/Prescription**: Inside `ApiTokensController.cs` and `ApiTokenService.cs`, encapsulate raw tokens inside strongly typed wrapper structures (e.g., standard .NET `SecretString` pattern or override `.ToString()` on a custom `RawToken` struct to return redacted metrics). Strictly limit logging inside `ApiTokenAuthenticationHandler` to token identifiers (such as the unhashed database ID GUID) and never log raw bearer values or request payloads.

### SEC-003: Disk/I/O Exhaustion and Mutex Concurrency DOS
- **Severity**: MODERATE (CVSS v3.1 Score: 4.8)
- **OWASP Category**: [A01:2021-Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- **CWE ID**: [CWE-400: Uncontrolled Resource Consumption](https://cwe.mitre.org/data/definitions/400.html)
- **Location**: `specs/002-rest-api-pat-auth/research.md:144`
- **Spec-Kit Task**: `TASK-T013`
- **Description**: Multi-replica deployments on OpenShift share `DataFolder`. If many downstream scrapers simultaneously execute keyword searches, each request triggers `RfcApiController.cs` to access current schedules and Mutex-gate write changes. An attacker could exploit this block by sending thousands of search queries, starving the filesystem I/O bandwidth and exhausting thread pools waiting on Global Mutex blocks.
- **Mitigation/Prescription**: Enforce a read-only lock for search retrievals. Baselines updating `ApiPreviousRFCs.txt` must utilize localized per-user file-locks rather than global process-wide Mutex blocking. The central `apitokens.json` must be read in a shared-read format (e.g., caching token hashes in memory with filesystem watchers instead of reading the filesystem list on every single HTTP API invocation).

### SEC-004: Sole Administrator De-elevation Race Conditions
- **Severity**: LOW (CVSS v3.1 Score: 3.1)
- **OWASP Category**: [A01:2021-Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- **CWE ID**: [CWE-269: Improper Privilege Management](https://cwe.mitre.org/data/definitions/269.html)
- **Location**: `specs/002-rest-api-pat-auth/research.md:132`
- **Spec-Kit Task**: `TASK-T026`
- **Description**: While the plan mandates a guard block preventing removal of the "last remaining admin", a concurrent race condition could occur if two administrative users simultaneously attempt to demote each other. Under extreme timing scenarios, both requests could read the file state, identify that ≥2 admins exist, proceed to demote the target, leaving the system with 0 active admins.
- **Mitigation/Prescription**: The demotion checks inside `UserRegistryService.SetAdmin` must be strictly nested inside the `users.json` file mutex lock, evaluating the finalized in-memory dictionary count of administrative users *immediately* before persisting modifications.

### SEC-005: Denied Security Audit Trail on Token Promotion/Revocation
- **Severity**: INFORMATIONAL (CVSS v3.1 Score: 0.0)
- **OWASP Category**: [A04:2021-Insecure Design](https://owasp.org/Top10/A04_2021-Insecure_Design/)
- **CWE ID**: [CWE-778: Insufficient Logging](https://cwe.mitre.org/data/definitions/778.html)
- **Location**: `specs/002-rest-api-pat-auth/contracts/service-contracts.md:37`
- **Spec-Kit Task**: `TASK-T030`
- **Description**: Privilege elevation (admin-promotion) and administrative token revocation (cutting off downstream integrations) are highly sensitive security actions. The designs do not mandate immediate structured security logs for these administration tasks.
- **Mitigation/Prescription**: Enforce structured, non-PII security audit logs whenever a administrative action takes place. Format: `[AUDIT] Action=PromoteAdmin, TargetUserHash=X, RequestorHash=Y` to satisfy governance and OpenShift auditing best practices.

---

## Confirmed Secure Patterns

The reviewed design follows several highly commendable secure-by-design patterns:
1. **Separated Token baselines**: Keeping `ApiPreviousRFCs.txt` distinct from `PreviousRFCs.txt` prevents session side-effects and data manipulation between administrative web users and automation integrations.
2. **Interactive Auth reuse**: The design avoids rolling its own interactive login or credentials mechanism; token initialization relies entirely on the pre-existing OIDC/Keycloak authentication scheme.
3. **One-Way Token Hashing**: Storing only SHA-256 tokens matching the standard configuration ensures that even a full filesystem leakage does not expose cleartext integration secrets.

---

## Action Plan & Next Steps

1. **Durable Memory Preservation**: These security constraints will be added to the project system memory.
2. **Remediation Task Mapping**: Security task definitions should be reviewed alongside `tasks.md` to ensure they directly mitigate SEC-001 through SEC-005.

---

## Proposed Memory Hub Row

```text
| docs/security-reviews/2026-07-02-api-pats-plan-review.md | plan | 2026-07-02 | MODERATE | C:0 H:0 M:3 L:1 | A01,A04,A07 |
```
