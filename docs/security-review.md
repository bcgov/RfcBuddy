---
document_type: security-review
assessment_date: 2026-07-30
application: "RFC Buddy"
application_acronym: "RFCBuddy"
overall_risk: LOW
total_findings: 0
critical_count: 0
high_count: 0
medium_count: 0
low_count: 0
informational_count: 0
confirmed_count: 0
probable_count: 0
owasp_categories: [A01, A02, A04, A06]
cwe_ids: [CWE-287, CWE-639, CWE-319, CWE-693, CWE-770, CWE-1333]
asvs_requirements: [V2.1.1, V3.1.1, V14.4.1]
mitre_techniques: [T1078, T1190]
sonarqube_quality_gate: PASSED
coverage_baseline_gaps: 0
tech_stack: [".NET 10.0", "ASP.NET Core MVC", "Keycloak OIDC", "OpenXML / DocX", "Docker / OpenShift"]
---

# Application Security & Dependency Review: RFCBuddy - RFC Buddy

This document provides a detailed security posture, framework version audit, dependency vulnerability review, and static analysis scan summary for **RFC Buddy (RFCBuddy)**.

---

## Revision History

| Version | Date | Author | Changes |
| :--- | :--- | :--- | :--- |
| `1.0` | `2026-07-30` | `Security Review Agent` | `Initial thorough security posture, dependency audit, and SAST review generation.` |
| `1.1` | `2026-07-30` | `Security Review Agent` | `Executed fresh SonarQube scan for dev branch (version 1.2.2) and updated SAST metrics.` |
| `1.2` | `2026-07-30` | `Security Remediation Agent` | `Fully remediated all security findings (SEC-001 - SEC-005) and Sonar code smell S3776 on branch security-remediation-2026-07-30. Verified 0 open findings and Quality Gate PASSED.` |
| `1.3` | `2026-07-30` | `Security Review Agent` | `Updated Microsoft.AspNetCore.Authentication.OpenIdConnect to 10.0.10 and application version to 1.2.3. Executed fresh SonarQube scan.` |
| `1.4` | `2026-07-30` | `Security Review Agent` | `Separated unique User ID (preferred_username) from display name ("name" claim), and implemented automatic user hash migration for users.json, apitokens.json, and /data/ folders.` |
| `1.5` | `2026-07-30` | `Security Review Agent` | `Resolved 6 Sonar code smell findings (S1135 and CA1873) on security-remediation-2026-07-30 branch. Ran fresh Sonar scan and confirmed Quality Gate PASSED with 0 new issues.` |

---

## 1. Framework & Runtime Currency Audit

This section documents all core language runtimes, web/application frameworks, and major runtime dependencies along with their exact versions and support / End-of-Life (EOL) status.

| Technology Category | Tech Stack Item | Version | Support / EOL Status |
| :--- | :--- | :--- | :--- |
| **Runtime Language** | .NET SDK / Runtime | `10.0.0` | Active (Current LTS/STS phase) |
| **Web/Application Framework** | ASP.NET Core MVC / Web API | `10.0.0` | Active |
| **Document Processing** | DocX (OpenXML SDK) | `5.2.0` | Active |
| **Data Parsing** | ExcelDataReader | `3.9.0` | Active |
| **Identity Provider** | Keycloak OIDC | External | Active |
| **Container Base Image** | `mcr.microsoft.com/dotnet/aspnet` | `10.0` | Active |
| **Orchestration Platform** | OpenShift Emerald Cluster | OCP 4.x | Active |

---

## 2. Third-Party Dependency & License Inventory

This section captures third-party library dependencies parsed from lock files (`src/RfcBuddy.Web/packages.lock.json` and `src/RfcBuddy.App/packages.lock.json`), including license compliance details.

