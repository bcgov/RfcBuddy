# Phase 0 Research: RFC Retrieval REST API with PATs & User Administration

**Feature**: 002-rest-api-pat-auth | **Date**: 2026-07-02

This document resolves the design unknowns implied by the specification and the
existing codebase. All spec-level `NEEDS CLARIFICATION` items were resolved during
authoring (see spec Clarifications). The decisions below cover implementation
approach and are grounded in the existing patterns in `RfcBuddy.App` and
`RfcBuddy.Web`.

## 1. Token storage & reverse lookup

- **Decision**: Store all tokens in a single central `apitokens.json` file under
  `DataFolder`, guarded by a named `Mutex`, mirroring `RfcArchiveService`. Each
  record holds `Id` (GUID), `TokenHash` (SHA-256 of the raw secret), `Label`,
  `UserId` (the existing SHA-256 hash of the identity), `Created`, `Expires`,
  `LastUsed`, and `Revoked`. Authentication hashes the presented token and looks it
  up by `TokenHash`.
- **Rationale**: The API receives a token before it knows the user, so a reverse
  lookup (token → user) is required. A central hash-keyed store is an O(1) lookup and
  reuses the proven Mutex-guarded JSON pattern already in the codebase. SHA-256
  matches `Core/Cryptography.cs`, so no new crypto dependency is introduced.
- **Alternatives considered**:
  - *Per-user token files only*: would require scanning every user directory on each
    API call to find the owner — slower and more complex. Rejected.
  - *JWT / self-contained signed tokens*: avoids server-side storage but cannot be
    revoked immediately (FR-020) without a denylist, which reintroduces storage.
    Rejected for added complexity with no benefit at this scale.
  - *Reversible encryption of the secret*: violates FR-015 (no recovery of raw
    value). Rejected.

## 2. Token format & one-time display

- **Decision**: Generate a token as an opaque, high-entropy string with a fixed
  human-recognizable prefix (e.g., `rfcbuddy_` + Base64Url of 32 random bytes from a
  cryptographic RNG). Return the raw value once from the create call; persist only
  `SHA-256(rawValue)`. The UI shows the raw value on the creation confirmation view
  and never again.
- **Rationale**: The prefix aids secret-scanning and user recognition; 32 bytes of
  CSPRNG entropy makes guessing infeasible; hashing satisfies FR-014/FR-015.
- **Alternatives considered**: Embedding the record `Id` in the token to look up by
  id then verify hash — marginally reduces hash-collision concerns but adds parsing;
  direct hash-key lookup is simpler and collision risk for SHA-256 is negligible.
  Rejected in favor of simplicity.

## 3. PAT authentication scheme

- **Decision**: Implement a custom `AuthenticationHandler<AuthenticationSchemeOptions>`
  registered as scheme `"ApiToken"`. It reads the credential from the
  `Authorization: Bearer <token>` header, validates it via `IApiTokenService`
  (unexpired, not revoked), updates `LastUsed`, and on success builds a
  `ClaimsPrincipal` whose `Name` is the token owner's identity. The API controller
  uses `[Authorize(AuthenticationSchemes = "ApiToken")]`.
- **Rationale**: Setting `HttpContext.User.Identity.Name` to the owning identity
  means the existing `IPrincipal`-based `UserService` resolves the same `UserId`
  (SHA-256 of the name) with no change — per-user data (keywords, baseline) lines up
  automatically. Keeping the API scheme separate from the interactive OIDC cookie
  scheme means browser and API auth never interfere.
- **Alternatives considered**: Middleware or an endpoint filter that manually parses
  the header — works but bypasses the standard `[Authorize]`/policy pipeline and is
  harder to test. Rejected.

## 4. Separating API vs. web change-tracking baselines

