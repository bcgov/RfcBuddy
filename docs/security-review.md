---
document_type: security-review
assessment_date: 2026-09-11
application: "RFC Buddy"
application_acronym: "RFCBuddy"
overall_risk: MODERATE
total_findings: 4
critical_count: 0
high_count: 0
medium_count: 1
low_count: 3
confirmed_count: 3
probable_count: 1
sonarqube_status: "Passed on 2026-09-11"
dependency_audit_status: "No vulnerable or outdated NuGet packages reported"
---

# Application Security & Dependency Review: RFC Buddy

This is the updated repository security and dependency review for 2026-09-11. Findings are evidence-backed and distinguish source-confirmed controls from operational evidence gaps.

## Executive Summary

The review found and remediated unrestricted forwarded-header trust, missing service-layer admin authorization, floating container base images, and stale dependency metadata. No committed credential material was identified; local development settings are excluded by the web project ignore file. Direct and transitive NuGet audits now report no vulnerable or outdated packages. The remaining risk is moderate because deployment-specific proxy addresses, external schedule degradation controls, and container/SBOM scanning are not fully established.

## Review Scope and Evidence

- ASP.NET Core MVC application under `src/RfcBuddy.Web`.
- Application services under `src/RfcBuddy.App`.
- Dockerfile and GitHub Actions workflow.
- Helm deployment chart under the companion `tenant-gitops-ca61f6` workspace.
- Direct and transitive `dotnet list package --vulnerable`.
- Direct `dotnet list package --outdated`.
- Manual authentication, authorization, secret, data-flow, configuration, and container review.

## Findings

### SEC-002 - Forwarded headers trusted arbitrary proxy hops

| Field | Result |
| :--- | :--- |
| Severity | `MEDIUM` |
| Classification | `Probable; remediated in application code` |
| Location | `src/RfcBuddy.Web/Program.cs` and the Helm deployment template |
| Issue | Previous configuration cleared trust lists and accepted unlimited hops |
| Fix | Forwarding is limited to one hop and only configured proxy IPs are trusted; invalid proxy configuration fails startup |
| Follow-up | Dev, test, and production GitOps values configure the selected F5 proxy addresses |

### SEC-003 - Admin service accepted an unverified requester identity

| Field | Result |
| :--- | :--- |
| Severity | `LOW` |
| Classification | `Confirmed; remediated` |
| Location | `src/RfcBuddy.App/Services/UserRegistryService.cs` |
| Issue | `SetAdmin` relied on its controller policy and did not validate the requester |
| Fix | The service now requires a non-empty requester that is an existing administrator before changing roles |
| Tests | `UserRegistryServiceTests` covers non-admin and unknown requesters |

### SEC-004 - Floating container base-image tags

| Field | Result |
| :--- | :--- |
| Severity | `LOW` |
| Classification | `Confirmed; remediated` |
| Location | `Dockerfile` |
| Issue | SDK and runtime images used floating `10.0` tags |
| Fix | Both images are pinned to immutable MCR manifest digests |
| Follow-up | Update digests through a controlled image dependency process and scan the built image |

### SEC-005 - Stale root dependency lock file

| Field | Result |
| :--- | :--- |
| Severity | `LOW` |
| Classification | `Confirmed; remediated` |
| Location | Root `packages.lock.json` |
| Issue | Obsolete `net8.0` lock data conflicted with the active `net10.0` project graphs |
| Fix | Obsolete root lock file was removed; project lock files were regenerated after package updates |

## Dependency Audit

### Package updates applied

| Package | Previous | Current |
| :--- | :--- | :--- |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | `10.0.10` | `10.0.12` |
| `Microsoft.Extensions.Configuration.Abstractions` | `10.0.9` | `10.0.12` |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.9` | `10.0.12` |
| `Microsoft.NET.Test.Sdk` | `18.7.0` | `18.10.0` |
| `MSTest.TestAdapter` | `4.2.3` | `4.4.0` |
| `MSTest.TestFramework` | `4.2.3` | `4.4.0` |

The current `dotnet list RfcBuddy.sln package --vulnerable --include-transitive` command reported no vulnerable packages. The current `dotnet list RfcBuddy.sln package --outdated` command reported no updates from the configured sources.

## SonarQube Results

The fresh `RfcBuddy@dev` MSBuild scan completed successfully at version `1.2.5`. The quality gate passed, with zero open issues, zero new-code issues, and zero security hotspots. The scan ran the solution tests and collected test result evidence; the existing CI workflow still does not enforce a numeric coverage threshold or run this scan automatically.

## Security Controls Verified

- OIDC browser authentication and PAT API authentication remain separate.
- Admin MVC routes require the `Admin` policy and antiforgery validation.
- PAT values are shown once and only hashes are persisted.
- Production cookies and OIDC correlation/nonce cookies require secure transport.
- Rate limiting is configured at 100 API requests per minute with no queue.
- The container runs as UID 1001.
- Docker build and runtime images are digest-pinned.
- Forwarded-header trust is finite and configuration-bound.
- User role mutation validates authorization inside the application service.

## Remaining Risks and Evidence Gaps

1. `ExcelService` now applies a 30-second timeout, but still needs cancellation propagation, bounded retry, and stale-data behavior for the external schedule source.
2. The CI coverage step does not enforce a numeric threshold.
3. The workflow does not visibly run dependency vulnerability, container, or SBOM scanning.
4. Backup schedule, restore testing, RTO, and RPO remain operational evidence gaps.

## Recommended Follow-up

1. Configure and verify environment-specific trusted ingress addresses.
2. Add external-source timeout/cancellation and clearly marked stale-data behavior.
3. Add vulnerability, container, and SBOM gates to CI.