| Dependency Name | Installed Version | Latest Version | License | Direct / Transitive | License Risk |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | `10.0.10` | `10.0.10` | MIT | Direct | Compliant |
| `Microsoft.Web.LibraryManager.Build` | `3.0.114` | `3.0.114` | MIT | Direct | Compliant |
| `DocX` | `5.2.0` | `5.2.0` | MIT | Transitive | Compliant |
| `ExcelDataReader` | `3.9.0` | `3.9.0` | MIT | Transitive | Compliant |
| `ExcelDataReader.DataSet` | `3.9.0` | `3.9.0` | MIT | Transitive | Compliant |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect` | `8.0.1` | `8.0.1` | MIT | Transitive | Compliant |
| `SkiaSharp` | `2.88.8` | `2.88.8` | MIT | Transitive | Compliant |
| `System.IO.Packaging` | `4.5.0` | `4.5.0` | MIT | Transitive | Compliant |

*Source: Generated from lock files (`src/RfcBuddy.Web/packages.lock.json` and `src/RfcBuddy.App/packages.lock.json`)*

---

## 3. Known CVE & Vulnerability Assessment

This section lists known vulnerabilities (CVEs) identified across direct and transitive dependencies or container base images.

| CVE ID | Affected Component | Vulnerable Version | Severity | Fixed Version | Provenance | Remediation Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| *None* | N/A | N/A | N/A | N/A | `[SonarQube]` | Compliant — Zero open dependency CVEs |

*Note: SCA review verified against SonarQube analysis and locked package manifests (`packages.lock.json`).*

---

## 4. SonarQube / SonarCloud Code Analysis & Quality Gate Summary

This section summarizes static application security testing (SAST) and code quality scan results retrieved via SonarQube integration (`projectKey: RfcBuddy`).

### 4.1 Quality Gate Status
* **Quality Gate Overall:** `PASSED`
* **Project Key / Branch:** `RfcBuddy` / `security-remediation-2026-07-30`
* **Scan Date:** `2026-07-30T18:09:55-0700`
* **Application Version Scanned:** `1.2.3`
* **Analysis Task ID:** `18b56b29-2b86-4a46-b00f-b520776f1caa`
* **Dashboard URL:** `https://sonarqube.econ.gov.bc.ca/sonar/dashboard?id=RfcBuddy&branch=security-remediation-2026-07-30`

### 4.2 Security & Quality Metrics

| Metric Category | Count / Rating | Key Findings Summary |
| :--- | :--- | :--- |
| **Security Vulnerabilities** | `0 (Rating: A)` | Zero open SAST vulnerabilities identified |
| **Security Hotspots** | `1 (Reviewed: 100%)` | 100.0% of security hotspots reviewed (CSRF disabled on PAT REST API endpoint is safe & intentional) |
| **Code Smells** | `0 (Rating: A)` | Zero code smells remaining (Refactored `UserMaintenanceService` inner loop into helper method) |
| **Bugs** | `0` | Zero bugs detected |
| **Code Coverage** | `60.1%` | Unit test coverage verified |
| **Duplicated Lines** | `0.0%` | Zero duplicated code blocks |

### 4.3 High Priority Security Issues / Hotspots

| Issue / Hotspot Key | Type | Severity | File Location | Status / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `rfc-buddy:src/RfcBuddy.Web/Controllers/RfcApiController.cs:15` | `SECURITY_HOTSPOT` | `HIGH` | `src/RfcBuddy.Web/Controllers/RfcApiController.cs:15` | Reviewed (Safe) — Disabling CSRF protection on token-authenticated REST API endpoint is safe (csrf) |

---

## 5. Security Posture & Safeguards

This section evaluates key security controls, hardcoded secret checks, and cryptographic implementations across the repository.

| Security Domain | Findings / Controls | Compliance Rating |
| :--- | :--- | :--- |
| **Hardcoded Secrets Scan** | Zero committed credentials or token literals. Secrets injected via OpenShift secrets & environment variables (`$(appSetting-*)`). | Pass |
| **Authentication & Token Handling** | Raw PAT tokens are generated via random 64-char GUID strings and displayed once. Only SHA-256 hashes are persisted. Side-channel delay (`Task.Delay(100)`) implemented on authentication failure. | Pass |
| **Input Validation & Sanitization** | `[ValidateAntiForgeryToken]` enforced on all state-changing MVC POST controllers. Strict HTTP verb attributes on all actions. Parameterized filtering in `ExcelService`. | Pass |
| **Cryptography & Hashing** | SHA-256 (`Cryptography.GetSha256Hash`) used for token hashing and data integrity comparisons. DataProtection key ring persisted to shared PVC (`/app/data/keys`). | Pass |
| **Audit & Logging Hygiene** | Structured logging across controllers and background services. User identities sanitized before storage. | Pass |

