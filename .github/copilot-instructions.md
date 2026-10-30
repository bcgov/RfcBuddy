# Runtime Development Guidance

Supplements project constitution at `.github/memory/constitution.md`. Constitution is highest-authority document; this file provides project-specific technology and architecture context for AI-assisted development.

Use the codebase-memory MCP server for any code searches or architecture queries. If the MCP server is unavailable, use local code search or GitHub search.

---

## SpecKit Instructions

When using SpecKit agents to generate or edit files, follow these instructions:
- Do not display generated file contents.
- Do not quote existing files.
- Provide only:
  - file name
  - status
  - concise summary

## Active Technologies

| Layer | Technology | Version |
|-------|-----------|---------|
| Language | C# | 14 |
| Runtime | .NET | 10 |
| Frontend | ASP.NET Core MVC (Razor Views) | 10.0.x |
| Auth | OIDC / Keycloak (cookie-based) | BC Gov Keycloak |
| Excel Processing | ExcelDataReader | 3.8.0 |
| Word Generation | DocX | 5.0.0 |
| Logging | Microsoft.Extensions.Logging | 10.0.x |
| Testing | MSTest, Moq, coverlet | latest |
| CI/CD | OpenShift / Docker | — |

---

## Project Structure

```text
src/
├── RfcBuddy.App/        # Core library: services, domain objects, cryptography
│   ├── Core/            # Cryptography helpers
│   ├── Objects/         # Domain models (Rfc, AppSettings, PreviousRfc, ApiToken, UserRecord)
│   └── Services/        # AppSettingsService, ExcelService, UserService, WordService, RfcArchiveService, ApiTokenService, UserRegistryService, RfcChangeTracker
├── RfcBuddy.Web/        # ASP.NET Core MVC web app (OIDC auth, controllers, Razor views)
│   ├── Authentication/  # ApiToken authentication handler
│   ├── Authorization/   # Admin policy and authorization handler
│   ├── Controllers/     # HomeController, RfcApiController, ApiTokensController, AdminController
│   ├── Models/          # ViewModels and API DTOs
│   ├── Services/        # ArchiveUpdateService, UserMaintenanceService
│   ├── Support/         # AppVersion (assembly version resolution), user registration filter
│   └── Views/           # Razor views for home, API token management, and admin workflows
├── RfcBuddy.App.Tests/  # MSTest unit tests for RfcBuddy.App
└── RfcBuddy.Web.Tests/  # MSTest unit tests for RfcBuddy.Web

RfcBuddy.sln             # Solution file
Dockerfile               # Multi-stage Docker build (sdk:10.0 → aspnet:10.0)
```

---

## UI & UX

Always follow the BC Design System at https://www2.gov.bc.ca/gov/content/digital/design-system/components for UI styling and components. The BC Design System is React-based, but we will only use the CSS custom properties (design tokens) and font. All UI components must be implemented as MVC and CSS components without JS interop or React wrappers, to keep the frontend lightweight and maintainable.

## Versioning

RfcBuddy uses semantic versioning. The version shown in the bottom-left footer (`AppVersion.Current` in `Views/Shared/_Layout.cshtml`) is resolved at runtime from the assembly informational version, which comes from `<Version>` in `src/RfcBuddy.Web/RfcBuddy.Web.csproj`. That element is the single source of truth; do not hard-code the version anywhere else.

### Required version bump

Every change that alters application behaviour, UI, configuration, dependencies, the Dockerfile, or anything else shipped in the container image must raise `<Version>` above the base branch's value in the same change. Before finishing such work:

1. Compare the branch's `<Version>` with the base branch's value: `git show origin/main:src/RfcBuddy.Web/RfcBuddy.Web.csproj`.
2. Read the newest version heading at the top of `CHANGELOG.md`. Reconcile the project version, changelog version, and base version before choosing an increment. Use the highest of these as the version baseline; do not select a version below any of them or reuse a version already recorded in the changelog.
3. Choose the increment from the table below, using the highest classification present in the full branch diff, not only the latest commit.
4. Bump `<Version>` once per branch. If the branch already carries a higher version than the base and changelog, keep it unless later work raises the classification (for example, from patch to minor).
5. Add a matching entry at the top of `CHANGELOG.md` with the new version, date, and a concise summary. If that version already has an entry, update it instead of adding a duplicate.
6. Build and confirm the footer shows the new version.

Changes limited to tests, specs, `.specify/`, `.github/` instructions or agent files, or other non-shipped documentation do not require a bump.

### Version selection

| Change | Version |
|---|---|
| Bug fix, security fix, dependency or framework patch update, UI correction, performance improvement, refactor, or documentation shipped with the app | Patch |
| New user-visible feature, page, API endpoint, background service, or new compatible configuration option | Minor |
| Incompatible API contract change, removed endpoint or feature, required configuration or deployment change, or data/archive format change that breaks existing installations | Major, only when the user decides |

Security fixes are patch unless they add a new user-visible capability or require an incompatible contract.

### Major release control

Never select a major version on your own. Explain the incompatibility and ask the user whether to release a major version. Neither prior release history nor the apparent size of a change authorizes a major release.

### Release tags

Release tags use the form `v<version>` and must match `<Version>` in `RfcBuddy.Web.csproj` on the tagged commit.

## Commands

### Development

```powershell
# Run the web app
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/RfcBuddy.Web/RfcBuddy.Web.csproj
```

### Testing

```powershell
# Run all tests
dotnet test RfcBuddy.sln

# Run a specific test project
dotnet test src/RfcBuddy.App.Tests/RfcBuddy.App.Tests.csproj
dotnet test src/RfcBuddy.Web.Tests/RfcBuddy.Web.Tests.csproj
```

### Build & Publish

```powershell
# Build solution
dotnet build RfcBuddy.sln

# Publish (Release)
dotnet publish src/RfcBuddy.Web/RfcBuddy.Web.csproj -c Release -o ./out
```

### Docker

```powershell
# Build the Docker image
docker build -t rfcbuddy .

# Run the container
docker run -p 8080:8080 rfcbuddy
```

## Recent Changes

- **002-rest-api-pat-auth (merged 2026-07-02)**: Added a PAT-authenticated RFC search API, self-service PAT creation/list/revocation workflow, admin user governance, and background cleanup for stale user directories. New components include `ApiTokenService`, `UserRegistryService`, `RfcChangeTracker`, `RfcApiController`, `ApiTokensController`, `AdminController`, and `UserMaintenanceService`.
- **001-recent-completed-rfcs (merged 2026-06-30)**: Added RFC history archive with weekly background refresh, completed RFC listings (5-week window, deduped per RFC number), and app version footer. New services: `RfcArchiveService` (shared JSON archive with Mutex concurrency, pruning, dedup), `ArchiveUpdateService` (weekly background update + startup catch-up). Refactored `ExcelService` with `GetAllRfcs()` and `CategorizeRfcs()`. Enhanced `WordService` signature and `HomeController` wiring. 18 new/updated unit tests, all passing.

## Known Issues & Gotchas

### ⚠️ One-time PAT disclosure
**Issue:** PATs are shown once at creation time and cannot be recovered later.
**Root Cause:** The implementation stores only non-reversible hashes to satisfy the security requirement.
**Prevention Rule:** Users must copy the token immediately and store it securely before leaving the confirmation view.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan at
`specs/002-rest-api-pat-auth/plan.md`.
<!-- SPECKIT END -->