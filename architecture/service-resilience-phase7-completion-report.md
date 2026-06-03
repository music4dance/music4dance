# Service Resilience Phase 7: Database Reconnection Recovery

**Date**: June 2, 2026
**Status**: ✅ COMPLETE
**Branch**: database-resiliency

## Overview

Phase 7 implements automatic database reconnection for the scenario where the app and an on-demand Azure SQL database start simultaneously: the app comes up first, the DB takes 20–45 seconds to wake, migrations fail, and the app has been running in degraded mode ever since. This phase adds a lightweight recovery mechanism that detects when the DB becomes reachable again on the next user request and re-runs deferred initialisation — all without restarting the app.

## Problem Statement

The Azure test instance uses on-demand SQL: both the App Service and the database wake up together when the first request arrives. Because migrations run synchronously during startup, the app marks `Database` as `Unavailable` and continues in degraded mode. Previously, the only way to recover was a manual app restart.

Secondary: mid-request `SqlException` / `Win32Exception` from `UserMetadata.Create` was unhandled, producing the ASP.NET developer exception page rather than a graceful degraded response.

## Implementation

### New: `DatabaseRecoveryService`

**File**: `m4d/Services/DatabaseRecoveryService.cs`

A singleton service that:

1. **`TriggerRecoveryIfNeeded()`** — called on every user request via pipeline middleware. Does nothing if the database is `Healthy` or `Unknown`. Re-reads the current connection string from `IConfiguration` on every call (so `appsettings.json` edits mid-run are picked up). Applies a configurable throttle (default 1 minute) to avoid hammering a still-down server. Fires `AttemptRecoveryAsync` as a background `Task.Run` — the user request is never delayed.

2. **`AttemptRecoveryAsync(string connectionString)`** — guarded by a `SemaphoreSlim(1,1)` to prevent concurrent attempts. Builds a fresh `DbContextOptions` with `ConnectTimeout=60` (no `EnableRetryOnFailure` — single fast probe). Calls `Database.CanConnectAsync()`. On success: marks `Database` healthy immediately, then creates a DI scope and runs `DanceStatsInstance.FixupStats` to complete the deferred startup work. A `FixupStats` failure does not undo the healthy mark — the DB is connectable and individual operations can proceed.

**Throttle interval** is read from `ServiceHealth:DatabaseRetryInterval` (default `00:01:00`).

### Middleware registration (`Program.cs`)

```csharp
var dbRecoveryService = app.Services.GetRequiredService<DatabaseRecoveryService>();
app.Use(async (context, next) =>
{
    dbRecoveryService.TriggerRecoveryIfNeeded();
    await next();
});
```

The middleware sits early in the pipeline (after `UseRouting`, before authentication) so it fires on every request, including anonymous ones.

`DatabaseRecoveryService` itself is registered as a singleton:

```csharp
services.AddSingleton<DatabaseRecoveryService>();
```

### Live connection string reload (`Program.cs`)

`AddDbContext` now uses the `(IServiceProvider, DbContextOptionsBuilder)` factory overload so the connection string is re-read from `IConfiguration` on every context resolution:

```csharp
services.AddDbContext<DanceMusicContext>((sp, options) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var liveConnStr = cfg["AZURE_SQL_CONNECTIONSTRING"]
        ?? cfg.GetConnectionString("DanceMusicContextConnection")
        ?? connectionString;  // fallback to startup value
    options.UseSqlServer(liveConnStr, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, ...);
        sqlOptions.CommandTimeout(60);
    });
});
```

`IConfiguration` reads from in-memory dictionaries (populated from `appsettings.json` via the file-watcher), so the per-resolution cost is a dictionary key lookup — negligible. This is what allows the recovery service's fresh `DanceMusicContext` scope to use the updated connection string without an app restart.

### `UserMetadata.Create` exception handling (`DMController.cs` + `UserMetadata.cs`)

`UserMetadata.Create` calls `UserManager.FindByNameAsync`, which hits the DB. When the DB is down this throws `SqlException` / `Win32Exception`. Previously this produced an unhandled exception page.

Fix: added a catch in `DanceMusicController.OnActionExecutionAsync`:

```csharp
UserMetadata userMetadata;
try
{
    userMetadata = await UserMetadata.Create(UserName, UserManager);
}
catch (Exception ex) when (ex is SqlException || ex is Win32Exception ||
                           ex.InnerException is SqlException ||
                           ex.InnerException is Win32Exception)
{
    ServiceHealth?.MarkUnavailable("Database", $"{ex.GetType().Name}: {ex.Message}");
    userMetadata = UserMetadata.Anonymous;
}
ViewData["UserMetadata"] = userMetadata;
```

`UserMetadata.Anonymous` is a new static factory (added to `UserMetadata.cs`) that returns a null-user instance — the same safe object the code already produces for unauthenticated visitors:

```csharp
public static UserMetadata Anonymous => new UserMetadata(null, null);
```

Execution continues through `OnActionExecutionAsync` normally, so the controller action fires, the service-status banner renders, and the user sees a meaningful degraded page rather than an error screen. The catch also calls `MarkUnavailable`, feeding the recovery trigger so the next request will attempt reconnection.

## Configuration

```json
"ServiceHealth": {
  "DatabaseRetryInterval": "00:01:00"
}
```

For local testing, set to a shorter interval such as `"00:00:15"`.

## Log Messages

| Level  | Message                                                                                   |
| ------ | ----------------------------------------------------------------------------------------- |
| `Info` | `DatabaseRecoveryService initialised (retry interval: 00:01:00)`                          |
| `Info` | `Database recovery: attempting reconnection...`                                           |
| `Info` | `Database recovery: connection restored in 7656ms – running post-recovery initialisation` |
| `Info` | `Database recovery: post-recovery FixupStats completed`                                   |
| `Info` | `Database recovery: probe failed after 5001ms – <error>`                                  |
| `Warn` | `Database recovery: post-recovery FixupStats failed (database still healthy): <error>`    |
| `Warn` | `Database recovery: DanceStatsManager.Instance is null – skipping FixupStats`             |

## Files Changed

| File                                      | Change                                                                        |
| ----------------------------------------- | ----------------------------------------------------------------------------- |
| `m4d/Services/DatabaseRecoveryService.cs` | **New** — reconnection service                                                |
| `m4d/Program.cs`                          | Register singleton + middleware; change `AddDbContext` to factory overload    |
| `m4d/Controllers/DMController.cs`         | Catch DB exceptions in `OnActionExecutionAsync`, use `UserMetadata.Anonymous` |
| `m4d/ViewModels/UserMetadata.cs`          | Add `Anonymous` static factory                                                |
| `m4d/appsettings.json`                    | Add `ServiceHealth:DatabaseRetryInterval` config key                          |

## Testing

### Local test procedure

1. Set `DanceMusicContextConnection` to a nonexistent server (e.g. `Server=nonexistent-server.invalid;Connect Timeout=5;...`) and start the app.
2. Verify: app starts, pages render in degraded mode (service status banner visible), no exception page.
3. While the app is running, restore the connection string to `(localdb)\\mssqllocaldb;Database=m4d;...`.
4. Hit any page — within `DatabaseRetryInterval` the recovery fires in the background.
5. Check logs for "connection restored" and "FixupStats completed".
6. Subsequent page loads work fully.

### Azure cold-start scenario

On the Azure test instance, the `ConnectTimeout=60` in the probe context gives the SQL database enough time to wake (typically 20–45 s). The user sees a degraded page on the first request; within one retry interval after the DB becomes reachable the app self-heals.