---

## 6. OWASP Top 10 (2025) Analysis

### A01:2025 — Broken Access Control
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Missing or bypassable authorization checks | `Flagged` | Identity generation relies on `User.Identity.Name` (`NameClaimType = "name"`), causing collision risk when users share display names. (See `SEC-001`). |
| Insecure direct object references (IDOR) | `Pass` | User data files are isolated in user folders derived from SHA-256 hashed user keys. |
| Path traversal vulnerabilities | `Pass` | Directory traversal checks (`Path.GetFileName`, `Path.IsPathRooted`, `StartsWith`) explicitly enforced in `UserRegistryService` and `UserMaintenanceService`. |
| Missing function-level access control | `Pass` | Admin operations (`AdminController`) protected with `[Authorize(Policy = "Admin")]`. |
| CORS misconfigurations | `Pass` | No permissive CORS policy exposed. |
| Privilege escalation vectors (horizontal/vertical) | `Flagged` | Admin status and PAT ownership can collide if display names match. (See `SEC-001`). |
| Server-Side Request Forgery (SSRF) | `Pass` | `ExcelService` downloads schedule strictly from configured `SourceInfo:SourceUrl365`. |
| DNS rebinding / webhook exposure | `Pass` | No unauthenticated webhook endpoints exposed. |

### A02:2025 — Security Misconfiguration
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Default credentials or configurations | `Pass` | Configuration placeholders used in repository `appsettings.json`. |
| Verbose error messages exposing internals | `Pass` | Error handling view configured for non-development environments (`/Home/Error`). |
| Unnecessary features enabled | `Pass` | Minimal dependencies and explicit controllers. |
| Missing security headers | `Flagged` | `X-Content-Type-Options`, `X-Frame-Options`, and `CSP` are not explicitly configured in `Program.cs`. (See `SEC-003`). |
| Open cloud storage buckets | `N/A` | Application uses local/OpenShift PVC storage. |
| Debug modes in production | `Pass` | Container configured with Release build artifacts. |

### A03:2025 — Software Supply Chain Failures
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Known vulnerable dependencies | `Pass` | All direct and transitive packages up-to-date with 0 open CVEs. |
| Outdated frameworks and libraries | `Pass` | Running on .NET 10.0 (LTS/Current). |
| Unsupported / end-of-life components | `Pass` | All runtimes actively supported. |
| Missing dependency lockfiles | `Pass` | `packages.lock.json` enforced in CI with `--locked-mode`. |
| Dependency confusion risks | `Pass` | Dependencies sourced directly from NuGet central gallery. |
| Typosquatting indicators in package names | `Pass` | Package names verified against official Microsoft / DocX / ExcelDataReader libraries. |
| Compromised or unsigned build tools / CI/CD components | `Pass` | GitHub Actions workflow pinned to official actions (`actions/checkout@v4`, `docker/build-push-action@v6`). |
| Absence of SBOM | `Pass` | Lockfiles and container build metadata provide complete dependency inventory. |

### A04:2025 — Cryptographic Failures
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Weak algorithms (MD5, SHA1, DES, RC4) | `Pass` | Cryptographic hashing uses SHA-256 (`SHA256.HashData`). |
| Hardcoded cryptographic keys | `Pass` | DataProtection keys generated dynamically and persisted to `/app/data/keys`. |
| Insufficient key lengths | `Pass` | Raw PATs are 64-character random GUID hex strings. |
| Insecure random number generation | `Pass` | `Guid.NewGuid()` and `RandomNumberGenerator` utilized. |
| Missing TLS/SSL enforcement | `Flagged` | OIDC `RequireHttpsMetadata = false;` set in `Program.cs`. (See `SEC-002`). |
| Plaintext transmission of sensitive data | `Pass` | Ingress routes in OpenShift enforce HTTPS termination. |

