---
document_type: security-review
review_type: tasks
assessment_date: 2026-06-30
codebase_analyzed: RfcBuddy
total_files_analyzed: 6
total_findings: 4
overall_risk: LOW
critical_count: 0
high_count: 0
medium_count: 1
low_count: 2
informational_count: 1
owasp_categories: [A05, A09]
cwe_ids: [CWE-362, CWE-778, CWE-116, CWE-22]
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

# Security Review — Task Review: Recent Completed RFCs & App Version Display

- **Assessment Date**: 2026-06-30
- **Feature Branch**: `001-recent-completed-rfcs`
- **Tasks Reviewed**: [specs/001-recent-completed-rfcs/tasks.md](specs/001-recent-completed-rfcs/tasks.md)
- **Prior Plan Review**: [docs/security-reviews/2026-06-30-completed-rfcs-plan-review.md](docs/security-reviews/2026-06-30-completed-rfcs-plan-review.md)
- **Supporting Artifacts**: plan.md, research.md, data-model.md, contracts/service-contracts.md, spec.md

---

## Executive Summary

The task list is well-constructed from a security standpoint. All three findings raised in the
prior plan security review (SEC-001 race condition, SEC-002 resource exhaustion, SEC-003
path traversal) are **explicitly tracked as implementation tasks** (T007, T008, T014),
and they are correctly sequenced in the **foundational phase (Phase 2)** before any user
story work begins. Tests are written before implementation throughout, and no user story
can bypass the security foundations.

Four residual gaps are identified in this task review:

1. **Medium**: The unit test task for `RfcArchiveService` (T005) does not explicitly call
   out concurrent-write safety (contract scenario AC-8) — the most important security
   control in the entire feature. Without an explicit test task, AC-8 may be left untested.
2. **Low**: No task explicitly logs security-relevant limit events (record cap hit, file
   size cap hit, file corruption detected, write retry triggered). These events are
   operationally important for incident detection.
3. **Low**: No test task ensures the transient `Keywords` field on `Rfc` is excluded from
   JSON serialization in the shared archive, which would silently bleed one user's keyword
   matches into another user's view of the same RFC.
4. **Informational**: No validation task guards the `DataFolder` configuration value
   against directory traversal before it is used to construct the archive file path.

All gaps are addressable by adding explicit requirements to existing tasks — no new tasks
are needed. Overall risk is assessed as **LOW**: the architecture is sound, the three
plan-level security findings are fully covered, and the remaining gaps involve test
coverage and logging completeness rather than missing mitigations.

---

## Tasks Reviewed

| ID | Phase | Description | Security Coverage |
|----|-------|-------------|-------------------|
| T001 | 1 | Add `ArchiveUpdateIntervalDays` to `AppSettings` | Configures background cadence; no security content |
| T002 | 1 | Register `IRfcArchiveService` as singleton | DI prerequisite for thread-safe singleton |
| T003 | 2 | Write failing tests for `GetAllRfcs`/`CategorizeRfcs` | Regression safety for parser |
| T004 | 2 | Implement `GetAllRfcs` and `CategorizeRfcs`; refactor `ProcessRfcs` | Keyword injection handled by existing regex |
| T005 | 2 | **Write failing tests for `IRfcArchiveService`** | Covers AC-2/AC-3/AC-4, file caps — **missing AC-8** |
| T006 | 2 | Implement `RfcArchiveService` with JSON I/O | Storage foundation |
| T007 | 2 | **[SEC-001]** Thread-safety: FileShare.None, tmp+move, retry loop | Addresses TOCTOU race condition |
| T008 | 2 | **[SEC-002]** Defensive limits: 20,000 records, 50 MB cap | Addresses DoS via unbounded growth |
| T009 | 3 | Write failing tests for `WordService` completed section | Regression safety for document output |
| T010 | 3 | Update `WordService`/`IWordService` for completed lists | No new security surface |
| T011 | 3 | Update `HomeController` POST orchestration | Error handling for archive failures unspecified |
| T012 | 3 | Update existing tests for new signatures | Regression safety |
| T013 | 4 | Write failing tests for `AppVersion` | Covers AV-1/AV-2/AV-3 — correct |
| T014 | 4 | **[SEC-003]** Implement `AppVersion` via reflection only | Prevents filesystem path traversal |
| T015 | 4 | Render version in `_Layout.cshtml` footer | Static template output; no dynamic user data |
| T016 | 4 | Footer CSS positioning | Styling only; no security surface |
| T017 | 5 | Write failing tests for `ArchiveUpdateService` | Covers DI, scope resolution, BG-3 |
| T018 | 5 | Implement `ArchiveUpdateService` (BackgroundService) | Try/catch loop, scoped factory |
| T019 | 5 | Register `AddHostedService<ArchiveUpdateService>` | DI wiring |
| T020 | 6 | Run `dotnet test RfcBuddy.sln` | Regression gate |
| T021 | 6 | Run quickstart.md scenarios A/B/C | End-to-end validation |
| T022 | 6 | Resolve static analyzer warnings | Warnings-as-errors policy |
| T023 | 6 | Run `/speckit.verify.run` | Post-implementation verification gate |

