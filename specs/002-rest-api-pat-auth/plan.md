# Implementation Plan: RFC Retrieval REST API with Personal Access Tokens & User Administration

**Branch**: `002-rest-api-pat-auth` | **Date**: 2026-07-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-rest-api-pat-auth/spec.md`

## Summary

Add a PAT-authenticated REST API to RfcBuddy that accepts a flat list of include
keywords and an optional list of ignore keywords (ignore wins) and returns matching
RFCs as JSON, annotated with per-user change-tracking status. Logged-in users
create and manage Personal Access Tokens (max 90-day lifetime) through the existing
MVC/Razor UI; the raw token is shown once and stored only as a non-reversible hash.
The first user to log in becomes an administrator and can promote others, view a
user list with last-active times, and revoke any user's tokens. A background
maintenance task deletes user directories inactive for 400+ days. The design reuses
the existing file-per-user storage model, the existing keyword-matching and
change-detection logic, and the existing background-service pattern — no new
projects and no external datastore.

## Technical Context

**Language/Version**: C# 14

**Runtime**: .NET 10

**Primary Dependencies**: ASP.NET Core MVC (Razor Views) 10.0.x, existing
`ExcelDataReader` (RFC parsing), `DocX` (unchanged), Microsoft.Extensions.Logging,
`Microsoft.AspNetCore.Authentication` (custom PAT scheme), OIDC/Keycloak (existing
interactive auth). No new third-party packages anticipated.

**Storage**: File system under the configured `DataFolder` (existing model). New
central JSON files (`apitokens.json`, `users.json`) guarded by a named `Mutex`,
mirroring `RfcArchiveService`. New per-user API baseline file (`ApiPreviousRFCs.txt`)
alongside the existing `PreviousRFCs.txt`.

**Testing**: MSTest + Moq + coverlet (existing), in `RfcBuddy.App.Tests` and
`RfcBuddy.Web.Tests`.

**Target Platform**: Linux container (OpenShift/Docker), multi-replica; shared
`DataFolder` volume and shared Data Protection key ring already in place.

**Project Type**: Web application — existing `RfcBuddy.App` core library +
`RfcBuddy.Web` ASP.NET Core MVC app. This feature adds services to the library and
controllers/handlers/views to the web app.

**Performance Goals**: Low-volume internal tool. API responses well under 2 s for a
single search over the ~365-day schedule plus the 5-week completed archive; token
authentication is an O(1) hash lookup in the loaded token store.

**Constraints**: Multi-replica safe (all shared-file writes serialized via Mutex,
consistent with `RfcArchiveService`). Compiler warnings treated as errors and static
analysis clean (constitution). Secrets and PII must never be logged. Token raw value
displayed exactly once.

**Scale/Scope**: Tens of users, tens–hundreds of tokens, a few downstream
integrations. Data volumes remain small.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Status |
|-----------|-----------|--------|
| I. Data Security (NON-NEGOTIABLE) | Tokens stored only as SHA-256 hashes; raw value shown once, never persisted or logged. User identity (PII) stored only in the non-versioned `DataFolder` (`users.json`) and never written to logs (log hashed `UserId` only). Existing OIDC secrets stay in configuration/env. No secrets/PII in prompts or committed files. | PASS |
| II. Simplicity First | Reuses file-per-user storage, the `Mutex`-guarded central-file pattern, existing keyword matching, existing background-service pattern. No new project, no database, no new external dependency. New services are narrow and single-purpose. | PASS |
| III. Adaptability Over Architecture | New behavior sits behind narrow interfaces (`IApiTokenService`, `IUserRegistryService`) and a self-contained authentication handler; the API controller depends only on existing/new interfaces. Storage format is JSON that can be swapped later. | PASS |
| IV. Regression Safety | Unit tests planned for token lifecycle (create/expire/revoke/hash), PAT authentication handler, include/ignore keyword filtering (ignore precedence), change-status computation, admin bootstrap race, and 400-day cleanup selection. Tests run in CI. | PASS |
| V. Ease of Use | Sensible defaults (90-day cap auto-applied with a message; multiple tokens allowed; cleanup automatic). Clear API error responses (401 for bad/expired/revoked tokens) and clear UI validation messages. | PASS |

**Security Requirements**: Dependencies stay pinned (no new packages);
warnings-as-errors and static analysis remain enforced; least-privilege maintained
(admin-only endpoints gated by an authorization policy).

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check (after Phase 1)**: PASS — the data model and contracts
introduce no new projects, no database, and no new dependencies; token secrets remain
hash-only and identity stays out of logs. Design holds to all five principles.

## UI & UX Design (BC Design System)

The user interfaces are fully integrated into the existing MVC server-rendered framework, styled using custom Bootstrap and CSS classes in general alignment with the BC Design System elements:
- **Typography and Fonts**: Handled by the custom `BCSans` and `Noto Sans` font declarations in `site.css`.
- **Theme Colors**: Uses deep BC Govt Blue `#0E3468` for panels/headers/primary buttons and Yellow `#F9B819` for borders and highlights.
- **Form Controls & Inputs**: Form fields use Bootstrap's standard form layout with strong structural CSS labels and input focus indicators.

### 1. Personal Access Tokens Page (`/ApiTokens/Index`)
- **Self-Service Actions**: Users can view all non-revoked and unexpired tokens or trigger a new creation.
- **Card-Structured Layouts**: Tokens are displayed inside clean, styled responsive bootstrap grids or cards instead of unstyled lists.
- **Badged Metrics & Status**:
  - `Active` tokens: Decorated with a prominent badge representing an active state.
  - `Expired` tokens: Marked clearly with a warning status indicator.
  - `Revoked` tokens: Displays a dark state badge that identifies disabled authentication capabilities.
