# Runtime Development Guidance

Supplements project constitution at `.github/memory/constitution.md`. Constitution is highest-authority document; this file provides project-specific technology and architecture context for AI-assisted development.

---

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
│   ├── Objects/         # Domain models (Rfc, AppSettings, PreviousRfc)
│   └── Services/        # AppSettingsService, ExcelService, UserService, WordService
├── RfcBuddy.Web/        # ASP.NET Core MVC web app (OIDC auth, controllers, Razor views)
│   ├── Controllers/     # HomeController (single-page MVC app)
│   ├── Models/          # ViewModels
│   └── Views/           # Razor views
├── RfcBuddy.App.Tests/  # MSTest unit tests for RfcBuddy.App
└── RfcBuddy.Web.Tests/  # MSTest unit tests for RfcBuddy.Web

RfcBuddy.sln             # Solution file
Dockerfile               # Multi-stage Docker build (sdk:10.0 → aspnet:10.0)
```

---

## UI & UX

Always follow the BC Design System at https://www2.gov.bc.ca/gov/content/digital/design-system/components for UI styling and components. The BC Design System is React-based, but we will only use the CSS custom properties (design tokens) and font. All UI components must be implemented as MVC and CSS components without JS interop or React wrappers, to keep the frontend lightweight and maintainable.

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


## Known Issues & Gotchas

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan at
`specs/001-recent-completed-rfcs/plan.md`.
<!-- SPECKIT END -->