- **Decision**: Persist the API baseline in a per-user file `ApiPreviousRFCs.txt`,
  parallel to the existing `PreviousRFCs.txt`. Generalize the previous-RFC read/write
  in `UserService` to accept a baseline scope (`Web` | `Api`) that selects the
  filename; each successful API search reads the `Api` baseline, computes change
  status, then rewrites the `Api` baseline. The web document flow keeps using `Web`.
- **Rationale**: Satisfies FR-006/FR-007 (separate baselines, API call advances only
  the API baseline). Reuses the identical serialization already in `UserService`, so
  the change is a small parameterization rather than new storage code.
- **Alternatives considered**: A shared baseline (spec Q1 option B) — rejected per
  clarification because API polling and web downloads would reset each other's
  markers. A read-only API baseline (option C) — rejected; the chosen approach lets
  each downstream poll see only changes since its last poll.

## 5. Reusing change-status computation

- **Decision**: Extract the new/changed determination currently embedded in
  `WordService.AddRfcSection` into a small reusable component `RfcChangeTracker` in
  `RfcBuddy.App`, exposing a method that, given a current RFC and the list of
  `PreviousRfc`, returns `RfcChangeStatus` (`New`, `Changed`, `Unchanged`). Both
  `WordService` and the API path call it.
- **Rationale**: DRY and regression-safe (Principle IV): one tested definition of
  "changed" (compares start/end dates and SHA-256 of asset tags, description, risk).
  Avoids drift between Word output and API output.
- **Alternatives considered**: Duplicating the comparison inside the API controller —
  rejected; risks divergent behavior and duplicate tests.

## 6. Include/ignore keyword filtering for the API

- **Decision**: Add a focused method `FilterRfcs(IEnumerable<Rfc> rfcs,
  List<string> includeKeywords, List<string> ignoreKeywords)` to `IRfcService`
  /`ExcelService` that returns RFCs matching at least one include keyword and no
  ignore keyword, reusing the existing `RfcKeywordMatches` (case-insensitive,
  whole-word regex over asset tags, description, risk, etc.). Ignore is evaluated
  first and short-circuits inclusion.
- **Rationale**: Reuses proven matching semantics so API and web results stay
  consistent; a dedicated method is clearer than coercing the ministry/general
  `CategorizeRfcs` signature into a single include list. Ignore-first guarantees
  FR-003 precedence.
- **Alternatives considered**: Reusing `CategorizeRfcs` with `includeKeywords` mapped
  to `ministryKeywords` — its "other" bucket would wrongly include non-matching RFCs.
  Rejected.
- **Note**: An empty include list yields an empty result (FR edge case), matching the
  spec ("no include keywords → empty list").

## 7. RFC source scope for the API

- **Decision**: Build the candidate set from `IRfcService.GetAllRfcs()` (current +
  upcoming schedule) unioned with `IRfcArchiveService.GetCompletedRfcs()` (last 5
  weeks), de-duplicated by RFC number keeping the most recent, then apply
  include/ignore filtering. This mirrors the web `HomeController` flow.
- **Rationale**: Satisfies spec Q2 (schedule + recently-completed archive) so API
  callers see the same universe of changes a reviewer sees.
- **Alternatives considered**: Schedule-only (Q2 option B) or full archive (option C)
  — rejected per clarification.

## 8. User registry, admin roles & first-admin bootstrap

- **Decision**: Add a central `users.json` (Mutex-guarded) holding `UserRecord`
  entries: `UserId` (hash), `Identity` (plaintext username/email for admin display),
  `IsAdmin`, `FirstSeen`. On each authenticated interactive request, ensure the
  current user is registered; if the registry currently contains zero admins,
  atomically promote this user (re-check inside the Mutex to resolve the first-login
  race, FR-021/FR-022). Admin promotion/demotion updates `IsAdmin`, with a guard that
  refuses to remove the last remaining admin (FR-024).
- **Rationale**: A human-readable identity is required so admins can recognize and
  manage users (FR-023/FR-025); the Mutex + in-lock re-check gives exactly-one first
  admin under concurrency. Storing identity is new but confined to the
  non-version-controlled `DataFolder` and never logged, keeping Principle I intact.
