# Phase 1 Contracts: Internal Service Interfaces

This is an MVC web application with no public/external API. The contracts below are the
**internal service interfaces** that change or are added. They are the stable seams the
implementation and tests target. Signatures are illustrative C#; exact naming is finalized
during implementation.

## 1. `IRfcArchiveService` (NEW) — `RfcBuddy.App.Services`

Owns the shared, persisted archive of observed RFCs.

```csharp
public interface IRfcArchiveService
{
    /// Upserts observed RFCs into the shared archive (latest version per RFC number,
    /// resolved by most recent EndDate) and prunes entries whose EndDate is older than
    /// the retention window. Persists atomically.
    void UpdateArchive(IEnumerable<Rfc> observedRfcs);

    /// Returns the archived RFCs whose change window ended within the retention window
    /// (now - 5 weeks ≤ EndDate ≤ now), deduplicated to the latest version per RFC number.
    /// RFCs with a missing/invalid EndDate are excluded.
    List<Rfc> GetCompletedRfcs();
}
```

**Behavioral contract**

| ID | Given | When | Then |
|----|-------|------|------|
| AC-1 | An empty/missing archive | `UpdateArchive` with observed RFCs | All are stored; a missing file is created |
| AC-2 | Archive has RFC `X` (EndDate E1) | `UpdateArchive` includes `X` with EndDate E2 > E1 | `X` is replaced by the E2 version (single entry) |
| AC-3 | Archive has RFC `X` (EndDate E1) | `UpdateArchive` includes `X` with EndDate E2 < E1 | The E1 (later) version is retained |
| AC-4 | Archive has RFC ended 6 weeks ago | `UpdateArchive` runs | That RFC is pruned |
| AC-5 | Archive has RFCs ended 1/2/3/4 weeks ago | `GetCompletedRfcs` | All four are returned (one per RFC) |
| AC-6 | Archive has a future-dated RFC | `GetCompletedRfcs` | It is **not** returned (not yet completed) but remains stored |
| AC-7 | Archive has an RFC with default/invalid EndDate | `GetCompletedRfcs` | It is excluded |
| AC-8 | Two concurrent `UpdateArchive` calls | Run together | No corruption / no lost-update of either RFC set (lock + atomic write) |

## 2. `IRfcService` additions — `RfcBuddy.App.Services`

```csharp
public interface IRfcService
{
    int ProcessRfcs(/* unchanged */ ...);   // preserved
    Task GetLatestChanges();                  // preserved

    /// Returns every RFC parsed from the current schedule file (keyword-independent).
    List<Rfc> GetAllRfcs();

    /// Sorts the given RFCs into the three areas using the supplied keywords and the
    /// existing matching rules (ignore-keywords excluded from Other).
    void CategorizeRfcs(
        IEnumerable<Rfc> rfcs,
        List<string> ministryKeywords,
        List<string> generalKeywords,
        List<string> ignoreKeywords,
        out List<Rfc> ministryRfcs,
        out List<Rfc> generalRfcs,
        out List<Rfc> otherRfcs);
}
```

**Behavioral contract**

| ID | Given | When | Then |
|----|-------|------|------|
| RS-1 | A schedule with K parseable RFCs | `GetAllRfcs` | Returns all K RFCs, independent of any keywords |
| RS-2 | A list of RFCs + keyword sets | `CategorizeRfcs` | Produces the same Ministry/General/Other split that `ProcessRfcs` produces for the live schedule |
| RS-3 | Existing callers | `ProcessRfcs` | Unchanged signature and results (composed from the two new methods) |

## 3. `IWordService` signature change — `RfcBuddy.App.Services`

```csharp
public interface IWordService
{
    void CreateWordFile(
        ref Stream wordFile,
        List<Rfc> ministryRfcs,
        List<Rfc> generalRfcs,
        List<Rfc> otherRfcs,
        List<PreviousRfc> previousRfcs,
        List<Rfc> completedMinistryRfcs,
        List<Rfc> completedGeneralRfcs,
        List<Rfc> completedOtherRfcs);
}
```

