---
document_type: security-review
review_type: plan
assessment_date: 2026-06-30
codebase_analyzed: RfcBuddy
total_files_analyzed: 6
total_findings: 3
overall_risk: MODERATE
critical_count: 0
high_count: 0
medium_count: 2
low_count: 1
informational_count: 0
owasp_categories: [A01, A05]
cwe_ids: [CWE-362, CWE-400, CWE-22]
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

# Security Review — Plan Review: Recent Completed RFCs & App Version Display

*   **Assessment Date**: 2026-06-30
*   **Feature Branch**: `001-recent-completed-rfcs`
*   **Plans Reviewed**: 
    *   [specs/001-recent-completed-rfcs/plan.md](specs/001-recent-completed-rfcs/plan.md)
    *   [specs/001-recent-completed-rfcs/research.md](specs/001-recent-completed-rfcs/research.md)
    *   [specs/001-recent-completed-rfcs/data-model.md](specs/001-recent-completed-rfcs/data-model.md)
    *   [specs/001-recent-completed-rfcs/contracts/service-contracts.md](specs/001-recent-completed-rfcs/contracts/service-contracts.md)

---

## Executive Summary

The proposed technical implementation is highly aligned with the project's **Principle II (Simplicity First)** and **Principle I (Data Security)**. By using a single flat file inside the existing, gitignored `data/` volume, the design avoids the overhead of introducing an database service (like SQLite/EF Core) while maintaining standard file security.

However, moving from a fully static schedule-generation model to a persistent, writable file system database introduces new state boundaries and concurrency risks. Because a singleton background worker (`ArchiveUpdateService`) and multiple concurrent user-initiated threads will now read, parse, write, and prune files on the same disk location, we must identify and resolve race conditions, resource exhaustion, and path traversal vectors in the design phase.

We have assessed the overall design risk as **MODERATE** due to concurrent file operations on a shared file system. All issues are remediable with simple, robust thread-safety and validation patterns before code is committed.

---

## Plan Artifacts Reviewed

1.  **[plan.md](specs/001-recent-completed-rfcs/plan.md)**: Master plan defining components (`RfcArchiveService`, `ArchiveUpdateService`, etc.) and the Constitution Check.
2.  **[research.md](specs/001-recent-completed-rfcs/research.md)**: Decisions D1–D12, addressing persistence, background scheduling, concurrency, and assembly reflection.
3.  **[data-model.md](specs/001-recent-completed-rfcs/data-model.md)**: Validation, state matching, and pruning parameters (35 days lookback / 7-day update cycle).
4.  **[contracts/service-contracts.md](specs/001-recent-completed-rfcs/contracts/service-contracts.md)**: Interface definitions for the background worker and the archive store.

---

## Vulnerability Findings

### SEC-001: Race Condition (TOCTOU) on Shared JSON Archive file

