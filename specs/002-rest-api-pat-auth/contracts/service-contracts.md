# Internal Service Contracts

**Feature**: 002-rest-api-pat-auth | **Date**: 2026-07-02

Interface-level contracts for the services this feature adds or modifies in
`RfcBuddy.App` and the web integration points in `RfcBuddy.Web`. Signatures are
indicative; exact shapes are finalized during implementation. All timestamps are UTC.

## IApiTokenService (new — RfcBuddy.App)

Manages the lifecycle and authentication of Personal Access Tokens. Backed by the
central Mutex-guarded `apitokens.json`.

| Member | Behavior |
|--------|----------|
| `TokenCreationResult CreateToken(string userId, string label, DateTime requestedExpiryUtc)` | Generates a token, stores only its SHA-256 hash, caps expiry at `now + 90d`, returns the raw value **once** plus metadata and the applied expiry. (FR-011–FR-015) |
| `IReadOnlyList<ApiToken> GetTokensForUser(string userId)` | Lists a user's tokens without raw values. (FR-016) |
| `IReadOnlyList<ApiToken> GetAllTokens()` | Admin: all tokens across users. |
| `AuthenticatedToken? Authenticate(string rawToken)` | Hashes and looks up the token; returns owner identity/UserId if active (not expired, not revoked) and updates `LastUsed`; otherwise null. (FR-009, FR-010, FR-017) |
| `bool RevokeToken(string tokenId, string requestingUserId)` | Revokes if the token belongs to the requester. (FR-018) |
| `bool RevokeTokenAsAdmin(string tokenId)` | Revokes any token. (FR-019) |
| `void PurgeTokensForUser(string userId)` | Removes all of a user's tokens (used by cleanup). (FR-028) |

**Contract notes**: `Authenticate` MUST reject expired or revoked tokens. Revocation
MUST take effect on the next call (live store read). Raw token values are never
returned by any member except `CreateToken`.

## IUserRegistryService (new — RfcBuddy.App)

Maintains the user registry and admin roles. Backed by the central Mutex-guarded
`users.json`.

| Member | Behavior |
|--------|----------|
| `UserRecord EnsureRegistered(string userId, string identity)` | Registers the user if new; if the registry has no admin, atomically promotes this user (first-admin bootstrap, race-safe). (FR-021, FR-022) |
| `bool IsAdmin(string userId)` | True if the user is an administrator. (FR-026) |
| `IReadOnlyList<UserListEntry> GetAllUsers()` | Returns all users with identity, admin flag, and derived `LastActive`. (FR-025) |
| `bool SetAdmin(string targetUserId, bool isAdmin, string requestingAdminUserId)` | Promotes/demotes; refuses to remove the last remaining admin. (FR-023, FR-024) |
| `void RemoveUser(string userId)` | Removes the registry entry (used by cleanup). |
| `IReadOnlyList<string> GetInactiveUserIds(TimeSpan threshold)` | Returns users whose derived `LastActive` is older than the threshold (default 400 days). (FR-027) |

**Contract notes**: `LastActive` is derived from change-tracking file timestamps, not
stored. All mutations serialize via the Mutex with in-lock re-checks. `identity` is
PII and MUST NOT be logged.

## IUserService (modified — RfcBuddy.App)

Add a baseline scope so change tracking can be stored per surface.

| Member | Change |
|--------|--------|
| `List<PreviousRfc> GetPreviousRfcs(BaselineScope scope)` | `scope` selects `PreviousRFCs.txt` (Web) or `ApiPreviousRFCs.txt` (Api). |
| `void SavePreviousRfcs(IEnumerable<Rfc> rfcs, BaselineScope scope)` | Writes to the scope-specific file. |

Existing parameterless overloads may remain as `Web`-scoped wrappers to avoid
touching the web document flow. `BaselineScope` is `{ Web, Api }`.

## IRfcService / ExcelService (modified — RfcBuddy.App)

| Member | Behavior |
|--------|----------|
| `List<Rfc> FilterRfcs(IEnumerable<Rfc> rfcs, List<string> includeKeywords, List<string> ignoreKeywords)` | Returns RFCs matching ≥1 include keyword and no ignore keyword; ignore evaluated first (precedence). Reuses existing `RfcKeywordMatches`. (FR-002, FR-003, FR-004) |

## RfcChangeTracker (new — RfcBuddy.App)

| Member | Behavior |
|--------|----------|
| `RfcChangeStatus GetStatus(Rfc current, IReadOnlyList<PreviousRfc> baseline)` | Returns `New` / `Changed` / `Unchanged` using the existing comparison (dates + SHA-256 of asset tags, description, risk). Shared by `WordService` and the API. (FR-006) |

## Web integration points (RfcBuddy.Web)

| Component | Responsibility |
|-----------|----------------|
| `ApiTokenAuthenticationHandler` (scheme `"ApiToken"`) | Reads `Authorization: Bearer`, calls `IApiTokenService.Authenticate`, builds a `ClaimsPrincipal` whose `Name` is the owner identity. (FR-009, FR-010) |
| `RfcApiController` `[Authorize(AuthenticationSchemes="ApiToken")]` | `POST /api/v1/rfcs/search`: builds candidate set (schedule ∪ 5-week completed, de-duped), applies `FilterRfcs`, computes `RfcChangeStatus` against the API baseline, advances the API baseline, returns the JSON contract. (FR-001, FR-005, FR-006, FR-007) |
| `ApiTokensController` `[Authorize]` (interactive) | Self-service create/list/revoke token UI; shows raw value once. (FR-011–FR-018) |
| `AdminController` `[Authorize(Policy="Admin")]` | User list with last-active, promote admins, revoke any token. (FR-019, FR-023, FR-025, FR-026) |
| `"Admin"` authorization policy | Backed by `IUserRegistryService.IsAdmin`. (FR-026) |
| Registration hook | On authenticated interactive requests, call `EnsureRegistered` (first-admin bootstrap). (FR-021, FR-022) |
| `UserMaintenanceService : BackgroundService` | Periodically deletes user directories inactive ≥ 400 days, purging tokens and registry entries. (FR-027, FR-028) |

## Cross-cutting contract guarantees

- **Security**: token secrets are hash-only; identity/PII never logged; API failures
  reveal no sensitive detail. (Constitution I)
- **Concurrency**: central-file mutations are Mutex-serialized and multi-replica safe.
- **Consistency**: API and Word change status share one implementation
  (`RfcChangeTracker`); API and web keyword matching share `RfcKeywordMatches`.
