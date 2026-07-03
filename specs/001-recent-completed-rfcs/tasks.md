# Tasks: Recent Completed RFCs & App Version Display

**Input**: Design documents from `/specs/001-recent-completed-rfcs/`

**Prerequisites**: [plan.md](plan.md) (required), [spec.md](spec.md) (required), [research.md](research.md), [data-model.md](data-model.md), [contracts/service-contracts.md](contracts/service-contracts.md)

**Tests**: Unit tests are required for all core logic (archive, parsing, word generation, versioning) to catch regressions per Project Principles.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story, plus setup and cross-cutting concerns.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project readiness and configuration changes

- [ ] T001 Add `ArchiveUpdateIntervalDays` setting (default `7`) to `AppSettings` model in [src/RfcBuddy.App/Objects/AppSettings.cs](src/RfcBuddy.App/Objects/AppSettings.cs)
- [ ] T002 Register `IRfcArchiveService` as a singleton in [src/RfcBuddy.Web/Program.cs](src/RfcBuddy.Web/Program.cs) (unblocks dependency injection)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core archive storage infrastructure and schedule parser additions. No user stories can run without this.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T003 **Write failing unit tests** for `RfcService` additions (`GetAllRfcs` and `CategorizeRfcs`) in [src/RfcBuddy.App.Tests/Services/ExcelServiceTests.cs](src/RfcBuddy.App.Tests/Services/ExcelServiceTests.cs).
- [ ] T004 Implement `GetAllRfcs` and `CategorizeRfcs` on `ExcelService` and `IRfcService` in [src/RfcBuddy.App/Services/ExcelService.cs](src/RfcBuddy.App/Services/ExcelService.cs). Refactor `ProcessRfcs` to internally compose these two methods without changing behavior.
- [ ] T005 **Write failing unit tests** for `IRfcArchiveService` in `RfcArchiveServiceTests.cs` covering:
  - Empty/missing archive state (AC-1)
  - Latest-version resolution (AC-2/AC-3)
  - Pruning of entries older than 5 weeks (AC-4)
  - File size (50 MB) and record count (20,000) caps triggering the expected exception/reset (SEC-002)
  - **AC-8 concurrent-write safety**: two simultaneous `UpdateArchive` calls via `Parallel.Invoke` must produce a valid JSON archive containing all expected RFC entries from both callers (SEC-001)
  - **Keywords field exclusion**: after a round-trip through `UpdateArchive`→`GetCompletedRfcs`, returned `Rfc` objects have an empty `Keywords` list regardless of what was set before archiving
  - **Security event logging**: `ILogger<RfcArchiveService>` is called at Warning or Error when a record cap, file size cap, corruption reset, or retry exhaustion occurs (use a Moq'd `ILogger` to verify)
- [ ] T006 Implement `RfcArchiveService` in [src/RfcBuddy.App/Services/RfcArchiveService.cs](src/RfcBuddy.App/Services/RfcArchiveService.cs), using `System.Text.Json` to read/write `archived-rfcs.json`. Implementation notes:
  - Inject `ILogger<RfcArchiveService>` and emit structured log entries at `Warning` or `Error` for every security-relevant event: record cap hit, file size cap hit, archive file corruption reset, and write-retry exhaustion (SEC-TASK-002). **Log messages MUST NOT include RFC `Description`, `AssetTags`, or `RiskAssessment` content — log only counts, file sizes, and RFC numbers** to comply with constitution Principle I (Logs MUST redact sensitive fields).
  - Serialize `Rfc` with `Keywords` excluded (`[JsonIgnore]` or equivalent) so the transient user-specific field is never written to the shared archive (SEC-TASK-003)
  - Resolve `AppSettings.DataFolder` to an absolute path with `Path.GetFullPath()` at construction time and validate it is the intended data directory before constructing the archive file path (SEC-TASK-004)
- [ ] T007 [SEC-001] Implement thread-safety in `UpdateArchive` using a singleton process-lock, opening streams with `FileShare.None`, writing atomically to a unique temp file (`archived-rfcs.json.tmp-{Guid}`), doing `File.Move(..., overwrite: true)`, and wrapping file writes in a lightweight exponential retry loop (3 efforts).
- [ ] T008 [SEC-002] Implement defensive limits in `RfcArchiveService`: (1) refuse to save more than **20,000** records during merge/prune; (2) throw an exception / reset if file size on disk is larger than **50MB**. Each limit condition MUST emit a structured log entry at `Warning` or `Error` via `ILogger<RfcArchiveService>` so operators can detect abnormal data volumes or infrastructure issues in production (SEC-TASK-002).

**Checkpoint**: Core archive persistence and parser infrastructure ready.

---

## Phase 3: User Story 1 - Word Completed Section (Priority: P1) 🎯 MVP

**Goal**: Deliver the completed RFC listing (last 5 weeks) grouped per keyword area (using current user keywords) in the generated Word download.

**Independent Test**: Provide a schedule containing completed RFCs (with older and duplicated versions). Download the document; verify each area gains a "Completed (last 5 weeks)" subsection showing only the latest version of each completed RFC (or "No RFCs found.").

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T009 [P] [US1] **Write failing unit tests** in [src/RfcBuddy.App.Tests/Services/WordServiceTests.cs](src/RfcBuddy.App.Tests/Services/WordServiceTests.cs) verifying signature changes, rendering of "Completed (last 5 weeks)" sections, "No RFCs found." messages, and correct sorting by end-date descending.

### Implementation for User Story 1

- [ ] T010 [US1] Update `IWordService` and `WordService` signature and implementation in [src/RfcBuddy.App/Services/WordService.cs](src/RfcBuddy.App/Services/WordService.cs) to accept three lists of completed RFCs and render them in a new subsection under each area (Ministry, General, Other).
- [ ] T011 [US1] Update HomeController index POST method in [src/RfcBuddy.Web/Controllers/HomeController.cs](src/RfcBuddy.Web/Controllers/HomeController.cs):
    1. Parse and extract all RFCs from the schedule, upsert then prune into the archive (`archive.UpdateArchive(allRfcs)`).
    2. Get completed RFCs from the archive (`archive.GetCompletedRfcs()`).
    3. Categorize both the active RFCs and the completed RFCs using the user's current keywords.
    4. Compile them into the updated `CreateWordFile(...)` signature.
    - **Error handling**: if `archive.UpdateArchive()` or `archive.GetCompletedRfcs()` throws (e.g., due to SEC-002 limits), the existing controller-level try/catch handles it. Ensure the resulting `ModelState` error message describes what went wrong in user-friendly terms (per Principle V) rather than surfacing a raw exception string.
- [ ] T012 [P] [US-1] Update existing unit tests in `WordServiceTests` and `HomeControllerTests` to use the updated signatures and mock/provide correct data.

**Checkpoint**: Word-based completed listings are functional, integrated, and verified.

---

## Phase 4: User Story 2 - Footer App Version Display (Priority: P2)

**Goal**: Sourced automatically from project file and shown bottom-left in layout footer on all pages.

**Independent Test**: Run the app. Verify the version shown in the bottom-left on all pages matches `<Version>` (1.1.0) in `src/RfcBuddy.Web/RfcBuddy.Web.csproj`, with a safe fallback of "unknown" if the assembly cannot be resolved.

### Tests for User Story 2

- [ ] T013 [P] [US2] **Write failing unit tests** in [src/RfcBuddy.Web.Tests/Controllers/HomeControllerTests.cs](src/RfcBuddy.Web.Tests/Controllers/HomeControllerTests.cs) (or a separate helper test file) verifying `AppVersion` informational version lookup, build metadata stripping (anything after `+`), and fallback to assembly version / "unknown".

### Implementation for User Story 2

- [ ] T014 [US2] Create helper class `AppVersion` in `src/RfcBuddy.Web/Support/AppVersion.cs` resolving `AssemblyInformationalVersionAttribute`, trimming build metadata, and falling back safely per [contracts](contracts/service-contracts.md#L60) [SEC-003].
- [ ] T015 [US2] Update the footer in layout file [src/RfcBuddy.Web/Views/Shared/_Layout.cshtml](src/RfcBuddy.Web/Views/Shared/_Layout.cshtml) to show `AppVersion.Current` on the bottom left.
- [ ] T016 [P] [US2] Update stylesheet [src/RfcBuddy.Web/wwwroot/css/site.css](src/RfcBuddy.Web/wwwroot/css/site.css) using BC Design System CSS tokens to guarantee the version is neatly aligned on the bottom-left of the footer.

**Checkpoint**: Application version footer display works.

---

## Phase 5: User Story 3 - Recurring Background Update (Priority: P2)

**Goal**: Deliver automatic background parsing and archive updates once-a-week and once-at-startup.

**Independent Test**: With no users signed in, run the app. Logs and `archived-rfcs.json` should reflect a catch-up update running immediately on startup, then scheduled updates running on a configurable recurring timer. One failed run must not crash the loop.

### Tests for User Story 3

- [ ] T017 [P] [US3] **Write failing unit tests** in [src/RfcBuddy.Web.Tests/Services/ArchiveUpdateServiceTests.cs](src/RfcBuddy.Web.Tests/Services/ArchiveUpdateServiceTests.cs) verifying:
  - **BG-1 (startup catch-up)**: `ArchiveUpdateService` performs one archive update immediately on `ExecuteAsync` before entering the recurring delay loop.
  - **BG-2 (interval recurrence)**: after the configured interval elapses, a second `UpdateArchive` call is made without any user-triggered action (can be tested by injecting a near-zero interval).
  - **BG-3 (error isolation)**: when `UpdateOnce` throws, the error is logged and the loop continues to the next interval without crashing.
  - **Scoped DI resolution**: `IRfcService` is resolved from a new `IServiceScopeFactory`-created scope on each run, not captured at construction.

### Implementation for User Story 3

- [ ] T018 [US3] Create `ArchiveUpdateService` inheriting from `BackgroundService` in [src/RfcBuddy.Web/Services/ArchiveUpdateService.cs](src/RfcBuddy.Web/Services/ArchiveUpdateService.cs) resolving dependencies from `IServiceScopeFactory`, running catch-up updates, scheduled updates, and capturing errors in a try/catch loop.
- [ ] T019 [US3] Register `hostedService` in [src/RfcBuddy.Web/Program.cs](src/RfcBuddy.Web/Program.cs) via `builder.Services.AddHostedService<ArchiveUpdateService>()`.

**Checkpoint**: Background updater is active and preserving history on schedule.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Run validation and confirm the "at least one copy per week" coverage is green.

- [ ] T020 Run `dotnet test RfcBuddy.sln` and ensure all tests are green.
- [ ] T021 Run `quickstart.md` validation scenarios A, B, and C with our revised files.
- [ ] T022 Ensure all static analyzer warnings are resolved before commit.
- [ ] T023 Run `/speckit.verify.run` verification gate to confirm compliance.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start first.
- **Foundational (Phase 2)**: Depends on Phase 1. Binds `excelService` refactoring and `rfcArchive` file logic. BLOCKS User Stories.
- **User Stories (Phases 3–5)**: All depend on Phase 2 completion.
  - Phase 3 (US1 word output) depends on parsing and archive files.
  - Phase 4 (US2 version footer) is highly independent and can run in parallel with Phase 3 once Phase 1 is done.
  - Phase 5 (US3 background worker) relies on parsing/archive.
- **Polish (Phase 6)**: Depends on all stories.

### Parallel Opportunities

- Unblocked stories (US1, US2, US3) can be implemented in parallel after Phase 2 (Foundational) is completed.
- T003 (RfcService tests) and T005 (Archive tests) can run in parallel.
- Test and view-styling tasks marked `[P]` can run in parallel.