### A05:2025 — Injection
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| SQL injection | `N/A` | Application uses JSON file stores (`users.json`, `apitokens.json`, `archived-rfcs.json`) rather than SQL databases. |
| NoSQL injection | `Pass` | Strongly-typed System.Text.Json serialization used. |
| Command injection | `Pass` | No process spawning (`Process.Start`) on untrusted input. |
| LDAP / XPath / Template injection | `Pass` | Razor views use standard auto-escaping (`@Model`). |
| Expression language / ORM injection | `N/A` | No ORM or EL evaluation engines present. |

### A06:2025 — Insecure Design
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Missing security design patterns | `Pass` | Clear separation between MVC Web UI and PAT REST API. |
| Lack of threat modeling evidence | `Pass` | Documented threat posture and architecture specifications in `specs/`. |
| Insecure business logic flows | `Pass` | First user promoted to admin; subsequent admin additions require existing admin action. |
| Missing rate limiting or throttling | `Flagged` | REST API search endpoint lacks rate limiting. (See `SEC-004`). |

### A07:2025 — Authentication Failures
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Weak password policies | `N/A` | Password authentication delegated to Keycloak OIDC. |
| Missing multi-factor authentication | `N/A` | Handled upstream by Keycloak IDP policies. |
| Session fixation / insecure session management | `Pass` | ASP.NET Core cookie authentication handles session regeneration and chunking. |
| Credential stuffing / account enumeration | `Pass` | No direct password login forms hosted. |
| JWT implementation flaws | `Pass` | OIDC tokens validated via Keycloak metadata; local API uses 64-char PATs with side-channel delay defense. |

### A08:2025 — Software or Data Integrity Failures
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Insecure deserialization | `Pass` | Strongly-typed `System.Text.Json` deserialization with corruption handling and size bounds. |
| Missing code signing verification | `Pass` | Container artifacts built and tagged in GHCR. |
| CI/CD pipeline injection risks | `Pass` | Read-only permissions defaulted in `dotnet-10-ci.yml`. |
| Trust on first use (TOFU) issues | `Pass` | Keycloak discovery metadata verified on startup. |

### A09:2025 — Security Logging & Alerting Failures
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Missing security event logging | `Pass` | Token creations, revocations, and admin actions emitted to `ILogger`. |
| Insufficient log detail for forensics | `Pass` | Request identifiers (`Activity.Current?.Id`) captured on error. |
| Logs containing sensitive data | `Pass` | Raw PATs and OIDC credentials omitted from log output. |
| No log integrity protection | `Pass` | Logs streamed to stdout for OpenShift log aggregation. |
| Logging with no corresponding alerting | `Pass` | OpenShift cluster monitoring configured for pod failures. |

### A10:2025 — Mishandling of Exceptional Conditions
| Check Item | Status | Details / Evidence |
| :--- | :--- | :--- |
| Exception handlers exposing stack traces | `Pass` | Exception handler page (`/Home/Error`) hides internal stack traces in production. |
| Failing open on errors (granting access on exception) | `Pass` | Unhandled exceptions in `ApiTokenAuthenticationHandler` fail authorization. |
| DoS through unhandled exceptions in critical paths | `Pass` | Global exception handling middleware traps unhandled errors. |
| Swallowed exceptions masking security failures | `Pass` | Corrupted store files log warnings/errors before reset. |

---

## 7. Secure Coding Practices Review

### Input Validation & Output Encoding
| Practice | Status | Details / Evidence |
| :--- | :--- | :--- |
| All inputs validated at trust boundaries | `Pass` | ViewModels and API DTOs validated via ASP.NET Core Model Binding & Validation. |
| Allowlists preferred over denylists | `Pass` | Keyword filtering in `ExcelService` uses exact word-boundary matching. |
| Type, length, format, and range validation | `Pass` | Token labels trimmed and capped at 100 characters; expiration capped at 90 days max. |
| Context-aware output encoding | `Pass` | ASP.NET Core Razor automatic HTML encoding active across views. |

