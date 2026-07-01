# Feature Specification: Recent Completed RFCs & App Version Display

**Feature Branch**: `001-recent-completed-rfcs`

**Created**: 2026-06-30

**Status**: Completed

**Input**: User description: "I want to see the last 5 weeks of completed RFCs in each of the 3 keyword areas. If an RFC was updated in those 5 weeks, I want to see only the latest version. Also, display the version number from the project file in the bottom left corner of the app."

## Clarifications

### Session 2026-06-30

- Q: The source schedule contains only current and future RFCs, so where should past/completed RFCs be persisted? → A: A single shared archive of all RFCs (full content), filtered into each user's keyword areas at generation time.
- Q: Which keywords decide which of the 3 areas a completed RFC appears in? → A: The user's current keywords at the time the document is generated.
- Q: How long should archived past RFCs be retained? → A: Prune anything whose end date is older than the 5-week window.
- Q: Is best-effort capture acceptable, or must completeness be guaranteed? → A: Best-effort capture during normal app use; gaps are acceptable when the application is not run while an RFC is in the schedule.

### Session 2026-06-30 (revision)

- Update: To meet the "at least one copy of every RFC for each week" target without depending on user activity, a recurring background update (at least weekly) refreshes the RFC source and updates/prunes the shared archive automatically. User-triggered generation still updates the archive and supplements this. Gaps remain possible only if the application process is stopped for longer than the update interval.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Review recently completed RFCs per keyword area (Priority: P1)

A CAB reviewer applies their keyword filters and downloads the RFC document. In
addition to the in-progress and upcoming changes, the document now includes, for
each of the three keyword areas (Ministry, General, and Other/unclassified), a
listing of the RFCs whose change window has finished within the last 5 weeks.
When the same RFC went through more than one revision during that window, only
its most recent version is shown, so the reviewer sees a clean history without
duplicate or superseded entries. Because the live schedule lists only current and
upcoming changes, these completed entries are drawn from RFC data the application
retained from earlier processing runs rather than from the current schedule.

**Why this priority**: This is the core of the request. It gives reviewers the
recent-change context they need during CAB meetings without manually scanning the
full 365-day schedule, and it is the larger of the two changes.

**Independent Test**: Provide a schedule containing RFCs with past, present, and
future change windows (including a duplicated RFC number with two different end
dates inside the 5-week window). Generate the document and confirm each keyword
area contains a "completed in the last 5 weeks" listing showing only the most
recent version of the duplicated RFC and excluding anything older than 5 weeks or
not yet finished.

**Acceptance Scenarios**:

1. **Given** a schedule with RFCs whose end date falls within the last 5 weeks,
   **When** the reviewer generates the document, **Then** each keyword area
   includes a section listing those completed RFCs.
2. **Given** an RFC number that appears multiple times within the 5-week window
   with different end dates, **When** the document is generated, **Then** only the
   occurrence with the most recent end date is shown for that RFC.
3. **Given** an RFC whose change window ended more than 5 weeks ago, **When** the
   document is generated, **Then** that RFC is not included in the completed
   listing.
4. **Given** an RFC whose change window has not yet ended, **When** the document
   is generated, **Then** that RFC is not included in the completed listing.
5. **Given** a keyword area with no RFCs completed in the last 5 weeks, **When**
   the document is generated, **Then** that area's completed listing clearly
   indicates that no RFCs were found.

---

### User Story 2 - See the application version in the footer (Priority: P2)

A user (or support contact) looks at the bottom-left corner of the application
and can read the current application version, so they can confirm which release
is deployed when reporting issues or verifying an update.

**Why this priority**: Useful operational/diagnostic information and explicitly
requested, but small and independent of the RFC document changes.

**Independent Test**: Open the application in a browser and confirm the version
matches the version declared in the project file, displayed in the bottom-left
corner on every page.

**Acceptance Scenarios**:

1. **Given** the application is running, **When** a user views any page, **Then**
   the current application version is displayed in the bottom-left corner.
2. **Given** the project file version changes for a new release, **When** the
   application is rebuilt and run, **Then** the footer shows the updated version
   without any further manual edits.

---

### User Story 3 - Reliable weekly capture of RFCs (Priority: P2)

As the system owner, I want the application to automatically refresh the RFC
source and record any new or changed RFCs on a recurring schedule (at least once a
week), so the completed-RFC history stays complete even during quiet periods when
no one generates a document.

