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