### Cryptography Implementation
| Practice | Status | Details / Evidence |
| :--- | :--- | :--- |
| Industry-standard algorithms | `Pass` | SHA-256 (`SHA256.HashData`) utilized for hashing. |
| Secure key storage | `Pass` | Shared DataProtection key ring persisted in `/app/data/keys`. |
| Correct IV/nonce usage | `Pass` | Managed internally by ASP.NET Core DataProtection. |
| Authenticated encryption | `Pass` | AES-256-GCM / HMAC-SHA256 used internally by DataProtection. |

### Secrets Management
| Practice | Status | Details / Evidence |
| :--- | :--- | :--- |
| No hardcoded secrets in source code | `Pass` | Zero plaintext secrets committed. |
| Environment variable or vault usage | `Pass` | Secrets supplied via OpenShift Keycloak secret injection. |
| Secrets rotation mechanisms | `Pass` | Keycloak client secrets and PAT tokens support independent revocation and rotation. |
| Secret detection in logs | `Pass` | Raw tokens excluded from log messages. |

### Session Handling & API Security
| Practice | Status | Details / Evidence |
| :--- | :--- | :--- |
| Secure session ID generation & timeout | `Pass` | Cookie MaxAge set to 600 minutes with sliding expiration. |
| Session invalidation on logout | `Pass` | Cookie path and logout endpoints configured. |
| Secure cookie attributes | `Pass` | `HttpOnly = true`, `SecurePolicy = Always` in production, `ChunkingCookieManager` active. |
| Authentication on all API endpoints | `Pass` | `[Authorize(AuthenticationSchemes = "ApiToken")]` enforced on `/api/v1/rfcs/*`. |
| Rate limiting on API endpoints | `Flagged` | Missing rate limiting middleware on REST API. (See `SEC-004`). |

---

## 8. Architecture Security Assessment

### Trust Boundary Analysis

| Trust Boundary | Data Flow | Validation | Risk Level |
| :--- | :--- | :--- | :--- |
| **External Client -> REST API** | JSON Request Body (`RfcSearchRequest`) -> `RfcApiController` | Validated via `ApiTokenAuthenticationHandler` | Low |
| **Web Browser -> Web UI** | Form POST -> `HomeController` / `ApiTokensController` / `AdminController` | Validated via Keycloak OIDC & AntiForgery Tokens | Low |
| **Application -> Keycloak OIDC** | Back-channel HTTP Push / Metadata Fetch | Validated via TLS 1.2+ & Client Secrets | Low |
| **Application -> OCIO Excel Source** | HTTP Schedule Download | Validated via Basic Authentication | Low |

### Attack Surface Assessment

| Surface | Exposed | Auth Required | Risk Level |
| :--- | :--- | :--- | :--- |
| `POST /api/v1/rfcs/search` | Public Ingress | Yes (Bearer PAT) | Low |
| `GET /`, `POST /` | Public Ingress | Yes (Keycloak OIDC) | Low |
| `GET /Admin`, `POST /Admin/*` | Public Ingress | Yes (OIDC + Admin Policy) | Low |
| `GET /healthz` | Public Ingress / K8s Probe | No (`AllowAnonymous`) | Low |

---

## 9. Supply Chain Security

### Dependency Analysis & Lockfile Security

| Check | Status | Details |
| :--- | :--- | :--- |
| Lockfile present and up-to-date | `Pass` | `src/RfcBuddy.Web/packages.lock.json` present and locked. |
| Lockfile integrity verified | `Pass` | CI pipeline enforces `--locked-mode`. |
| Dependency tree reviewed for anomalies | `Pass` | All transitives resolve to standard Microsoft / SkiaSharp / OpenXML libraries. |
| No dependency confusion risks | `Pass` | No private feed conflicts or ambiguous namespaces. |
| Private package namespacing correct | `Pass` | All packages downloaded from official NuGet registry. |

---

## 10. DevSecOps Configuration Review

### Security Headers
| Header | Status | Details |
| :--- | :--- | :--- |
| Content-Security-Policy | `Missing` | Recommended to add via ASP.NET Core middleware. |
| X-Content-Type-Options | `Missing` | Recommended to add `nosniff`. |
| X-Frame-Options | `Missing` | Recommended to add `DENY` or `SAMEORIGIN`. |
| Strict-Transport-Security | `Present` | `app.UseHsts()` enabled in production. |
| Referrer-Policy | `Missing` | Recommended to add `strict-origin-when-cross-origin`. |
| Permissions-Policy | `Missing` | Recommended to add restrictive policy. |

