# Quickstart & Validation Guide

**Feature**: 002-rest-api-pat-auth | **Date**: 2026-07-02

This guide validates the feature end-to-end. It references the
[REST API contract](contracts/rest-api.md), [service contracts](contracts/service-contracts.md),
and [data model](data-model.md) rather than repeating them.

## Prerequisites

- .NET 10 SDK installed.
- Existing RfcBuddy configuration (OIDC/Keycloak settings and `DataFolder`) as per
  the project README / `appsettings`.
- A populated RFC schedule (the app fetches `ServiceNow-365-Day-Changes.xlsx` into
  `DataFolder`) so searches return data.

## Run the app

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/RfcBuddy.Web/RfcBuddy.Web.csproj
```

## Run the tests

```powershell
dotnet test RfcBuddy.sln
```

Expected: all tests pass, including the new suites listed in
[plan.md](plan.md) (token service, user registry, change tracker, filtering,
auth handler, and the three new controllers).

## Validation scenarios

### 1. First login becomes admin (User Story 3, FR-021/FR-022)

1. On a fresh `DataFolder` (no existing admin), log in via the browser.
2. Confirm an Admin link/area is available to this user.
3. **Expected**: exactly one admin exists (`users.json` shows `IsAdmin: true` for
   this user).

### 2. Create a Personal Access Token (User Story 2, FR-011–FR-014)

1. Open the token management page.
2. Create a token with a label and an expiry within 90 days.
3. **Expected**: the raw token value is displayed exactly once; copy it. Requesting
   an expiry beyond 90 days is capped to 90 days with a message (FR-013).
4. Reload the token list. **Expected**: the token appears with label/created/expiry
   /last-used, but the raw value is never shown again.

### 3. Retrieve RFCs via the API (User Story 1, FR-001–FR-006)

```powershell
$token = "<paste-raw-token>"
$body  = '{ "includeKeywords": ["payments"], "ignoreKeywords": ["sandbox"] }'
Invoke-RestMethod -Method Post -Uri "https://localhost:7xxx/api/v1/rfcs/search" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json" -Body $body
```

- **Expected**: `200` with a JSON body matching the
  [REST API contract](contracts/rest-api.md); every returned RFC matches an include
  keyword and none matches `sandbox`.
- Call again immediately. **Expected**: previously-returned RFCs now report
  `changeStatus: "Unchanged"` (API baseline advanced, FR-007) — independent of the
  web UI's change tracking.

### 4. Ignore precedence & empty include (FR-002, FR-003, edge cases)

- Send a keyword in both `includeKeywords` and `ignoreKeywords`.
  **Expected**: matching RFCs are excluded (ignore wins).
- Send `{ "includeKeywords": [] }`. **Expected**: `200` with an empty `rfcs` array.

### 5. Revocation (User Story 2/3, FR-018–FR-020)

1. Revoke the token from the token page (self-service).
2. Repeat the API call from step 3.
   **Expected**: `401 Unauthorized`, no RFC data.
3. As an admin, revoke another user's token; that user's token then returns `401`.

### 6. Expired token (FR-013, edge case)

- Use (or simulate) a token past its expiry.
  **Expected**: `401 Unauthorized`.

### 7. Admin user list & last-active (User Story 3, FR-025)

1. As admin, open the user list.
2. **Expected**: every known user is listed with their identity and a last-active
   time derived from change-tracking activity.
3. Promote a second user to admin; confirm they gain admin access. Confirm the
   system refuses to remove the last remaining admin (FR-024).

### 8. Inactive-user cleanup (User Story 4, FR-027/FR-028)

1. Seed one user directory whose change-tracking files are dated ≥ 400 days ago and
   one recent.
2. Trigger the maintenance run (startup catch-up or wait for the interval).
3. **Expected**: only the stale directory is deleted; its tokens no longer
   authenticate; the recent user is retained.

## Success check

The feature is validated when scenarios 1–8 pass and `dotnet test RfcBuddy.sln`
is green. Map each scenario back to the Success Criteria in
[spec.md](spec.md) (SC-001 … SC-010).
