# Project Implementation Plan

This document consolidates design decisions and architectural patterns established by completed features.

---

## Recent Completed RFCs & App Version Display [Source: specs/001-recent-completed-rfcs]

**Merged**: 2026-06-30

### Architecture Summary
The feature introduces local server-side RFC history tracking via a shared JSON archive, an automated background refresh service, and a build-number footer display.

### Storage & Persistence
- **Archive Format**: JSON array of `Rfc` objects in `data/archived-rfcs.json`
- **Retention**: 5 weeks (35 days) measured from end-date; future-dated RFCs retained until 5 weeks post-completion
- **Deduplication**: Latest version per RFC number, resolved by most recent end-date
- **Concurrency**: Singleton archive service with process-scoped Mutex lock, atomic writes via temp file + replace
- **Defensive Limits**: 20,000 record cap, 50MB file size cap

### Primary Dependencies
- **Language/Runtime**: C# 14 / .NET 10
- **Web Framework**: ASP.NET Core MVC (Razor), Xceed DocX 5.0.0 (Word generation), ExcelDataReader 3.8.0 (schedule parsing)
- **Archive Serialization**: `System.Text.Json` (in-framework)
- **Background Service**: `Microsoft.Extensions.Hosting.BackgroundService` (in-framework)
- **Testing**: MSTest, Moq, coverlet (existing)
- **CI/CD**: OpenShift / Docker, .NET 10 ASP.NET Core

### Project Structure Changes
- **New Services**:
  - `IRfcArchiveService` / `RfcArchiveService`: Shared archive management with concurrency, pruning, deduplication
  - `ArchiveUpdateService`: Hosted background service for weekly archive refresh + catch-up on startup
  - `AppVersion`: Static helper for assembly informational version resolution with fallback
- **Refactored Services**:
  - `IRfcService`: Added `GetAllRfcs()` and `CategorizeRfcs(...)` methods; `ProcessRfcs` preserved (internal composition)
  - `IWordService`: Extended `CreateWordFile` signature to accept completed RFC lists per area
  - `AppSettings`: Added `ArchiveUpdateIntervalDays` (default 7)
- **Updated Controllers**:
  - `HomeController`: Wires archive updates into document generation; categorizes both live and completed RFCs
- **Updated Views**:
  - `_Layout.cshtml`: Footer displays `AppVersion.Current`
  - `site.css`: Version alignment using BC Design System tokens

### Key Design Decisions
1. **D1. Single Shared Archive**: Not per-user; RFC data is not user-specific; current keywords applied at generation time
2. **D2. JSON Format**: Free-form RFC content (descriptions, risks) would corrupt line-delimited formats; `System.Text.Json` in-framework
3. **D3. Dual Capture Paths**: Background update (weekly) + user-triggered (on-demand); both update shared archive per same rules
4. **D4. Reusable Parsing**: `GetAllRfcs()` returns all observed RFCs (keyword-independent); `CategorizeRfcs()` applies keyword matching
5. **D5. Current Keywords for Completed**: Completed RFCs categorized using user's keywords **at generation time**, not stored keywords
6. **D6. Latest-Version Dedup**: Same RFC number → retain only highest end-date occurrence in archive and completed listing
7. **D7. Fixed 5-Week Window**: No user configuration; prune at 35 days old; future-dated RFCs retained
8. **D8. Singleton + Mutex + Atomic Write**: Shared archive accessed by multiple user threads + background service; lock + temp write prevents corruption
9. **D9. Assembly Version + Safe Fallback**: Informational version from assembly attribute, trimmed of build metadata, falls back to "unknown"

### Configuration
- `ArchiveUpdateIntervalDays` in `appsettings.json` (default 7 days)
- No user-facing configuration for retention window (fixed 5 weeks)

### Data Flow
1. **On Document Generation**:
   - Refresh schedule source via `GetLatestChanges()`
   - Parse all RFCs via `GetAllRfcs()`
   - Update archive via `UpdateArchive(allRfcs)` (dedup + prune)
   - Query completed RFCs via `GetCompletedRfcs()` (within 5 weeks)
   - Categorize live and completed RFCs using current user keywords
   - Generate Word document with completed section per area
   - Save hashes of current RFCs to `PreviousRFCs.txt` for next-run comparison

2. **On Background Update Interval** (default weekly):
   - Refresh schedule source via `GetLatestChanges()`
   - Parse all RFCs via `GetAllRfcs()`
   - Update archive via `UpdateArchive(allRfcs)` (dedup + prune)
   - Log completion; continue to next interval

3. **On Startup**:
   - Background service runs catch-up update immediately
   - Then enters recurring interval loop

### Security & Compliance
- **Principle I (Data Security)**: Archive stores full RFC content server-side in gitignored `data/` folder (same trust boundary as existing files); no secrets/credentials stored; transient user Keywords marked `[JsonIgnore]` so never persisted; logs redact sensitive fields (only counts, sizes, RFC numbers logged)
- **Principle II (Simplicity First)**: One new service + JSON file; reuses existing patterns; no new projects/databases/dependencies
- **Principle III (Adaptability)**: Narrow `IRfcArchiveService` interface; categorization extracted for reuse
- **Principle IV (Regression Safety)**: New unit tests for archive dedup/prune/query, Word completed sections, version resolution; existing tests updated
- **Principle V (Ease of Use)**: Fixed 5-week window (no config), safe fallback version, works with existing defaults
- **Compiler Warnings**: Treated as errors; all resolved

