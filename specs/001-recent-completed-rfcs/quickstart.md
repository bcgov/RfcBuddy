# Quickstart & Validation: Recent Completed RFCs & App Version Display

This guide validates the feature end-to-end. See [spec.md](spec.md) for requirements,
[data-model.md](data-model.md) for rules, and
[contracts/service-contracts.md](contracts/service-contracts.md) for interface behavior.

## Prerequisites

- .NET 10 SDK
- The existing app configuration (`DataFolder`, schedule `SourceUrl`, Keycloak settings)

## Build & test

```powershell
# From repo root
dotnet build RfcBuddy.sln
dotnet test RfcBuddy.sln
```

Expected: build succeeds with warnings-as-errors clean; all tests pass, including the new
archive, categorization, Word completed-section, and version-provider tests.

## Run the app

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/RfcBuddy.Web/RfcBuddy.Web.csproj
```

## Scenario A — Completed RFCs in the Word document (User Story 1)

1. Sign in and enter keywords for the three areas, then "Apply filters and download RFCs".
2. Open the generated `.docx`. For each area (Ministry, General, Other), confirm a new
   **"Completed (last 5 weeks)"** subsection appears after "Previously Reviewed".
3. Confirm only RFCs whose change window ended within the last 5 weeks appear there, and
   that nothing in progress or future-dated is listed as completed.
4. For an area with no recent completions, confirm it shows **"No RFCs found."**

**Dedup / persistence checks** (best validated via unit tests, see contracts):

- A schedule containing the same RFC number twice with different end dates yields a single
  completed entry — the one with the most recent end date. (AC-2/AC-3, WS-1)
- An RFC seen in an earlier run still appears after it has dropped off the live schedule,
  as long as its end date is within 5 weeks. (AC-5, SC-006)
- An RFC whose end date is older than 5 weeks does not appear and is pruned. (AC-4, R4)

**Archive artifact**: after step 1, confirm `archived-rfcs.json` exists in the configured
`DataFolder` (the gitignored `data/` folder) and contains full RFC content.

## Scenario B — Version in the footer (User Story 2)

1. Open any page in the browser.
2. Confirm the application version appears in the **bottom-left** of the footer and matches
   `<Version>` in `src/RfcBuddy.Web/RfcBuddy.Web.csproj` (currently `1.1.0`).
3. Bump `<Version>`, rebuild, reload — confirm the footer updates automatically. (FR-010)
4. (Negative) With no version available, confirm the footer shows **"unknown"**. (FR-011)

## Scenario C — Weekly background capture (User Story 3)

1. Start the app and **do not** sign in or generate a document.
2. Confirm a catch-up update runs at startup: `archived-rfcs.json` is created/refreshed in
   the `DataFolder` and the logs record an archive update. (BG-1, FR-020)
3. To exercise the recurring path quickly, set `ArchiveUpdateIntervalDays` to a small
   value (or temporarily a fraction) in config, wait for the interval, and confirm a second
   update runs with no user interaction. (BG-2, FR-018)
4. Confirm the scheduled update applies the same dedup + 5-week pruning as the user path
   (best validated via unit tests). (BG-3/BG-5, FR-019)

> Note: the default interval is 7 days; lower it only for local validation.

## Mapping to acceptance criteria

| Scenario step | Requirements / Contracts |
|---------------|--------------------------|
| A.2 | FR-001, FR-006, FR-012, WS-1, WS-3 |
| A.3 | FR-002, FR-003, FR-008, R1, R2, AC-6/AC-7 |
| A.4 | FR-007, WS-2 |
| A dedup/persistence | FR-004, FR-013–FR-017, SC-002, SC-006, AC-2..AC-5 |
| B.2 | FR-009, AV-1 |
| B.3 | FR-010, AV-2 |
| B.4 | FR-011, AV-3 |
| C.2 | FR-020, BG-1, SC-007 |
| C.3 | FR-018, FR-021, BG-2, SC-007 |
| C.4 | FR-019, BG-3, BG-5 |
