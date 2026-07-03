# Project Changelog

## Merged Features Log

### Recent Completed RFCs & App Version Display — 2026-06-30
**Branch**: `001-recent-completed-rfcs`
**Spec**: [specs/001-recent-completed-rfcs/spec.md](../../specs/001-recent-completed-rfcs/spec.md)

**What was added**:
- RFC history archive: Local JSON-based persistence of completed RFCs (5-week retention window) for display in generated Word documents
- Weekly background capture: Automatic refresh and update of the archive on a recurring interval (default 7 days) to ensure weekly RFC coverage independent of user activity
- Completed RFC listings: New "Completed (last 5 weeks)" section in Word output for each keyword area (Ministry, General, Other), showing only the latest version of each RFC
- Application version footer: Version number from project file displayed in bottom-left corner of all pages with safe fallback
- Refactored RFC processing: Extracted reusable `GetAllRfcs()` and `CategorizeRfcs()` methods for both live and archived RFC categorization

**New Components**:
- `RfcArchiveService`: Manages shared archive with concurrency safety (Mutex), deduplication, pruning, and defensive limits
- `ArchiveUpdateService`: Hosted background service for scheduled archive refreshes and startup catch-up
- `AppVersion`: Static helper for assembly informational version resolution with fallback
- Enhanced `ExcelService`: `GetAllRfcs()` and `CategorizeRfcs(...)` public methods
- Enhanced `WordService`: Updated signature to include completed RFC lists; new "Completed" subsections
- Enhanced `HomeController`: Wires archive captures and completed RFC categorization into document generation
- Enhanced `_Layout.cshtml` and `site.css`: Version display in footer

**Tasks Completed**: 23 / 23 (100%)

**Architecture Decisions**:
- Single shared archive (not per-user) with JSON serialization
- Singleton archive service with process-scoped Mutex for concurrency safety
- Latest-version deduplication by most recent end-date
- Fixed 5-week retention window with automatic pruning
- Dual capture paths: weekly background update + user-triggered on-demand
- Safe assembly version resolution with fallback

**Testing**:
- 18 unit tests covering archive operations, RFC parsing, Word rendering, and version resolution
- All existing tests updated and passing
- No regressions detected

**Security & Compliance**:
- Archive stores full RFC content server-side in gitignored data folder (no secrets leaked)
- Transient keywords marked `[JsonIgnore]` to prevent persistence
- Logs redact sensitive fields (only counts, sizes, and RFC numbers recorded)
- Defensive limits: 20,000 records, 50MB file size
- Atomic writes with temp file + replace to prevent corruption
- Concurrency-safe via Mutex locking

### RFC Retrieval REST API with Personal Access Tokens & User Administration — 2026-07-02
**Branch**: `002-rest-api-pat-auth`
**Spec**: [specs/002-rest-api-pat-auth/spec.md](../../specs/002-rest-api-pat-auth/spec.md)

**What was added**:
- PAT-authenticated RFC search API returning JSON results filtered by include/ignore keywords and change-tracking status
- Self-service token creation, listing, and revocation UI for signed-in users
- Administrator governance for users and tokens, including first-login admin bootstrap and last-active display
- Automatic cleanup of stale user data after 400+ days of inactivity

**New Components**:
- `ApiTokenService`, `UserRegistryService`, `RfcChangeTracker`, and `UserMaintenanceService`
- `RfcApiController`, `ApiTokensController`, `AdminController`, `ApiTokenAuthenticationHandler`, and admin authorization support
- Razor views and supporting DTO/view-models for the API token and admin experiences

**Tasks Completed**: 38 / 38 (100%)

**Security & Compliance**:
- PATs are stored only as non-reversible hashes and are shown once at creation time
- Admin-only actions are restrictions enforced by policy-based authorization
- Sensitive administrative actions are logged with hashed actor references

**Testing**:
- New unit and web tests cover PAT authentication, API filtering, token lifecycle, admin promotion/revocation, and cleanup behavior
