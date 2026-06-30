# Project Master Specification

This document archives and consolidates all feature specifications that have been completed and merged into the main project.

---

## Recent Completed RFCs & App Version Display [Source: specs/001-recent-completed-rfcs]

**Completed**: 2026-06-30

### Core Requirement
Add a "Completed (last 5 weeks)" listing to the generated Word document for each of the three keyword areas (Ministry, General, Other), and show the application version in the footer.

### User Stories

#### User Story 1 - Review Recently Completed RFCs Per Keyword Area (Priority: P1)
A CAB reviewer applies their keyword filters and downloads the RFC document. In addition to the in-progress and upcoming changes, the document now includes, for each of the three keyword areas (Ministry, General, and Other/unclassified), a listing of the RFCs whose change window has finished within the last 5 weeks. When the same RFC went through more than one revision during that window, only its most recent version is shown.

**Acceptance Criteria:**
1. For a schedule with RFCs ended within 5 weeks, each keyword area includes a "Completed" section listing them.
2. RFC duplicates within the window show only the most recent end-date occurrence.
3. RFCs ended more than 5 weeks ago are excluded.
4. RFCs not yet completed are excluded.
5. Empty areas display "No RFCs found."

#### User Story 2 - See Application Version in Footer (Priority: P2)
A user looks at the bottom-left corner of the application and reads the current application version for diagnostic/reporting purposes.

**Acceptance Criteria:**
1. The application version is displayed in the bottom-left corner on every page.
2. The version matches the project file version without manual edits.

#### User Story 3 - Reliable Weekly Capture of RFCs (Priority: P2)
The application automatically refreshes the RFC source and records new/changed RFCs on a recurring schedule (at least weekly), maintaining history even during quiet periods.

**Acceptance Criteria:**
1. When the weekly update interval elapses, the source is refreshed and archive updated automatically.
2. On startup, a catch-up update runs so restarts don't skip scheduled captures.
3. Background updates apply the same latest-version and 5-week pruning rules as user-triggered paths.

### Key Functional Requirements
- **FR-001**: Document MUST include completed listings per keyword area for end-dates in last 5 weeks.
- **FR-002**: RFC is completed when end-date is in the past.
- **FR-003**: Completed listing includes only entries within the 5-week window.
- **FR-004**: Duplicate RFC numbers show only the most recent end-date occurrence.
- **FR-005**: 5-week window is fixed (not user-configurable).
- **FR-006**: Completed listings display the same RFC details as other sections (number, dates, status, description).
- **FR-007**: Empty areas clearly indicate no completed RFCs were found.
- **FR-008**: RFCs with missing/unreadable end dates are excluded.
- **FR-009**: Application version displays in bottom-left corner on every page.
- **FR-010**: Version is sourced from the project file version.
- **FR-011**: Missing version displays safe fallback (e.g., "unknown").
- **FR-012**: Existing document sections (in-progress, new/changed, previously-reviewed) remain unchanged.
- **FR-013**: System persists observed RFCs for later display after they leave the schedule.
- **FR-014**: Shared archive stores full RFC content (not per-user copies).
- **FR-015**: Completed listings are derived from shared archive using current user keywords.
- **FR-016**: Archive prunes entries older than 5 weeks; future-dated RFCs are retained.
- **FR-017**: Archive resolves duplicate RFC numbers to the most recent end-date version.
- **FR-018**: Recurring background update (at least weekly) refreshes source and updates archive independently.
- **FR-019**: Background update applies same latest-version and pruning rules.
- **FR-020**: Background update runs at startup and on schedule; failures don't stop subsequent runs.
- **FR-021**: User-triggered generation continues to update archive alongside background updates.

### Success Criteria
- **SC-001**: 100% of RFCs completed within 5 weeks appear in correct area; 0% outside this window.
- **SC-002**: Duplicate RFCs show exactly one entry (most recent end-date).
- **SC-003**: Reviewers can locate recent history without external sources.
- **SC-004**: Footer version matches project file on all pages and updates on rebuild.
- **SC-005**: No regressions in existing document sections.
- **SC-006**: Archived RFCs appear in completed listings while within 5-week window after leaving schedule.
- **SC-007**: Archive refreshed/pruned at least once every 7 days with no user activity.

### Key Entities
- **RFC**: Change request with number, status, assets, change window (start/end dates), description, and risk assessment.
- **Keyword Area**: One of three groupings (Ministry, General, Other/unclassified) for RFC sorting.
- **Completed RFC Listing**: Per-keyword-area collection of latest RFC versions from last 5 weeks.
- **RFC Archive**: Single shared persistent store of observed RFCs, limited to 5-week retention.
- **Scheduled Archive Update**: Recurring process refreshing source and updating/pruning archive at least weekly.
- **Application Version**: Release identifier from project file, surfaced in interface footer.

### Edge Cases
- Missing/unreadable end dates: excluded from completed listing.
- Identical end dates: single entry shown.
- Completed and future occurrences: only completed is eligible.
- 5-week boundary: inclusive of today, exclusive of 35+ days past.
- Missing project version: displays "unknown" fallback.
- Long application downtime: RFC updates missed during gap; restarts perform catch-up.
- RFC ends within update gap: may be missed; shorter intervals reduce risk.
- Archived RFC no longer matches user keywords: categorized by current keywords at generation time.

---

## Revision History

| Feature | Date | Status | Summary |
|---------|------|--------|---------|
| Recent Completed RFCs & App Version Display | 2026-06-30 | Completed | Added completed RFC listings (5-week window, deduped), footer version display, and weekly background archive updates. |
