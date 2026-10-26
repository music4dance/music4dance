# Persistent Site Update Message

**Type:** Plan
**Status:** Partially implemented. Options A and A2 (a shared file in `wwwroot/AppData`, re-read
every 30 seconds) shipped 2026-10-25. The options below are for running beyond one App Service
plan.
**Last verified:** 2026-10-25
**Code:** `m4d/Services/UpdateMessageService.cs`, `m4d/Services/UpdateMessageFileManager.cs`

## Where it stands

The admin update message is persisted and shared as described in
[service-resilience § Update message](../infrastructure/service-resilience.md#update-message).
The file lives on the `/home` volume, which every instance in one App Service plan shares, so
scale-out within the plan works today with up to about 30 seconds of lag.

What the file doesn't reach: deployment slots, a second plan, or a second region. Each of those
has its own `/home`. The options below cover those cases. Each one is a replacement for
`IUpdateMessageFileManager` (read, write and delete one small JSON document) and leaves
`UpdateMessageService`, the admin page and the banner unchanged.

The requirement that rules options in or out: **the store mustn't depend on what's being
upgraded.** The banner is mostly used during infrastructure work, often with SQL or Search
degraded.

One check is still open: confirm in Kudu that `update-message.json` survives a deploy. An
`AzureWebApp@1` zip deploy should remove only files from the previous package, and
`dance-environment.json` already relies on that.

## Option B: Azure App Configuration key

Store the message as a key (for example `Site:UpdateMessage`) in the App Configuration store
the app already reads, with sentinel refresh.

- **Reach:** every instance, slot and region that shares the store.
- **Latency:** the sentinel refresh interval is 5 minutes, and a change only shows up after
  `Configuration:Sentinel` is bumped. Either the admin action writes both the key and the
  sentinel (which triggers a refresh-all on every instance, a bigger hammer than a banner
  deserves), or the key gets its own registration with a shorter interval.
- **Permissions:** the managed identity has only **App Configuration Data Reader**. Writing
  from the admin page needs **Data Owner**, which widens what a compromised app can do to its
  own config. The alternative is to set the key by hand in the portal and lose the one-click
  admin flow.
- **Availability:** App Configuration is itself one of the services the banner can be warning
  about. Reads fall back to the last loaded value, but setting the message would fail during an
  App Configuration outage.
- **Effort:** medium. Add the SDK client for writes, a role assignment, refresh registration,
  and tests that stub `IConfigurationRefresher`.
- **Fit:** good for a message that's planned ahead and set by hand. Awkward for "flip the banner
  on now from the admin page".

## Option C: SQL table

A one-row (or key/value `SiteSetting`) table through an EF migration.

- **Reach:** everything that shares the database.
- **Availability:** fails the requirement above. Database maintenance is one of the main reasons
  to post the banner, and with SQL down, a restarted instance can't load the message.
- **Fit:** only worth it if a general "site settings" table is wanted for other reasons, and even
  then the file should stay as a fallback.

## Option D: Azure Blob Storage

A single JSON blob. This is the shipped file design on storage shared across plans, slots and
regions.

- **Reach:** everything with access to the container.
- **Availability:** independent of SQL, Search and App Configuration.
- **Effort:** medium. Needs a storage account (none is in use today; see
  [log-persistence-options](log-persistence-options.md), which may add one), a container, a
  **Storage Blob Data Contributor** grant for the managed identity, and the SDK. The read path
  becomes a network call every 30 seconds per instance, so it should be async, or done on a
  timer rather than inside the health request.

## Not recommended

- **Redis / distributed cache:** a new paid service just for one string.
- **SignalR push:** the client already polls at 30 seconds while a message is up, which is fast
  enough.

## Recommendation

If the app goes multi-plan, multi-slot or multi-region, implement **Option D**. Prefer D over B
unless the main workflow becomes planned messages entered in the portal, and avoid C because it
fails exactly when the banner is most needed. Until then, nothing more is needed.

## Related

- [service-resilience](../infrastructure/service-resilience.md): health endpoints, the status
  banner, and the shipped update-message design
- [configuration-and-feature-flags](../infrastructure/configuration-and-feature-flags.md):
  App Configuration sentinel refresh (Option B)
- [hosting-and-identity](../infrastructure/hosting-and-identity.md): managed identity roles