*   **Location**: [specs/001-recent-completed-rfcs/contracts/service-contracts.md](specs/001-recent-completed-rfcs/contracts/service-contracts.md#L3) (and [research.md](specs/001-recent-completed-rfcs/research.md#L45))
*   **OWASP Category**: [A05:2025-Security Misconfiguration](https://owasp.org/Top10/A05_2021-Security_Misconfiguration/) / Broader race conditions
*   **CWE ID**: [CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization](https://cwe.mitre.org/data/definitions/362.html)
*   **CVSS v3.1 Score**: 5.3 (Medium) (AV:N/AC:H/PR:L/UI:N/S:U/C:N/I:H/A:N)
*   **Spec-Kit Task Link**: `TASK-SEC-001`
*   **Risk Description**: 
    The plan correctly notes in **D8** and **D12** that `IRfcArchiveService` is registered as a singleton with a private lock object to serialize writes. However, if multiple replicas of the web application run in the target **OpenShift container hosting environment** (where pods are scaled out for load balancing, sharing the persistent network volume `data/`), **in-process locks (like C# `lock` statements) will not serialize write access across separate container processes/pods**. 
    
    If Pod A and Pod B both write to `archived-rfcs.json` concurrently (either triggered by concurrent users or-interleaved background schedules), the file will experience interleaved stream writes resulting in corruption (invalid JSON) or a "lost update" where Pod A's changes are wiped out by Pod B.
*   **Remediation Recommendation**:
    We must use an OS-level file lock rather than or in addition to an in-memory lock if files are shared on a networked mount, OR we can handle concurrency via an atomic write-and-replace loop that catches file lock/sharing violations. In standard .NET:
    
    1.  Use `FileStream` with `FileShare.None` when opening for write. This coerces the operating system / NFS layer to enforce exclusion.
    2.  Write the updated content to a unique temporary file in the same directory (e.g., `archived-rfcs.json.tmp-{Guid}`) under a retry block.
    3.  Call `File.Move(tempFile, targetFile, overwrite: true)`. On Windows/Linux, `File.Move` is atomic when on the same partition, preventing readers from seeing a half-written file.
    4.  Wrap file operations in a lightweight retry loop (e.g. 3 attempts with exponential backoff other than standard exceptions) to handle transient locking when shared mounts experience tight races.

---

### SEC-002: Potential Denial of Service (DoS) via Unbounded Archive Growth

*   **Location**: [specs/001-recent-completed-rfcs/data-model.md](specs/001-recent-completed-rfcs/data-model.md#L25)
*   **OWASP Category**: [A05:2025-Security Misconfiguration](https://owasp.org/Top10/A05_2021-Security_Misconfiguration/) / Resource management
*   **CWE ID**: [CWE-400: Uncontrolled Resource Consumption](https://cwe.mitre.org/data/definitions/400.html)
*   **CVSS v3.1 Score**: 4.3 (Medium) (AV:N/AC:L/PR:L/UI:N/S:U/C:N/I:N/A:L)
*   **Spec-Kit Task Link**: `TASK-SEC-002`
*   **Risk Description**: 
    The plan relies on a weekly pruning cycle (`EndDate < now - 35 days`) to keep the archive bounded. However, if the background schedule fails to run (e.g., due to background update network errors) or if a very large or corrupted Excel file is parsed with many thousands of past or future RFCs, the in-memory processing loop and JSON serializer will consume substantial heap memory, potentially causing an Out-Of-Memory (OOM) crash (Denial of Service) on the container.
*   **Remediation Recommendation**:
    1.  **Cap the input**: In `RfcArchiveService.UpdateArchive`, refuse to merge or write more than a sensible hard ceiling of distinct RFC records (e.g., maximum 5,000 active records in the 5-week window).
    2.  **Defensive read limits**: When deserializing `archived-rfcs.json`, limit the maximum character/byte read stream size. If the file is larger than 10MB (which is extremely large for raw RFC JSON text), reject it as corrupt and recreate a fresh archive.

---

### SEC-003: Path Traversal Risks on AppVersion Resource Retrieval

*   **Location**: [specs/001-recent-completed-rfcs/contracts/service-contracts.md](specs/001-recent-completed-rfcs/contracts/service-contracts.md#L75)
*   **OWASP Category**: [A01:2025-Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
*   **CWE ID**: [CWE-22: Improper Limitation of a Pathname to a Restricted Directory](https://cwe.mitre.org/data/definitions/22.html)
*   **CVSS v3.1 Score**: 2.7 (Low) (AV:N/AC:H/PR:H/UI:N/S:U/C:L/I:N/A:N)
*   **Spec-Kit Task Link**: `TASK-SEC-003`
*   **Risk Description**: 
    `AppVersion` utilizes assembly reflection, which is extremely secure. However, in the event that the system is extended to search for adjacent package configuration files on disk (like reading `RfcBuddy.Web.csproj` or a package manifest directly from the bin directory to locate additional version details), a maliciously crafted directory environment could cause path traversal issues if relative paths are incorrectly resolved.
*   **Remediation Recommendation**:
    We must strictly adhere to **reflection-only version resolution**, as proposed in the contracts. Avoid looking up physical file structures on disk (like `.csproj` files or build files in sibling folders) to gather version info. Stick to resolving version numbers via `typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()` which takes place safely within memory.

---

## Confirmed Secure Patterns

### SP-001: Data Isolation inside the Gitignored Volumetric Boundary

*   The archive location uses `AppSettings.DataFolder` (`data/` folder in the project root).
*   The `data/` folder is explicitly gitignored in `src/RfcBuddy.Web/.gitignore` (`data/`).
*   This ensures that no historical RFC data (which may contain internal asset names, server names, descriptions, or risk records) can accidentally be committed to Github or exposed in code repositories.

### SP-002: Minimal Dependency Surface Area

*   The design utilizes the web framework's native `Microsoft.Extensions.Hosting.BackgroundService` for scheduling and the native `System.Text.Json` for serialization.
*   This strictly complies with **Principle II (Simplicity First)** and prevents the introduction of bloated external schedulers (like Quartz.NET or Hangfire) or external DB models, preserving a low vulnerability surface.

### SP-003: Non-interactive Backchannel Isolation

*   The background scheduled processor (`ArchiveUpdateService`) operates as an unsupervised singleton thread inside the web host.
*   It does not expose any new HTTP routes, parameters, or controller actions, entirely bypassing public-facing endpoint vectors (Broken Access Control) for the scheduled run.

---

## Action Plan & Next Steps

1.  **Durable Memory Preservation**: We will write this security review to the repository index.
2.  **Backlog Task Generation**: In the task creation phase (`/speckit.tasks`), we will explicitly inject technical tasks for these three vulnerabilities under the following tracking IDs:
    *   `TASK-SEC-001` (Concurrency-safe atomic file replacement loop)
    *   `TASK-SEC-002` (Max record count and file size limits for protection against resource exhaustion)
    *   `TASK-SEC-003` (Adherence to reflection-only assembly reading for version resolution)

---

## Memory Hub INDEX.md Routing Row

Paste this routing entry into your `docs/memory/INDEX.md` once created:

```text
| docs/security-reviews/2026-06-30-completed-rfcs-plan-review.md | plan | 2026-06-30 | MODERATE | C:0 H:0 M:2 L:1 | A01,A05 |
```