**Why this priority**: The completed listing (User Story 1) is only as complete as
the data captured. Automatic weekly capture removes the dependency on user activity
and is what makes the "at least one copy of every RFC for each week" guarantee hold.

**Independent Test**: With no user interaction, let the scheduled update run (or
trigger it) and confirm the shared archive is refreshed from the source and pruned,
without anyone generating a document.

**Acceptance Scenarios**:

1. **Given** the application is running and no user is active, **When** the weekly
   update interval elapses, **Then** the source is refreshed and the archive is
   updated and pruned automatically.
2. **Given** the application has just started, **When** it initializes, **Then** it
   performs a catch-up update so a restart does not skip a scheduled capture.
3. **Given** the scheduled update runs, **When** it updates the archive, **Then** it
   applies the same latest-version and 5-week pruning rules as the user-triggered
   path.

---

### Edge Cases

- An RFC row has a missing or unreadable end date: it is treated as not having a
  determinable completion date and is excluded from the completed listing.
- The same RFC number appears multiple times with identical end dates inside the
  window: a single entry is shown (one of the identical occurrences).
- An RFC has both a completed occurrence and a separate future occurrence: only
  the completed occurrence is eligible for the completed listing; the future
  occurrence continues to appear in the existing upcoming/new sections.
- The 5-week boundary is inclusive of changes completing today and excludes those
  completing exactly more than 5 weeks ago.
- The project file does not declare a version: the footer shows a safe fallback
  (e.g. "unknown") rather than failing or rendering blank.
- The application process is stopped for longer than the update interval: the
  scheduled capture cannot run during the outage, so RFCs that both appear and
  complete entirely within that gap may be missed; on restart the application
  performs a catch-up update.
- An RFC appears and its change window ends entirely within a single gap between
  scheduled updates: it may be missed; a shorter update interval reduces this risk.
- An RFC was captured earlier but its current version is no longer relevant to any
  of the user's keyword areas: it is categorized using the user's current keywords
  at generation time, the same as any other RFC.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The generated RFC document MUST include, for each of the three
  keyword areas (Ministry, General, Other/unclassified), a listing of RFCs whose
  change window ended within the last 5 weeks.
- **FR-002**: An RFC MUST be treated as "completed" when its change-window end
  date/time is in the past relative to the moment the document is generated.
- **FR-003**: The completed listing MUST only include RFCs whose end date is
  within the most recent 5-week period (i.e. not earlier than 5 weeks before the
  current date and not in the future).
- **FR-004**: When the same RFC number appears more than once within the 5-week
  window, the listing MUST show only the occurrence with the most recent end
  date/time and MUST suppress the older occurrence(s). *(Display-layer rule; the
  archive-layer enforcement of the same dedup rule is FR-017.)*
- **FR-005**: The 5-week lookback window MUST be a fixed period and MUST NOT
  require user configuration.
- **FR-006**: Each keyword area's completed listing MUST display, for every RFC
  shown, the same identifying details already used for RFCs elsewhere in the
  document (such as RFC number, dates, status, and description) so reviewers can
  interpret each entry consistently.
- **FR-007**: When a keyword area has no RFCs completed in the last 5 weeks, the
  document MUST clearly indicate that no completed RFCs were found for that area.
- **FR-008**: An RFC with a missing or unreadable end date MUST be excluded from
  the completed listing.
- **FR-009**: The application MUST display its current version in the bottom-left
  corner of the interface, visible on every page.
- **FR-010**: The displayed version MUST be sourced from the application's project
  file version so it stays correct across releases without manual edits.
- **FR-011**: If no version is available from the project file, the application
  MUST display a safe fallback indicator instead of an empty or broken value.
- **FR-012**: Adding the completed listing MUST NOT remove or alter the existing
  in-progress, new/changed, and previously-reviewed sections of the document.
- **FR-013**: Because the source schedule contains only current and future RFCs,
  the system MUST persist the RFCs it observes so that RFCs whose change window
  has since ended can still be listed after they leave the schedule.
- **FR-014**: The persisted store MUST be a single shared archive covering all
  observed RFCs (not a separate per-user copy) and MUST retain the full RFC detail
  needed to render the completed listing.
- **FR-015**: The completed listing MUST be derived from the shared archive and
  sorted into the three keyword areas using the user's current keywords at the
  time the document is generated.
