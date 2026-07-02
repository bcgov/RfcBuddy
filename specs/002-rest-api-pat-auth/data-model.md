# Phase 1 Data Model: RFC Retrieval REST API with PATs & User Administration

**Feature**: 002-rest-api-pat-auth | **Date**: 2026-07-02

Entities below are persisted in the existing file-system model under the configured
`DataFolder`. Central files are JSON, serialized with `System.Text.Json` and guarded
by a named `Mutex` (as `RfcArchiveService` already does). Per-user files stay in the
existing `DataFolder/{UserId}/` directory, where `UserId = SHA-256(identity)`.

## Entity: ApiToken

Represents one Personal Access Token. Persisted in the central `apitokens.json`
(a list of records). The raw secret is never stored.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | GUID (string) | Stable identifier used by the UI for revoke actions. |
| `TokenHash` | string | SHA-256 hash of the raw token. Lookup key on authentication. |
| `Label` | string | User-supplied name (required, non-empty, trimmed, length-limited). |
| `UserId` | string | Owner's hashed identity (`SHA-256(identity)`), matches the user directory. |
| `Created` | DateTime (UTC) | Creation timestamp. |
| `Expires` | DateTime (UTC) | Expiry; MUST be ≤ `Created + 90 days`. |
| `LastUsed` | DateTime? (UTC) | Updated on each successful authentication; null until first use. |
| `Revoked` | bool | True once revoked; revoked tokens never authenticate. |

**Validation rules**:

- `Label` required, non-empty after trim; enforce a reasonable max length.
- `Expires` MUST NOT exceed `Created + 90 days`; a longer requested expiry is capped
  to the maximum and the user is informed (FR-013).
- `TokenHash` unique; generated from a fresh CSPRNG value each time.

**State transitions**:

```text
(created, active) --use--> (active, LastUsed updated)
(active) --expires reached--> (expired: rejected by auth)
(active|expired) --revoke--> (revoked: rejected by auth)
(any) --owner directory deleted by cleanup--> (record purged)
```

**Derived / not persisted**: the raw token value (exists only transiently at
creation, returned once).

## Entity: UserRecord

Represents a known user in the central registry `users.json` (a list of records).

| Field | Type | Notes |
|-------|------|-------|
| `UserId` | string | `SHA-256(identity)`; primary key; matches the user directory name. |
| `Identity` | string | Human-readable identity (username/email) for admin display. PII — stored only in `DataFolder`, never logged. |
| `IsAdmin` | bool | Administrator flag. |
| `FirstSeen` | DateTime (UTC) | When the user was first registered. |

**Validation rules**:

- Exactly one first admin is created on a fresh registry (bootstrap under Mutex).
- The registry MUST always retain at least one admin (FR-024); demotion/last-admin
  removal is refused.

**Derived (not stored)**:

- `LastActive` — computed from the max file-system last-write time of the user's
  change-tracking files (`PreviousRFCs.txt`, `ApiPreviousRFCs.txt`) in the user
  directory. Used for the admin list (FR-025) and the 400-day cleanup (FR-027).

**State transitions**:

```text
(unknown) --first authenticated request--> (registered; admin if no admin exists yet)
(registered, non-admin) --admin promotes--> (registered, admin)
(registered, admin) --admin demotes (if not last admin)--> (registered, non-admin)
(registered) --inactive >= 400 days--> (directory + tokens + record purged)
```

## Entity: API change-tracking baseline (per user)

The record of RFCs an identity has already seen **via the API**, kept separate from
the web baseline.

- **Storage**: `DataFolder/{UserId}/ApiPreviousRFCs.txt`, same line format and
  serialization as the existing `PreviousRFCs.txt` (RFC number + start/end dates +
  SHA-256 of asset tags, description, risk assessment).
- **Lifecycle**: read at the start of an API search to compute change status, then
  rewritten with the RFCs returned by that search (advances the API baseline only).
- **Reuse**: the existing `PreviousRfc` object represents each entry.

## Entity: RfcChangeStatus (value)

Enum describing an RFC's status relative to a baseline.

- `New` — RFC number not present in the baseline.
- `Changed` — present but dates or any tracked hash differ.
- `Unchanged` — present and all tracked fields match.

Computed by `RfcChangeTracker` (shared by `WordService` and the API), using the same
comparison as today: equal `StartDate`, equal `EndDate`, and matching SHA-256 of
`AssetTags`, `Description`, and `RiskAssessment`.

## Existing entities (reused, unchanged)

- **Rfc** — the change record parsed from the schedule and archive; the unit returned
  by the API (projected into the `RfcResult` DTO — see contracts).
- **PreviousRfc** — one baseline entry (RFC number + dates + field hashes).
- **AppSettings** — global settings (`DataFolder`, intervals). A cleanup interval /
  threshold may be surfaced here if configurability is desired (default 400 days).

## Relationships

```text
UserRecord (1) ──owns──> (0..*) ApiToken        // via UserId
UserRecord (1) ──has──>  (0..1) Web baseline (PreviousRFCs.txt)
UserRecord (1) ──has──>  (0..1) API baseline (ApiPreviousRFCs.txt)
ApiToken   (1) ──authenticates as──> UserRecord // token owner drives per-user data
Rfc        (*) ──projected to──> RfcResult (+ RfcChangeStatus per API baseline)
```

## Data security notes (Principle I)

- Only `TokenHash` (SHA-256) is stored for tokens; the raw value is returned once and
  never persisted.
- `Identity` (PII) lives only in `users.json` inside the non-versioned `DataFolder`;
  logs use the hashed `UserId` only.
- Central files inherit the existing `DataFolder` volume permissions; no secrets are
  placed in configuration files or source control.