### Testing Strategy
- **Unit Tests**: Archive dedup/prune logic, RFC categorization, Word section rendering, version resolution
- **Integration Tests**: Controller wiring of archive into document generation, background service startup behavior
- **Coverage**: Core business logic (archive, parsing, Word output) fully covered; existing regressions caught by updated tests

### Operational Notes
- Background service catches up at startup and runs weekly (default).
- Archive file corruption resets to empty and continues (no crash).
- Write failures retry exponentially 3 times before failing.
- Archive defensively caps at 20,000 records and 50MB to prevent DoS/runaway growth.
- Scheduled update failures are logged but do not stop subsequent intervals.

---

## RFC Retrieval REST API with Personal Access Tokens & User Administration [Source: specs/002-rest-api-pat-auth]

**Merged**: 2026-07-02

### Architecture Summary
The feature adds a PAT-authenticated JSON search endpoint, self-service token management, admin governance, and a background cleanup worker. It stays within the existing ASP.NET Core MVC solution and reuses the file-based storage model already used by RFC processing and archive capture.

### Storage & Persistence
- **Token Store**: `apitokens.json` persists hashed PATs, labels, owner identity, expiry, last-used time, and revocation state under the shared `DataFolder`.
- **User Registry**: `users.json` stores user identity, email, and admin status with a shared mutex to prevent race conditions on first-admin bootstrap and promotion changes.
- **Baselines**: Each user keeps a separate `ApiPreviousRFCs.txt` baseline for API change tracking alongside the existing web baseline.
- **Concurrency**: Named mutexes protect token and registry writes so the solution remains safe on multi-replica deployments.

### Primary Dependencies
- **Language/Runtime**: C# 14 / .NET 10
- **Web Framework**: ASP.NET Core MVC (Razor), custom `ApiToken` bearer authentication, policy-based admin authorization
- **Serialization**: `System.Text.Json` (in-framework)
- **Hosted Services**: `Microsoft.Extensions.Hosting.BackgroundService` (in-framework)
- **Testing**: MSTest, Moq, coverlet (existing)

### Project Structure Changes
- **New Domain Objects**: `ApiToken`, `UserRecord`, `RfcChangeStatus`, `BaselineScope`
- **New Services**: `ApiTokenService`, `UserRegistryService`, `RfcChangeTracker`, `UserMaintenanceService`
- **New Web Components**: `RfcApiController`, `ApiTokensController`, `AdminController`, `ApiTokenAuthenticationHandler`, `AdminRequirement`, `UserRegistrationFilter`
- **New UI**: Razor views for token creation/listing and the admin panel, plus supporting view models and DTOs

### Key Design Decisions
1. **D1. Hash-only PAT storage**: Raw token values are shown once and never persisted; later validation uses a SHA-256 hash lookup.
2. **D2. Separate API baseline**: Successful API requests advance only the API baseline, leaving the web UI baseline unchanged.
3. **D3. Ignore precedence**: API filtering excludes RFCs matching any ignore keyword, even if they also match include keywords.
4. **D4. First-login admin bootstrap**: The first authenticated user becomes an admin, with a mutex-guarded recheck to avoid race conditions.
5. **D5. Cleanup by activity timestamp**: Inactive user folders are removed based on the change-tracking files' last-write time, and related tokens/registry entries are removed with them.

### Configuration
- No new external datastore or dependency was introduced; the feature uses the existing `DataFolder` layout and shared file storage.
- Token lifetimes are capped at 90 days and can be enforced by the UI and service layer.

### Data Flow
1. A caller authenticates with a PAT and the `ApiToken` handler resolves the owner identity.
2. The API controller filters candidate RFCs against the schedule/archive universe, applies include/ignore criteria, and annotates each RFC with change status.
3. Each successful request updates the caller's API baseline and the token's last-used timestamp.
4. Logged-in users can create tokens, and admins can manage user roles and revoke tokens system-wide.
5. The maintenance service periodically removes stale user storage based on inactivity age.

### Security & Compliance
- Raw secrets are never logged or persisted; only hashes are stored.
- Admin-only endpoints are protected by authorization policy and cross-user token revocation is restricted to administrators.
- Structured audit logging records administrative actions and revocations with hashed actor identifiers.

### Testing Strategy
- Unit tests cover token lifecycle rules, admin bootstrap and promotion constraints, API filtering precedence, change-tracking logic, authentication, and cleanup selection.
- Web-controller tests verify API responses, token UI behavior, and admin policy enforcement.

### Revision Note
- 2026-07-02 — Archived from the completed feature implementation and verification run into project memory.

## Revision History

| Feature | Date | Components | Status |
|---------|------|-----------|--------|
| Recent Completed RFCs & App Version Display | 2026-06-30 | RfcArchiveService, ArchiveUpdateService, AppVersion, ExcelService enhancements, WordService enhancements, HomeController wiring, layout/styles | Completed |