- **FR-016**: The archive MUST prune entries whose end date is older than 5 weeks
  (`EndDate < now − 35 days`). Future-dated RFCs (end date not yet passed) MUST NOT
  be pruned — they remain stored until 5 weeks after their end date. (See data-model R4.)
- **FR-017**: When the same RFC number has been observed more than once, the
  archive MUST resolve to the version with the most recent end date/time for
  display. *(Archive-layer enforcement of the same dedup rule stated in FR-004.)*
- **FR-018**: The system MUST run a recurring background update, at least weekly,
  that refreshes the RFC source and updates the shared archive without requiring
  any user interaction.
- **FR-019**: The background update MUST apply the same latest-version resolution
  and 5-week pruning rules as the user-triggered update path (FR-016, FR-017).
- **FR-020**: The background update MUST run once at application startup (catch-up)
  and then on its recurring interval, and a failure of one run MUST NOT stop
  subsequent scheduled runs.
- **FR-021**: User-triggered document generation MUST continue to update the
  archive, supplementing the background update; the recurring update interval
  SHOULD be configurable with a default of one week.

### Key Entities *(include if feature involves data)*

- **RFC**: A single change request with an identifying number, an approval status,
  affected assets, a change window (start and end date/time), a description, and a
  risk assessment. May appear more than once in the schedule when revised.
- **Keyword Area**: One of three reviewer-defined groupings (Ministry-specific,
  General, and Other/unclassified) into which RFCs are sorted for review.
- **Completed RFC Listing**: A per-keyword-area collection of the latest versions
  of RFCs whose change window ended within the last 5 weeks.
- **RFC Archive**: A single shared, persisted store of RFCs observed from earlier
  schedules, retaining the full details of the most recent version of each RFC,
  limited to the recent retention window, and used to produce the completed
  listings after RFCs leave the live schedule.
- **Scheduled Archive Update**: A recurring, user-independent process that refreshes
  the RFC source and updates/prunes the shared archive at least weekly.
- **Application Version**: The release identifier declared in the application's
  project file, surfaced to users in the interface footer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a schedule containing RFCs completed within the last 5 weeks,
  100% of those RFCs (latest version only) appear in the correct keyword area's
  completed listing, and 0% of RFCs completed more than 5 weeks ago or not yet
  completed appear there.
- **SC-002**: When an RFC has multiple versions within the window, exactly one
  entry (the most recently ending version) is shown for that RFC number.
- **SC-003**: A reviewer can locate the recent completed-change history for a
  keyword area within the document without consulting any external source.
- **SC-004**: The application version shown in the footer matches the project
  file version on 100% of pages and updates automatically when the project file
  version changes and the application is rebuilt.
- **SC-005**: Existing document sections and behaviors remain unchanged, with no
  regression in the in-progress, new/changed, and previously-reviewed listings.
- **SC-006**: An RFC observed in an earlier schedule still appears in the correct
  keyword area's completed listing after its change window has ended and after it
  has dropped off the live schedule, for as long as it remains within the 5-week
  window.
- **SC-007**: With no user activity, the shared archive is refreshed and pruned at
  least once every 7 days, so every RFC present in the source is captured for each
  week it is active.

## Assumptions

- "The 3 keyword areas" refers to the existing Ministry, General, and
  Other/unclassified groupings already used when producing the document.
- "Completed" means the change window has ended (end date in the past); approval
  status is not used to determine completion.
- "Latest version" is determined by the most recent change-window end date/time
  when the same RFC number appears multiple times in the window.
- The 5-week window is fixed (not user-adjustable) and is measured back from the
  moment the document is generated.
- The source schedule contains only current and future RFCs; completed RFCs are
  sourced from a persisted archive that the application builds up as it processes
  schedules over time.
- The archive is shared across users because the underlying RFC data is not
  user-specific; per-user keyword areas are applied at document-generation time.
- A recurring background update (default weekly) captures RFCs without user
  activity; user-triggered generation supplements it. Gaps are possible only if the
  application process is stopped for longer than the update interval.
- The application runs as a long-lived hosted process (the web application), so a
  background scheduler can execute on a timer.
- The completed listing is delivered as a new section within the existing
  generated document rather than as a separate on-screen view.
- "Bottom left corner of the app" refers to the persistent footer area shown on
  every page of the web interface.
- The version to display is the project file version of the running web
  application.
