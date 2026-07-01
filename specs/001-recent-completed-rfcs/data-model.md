# Phase 1 Data Model: Recent Completed RFCs & App Version Display

## Entities

### Rfc (existing — reused as the archive record)

Defined in `src/RfcBuddy.App/Objects/Rfc.cs`. No shape change.

| Field | Type | Notes |
|-------|------|-------|
| `RfcNumber` | string | Identity / dedup key |
| `ApprovalStatus` | string | Displayed; not used to decide "completed" |
| `Platform` | string | Displayed |
| `AssetTags` | string | Matched for keyword categorization + displayed |
| `StartDate` | DateTime | Change window start |
| `EndDate` | DateTime | Change window end — drives "completed", dedup, and pruning |
| `Description` | string | Matched + displayed |
| `RiskAssessment` | string | Matched + displayed |
| `Keywords` | List<string> | Transient highlight list; **not persisted** (recomputed at categorization) |

### Archived RFC store

- **Persistence**: single shared file `archived-rfcs.json` in `AppSettings.DataFolder`.
- **Shape**: a JSON array of `Rfc` (serialized without transient `Keywords`, or with it
  ignored on read).
- **Uniqueness**: at most one entry per `RfcNumber` — the latest version by `EndDate`.
- **Scope**: shared across all users (not partitioned by user).

### Retention window (value object / constant)

- `ArchiveRetentionWeeks = 5` (35 days), measured back from the current time at the moment
  the document is generated.

### Update cadence (configuration)

- `AppSettings.ArchiveUpdateIntervalDays` (default `7`): how often the background service
  refreshes the source and updates/prunes the shared archive, independent of user activity.

### Application version (read-only value)

- Source: `RfcBuddy.Web` assembly informational version (from `<Version>` in the project
  file). Surfaced as a string with build metadata (anything after `+`) trimmed; fallback
  `"unknown"`.

## Derived collections (computed, not stored)

| Collection | Definition |
|------------|------------|
| Completed RFCs | Archived RFCs where `now - ArchiveRetentionWeeks ≤ EndDate ≤ now` |
| Completed Ministry / General / Other | Completed RFCs categorized with the user's **current** keywords using the existing matching rules (ignore-keywords excluded) |

## Validation & rules

- **R1 (completed window)**: An RFC is "completed" only when `EndDate ≤ now`; included in
  the listing only when `EndDate ≥ now - 35 days`. (FR-002, FR-003)
- **R2 (missing/invalid end date)**: An RFC whose `EndDate` is `default`/unparseable is
  excluded from the completed listing and is not counted as completed. (FR-008)
- **R3 (dedup)**: For a given `RfcNumber`, only the entry with the most recent `EndDate`
  is retained and shown; ties resolve to a single entry. (FR-004, FR-017)
- **R4 (retention/prune)**: Entries with `EndDate < now - 35 days` are removed on update.
  Future/in-progress entries (`EndDate ≥ now - 35 days`) are kept. (FR-016)
- **R5 (coverage guarantee)**: Every RFC observed at least once while in the schedule is
  retained until 5 weeks after its end date, ensuring at least one copy per RFC for each
  week in the window. (planning constraint)
- **R6 (shared archive)**: One archive instance for all users; the underlying record holds
  full RFC detail. (FR-014)
- **R7 (current-keyword categorization)**: Completed RFCs are sorted into the three areas
  using the keywords currently saved for the requesting user. (FR-015)
- **R8 (scheduled capture)**: A background process refreshes the source and runs the same
  upsert + prune (R3, R4) at least weekly and once at startup, so capture does not depend
  on user activity. A failed run does not stop later runs. (FR-018, FR-019, FR-020)

## State transitions (an RFC, over time)

```text
Observed in schedule ──upsert──▶ In archive (latest version)
   ▲ (user action OR weekly        │
   │  background update)           │
            EndDate passes (now)   ▼
                                Completed (eligible for listing while within 5 weeks)
                                   │
        EndDate older than 5 weeks ▼
                                 Pruned (removed on next archive update)
```

## Document structure impact (Word)

For each keyword area (Ministry, General, Other), the existing subsections remain
unchanged, with a new final subsection:

```text
<Area>: N RFCs
  In Progress
  New or Changed
  Previously Reviewed
  Completed (last 5 weeks)        ← NEW: latest version per RFC, EndDate desc, or "No RFCs found."
```
