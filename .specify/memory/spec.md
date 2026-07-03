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

## RFC Retrieval REST API with Personal Access Tokens & User Administration [Source: specs/002-rest-api-pat-auth]

**Completed**: 2026-07-02

### Core Requirement
Add a PAT-authenticated RFC search API to the existing MVC app so downstream systems can request relevant RFCs as JSON, while logged-in users manage their tokens and administrators govern access.

### User Stories

#### User Story 1 - Retrieve relevant RFCs via authenticated API (Priority: P1)
A downstream system submits include and optional ignore keywords to the API using a Personal Access Token. The API returns RFCs matching the include list while honoring ignore precedence and annotating each item with per-user change-tracking state.

#### User Story 2 - Create and manage Personal Access Tokens (Priority: P1)
A logged-in user creates a PAT with a label and expiry, sees the raw token once, and can later list or revoke it through the self-service token UI.

#### User Story 3 - Administer users and roles (Priority: P2)
The first user to sign in becomes an administrator, and admins can view the user roster with last-active information, promote other users, and revoke tokens across the system.

#### User Story 4 - Automatically remove inactive user data (Priority: P3)
A background maintenance task deletes user data and tokens for individuals whose change-tracking activity is older than 400 days.

### Key Functional Requirements
- **FR-001**: The system exposes a JSON RFC search endpoint protected by Personal Access Tokens.
- **FR-002**: The API filters against the same schedule/archive universe the web app uses and applies ignore precedence over include matches.
- **FR-003**: Each successful API call advances a separate API baseline for the PAT owner and annotates RFCs as new/changed/unchanged.
- **FR-004**: Token creation, listing, and revocation are available via the MVC UI; tokens are stored only as non-reversible hashes.
- **FR-005**: The first login becomes the first administrator, with safeguards against zero-admin states and cleanup of stale user data after 400+ days of inactivity.

### Success Criteria
- Downstream integrations can authenticate with a PAT and receive a stable JSON response containing matching RFCs.
- Users can safely create, inspect, and revoke their own tokens without exposing the secret again.
- Administrators can manage users and tokens while the cleanup worker removes stale accounts automatically.

### Key Entities
- **ApiToken**: Label, owner, expiry, last-used time, revocation status, and non-reversible hash storage.
- **UserRecord**: User identity, email, admin flag, and first-seen time.
- **RfcChangeStatus**: Change annotation for API responses (`New`, `Changed`, `Unchanged`).

### Edge Cases
- Ignore-only requests return no matches.
- Expired, revoked, or malformed tokens are rejected with authentication errors.
- The last remaining administrator cannot be demoted, and cleanup removes directories only when the inactivity threshold is reached.

### Revision Note
- 2026-07-02 — Archived from the completed feature implementation and verification run into project memory.

## Revision History

| Feature | Date | Status | Summary |
|---------|------|--------|---------|
| Recent Completed RFCs & App Version Display | 2026-06-30 | Completed | Added completed RFC listings (5-week window, deduped), footer version display, and weekly background archive updates. |
