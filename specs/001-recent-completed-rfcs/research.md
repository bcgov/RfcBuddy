# Phase 0 Research: Recent Completed RFCs & App Version Display

All Technical Context items are known (existing .NET 10 / ASP.NET Core MVC codebase).
The decisions below resolve the design questions raised by the spec and the planning
constraint *"make sure I have at least one copy of all RFCs for each week."*

## D1. Persisting past RFCs (the core problem)

- **Decision**: Maintain a single **shared** archive file, `archived-rfcs.json`, in the
  configured `DataFolder` root, holding the **full content** of every RFC the app has
  observed in the schedule.
- **Rationale**: The 365-day schedule contains only current/future RFCs, so completed
  RFCs vanish once their window passes. The existing per-user `PreviousRFCs.txt` is
  unusable for this: it stores only **hashes** (cannot reconstruct content for display)
  and is **truncated/overwritten every run**. The RFC data itself is not user-specific,
  so one shared archive avoids duplicating the same content per user (Simplicity First)
  and matches the clarified decision.
- **Alternatives considered**:
  - *Per-user archive*: rejected — duplicates identical data across users and complicates
    categorization when keywords change.
  - *Extend `PreviousRFCs.txt`*: rejected — hash-only and per-user; repurposing it would
    break its existing "previously reviewed" change-detection role.

## D2. Storage format

- **Decision**: JSON array of `Rfc` serialized with `System.Text.Json`.
- **Rationale**: RFC descriptions and risk text are free-form and contain newlines and
  arbitrary characters, which would corrupt the existing `#`-delimited line format.
  `System.Text.Json` is in-framework (no new dependency) and robust.
- **Alternatives considered**: delimited text (rejected — delimiter/newline collisions);
  SQLite/EF (rejected — new dependency and schema for a simple list; violates YAGNI).

## D3. Capturing observed RFCs (and "at least one copy per week")

- **Decision**: RFCs are captured from two paths that share the same archive logic:
  (1) a recurring **background update** (default weekly, see D12) and (2) the existing
  user-triggered "Apply filters and download RFCs" action. Each path refreshes the
  schedule, reads **all** RFCs (keyword-independent), **upserts** them into the archive
  keyed by `RfcNumber` (keeping the most recent end date), then **prunes** entries whose
  `EndDate` is older than the 5-week window.
- **Why this guarantees weekly coverage**: The background update runs at least every 7
  days regardless of user activity, and pruning is driven solely by end-date age, so for
  each of the last 5 weeks there is always **at least one** retained copy (the latest
  version) of every RFC that was present in the source that week.
- **Rationale**: A single shared capture routine is reused by both paths; the scheduled
  path removes the dependency on user activity that the original best-effort model had.
- **Alternatives considered**: relying on user-triggered capture alone (rejected — cannot
  guarantee the weekly metric during quiet periods); an external cron/job runner (rejected
  — the app already runs as a long-lived hosted process, so an in-process
  `BackgroundService` is simpler and needs no extra infrastructure).

## D4. Reading all RFCs vs. only categorized ones

- **Decision**: Add `List<Rfc> GetAllRfcs()` to `IRfcService` to return every RFC parsed
  from the schedule, and extract the existing categorization into a reusable
  `CategorizeRfcs(...)` method. `ProcessRfcs(...)` is preserved (it internally composes the
  two) so current behavior and tests keep working.
- **Rationale**: The archive needs all observed RFCs, not just the current user's matches;
  the completed listing needs the same categorization rules applied to archived RFCs.
- **Alternatives considered**: re-reading/re-parsing the Excel separately for the archive
  (rejected — wasteful double parse and divergent logic).

## D5. Categorizing completed RFCs

- **Decision**: At generation time, query completed RFCs (`EndDate` within the last 5
  weeks) from the archive and run them through `CategorizeRfcs(...)` with the user's
  **current** keywords, producing completed Ministry/General/Other lists. Apply the same
  ignore-keyword filtering used for the live schedule.
- **Rationale**: Matches the clarified decision and keeps the completed section consistent
  with the rest of the document. Keyword highlights are recomputed (the archive does not
  persist transient `Keywords`).

## D6. Deduplication to the latest version

