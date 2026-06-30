# Implementation Plan: Recent Completed RFCs & App Version Display

**Branch**: `001-recent-completed-rfcs` | **Date**: 2026-06-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-recent-completed-rfcs/spec.md`

## Summary

Add a "Completed (last 5 weeks)" listing to the generated Word document for each of
the three keyword areas (Ministry, General, Other), and show the application version
in the footer.

Because the source 365-day schedule contains only current and future RFCs, completed
RFCs must be persisted. The plan introduces a single **shared** archive of full RFC
content (a JSON file in the existing `data/` folder) updated on every document
generation. The archive keeps **at least one copy of every observed RFC** (the latest
version, keyed by RFC number and resolved by most recent end date) and prunes entries
whose end date is older than 5 weeks — guaranteeing weekly coverage across the window.
At generation time, completed RFCs (end date within the last 5 weeks) are read from the
archive and sorted into the three keyword areas using the user's **current** keywords,
reusing the existing categorization logic. The version is read from the web assembly's
informational version (sourced from `<Version>` in the project file) with a safe
fallback.

To meet the "at least one copy per week" target independently of user activity, a hosted
**background service** refreshes the source and updates/prunes the archive on a recurring
interval (default weekly), running once at startup as catch-up. User-triggered generation
continues to update the archive and supplements the scheduled run.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: ASP.NET Core MVC (Razor), DocX 5.0.0 (Word output),
ExcelDataReader 3.8.0 (schedule parsing), `System.Text.Json` (archive serialization,
in-framework), `Microsoft.Extensions.Hosting` `BackgroundService` (recurring update,
in-framework)

**Storage**: File system in the configured `DataFolder` (gitignored `data/`). New shared
`archived-rfcs.json`; existing per-user `Keywords.txt` / `PreviousRFCs.txt` unchanged. New
setting `ArchiveUpdateIntervalDays` (default 7) controls the background cadence.

**Testing**: MSTest, Moq, coverlet (existing `RfcBuddy.App.Tests` and `RfcBuddy.Web.Tests`)

**Target Platform**: Linux container (OpenShift / Docker), .NET 10 ASP.NET Core

**Project Type**: Web application — `RfcBuddy.App` core library + `RfcBuddy.Web` MVC app

**Performance Goals**: Not latency-critical; archive read-modify-write happens once per
"Apply filters and download RFCs" action. Archive bounded to ≤ 365 days of RFCs.

**Constraints**: Compiler warnings treated as errors. Shared archive file writes MUST be
concurrency-safe (multiple authenticated users share one instance + one archive file, and
the background service writes concurrently with user-triggered updates).

**Scale/Scope**: Single shared schedule (hundreds–low thousands of RFCs); small
per-deployment data folder.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Status |
|-----------|-----------|--------|
| I. Data Security (NON-NEGOTIABLE) | Archive stores full RFC change text (descriptions, risk) server-side in the **gitignored** `data/` folder — same location/trust boundary as existing user files. No secrets, credentials, or auth tokens are stored. This is a deliberate change from the prior hash-only `PreviousRFCs.txt`, explicitly approved during clarification (full-content archive). No new data leaves the trust boundary. | PASS (noted) |
| II. Simplicity First | Reuses existing file-storage + service patterns; adds **one** narrow service and one JSON file; no new project, no database. JSON chosen over a delimited format only because RFC free-text would break the `#`-delimited scheme. The recurring capture uses the framework-standard `BackgroundService` (no new dependency) and is justified because the "at least weekly" guarantee cannot be met from user-triggered runs alone. | PASS |
| III. Adaptability Over Architecture | New behavior sits behind a narrow `IRfcArchiveService` interface; categorization logic is extracted for reuse without coupling. | PASS |
| IV. Regression Safety | New unit tests for archive merge/dedup/prune/query, categorization reuse, Word completed section, and version provider; existing `WordServiceTests` updated for the new signature. | PASS |
| V. Ease of Use | Fixed 5-week window (no config), safe version fallback ("unknown"), works with existing defaults. | PASS |
| Security Requirements | Dependencies remain pinned; `System.Text.Json` is in-framework (no new package); warnings-as-errors honored. | PASS |

No violations — Complexity Tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/001-recent-completed-rfcs/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── service-contracts.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Created by /speckit.tasks (not this command)
```

### Source Code (repository root)

```text
src/
├── RfcBuddy.App/
│   ├── Objects/
│   │   └── Rfc.cs                     # Reused as the archive record (unchanged shape)
│   └── Services/
│       ├── ExcelService.cs           # Add GetAllRfcs() + reusable CategorizeRfcs(); ProcessRfcs preserved
│       ├── RfcArchiveService.cs       # NEW: IRfcArchiveService — update/prune/query shared archive
│       └── WordService.cs            # Extend CreateWordFile() with completed RFC lists; add Completed subsection
├── RfcBuddy.App/
│   └── Objects/
│       └── AppSettings.cs            # Add ArchiveUpdateIntervalDays (default 7)
├── RfcBuddy.Web/
│   ├── Controllers/
│   │   └── HomeController.cs          # Capture observed RFCs → archive; read completed → categorize → Word
│   ├── Services/
│   │   └── ArchiveUpdateService.cs     # NEW: BackgroundService — recurring source refresh + archive update/prune
│   ├── Support/
│   │   └── AppVersion.cs              # NEW: resolves assembly informational version with fallback
│   ├── Views/Shared/
│   │   └── _Layout.cshtml            # Version in bottom-left footer
│   ├── wwwroot/css/site.css          # Footer/version positioning (BC Design System tokens)
│   └── Program.cs                    # Register IRfcArchiveService (singleton) + AddHostedService
├── RfcBuddy.App.Tests/
│   └── Services/
│       ├── RfcArchiveServiceTests.cs  # NEW
│       ├── ExcelServiceTests.cs       # Add GetAllRfcs/categorization coverage
│       └── WordServiceTests.cs        # Update for new signature + completed section
└── RfcBuddy.Web.Tests/
    └── Controllers/
        └── HomeControllerTests.cs     # Archive wiring + AppVersion behavior
```

**Structure Decision**: The existing two-project web layout is retained. Domain and
persistence logic live in `RfcBuddy.App` (testable without the web host); web concerns
(version footer, controller wiring, DI) live in `RfcBuddy.Web`. No new projects are added,
honoring Simplicity First.

## Phase 0 — Research

See [research.md](research.md). All Technical Context items are resolved; no `NEEDS
CLARIFICATION` remain. The four spec clarifications plus the planning constraint
"at least one copy of all RFCs for each week" are addressed there, including the recurring
background update design (D3, D12).

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): archive record, retention window, validation/state rules.
- [contracts/service-contracts.md](contracts/service-contracts.md): `IRfcArchiveService`,
  `IRfcService` additions, `IWordService` signature change, and the version provider.
- [quickstart.md](quickstart.md): build/test/run validation scenarios for both user stories.

## Complexity Tracking

No constitution violations — not applicable.