---

## Sequencing Assessment

**Secure foundations are correctly ordered before user stories.** T007 (SEC-001
concurrency) and T008 (SEC-002 resource caps) both sit in Phase 2, which is marked
⚠️ CRITICAL and blocks all user story phases. No user story task (Phase 3–5) can begin
before these security controls are in place. This is the correct architecture.

The test-first discipline (T005 before T006/T007/T008, T009 before T010/T011, T013 before
T014, T017 before T018) is sound and consistent with Principle IV (Regression Safety).

No parallel task bypasses a security prerequisite. Tasks marked `[P]` (T003, T005, T009,
T013, T016, T017) are test authoring or styling work that does not depend on security
controls being built first.

---

## Vulnerability Findings

### SEC-TASK-001: AC-8 Concurrent-Write Safety Not Explicitly in Test Scope

- **Location**: [specs/001-recent-completed-rfcs/tasks.md — T005](specs/001-recent-completed-rfcs/tasks.md)
- **OWASP Category**: A05:2025-Security Misconfiguration
- **CWE**: CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization
- **CVSS v3.1 Score**: 5.3 (Medium)
- **Spec-Kit Task**: Amend T005
- **Prior finding**: SEC-001 (plan review)

T007 correctly implements the atomic-write + retry pattern specified in SEC-001. However,
the test scope in T005 lists `empty state, AC-2/AC-3 (latest-version resolution), AC-4
(pruning), and file size/record caps` — but **not AC-8 (concurrent `UpdateArchive`
calls)**. AC-8 is defined in the service contract as a required behavioral guarantee. If
AC-8 has no test, the critical locking mechanism can regress silently: a future refactor
that removes the `FileShare.None` or the retry loop will not be caught by CI.

**Remediation**: Amend T005's description to explicitly include an AC-8 test that
simulates two concurrent `UpdateArchive` calls (via `Parallel.Invoke` or two tasks on a
`ThreadPool`) and asserts the archive file is valid JSON containing all expected RFC
entries from both callers afterward.

---

### SEC-TASK-002: Security-Relevant Limit Events Are Not Logged

- **Location**: [specs/001-recent-completed-rfcs/tasks.md — T006, T007, T008](specs/001-recent-completed-rfcs/tasks.md)
- **OWASP Category**: A09:2025-Security Logging and Monitoring Failures
- **CWE**: CWE-778: Insufficient Logging
- **CVSS v3.1 Score**: 3.1 (Low)
- **Spec-Kit Task**: Amend T006/T008

No task explicitly requires logging when a security-relevant limit or failure event
occurs:

- The 20,000-record cap is hit (SEC-002 mitigation triggers)
- The 50 MB file size cap is hit (SEC-002 mitigation triggers)
- The archive file is found corrupt on read and is reset
- A file-write retry is required (SEC-001 mitigation triggers)

These events indicate either a misconfigured environment, an abnormally large data source,
or an infrastructure problem. Without log entries at Warning or Error severity, operators
have no way to detect an attack pattern (e.g. a feed being poisoned with thousands of
entries) or a production incident, which is exactly the gap OWASP A09 targets.

**Remediation**: Amend T006 and T008 to include an explicit logging requirement: each
limit or error condition in `RfcArchiveService` (record cap, file size cap, corruption
reset, retry exhaustion) MUST emit a structured log entry at `Warning` or `Error` via
`ILogger<RfcArchiveService>`. Corresponding assertions in the T005 test task should verify
logging calls are made via a Moq'd `ILogger`.

---

### SEC-TASK-003: No Test Task for Transient `Keywords` Field Exclusion from Archive

- **Location**: [specs/001-recent-completed-rfcs/tasks.md — T005, T006](specs/001-recent-completed-rfcs/tasks.md)
- **OWASP Category**: A05:2025-Security Misconfiguration
- **CWE**: CWE-116: Improper Encoding or Escaping of Output
- **CVSS v3.1 Score**: 2.3 (Low)
- **Spec-Kit Task**: Amend T005

The `Rfc` object has a transient `Keywords` property (`List<string>`) populated at
categorization time from a specific user's keyword set. The data model notes this field
must not be persisted to the shared archive. If it is inadvertently serialized, one user's
keyword matches will be stored in the shared archive and surfaced as highlighted keywords
for all subsequent users who view the same RFC — leaking user-specific filter choices and
producing incorrect rendering.

T006 describes implementing `RfcArchiveService` with JSON I/O, and T005 describes its
tests, but neither explicitly calls out the `[JsonIgnore]` / serialization-exclusion
behavior for the `Keywords` field.

**Remediation**: Amend T005 to include a test asserting that after a round-trip through
`UpdateArchive` and `GetCompletedRfcs`, the returned `Rfc` objects have an empty
`Keywords` list regardless of what was set before archiving. Amend T006 to note that `Rfc`
must be serialized with `Keywords` excluded (e.g., `[JsonIgnore]` on the property or a
custom `JsonConverter`).