**Behavioral contract**

| ID | Given | When | Then |
|----|-------|------|------|
| WS-1 | Completed lists with RFCs | `CreateWordFile` | Each area gains a "Completed (last 5 weeks)" subsection listing them, EndDate desc |
| WS-2 | Empty completed list for an area | `CreateWordFile` | That area's completed subsection shows "No RFCs found." |
| WS-3 | Any inputs | `CreateWordFile` | Existing In Progress / New or Changed / Previously Reviewed subsections are unchanged |
| WS-4 | Empty inputs | `CreateWordFile` | Produces a non-empty document (existing test invariant holds) |

## 4. Application version provider — `RfcBuddy.Web`

```csharp
public static class AppVersion
{
    /// The web assembly's informational version with build metadata trimmed,
    /// or "unknown" when unavailable.
    public static string Current { get; }
}
```

**Behavioral contract**

| ID | Given | When | Then |
|----|-------|------|------|
| AV-1 | Assembly built with `<Version>1.1.0</Version>` | read `Current` | Returns `1.1.0` |
| AV-2 | Informational version `1.1.0+abc123` | read `Current` | Returns `1.1.0` (metadata trimmed) |
| AV-3 | No version attribute available | read `Current` | Returns `unknown` |

## 5. Controller orchestration (HomeController POST) — behavior, not a new interface

On "Apply filters and download RFCs":

1. `GetLatestChanges()` → refresh schedule.
2. `allRfcs = GetAllRfcs()` → `archive.UpdateArchive(allRfcs)`.
3. `CategorizeRfcs(allRfcs, currentKeywords...)` → live Ministry/General/Other.
4. `completed = archive.GetCompletedRfcs()` → `CategorizeRfcs(completed, currentKeywords...)`
   → completed Ministry/General/Other.
5. `CreateWordFile(... live lists ..., previousRfcs, ... completed lists ...)`.

## 6. DI registration — `Program.cs`

```csharp
builder.Services.AddSingleton<IRfcArchiveService, RfcArchiveService>();
builder.Services.AddHostedService<ArchiveUpdateService>();
```

Singleton archive (shared + lock). The hosted service is singleton by nature; existing
scoped registrations are unchanged.

## 7. `ArchiveUpdateService` (NEW hosted `BackgroundService`) — `RfcBuddy.Web`

Recurring, user-independent capture. Not consumed by other code; registered via
`AddHostedService`.

```csharp
public sealed class ArchiveUpdateService : BackgroundService
{
    // ctor(IServiceScopeFactory scopeFactory, IRfcArchiveService archive,
    //      IAppSettingsService settings, ILogger<ArchiveUpdateService> logger)

    // ExecuteAsync:
    //   run UpdateOnce() at startup (catch-up), then every ArchiveUpdateIntervalDays
    //   until shutdown is requested.
    //
    // UpdateOnce():
    //   using var scope = scopeFactory.CreateScope();
    //   var rfcService = scope.ServiceProvider.GetRequiredService<IRfcService>();
    //   await rfcService.GetLatestChanges();
    //   archive.UpdateArchive(rfcService.GetAllRfcs());
}
```

**Behavioral contract**

| ID | Given | When | Then |
|----|-------|------|------|
| BG-1 | App starts | Service initializes | A catch-up update runs once immediately |
| BG-2 | No user activity | The configured interval elapses | The source is refreshed and `UpdateArchive` runs |
| BG-3 | One update throws | During a scheduled run | The error is logged and the loop continues to the next interval (no crash) |
| BG-4 | Application shutdown | Cancellation requested | The loop stops promptly without starting a new run |
| BG-5 | Concurrent with a user-triggered update | Both update the archive | No corruption / no lost update (shared lock + atomic write, AC-8) |