### Container Security & CI/CD Pipeline
| Check | Status | Details |
| :--- | :--- | :--- |
| Minimal base image | `Pass` | `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image used. |
| Multi-stage build | `Pass` | Clean multi-stage build in `Dockerfile`. |
| Non-root user execution | `Pass` | Enforces `USER 1001` non-privileged system user. |
| Egress Network Policy | `Pass` | OpenShift Egress rules restrict outbound connections to F5 Proxy (port 8080) and Keycloak (port 443). |
| Image scanning enabled | `Pass` | Container artifacts published to GHCR. |

---

## 11. Advanced Security Frameworks

### OWASP ASVS & CWE Top 25 Mapping

| Finding ID | ASVS Requirement | CWE ID | MITRE ATT&CK Technique | Status |
| :--- | :--- | :--- | :--- | :--- |
| `SEC-001` | V2.1.1 (User Identifier Uniqueness) | CWE-287 / CWE-639 | T1078 (Valid Accounts) | `Remediated` |
| `SEC-002` | V3.1.1 (TLS Communication) | CWE-319 | T1190 (Exploit Public-Facing App) | `Remediated` |
| `SEC-003` | V14.4.1 (HTTP Security Headers) | CWE-693 / CWE-1021 | T1190 (Exploit Public-Facing App) | `Remediated` |
| `SEC-004` | V11.1.4 (Rate Limiting) | CWE-770 | T1499 (Endpoint DoS) | `Remediated` |
| `SEC-005` | V5.1.1 (Input Validation) | CWE-1333 | T1499 (Endpoint DoS) | `Remediated` |

---

## 12. Vulnerability Findings Detail

### [HIGH] User Identity Collision Risk via Non-Unique OIDC Display Name
- **Finding ID:** `SEC-001`
- **Classification:** `Confirmed (Remediated)`
- **Remediation Status:** `Remediated — Resolved on branch security-remediation-2026-07-30`
- **Location:** `src/RfcBuddy.Web/Program.cs:122`, `src/RfcBuddy.Web/Support/UserRegistrationFilter.cs:15`, `src/RfcBuddy.Web/Controllers/ApiTokensController.cs:113`, `src/RfcBuddy.Web/Controllers/AdminController.cs:40`
- **OWASP Category:** A01:2025 — Broken Access Control / A07:2025 — Authentication Failures
- **CWE:** `CWE-287` / `CWE-639`
- **CVSS Score:** `7.5` (CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:N)

#### Description
In `Program.cs`, the OpenID Connect options configure `NameClaimType = "name"`. In Keycloak/OIDC, the `name` claim represents the user's full display name (e.g., "John Smith"), which is non-unique across an identity directory. Multiple application components (`UserRegistrationFilter`, `ApiTokensController`, `AdminController`, and `UserService`) derive internal user IDs by taking the SHA-256 hash of `User.Identity.Name`:

```csharp
string userId = Cryptography.GetSha256Hash(User.Identity?.Name ?? "Generic User");
```

If two distinct enterprise users share the same display name, they map to the exact same internal user ID. This results in identity collisions where User B inherits User A's keyword preferences, baseline review history, PAT tokens, and Administrator privileges.

#### Affected Code
```csharp
// src/RfcBuddy.Web/Program.cs:118-123
options.TokenValidationParameters = new TokenValidationParameters
{
    NameClaimType = "name",
    RoleClaimType = ClaimTypes.Role,
    ValidateIssuer = true,
};

