# Persistent Site Update Message

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-10-25
**Code:** `m4d/Utilities/GlobalState.cs`, `m4d/Controllers/AdminController.cs` (`UpdateWarning`,
`UpdateSearchIdx`), `m4d/Views/Admin/InitializationTasks.cshtml`,
`m4d/Controllers/HealthController.cs`, `m4d/ClientApp/src/components/ServiceStatusBanner.vue`

## Today

The admin "Update Warning" on `/Admin/InitializationTasks` posts to `Admin/UpdateWarning`, which
sets `GlobalState.UpdateMessage`. That's a plain `static string` with no backing store, so:

- **A restart clears it.** That includes the App Service recycles from OOM (see the October 2026
  outages), platform maintenance, a config change that restarts the site, and a deploy. These are
  often the very moments when the banner matters most.
- **It's per process.** With more than one instance, each instance has its own copy, and
  whichever instance handles the admin POST is the only one that shows it.

`UpdateSearchIdx` also sets the message programmatically for the length of an index rebuild and
clears it when the rebuild is done.

The message reaches users through `GET /api/health/status` (`updateMessage`).
`useServiceHealth()` polls that every 30 seconds while a message is showing, and hourly
otherwise, and `ServiceStatusBanner.vue` renders it. None of the client side needs to change for
any option below.

## Goals

1. An admin can choose to make a message **persist** through a restart. A non-persisted message
   keeps today's behavior.
2. Clearing the message also clears any persisted copy, so a stale banner can't come back after
   the next restart.
3. Reading the message stays cheap. `/api/health/status` is polled by every open tab.
4. **The store doesn't depend on what's being upgraded.** The banner is mostly used during
   infrastructure work, often with SQL or Search degraded. A store that's unavailable at those
   times defeats the purpose.
5. There's a path to multi-instance that doesn't mean redoing the admin UI or the call sites.

## Shared shape (all options)

Put the storage behind a small seam so the options below are interchangeable:

```csharp
public record UpdateMessageState(string Message, bool Persist, string SetBy, DateTime SetAt);

public interface IUpdateMessageStore
{
    Task<UpdateMessageState> Load();       // null when nothing is stored
    Task Save(UpdateMessageState state);   // only called when Persist is true
    Task Clear();
}
```

- `GlobalState.UpdateMessage` stays the in-memory value that `HealthController` and the admin
  view read, so the hot path never does I/O.
- On startup (`StartupInitializationService` or an `ApplicationStarted` hook), load the stored
  state and seed `GlobalState.UpdateMessage`.
- `Admin/UpdateWarning` gains a **"Persist across restarts"** checkbox. Set with the box checked
  means `Save`. Set with it unchecked, or Clear, means `Clear` the store, then set memory.
- The admin view shows whether the current message is persisted and who set it, and when.
- `UpdateSearchIdx` keeps setting the message in memory only. Persisting it might seem useful,
  since a crash mid-rebuild would leave the banner up, but nothing would clear it automatically
  and the rebuild doesn't resume after a restart, so a persisted copy would be wrong.
- Optional, cheap once the record exists: an `ExpiresAt` so a persisted message can't outlive a
  forgotten maintenance window.

## Option A: JSON file in `wwwroot/AppData` (single instance)

Write `wwwroot/AppData/update-message.json` next to `dance-environment.json`, following the
pattern of `DanceStatsFileManager`.

- **Effort:** small. A file-backed `IUpdateMessageStore`, a startup load, the checkbox, and
  tests. No Azure changes and no new permissions.
- **Restart:** survives. On Linux App Service, `/home/site/wwwroot` is persistent storage.
- **Deploy:** should survive. `AzureWebApp@1` zip deploy removes only files from the previous
  package, and `dance-environment.json` already relies on that. This needs a one-time check
  (confirm the file is still there in Kudu after a deploy), since a stale banner coming back
  after a deploy would be worse than losing it.
- **Security:** `BlockAppDataMiddleware` already returns 404 for `/AppData`, so the file isn't
  publicly served. It contains nothing sensitive anyway.
- **Availability:** independent of SQL, Search and App Configuration. If the write fails, log it
  and keep the in-memory message, so the admin action still takes effect.
- **Local dev:** `m4d`'s `clean-client` target deletes `wwwroot` on a rebuild, so a local
  persisted message goes away then. That's the same as the stats cache, and harmless.
- **Multi-instance:** partly. App Service mounts `/home` as one share across all instances of a
  plan, so every instance would *see* the same file. But each instance reads it only at startup,
  so an instance won't pick up a change made on another one. See A2.

### A2: file plus a short re-read (cheap multi-instance)