- **Decision**: Latest version = the occurrence with the most recent `EndDate` for a given
  `RfcNumber` (spec FR-004 / FR-017). Both archive upsert and the completed query resolve
  to a single entry per RFC number.
- **Edge**: Identical end dates → keep one (last writer wins). Unparseable/`default`
  end date → excluded from the completed listing (FR-008) and not treated as completed.

## D7. Retention window value

- **Decision**: 5 weeks expressed as a constant (`ArchiveRetentionWeeks = 5`, i.e. 35
  days), measured back from "now" at generation time. Future-dated RFCs are retained
  (their end date is not older than the window) so they are already archived when they
  later complete.
- **Rationale**: Fixed window per clarification; constant keeps it explicit and testable.

## D8. Concurrency on the shared file

- **Decision**: Register `IRfcArchiveService` as a **singleton** holding a private lock
  object; perform read-modify-write under the lock and write atomically (write to a temp
  file, then replace the target). Tolerate a missing/corrupt file by treating it as empty.
- **Rationale**: Multiple authenticated users share one app instance and one archive file;
  a singleton + lock + atomic replace prevents interleaved writes and partial reads without
  adding infrastructure. Consistent with the app's single shared `DataFolder` model.

## D9. Source of the displayed version

- **Decision**: Read `AssemblyInformationalVersionAttribute.InformationalVersion` from the
  `RfcBuddy.Web` assembly, trim any build metadata after `+`, and fall back to the
  `AssemblyName.Version` and then the literal `"unknown"` if unavailable.
- **Rationale**: `<Version>1.1.0</Version>` flows into the informational version at build,
  so the footer updates automatically on rebuild (FR-010) with a safe fallback (FR-011).
- **Alternatives considered**: reading the `.csproj` at runtime (rejected — the project
  file is not deployed with the published app); hard-coding (rejected — FR-010).

## D10. Word document placement & styling

- **Decision**: Add a `"Completed (last 5 weeks)"` subsection to each keyword area, after
  the existing "Previously Reviewed" subsection, listing that area's completed RFCs sorted
  by `EndDate` descending and reusing the existing per-RFC rendering. Show "No RFCs found."
  when empty (FR-007). Existing subsections are untouched (FR-012).
- **Rationale**: Minimal, consistent extension of the current `AddRfcSection` structure.
  Per-RFC end dates already display, so weekly boundaries are visible without extra
  grouping (the "per week" constraint is a retention guarantee, not a layout requirement).

## D11. Footer position ("bottom left corner")

- **Decision**: Render the version in the existing `<footer>` as a left-aligned element
  using BC Design System CSS tokens, keeping the existing copyright line. Visible on every
  page via `_Layout.cshtml`.
- **Rationale**: The layout footer is the persistent bottom region; CSS-only positioning
  keeps the frontend JS-free per project UI guidance.

## D12. Recurring background update

- **Decision**: Add an in-process hosted `BackgroundService` (`ArchiveUpdateService`) in
  `RfcBuddy.Web` that, on startup and then on a recurring interval, refreshes the source
  and updates/prunes the shared archive. Cadence is configured by a new
  `AppSettings.ArchiveUpdateIntervalDays` (default 7). Because the service is a singleton
  while `IRfcService` is scoped, each run creates a DI scope via `IServiceScopeFactory` to
  resolve `IRfcService`, and resolves the singleton `IRfcArchiveService` directly.
- **Reliability**: It performs a **catch-up run at startup** so a restart does not skip a
  scheduled capture, and each run is wrapped in try/catch with logging so one failed run
  does not stop the loop (FR-020). Cancellation honors application shutdown.
- **Why "at least weekly" holds**: With the default 7-day interval plus the startup
  catch-up, the archive is refreshed at least once per week unless the process is stopped
  continuously for longer than the interval (documented edge case).
- **Concurrency**: The background run and user-triggered runs both call
  `IRfcArchiveService.UpdateArchive`, which is lock-guarded with atomic file replace (D8),
  so concurrent updates are safe.
- **Alternatives considered**: a `PeriodicTimer`-only loop without startup catch-up
  (rejected — a restart could delay capture beyond a week); persisting a "last update"
  timestamp to schedule precisely (deferred — unnecessary for the accepted guarantee and
  adds state; YAGNI).