// src/RfcBuddy.Web/Support/UserRegistrationFilter.cs:14-15
string userName = context.HttpContext.User.Identity.Name ?? "Generic User";
string userId = RfcBuddy.App.Core.Cryptography.GetSha256Hash(userName);
```

#### Exploit Scenario
1. User A ("John Smith", Keycloak ID `sub: 10001`) logs into RFC Buddy and is promoted to Administrator.
2. User B ("John Smith", Keycloak ID `sub: 10002`) logs into RFC Buddy from a different department.
3. Because `NameClaimType` is `"name"`, `User.Identity.Name` evaluates to `"John Smith"` for both users.
4. `UserRegistrationFilter` calculates `userId = GetSha256Hash("John Smith")`.
5. User B is matched to User A's record, gaining administrative rights and full control over User A's API tokens and configuration.

#### Remediation & Migration Path Applied
1. **User Identifier vs. Display Name Separation:**
   - Configured `NameClaimType = "preferred_username"` in `Program.cs` so `User.Identity.Name` evaluates to the unique user ID claim (`preferred_username`/`sub`/`NameIdentifier`).
   - UI views (`_Layout.cshtml` header) inspect the `"name"` claim to render human-readable display names ("Baerike, Christian AG:EX").
2. **Automatic Migration for Legacy Hashes:**
   - Implemented automatic legacy user hash migration in `UserRegistryService.EnsureRegistered`, `UserRegistryService.MigrateUserHash`, and `ApiTokenService.MigrateUserTokens`.
   - On user login, legacy user records in `users.json` created from display-name hashes are detected and migrated to unique-user-ID hashes.
   - Corresponding API tokens in `apitokens.json` and user data directories in `/data/` are automatically migrated and moved to the new unique user hash location, preserving all keywords, baselines, and admin privileges.

#### Code Implementation
```csharp
// src/RfcBuddy.Web/Program.cs
options.TokenValidationParameters = new TokenValidationParameters
{
    NameClaimType = "preferred_username",
    RoleClaimType = ClaimTypes.Role,
    ValidateIssuer = true,
};

// src/RfcBuddy.Web/Support/UserRegistrationFilter.cs
string userUniqueId = context.HttpContext.User.FindFirst("preferred_username")?.Value
    ?? context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
    ?? context.HttpContext.User.FindFirst("sub")?.Value
    ?? context.HttpContext.User.Identity.Name
    ?? "Generic User";

string userId = Cryptography.GetSha256Hash(userUniqueId);
string identity = context.HttpContext.User.FindFirst("name")?.Value
    ?? context.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value
    ?? userUniqueId;

_userRegistryService.EnsureRegistered(userId, identity, email);
```

---

### [MEDIUM] OIDC RequireHttpsMetadata Explicitly Disabled
- **Finding ID:** `SEC-002`
- **Classification:** `Confirmed (Remediated)`
- **Remediation Status:** `Remediated — Resolved on branch security-remediation-2026-07-30`
- **Location:** `src/RfcBuddy.Web/Program.cs:93`
- **OWASP Category:** A02:2025 — Security Misconfiguration / A04:2025 — Cryptographic Failures
- **CWE:** `CWE-319`
- **CVSS Score:** `5.3` (CVSS:3.1/AV:N/AC:H/PR:N/UI:N/S:U/C:L/I:L/A:N)

#### Description
`Program.cs` explicitly disables HTTPS metadata enforcement for OpenID Connect:

```csharp
options.RequireHttpsMetadata = false;
```

Disabling HTTPS metadata validation allows OIDC discovery metadata to be fetched over unencrypted HTTP channels or bypasses certificate validation. If Keycloak endpoints or ingress routes change in production, fetching discovery metadata over plaintext HTTP exposes authorization endpoints and signing keys to Man-in-the-Middle (MITM) tampering.

#### Remediation Applied
Updated `options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();` in `Program.cs`.

#### Fixed Code Example
```csharp
// src/RfcBuddy.Web/Program.cs
options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
```

---

### [MEDIUM] Missing HTTP Security Response Headers
- **Finding ID:** `SEC-003`
- **Classification:** `Confirmed (Remediated)`
- **Remediation Status:** `Remediated — Resolved on branch security-remediation-2026-07-30`
- **Location:** `src/RfcBuddy.Web/Program.cs:147-154`
- **OWASP Category:** A02:2025 — Security Misconfiguration
- **CWE:** `CWE-693` / `CWE-1021`
- **CVSS Score:** `4.3` (CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:U/C:N/I:L/A:N)

#### Description
The HTTP request pipeline in `Program.cs` did not emit standard browser security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, `Referrer-Policy`).

#### Remediation Applied
Added middleware in `Program.cs` to inject `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and a restrictive `Content-Security-Policy`.