Same file, but `HealthController` gets the message through a small cache that re-reads the file
when it's more than about 30 seconds old (matching the client's fast poll interval), instead of
reading only the static.

- **Effort:** small on top of A. Don't use `FileSystemWatcher`, which is unreliable on the
  SMB-backed `/home` share.
- **Multi-instance:** yes, with up to about 30 seconds of lag, as long as all instances are in
  one App Service plan. Deployment slots or a second region don't share `/home`, so for those
  it's still per-site.
- **Cost:** one small file read every 30 seconds per instance.
- **Caveat:** a non-persisted message is still in memory only and per instance. Under
  multi-instance, the honest version is to always write the file and keep the checkbox for
  whether the message survives a restart (for example, delete it on startup unless it's marked
  `Persist`). Decide that if and when the app scales out.

## Option B: Azure App Configuration key

Store the message as a key (for example `Site:UpdateMessage`) in the App Configuration store
the app already reads, with sentinel refresh.

- **Multi-instance:** yes. Every instance refreshes from the same store, including other slots
  and regions if they share the store.
- **Latency:** the sentinel refresh interval is 5 minutes, and a change only shows up after
  `Configuration:Sentinel` is bumped. Either the admin action writes both the key and the
  sentinel (which triggers a refresh-all on every instance, a bigger hammer than a banner
  deserves), or the key gets its own registration with a shorter interval.
- **Permissions:** the managed identity has only **App Configuration Data Reader**. Writing
  from the admin page needs **Data Owner**, which widens what a compromised app can do to its
  own config. The alternative is to set the key by hand in the portal and lose the one-click
  admin flow.
- **Availability:** App Configuration is itself one of the services the banner can be warning
  about (`ConfigurationHealthy`). Reads fall back to the last loaded value, so this is mostly
  fine, but setting the message would fail during an App Configuration outage.
- **Effort:** medium. Add the SDK client for writes, a role assignment, refresh registration,
  and tests that stub `IConfigurationRefresher`.
- **Fit:** good for a message that's planned ahead and set by hand. Awkward for "flip the banner
  on now from the admin page".

## Option C: SQL table

A one-row (or key/value `SiteSetting`) table through an EF migration, read with a short
in-memory cache.

- **Multi-instance:** yes, with whatever lag the cache TTL allows.
- **Availability:** this fails goal 4. Database maintenance is one of the main reasons to post
  the banner, and with SQL down, a restarted instance can't load the message. That's the case
  this work exists to fix. It'd need a file fallback on top, at which point it's A plus extra
  steps.
- **Effort:** medium. Migration, fluent mapping in `DanceMusicContext.OnModelCreating`,
  service, tests.
- **Fit:** only worth it if a general "site settings" table is wanted for other reasons.

## Option D: Azure Blob Storage

A single JSON blob, read with a short cache. This is structurally the same as A2, but on
storage that's shared across plans, slots and regions.

- **Multi-instance:** yes, across everything.
- **Availability:** independent of SQL, Search and App Configuration.
- **Effort:** medium. Needs a storage account (none is in use today; see
  [log-persistence-options](log-persistence-options.md), which may add one), a container, a
  **Storage Blob Data Contributor** grant for the managed identity, and the SDK.
- **Fit:** the robust multi-instance choice if the app ever runs outside a single plan. Worth
  doing then, and cheapest if a storage account already exists for logs.

## Not recommended

- **Redis / distributed cache:** a new paid service just for one string.
- **SignalR push:** the client already polls at 30 seconds while a message is up, which is fast
  enough.

## Recommendation

1. **Ship Option A now**, behind `IUpdateMessageStore`, with the checkbox, the startup load,
   clear-also-clears-the-file, and an optional `ExpiresAt`. Verify once in Kudu that the file
   survives a deploy.
2. **If the app scales out within one plan**, add A2's re-read cache. It's a few lines, and the
   admin UI and call sites stay the same.
3. **If it goes multi-plan, multi-slot or multi-region**, swap the store for Option D. Prefer D
   over B unless the main workflow becomes planned messages entered in the portal, and avoid C
   because it fails exactly when the banner is most needed.

When this ships, fold the as-built design into
[service-resilience § Client status banner](../infrastructure/service-resilience.md#client-status-banner),
replace its "Persist the update message" future-improvement bullet, and delete this plan.

## Related

- [service-resilience](../infrastructure/service-resilience.md): health endpoints and the status
  banner
- [dance-domain-model](../dances/dance-domain-model.md): `DanceStatsFileManager` and the
  `AppData` runtime cache this mirrors
- [configuration-and-feature-flags](../infrastructure/configuration-and-feature-flags.md):
  App Configuration sentinel refresh (Option B)
- [hosting-and-identity](../infrastructure/hosting-and-identity.md): managed identity roles