- **Revocation Trigger**: Direct, inline form with an anti-forgery token protecting against unauthorized cross-site revocation requests.

### 2. Create Token Form (`/ApiTokens/Create`)
- **Form Elements**: Self-service input fields for "Label" and "Expiry Date".
- **Guidance messaging**: Features help copy explaining that maximum lifetime is strictly 90 days. Expiry datepicker implements bounds dynamically.
- **Single-view raw token reveal**: On successful creation, redirects to the list index but carries the raw token secret through temporary storage. The UI renders this raw token *exactly once* inside an alert panel (`alert-warning` or `alert-critical`) with explicit visual warning instructions to copy the token immediately as it cannot be shown again.

### 3. User Administration Panel (`/Admin/Index`)
- **Interactive Portal**: Fully secure space gated by the `"Admin"` authorization policy handler.
- **Users Table**: Fully-responsive grid displaying:
  - User Identity (Display Name / NameIdentifier mapped fields) and registered Email Address
  - Last-Active times (resolved safely from change-tracking logs in a friendly format)
  - Current Administrative Role (Yes/No with clear visual indicators)
- **Administrative Utilities**:
  - Mutex-safe Promoted / Demoted options via styled action buttons. Prevent administrative lockout races.
  - Global Token Inspection & Revocation: Displays a table of all active tokens in the entire system, mapping ownership clearly by user, and allowing the administrator to globally revoke any token instantly.

## Project Structure

### Documentation (this feature)

```text
specs/002-rest-api-pat-auth/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── rest-api.md          # Public REST API contract (request/response/errors)
│   └── service-contracts.md # Internal service interface contracts
├── spec.md              # Feature specification
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
src/
├── RfcBuddy.App/
│   ├── Objects/
│   │   ├── ApiToken.cs           # NEW: token record (hash, label, owner, expiry, lastUsed, revoked)
│   │   ├── UserRecord.cs         # NEW: registry entry (userId hash, identity, email, isAdmin, firstSeen)
│   │   ├── RfcChangeStatus.cs    # NEW: enum { New, Changed, Unchanged }
│   │   └── (existing Rfc.cs, PreviousRfc.cs, AppSettings.cs)
│   ├── Services/
│   │   ├── ApiTokenService.cs        # NEW: IApiTokenService — create/list/revoke/authenticate/purge
│   │   ├── UserRegistryService.cs    # NEW: IUserRegistryService — registry (with email resolution), admin bootstrap/roles, last-active, prune
│   │   ├── RfcChangeTracker.cs       # NEW: shared change-status computation (extracted from WordService)
│   │   ├── UserService.cs            # MODIFY: add API-scoped baseline (Web/Api) previous-RFC storage
│   │   └── ExcelService.cs           # MODIFY: add FilterRfcs(include, ignore) helper for the API
│   └── Core/Cryptography.cs          # REUSE: SHA-256 hashing/verification
├── RfcBuddy.Web/
│   ├── Authentication/
│   │   └── ApiTokenAuthenticationHandler.cs  # NEW: custom "ApiToken" auth scheme
│   ├── Authorization/
│   │   └── AdminRequirement.cs + handler      # NEW: "Admin" policy backed by the registry
│   ├── Controllers/
│   │   ├── RfcApiController.cs        # NEW: [ApiController] POST /api/v1/rfcs/search
│   │   ├── ApiTokensController.cs     # NEW: MVC self-service token management
│   │   └── AdminController.cs         # NEW: MVC admin user list & role management
│   ├── Models/
│   │   ├── Api/RfcSearchRequest.cs, RfcSearchResponse.cs, RfcResult.cs  # NEW: API DTOs
│   │   └── TokenListViewModel.cs, UserListViewModel.cs                  # NEW: MVC view models
│   ├── Services/
│   │   └── UserMaintenanceService.cs # NEW: BackgroundService — 400-day inactive cleanup
│   ├── Views/ApiTokens/*, Views/Admin/*  # NEW: Razor views (BC Design System CSS)
│   └── Program.cs                     # MODIFY: register services, PAT scheme, Admin policy, hosted service
tests/
├── RfcBuddy.App.Tests/Services/
│   ├── ApiTokenServiceTests.cs        # NEW
│   ├── UserRegistryServiceTests.cs    # NEW
│   ├── RfcChangeTrackerTests.cs       # NEW
│   └── ExcelServiceTests.cs           # MODIFY: FilterRfcs include/ignore precedence
└── RfcBuddy.Web.Tests/
    ├── Authentication/ApiTokenAuthenticationHandlerTests.cs  # NEW
    ├── Controllers/RfcApiControllerTests.cs                  # NEW
    ├── Controllers/ApiTokensControllerTests.cs               # NEW
    └── Controllers/AdminControllerTests.cs                   # NEW
```

**Structure Decision**: Retain the existing two-project layout (`RfcBuddy.App` core
library + `RfcBuddy.Web` MVC app). Domain logic (tokens, registry, change tracking,
filtering) lives in `RfcBuddy.App` so it is unit-testable without the web host; web
concerns (authentication scheme, authorization policy, controllers, views, background
maintenance) live in `RfcBuddy.Web`. No new projects, consistent with Simplicity
First.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