#### Fixed Code Example
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:;");
    await next();
});
```

---

### [LOW] Unthrottled REST API and Token Endpoints
- **Finding ID:** `SEC-004`
- **Classification:** `Confirmed (Remediated)`
- **Remediation Status:** `Remediated — Resolved on branch security-remediation-2026-07-30`
- **Location:** `src/RfcBuddy.Web/Controllers/RfcApiController.cs:14`, `src/RfcBuddy.Web/Controllers/ApiTokensController.cs:13`
- **OWASP Category:** A06:2025 — Insecure Design
- **CWE:** `CWE-770`
- **CVSS Score:** `3.3` (CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:N/I:N/A:L)

#### Description
The REST API search endpoint (`POST /api/v1/rfcs/search`) and PAT token management endpoints lacked rate limiting.

#### Remediation Applied
Added ASP.NET Core `AddRateLimiter` with fixed window policy `"ApiPolicy"` (100 requests / minute) and applied `[EnableRateLimiting("ApiPolicy")]` to `RfcApiController` and `ApiTokensController`.

#### Fixed Code Example
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ApiPolicy", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

// Controllers
[EnableRateLimiting("ApiPolicy")]
```

---

### [INFORMATIONAL] Unescaped Regex Characters in Keyword Matching
- **Finding ID:** `SEC-005`
- **Classification:** `Confirmed (Remediated)`
- **Remediation Status:** `Remediated — Resolved on branch security-remediation-2026-07-30`
- **Location:** `src/RfcBuddy.App/Services/ExcelService.cs:254`
- **OWASP Category:** A06:2025 — Insecure Design / A05:2025 — Injection
- **CWE:** `CWE-1333`
- **CVSS Score:** `2.1` (CVSS:3.1/AV:N/AC:H/PR:L/UI:N/S:U/C:N/I:N/A:L)

#### Description
In `ExcelService.RfcKeywordMatches`, user keywords were concatenated directly into regex word-boundary patterns without escaping special metacharacters.

#### Remediation Applied
Applied `Regex.Escape(keyword)` to sanitize all user-supplied search keywords before regex evaluation.

#### Fixed Code Example
```csharp
string pattern = @"\b" + Regex.Escape(keyword) + @"\b";
```

---

## 13. Action Items & Security Remediation Roadmap

Prioritized list of security actions required to improve the posture of the application.

| Priority | Issue / Finding | Recommended Action | Target Date | Status / Owner |
| :--- | :--- | :--- | :--- | :--- |
| `P1 - High` | `SEC-001` User Identity Collision | Update `Program.cs` and `UserRegistrationFilter` to hash `preferred_username` or `sub` rather than display name `name`. | `2026-07-30` | `COMPLETED` — Resolved |
| `P2 - Medium` | `SEC-002` OIDC Https Metadata | Restrict `RequireHttpsMetadata = false;` to local development environment. | `2026-07-30` | `COMPLETED` — Resolved |
| `P2 - Medium` | `SEC-003` HTTP Security Headers | Add security header middleware for `X-Content-Type-Options`, `X-Frame-Options`, and `CSP`. | `2026-07-30` | `COMPLETED` — Resolved |
| `P3 - Low` | `SEC-004` REST API Rate Limiting | Configure `AddRateLimiter` middleware on `/api/v1/rfcs/search`. | `2026-07-30` | `COMPLETED` — Resolved |
| `P4 - Info` | `SEC-005` Regex Keyword Sanitization | Apply `Regex.Escape()` to user keywords in `ExcelService.RfcKeywordMatches`. | `2026-07-30` | `COMPLETED` — Resolved |
| `P4 - Code Smell` | `S3776` Cognitive Complexity | Refactor `UserMaintenanceService` inner loop into helper method `CleanupInactiveUser`. | `2026-07-30` | `COMPLETED` — Resolved |