- **Alternatives considered**: Deriving admin from an OIDC/Keycloak role claim —
  cleaner in theory but the spec dictates in-app bootstrap ("first user to log in")
  and in-app promotion, which an external IdP role would not provide without config
  changes outside this project. Rejected. Displaying only the hashed `UserId` —
  rejected; admins could not meaningfully identify users.

## 9. Last-active time & 400-day inactive cleanup

- **Decision**: Derive "last active" from the file-system last-write time of the
  user's change-tracking files (`PreviousRFCs.txt` / `ApiPreviousRFCs.txt`) within the
  user directory — the existing change-tracking mechanism — rather than a stored
  field. Add a `UserMaintenanceService : BackgroundService` that runs on the same
  interval pattern as `ArchiveUpdateService` (startup catch-up + periodic), deleting
  any user directory whose last activity is ≥ 400 days old and purging that user's
  tokens (`IApiTokenService`) and registry entry (`IUserRegistryService`).
- **Rationale**: Reuses the existing activity signal (FR-025/FR-027) and the proven
  hosted-service pattern. A dedicated single-purpose service keeps concerns loosely
  coupled (Principle III) without adding a new project.
- **Alternatives considered**: Folding cleanup into `ArchiveUpdateService` — rejected
  to avoid mixing unrelated responsibilities. Manual/admin-triggered cleanup —
  rejected; spec requires automatic removal.

## 10. Admin-only authorization

- **Decision**: Register an `"Admin"` authorization policy backed by an
  authorization handler that checks `IUserRegistryService.IsAdmin(currentUserId)`.
  Apply `[Authorize(Policy = "Admin")]` to `AdminController` actions and to cross-user
  token revocation. Optionally surface admin status as a role claim via claims
  transformation so views can conditionally render the Admin link.
- **Rationale**: Standard ASP.NET Core policy-based authorization; least-privilege
  (FR-026); testable in isolation.
- **Alternatives considered**: Manual per-action checks in controllers — more error
  prone and harder to test consistently. Rejected.

## 11. Concurrency & multi-replica safety

- **Decision**: All writes to `apitokens.json` and `users.json` go through a named
  `Mutex` (as `RfcArchiveService` does for the archive), with read-modify-write under
  the lock. Revocation and `LastUsed` updates are last-write-wins on a per-record
  basis; because revocation only ever sets `Revoked = true`, concurrent updates
  converge safely.
- **Rationale**: Matches the deployment model (multiple pods, shared volume) and the
  existing concurrency approach, satisfying FR-020 immediacy.
- **Alternatives considered**: A database with transactions — over-engineered for the
  scale and contrary to Simplicity First. Rejected.

## Summary of decisions

| # | Area | Decision |
|---|------|----------|
| 1 | Token store | Central Mutex-guarded `apitokens.json`, hash-keyed lookup |
| 2 | Token format | `rfcbuddy_` + 32-byte CSPRNG Base64Url; store SHA-256; show once |
| 3 | API auth | Custom `"ApiToken"` `AuthenticationHandler`, `Bearer` header |
| 4 | Baselines | Separate `ApiPreviousRFCs.txt`; API call advances API baseline only |
| 5 | Change status | Shared `RfcChangeTracker` used by Word + API |
| 6 | Filtering | `FilterRfcs(include, ignore)`, ignore-first precedence |
| 7 | RFC scope | Schedule + 5-week completed archive, de-duped |
| 8 | Users/roles | Central `users.json`; Mutex-safe first-admin bootstrap |
| 9 | Cleanup | `UserMaintenanceService`; delete dirs inactive ≥ 400 days |
| 10 | Authz | `"Admin"` policy backed by registry |
| 11 | Concurrency | Named Mutex read-modify-write, multi-replica safe |

All unknowns resolved. Ready for Phase 1 (data model, contracts, quickstart).