---

### SEC-TASK-004: `DataFolder` Path Not Validated Against Traversal

- **Location**: [specs/001-recent-completed-rfcs/tasks.md — T006](specs/001-recent-completed-rfcs/tasks.md)
- **OWASP Category**: A05:2025-Security Misconfiguration
- **CWE**: CWE-22: Improper Limitation of a Pathname to a Restricted Directory
- **CVSS v3.1 Score**: 1.5 (Informational)
- **Spec-Kit Task**: Amend T006

`RfcArchiveService` constructs the archive path as
`Path.Combine(AppSettings.DataFolder, "archived-rfcs.json")`. `DataFolder` is sourced
from application configuration (`appsettings.json` / environment variables). In the
deployment model (OpenShift / Docker), this value is set by the operator and is not
user-controlled at runtime, making exploitation highly unlikely. However, no task requires
validating or normalizing `DataFolder` at construction time.

If `DataFolder` were set to a relative path with traversal sequences (e.g., `../../etc`),
`Path.Combine` with a literal filename would still write outside the intended directory.
This exists across the whole app (existing `PreviousRFCs.txt` and `Keywords.txt` have the
same pattern), but the new `RfcArchiveService` makes it worth noting explicitly.

**Remediation** (informational — low deployment risk): Amend T006 to note that at
`RfcArchiveService` construction, `DataFolder` should be resolved to an absolute path with
`Path.GetFullPath()` and validated that the resolved path does not escape the working
directory or a configured root before use. This is consistent with how the existing
`data/keys` path is handled in `Program.cs`.

---

## Confirmed Secure Patterns

### SP-TASK-001: All Three Plan-Level Findings Are Fully Tracked as Tasks

SEC-001 (T007), SEC-002 (T008), and SEC-003 (T014) from the plan security review are each
represented by an explicit, clearly labelled implementation task in the correct foundational
phase. None of the security remediations are deferred to a polish phase or left as
implementation notes only.

### SP-TASK-002: Security Controls Are Sequenced Before User Stories

Phase 2 (Foundational) is marked as a hard blocker for all user story work, and both
SEC-001 and SEC-002 mitigations (T007/T008) are in Phase 2. User stories in Phase 3–5
cannot begin until the concurrency and resource-exhaustion protections are in place,
preventing incomplete implementation from reaching user-facing code.

### SP-TASK-003: Test-First Discipline on All Security Tasks

T005 (tests before T006/T007/T008), T013 (tests before T014), and T017 (tests before
T018) all require tests to be written and failing before implementation begins. This
directly embeds Principle IV (Regression Safety) into the delivery sequence and reduces
the chance of security controls being skipped or silently bypassed under time pressure.

### SP-TASK-004: No New Public Attack Surface

The background service (T018/T019) runs entirely in-process and registers no HTTP routes,
controller actions, or new endpoints. The `AppVersion` helper (T014/T015) renders a
static string from assembly metadata into a Razor template — no user-supplied input is
processed or reflected. The `Keywords` field highlight injection into `Rfc` objects is
handled at read time from the user's own stored keywords, not from URL/form parameters.

### SP-TASK-005: Static Analyzer Gate in Polish Phase

T022 (resolve all static analyzer warnings before commit) is explicitly included in
Phase 6, aligning with the constitution's "treat compiler warnings as errors" security
requirement. Combined with T023 (`/speckit.verify.run`), the final phase provides a
structured checklist that catches missed hardening before merge.

---

## Required Task Amendments

No new tasks are needed. The following amendments to existing tasks resolve all findings:

| Finding | Task to Amend | Amendment |
|---------|---------------|-----------|
| SEC-TASK-001 | **T005** | Add AC-8 concurrent-write test to test scope |
| SEC-TASK-002 | **T006, T008** | Require `ILogger<RfcArchiveService>` Warning/Error on each limit/failure condition; verify in T005 test |
| SEC-TASK-003 | **T005, T006** | Add round-trip `Keywords`-exclusion test to T005; note `[JsonIgnore]`/exclusion in T006 |
| SEC-TASK-004 | **T006** | Note `Path.GetFullPath()` validation of `DataFolder` at construction |

---

## Action Plan & Next Steps

1. **Apply amendments** to tasks.md (T005, T006, T008) using `/speckit.security-review.apply` or inline edits before implementation begins.
2. **Durable memory**: The logging gap (SEC-TASK-002) and Keywords serialization exclusion (SEC-TASK-003) are patterns that recur across any feature that persists shared state — worth recording in project memory.
3. **Proceed to implementation** once amendments are applied. All three plan-level security findings are sequenced correctly; no blocking issues remain.

---

## Memory Hub INDEX.md Row

```text
| docs/security-reviews/2026-06-30-completed-rfcs-tasks-review.md | tasks | 2026-06-30 | LOW | C:0 H:0 M:1 L:2 I:1 | A05,A09 |
```
