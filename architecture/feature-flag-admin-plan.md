# Feature Flag Admin Plan

Turning feature flags on and off from the admin UI, writing through to Azure App Configuration and
taking effect immediately on the instance the change was made from.

**Verdict: feasible.** The moving parts are all already in the app; the only thing it doesn't have
today is permission to write. The one genuinely new piece of engineering is making a change visible
*now* on the instance that made it, because the App Configuration provider is a poller and a poll
interval is a poll interval. That's solved with a small in-process override layer, described in
[§4](#4-making-it-take-effect-here-and-now).

---

## 1. Where things stand

| Piece | Today |
| --- | --- |
| Flag names | `m4d/Utilities/FeatureFlags.cs` - ten `const string`s |
| Local defaults | `m4d/appsettings.json` → `FeatureManagement` (three entries; anything absent is off) |
| Remote source | Azure App Configuration `music4dance` (`https://music4dance.azconfig.io`), wired in `m4d/Configuration/M4dApplicationExtensions.cs` |
| Auth | Managed identity, **App Configuration Data Reader** (`azure-app-service-setup-managed-identity.md` §2.1) |
| Read path | `IFeatureManagerSnapshot.IsEnabledAsync(...)`, injected into every controller via `DMController` |
| Refresh | Polling, 5-minute interval for both key-values and feature flags |
| Admin UI | None. `Admin/InitializationTasks` *displays* `ArtistIndex` read-only (`AdminController.cs:834`) |
| Environments | `Production` → `msc4dnc`, `Staging` → `m4d-test` (`azure-pipelines.yml:35-40`), one instance each |

Flags are selected twice, unlabelled then labelled, so an environment-labelled flag wins over the
shared default for that environment:

```csharp
_ = featureFlagOptions.Select(KeyFilter.Any, LabelFilter.Null);
_ = featureFlagOptions.Select(KeyFilter.Any, environment.EnvironmentName);
_ = featureFlagOptions.SetRefreshInterval(TimeSpan.FromMinutes(5));
```

That is exactly the per-environment targeting this feature needs - a toggle made on staging should
land on the `Staging` label and not touch production. No configuration change required.

App Configuration is **only registered outside Development** (the `if (!isDevelopment)` block around
`AddAzureAppConfiguration`). Locally, flags come from `appsettings.json` and there is no remote store
to write to. The design has to have an answer for that, and it does -
[§6](#6-development-and-degraded-modes).

---

## 2. The two halves of the problem

Writing a flag and seeing a flag are separate problems with separate solutions, and conflating them
is the main way this goes wrong.

1. **Durable.** The new value has to survive a restart and reach the other instance. That means
   writing it to App Configuration. Every instance picks it up on its own refresh - up to 5 minutes.
2. **Immediate.** The instance whose admin page was used has to reflect the change on the very next
   request. The provider won't refresh before its interval elapses, so something local has to carry
   the value across the gap.

---

## 3. Writing to App Configuration

`Azure.Data.AppConfiguration` (1.11.0) is already in the dependency graph as a transitive of
`Microsoft.Azure.AppConfiguration.AspNetCore`; using `ConfigurationClient` directly means adding a
`PackageReference` and a `PackageVersion`, nothing more.

```csharp
var client = new ConfigurationClient(new Uri(appConfigEndpoint), azureCredential);
var key = FeatureFlagConfigurationSetting.KeyPrefix + name;   // ".appconfig.featureflag/ArtistIndex"
```

**Read, modify, write - not blind write.** `new FeatureFlagConfigurationSetting(name, enabled, label)`
straight into `SetConfigurationSettingAsync` would replace the whole flag, discarding any description
or client filters set up in the portal. The app's flags are plain booleans today, but a targeting
filter added later shouldn't be silently erased by a toggle:

```csharp
var existing = await client.GetConfigurationSettingAsync(key, label);      // 404 is expected
var flag = existing.Value as FeatureFlagConfigurationSetting
           ?? new FeatureFlagConfigurationSetting(name, enabled, label);
flag.IsEnabled = enabled;
var response = await client.SetConfigurationSettingAsync(flag, onlyIfUnchanged: true);
```

`onlyIfUnchanged` sends the ETag, so two admins racing produce a 412 rather than a lost update.

### Which label

**The environment's own label**, `IWebHostEnvironment.EnvironmentName` - `Staging` from the test
instance, `Production` from the live one. That's what makes "the change affects the kind of instance
the page is running on" true, and it falls straight out of the existing `Select` calls.

Worth knowing: if a flag currently lives in the store **unlabelled**, toggling it from staging
doesn't modify that entry - it creates a `Staging`-labelled one that shadows it for staging only.
Production keeps reading the unlabelled value until it's toggled too. That's the right behaviour,
but it means the store accumulates labelled entries and the unlabelled one stops being the whole
story. The admin page should therefore show, per flag, **which label the current value came from**.

---

## 4. Making it take effect here and now

### Why a refresh call isn't enough

`IConfigurationRefresher.TryRefreshAsync()` respects the configured refresh interval - inside those
5 minutes it's a no-op. There is a supported way to override that:
`ProcessPushNotification(PushNotification, TimeSpan?)`, which exists for Event Grid webhooks. It
takes the `Sync-Token` header off the write response, marks the provider dirty and guarantees the
next refresh sees at least that write:

```csharp
response.GetRawResponse().Headers.TryGetValue("Sync-Token", out var syncToken);
refresher.ProcessPushNotification(new PushNotification
{
    ResourceUri = refresher.AppConfigurationEndpoint,
    SyncToken = syncToken,
    EventType = "Microsoft.AppConfiguration.KeyValueModified",
}, TimeSpan.Zero);
await refresher.TryRefreshAsync();
```

This is worth doing - it's the documented mechanism and it costs ten lines. But it is not worth
*depending* on: whether marking the provider dirty also resets the separate feature-flag refresh
timer is an implementation detail of the provider, not something its contract promises. "The toggle
usually takes effect immediately" is a worse outcome than either extreme.

### The override layer

So the guarantee comes from the app, not the provider. A singleton holds flags that have been
toggled but whose cloud value hasn't come back round yet:

```csharp
public sealed class FeatureFlagOverrides
{
    // name -> (value, when it was set)
    private readonly ConcurrentDictionary<string, (bool Enabled, DateTimeOffset Set)> _overrides = new();
    public void Set(string name, bool enabled);
    public bool? Get(string name, bool underlying);   // retirement rules below
}
```

An override is a **bridge, not a pin**, and it retires itself:

- **On agreement.** The first read where the underlying configuration already says what the override
  says means the write has landed; drop it and get out of the way. Without this rule, a later change
  made in the Azure portal would be masked on that instance until restart.
- **On timeout.** A backstop TTL (15 minutes, comfortably past two refresh intervals) covers a write
  that was rolled back before this instance ever saw it.

Both rules are pure functions of `(override value, underlying value, elapsed)`, so this is
straightforward to unit test with no Azure and no clock of its own beyond an injected `TimeProvider`.

### Where it hooks in

Decorate **`IFeatureDefinitionProvider`**, the single point every flavour of feature manager
(`IFeatureManager`, `IFeatureManagerSnapshot`, `IVariantFeatureManager`, the `<feature>` tag helper)
resolves definitions through. Decorating `IFeatureManagerSnapshot` instead would cover every call
site the app has today, but would quietly not cover the next one.

```csharp
builder.Services.AddFeatureManagement();

// Replace rather than race TryAdd: take whatever AddFeatureManagement registered, construct it, and
// wrap it. ConfigurationFeatureDefinitionProvider's public ctor takes IConfiguration.
var inner = services.Single(d => d.ServiceType == typeof(IFeatureDefinitionProvider));
services.Remove(inner);
services.AddSingleton<IFeatureDefinitionProvider>(sp => new OverridableFeatureDefinitionProvider(
    (IFeatureDefinitionProvider)ActivatorUtilities.CreateInstance(sp, inner.ImplementationType),
    sp.GetRequiredService<FeatureFlagOverrides>()));
```

The decorator asks the inner provider for the definition, consults the override, and returns either
the definition unchanged or a substituted one:

```csharp
new FeatureDefinition
{
    Name = name,
    // "AlwaysOn" is the filter name Microsoft.FeatureManagement gives a flag configured as a bare
    // `true`, and FeatureManager short-circuits on it - the same shape "Flag": true produces.
    EnabledFor = enabled ? [new FeatureFilterConfiguration { Name = "AlwaysOn" }] : [],
}
```

Cost when nothing is overridden: one lookup on a `ConcurrentDictionary` that is almost always empty.

---

## 5. The admin page

`/Admin/FeatureFlags`, `[Authorize(Roles = "dbAdmin")]` - writing a production flag is squarely in
the same class as the other `dbAdmin` actions, not `showDiagnostics`.

A Razor view is the right call here, not a Vue page: ten rows, one POST per row, no filtering,
sorting or paging. It follows `InitializationTasks.cshtml`'s existing form-per-action pattern
(`Html.BeginForm` + `Html.AntiForgeryToken` + a `confirm()`), and the read-only `ArtistIndex` line
that page shows today should be replaced by a link to the new one.

Per flag, show:

- Name, and whether it is **on or off right now on this instance**
- **Where the value came from**: this instance's override, the `Production`/`Staging` label, the
  unlabelled default in the store, or `appsettings.json`
- A toggle, with a confirm naming the environment being changed

Above the table:

- The environment this page is running in, spelled out - "changing a flag here affects **Staging**
  (`m4d-test`)" - because the most expensive mistake available on this page is thinking you're on the
  other one
- When this instance last refreshed from App Configuration, and the 5-minute interval
- The multi-instance warning:

  > Both environments run a single instance today, so a change is live here immediately, and on the
  > other environment only when you make it there. If either environment is ever scaled out, the
  > other instances will keep serving the old value for up to 5 minutes after a change.

Flags are listed from `FeatureFlags.cs` rather than from whatever happens to exist in the store, so a
flag that has never been written is still a row you can turn on. (`GetFeatureNamesAsync()` returns
only configured flags; the constants are the real inventory.)

---

## 6. Development and degraded modes

Three states, and the page should say which one it's in rather than failing:

| State | Toggle behaviour |
| --- | --- |
| App Configuration available (Staging/Production) | Write through, then set the bridging override |
| Development - no store registered at all | Override only, no expiry, labelled "this instance, until restart" |
| Store registered but unavailable (`ServiceHealthManager.IsServiceAvailable("AppConfiguration")` false) | Refuse the write and say so; offer the same local-only override explicitly, so a flag can still be turned off during an incident |

The local-only mode is worth having for its own sake: flipping `ArtistIndex` while developing
currently means editing `appsettings.json` and restarting.

---

## 7. Security

**This is the part that deserves a second look before any of it ships.**

Writing requires **App Configuration Data Owner** in place of (or alongside) the current Data Reader
assignment, for both app services' managed identities. App Configuration RBAC has no per-key scoping:
Data Owner on the store is write access to *everything* in it, including its Key Vault references. An
app compromise that today can read configuration would then be able to rewrite it - for instance, to
repoint a Key Vault reference or a service endpoint.

Options, in order of preference:

1. **A second App Configuration store for flags only.** The app keeps Data Reader on the main store
   and gets Data Owner on the flags store, which holds nothing but `.appconfig.featureflag/*` and no
   Key Vault references. Costs a second `AddAzureAppConfiguration` source and a second endpoint
   setting; caps the blast radius at "someone can toggle features".
2. **Data Owner on the single existing store**, accepting the widened radius. Simplest, and
   defensible given the endpoint is `dbAdmin`-only and antiforgery-protected - but it is a real
   widening of what a compromised web app can do, and worth a deliberate decision rather than a
   default.

Either way the write endpoint wants: `dbAdmin` only, `[ValidateAntiForgeryToken]`, POST only, the
flag name validated against the `FeatureFlags` constants (never a free-text key - that is what would
turn this into "write anything to the store"), and every change logged with user, flag, old value,
new value and environment.

---

## 8. Work items

Each phase is independently shippable, and the first two need no Azure change at all.

| # | Phase | Needs |
| --- | --- | --- |
| 1 | Read-only `/Admin/FeatureFlags`: every flag, its current value, its source, last refresh time, environment banner | Nothing new |
| 2 | `FeatureFlagOverrides` + `OverridableFeatureDefinitionProvider` + local-only toggles | Nothing new |
| 3 | `IFeatureFlagWriter` over `ConfigurationClient`, write-through with ETag, `ProcessPushNotification` + `TryRefreshAsync`, override set on success | **RBAC decision from §7**, `Azure.Data.AppConfiguration` package reference |
| 4 | Audit logging, multi-instance warning copy, replace the read-only `ArtistIndex` line in `InitializationTasks.cshtml` with a link | Nothing new |

Phase 1 is worth having whatever happens to the rest: "what is this instance's `ArtistIndex` actually
set to, and where did that come from" is a question the app currently can't answer.

---

## 9. Testing

- **`FeatureFlagOverrides`** - set, read, retire-on-agreement, retire-on-TTL. Pure, with an injected
  `TimeProvider`; no Azure, no configuration.
- **`OverridableFeatureDefinitionProvider`** - wrapping a stub inner provider: override on and off, no
  override passes through untouched, a flag the inner provider has never heard of can still be
  overridden on.
- **The controller** - against a fake `IFeatureFlagWriter`: rejects a name that isn't in
  `FeatureFlags`, rejects without the `dbAdmin` role, sets the override only when the write succeeded,
  surfaces a 412 as "someone else changed this" rather than a 500.
- **Manual, on staging first** - toggle `ArtistIndex` off and on, confirm the page reflects it on the
  next request, confirm the Azure portal shows the `Staging`-labelled flag, confirm production is
  untouched, then restart the staging app and confirm the value survives. That last step is the test
  that the write, and not just the override, did its job.

---

## 10. Verify before building

1. **What's in the store now.** `az appconfig feature list --name music4dance` - which flags exist,
   under which labels. Everything above assumes flags are either unlabelled or environment-labelled;
   anything else changes the label story in §3.
2. **Schema coexistence.** The provider surfaces flags under `feature_management:feature_flags`, while
   `appsettings.json` uses the older `FeatureManagement:{name}` shape.
   `ConfigurationFeatureDefinitionProvider` reads both, but it is worth confirming on staging that a
   flag present only in `appsettings.json` still works once other flags arrive from the store - that
   the two schemas genuinely merge rather than the newer one winning wholesale. Phase 1's "where did
   this value come from" column answers this directly and is the cheapest way to find out.
3. **App Configuration tier and request quota**, should the 5-minute interval ever be shortened to
   make other instances converge faster. Two instances at 5 minutes is ~576 requests/day; at 30
   seconds it is ~5,760, over the free tier's daily allowance.
