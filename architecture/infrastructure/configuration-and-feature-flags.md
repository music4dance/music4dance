# Configuration and Feature Flags

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4d/Configuration/M4dApplicationExtensions.cs`, `m4d/Program.cs`,
`m4d/Utilities/FeatureFlags.cs`, `m4d/appsettings.json`, `m4d/appsettings.Development.json`,
`m4d.Sandbox/appsettings.json`, `m4d/Properties/launchSettings.json`

Every configuration key and feature flag the app reads: where it's read, where its value comes
from, and what happens when it's missing. This doc never records secret values. For *how* App
Configuration, Key Vault and managed identity are wired up (labels, sentinel refresh, credential
chain), see [hosting-and-identity](hosting-and-identity.md#identity-and-secrets).

## Sources and precedence

The app uses the standard `WebApplication.CreateBuilder` sources, in increasing precedence:
`appsettings.json`, `appsettings.{Environment}.json`, user secrets (Development only), environment
variables, command line. On top of those:

| Environment | Extra source | Notes |
| --- | --- | --- |
| Production / Staging (Azure) | Azure App Configuration at `AppConfig:Endpoint`, with Key Vault references resolved through `ConfigureKeyVault` | Added in `AddM4dApplication` **only when the environment isn't Development**. Keys and flags with no label, overlaid by keys labeled with the environment name. |
| Development (`m4d`) | User secrets, `UserSecretsId` `60050f39-…` in `m4d/m4d.csproj` | Where secrets and the `PROD_DB` / `TEST_DB` connection strings go locally. App Configuration isn't loaded, even though `appsettings.Development.json` sets `AppConfig:Endpoint`. |
| Development (`m4d.Sandbox`) | None | Content root is `m4d.Sandbox/`, so only `m4d.Sandbox/appsettings.json` loads. The project has no `UserSecretsId`. |

App Service app settings arrive as environment variables. The pipeline sets
`ASPNETCORE_ENVIRONMENT`, `SEARCHINDEX`, `SEARCHINDEXVERSION`, `SELF_CONTAINED_DEPLOYMENT` and
`WEBSITES_CONTAINER_START_TIME_LIMIT` on every deploy; Service Connector injects
`AZURE_SQL_CONNECTIONSTRING` (see [hosting-and-identity](hosting-and-identity.md#deployment)).
Nested keys use `__` in environment variables (`FeatureManagement__SongPropertyCompression`).

**When a change takes effect.** App Configuration refreshes everything when
`Configuration:Sentinel` changes, and feature flags on their own 5-minute interval. That only
helps code that re-reads `IConfiguration` or `IFeatureManager` per request. Many keys below are
read once, at service registration or in a constructor of a singleton, and need an app restart.
The **Read** column says which.

## Feature flags

All flags are `Microsoft.FeatureManagement` flags named by constants in `FeatureFlags`
(`m4d/Utilities/FeatureFlags.cs`), registered with `AddFeatureManagement()`. In Azure they come
from App Configuration feature flags; locally from the `FeatureManagement` section of
appsettings. A flag that isn't defined anywhere evaluates to off. `/Admin/Diagnostics` lists
every flag the feature manager knows and its current value.

| Flag | What it gates | Checked in | `appsettings.json` | `appsettings.Development.json` | Sandbox |
| --- | --- | --- | --- | --- | --- |
| `ActivityLogging` | Writing `ActivityLog` rows for Spotify exports, payments and profile changes | `SongController`, `PaymentController`, `SpotifyPlaylistController`, `Manage/Index.cshtml.cs` | (unset: off) | `true` | (unset) |
| `ArtistIndex` | Artist page and artist suggestions; `artistIndex` in the client `menuContext` | `SongController`, `SuggestionController`, `AdminController`, `_head.cshtml` | `false` | `true` | `true` |
| `Captcha` | reCAPTCHA on login (once escalated by failures or rate limiting), register and anonymous contribute/checkout | `Login.cshtml.cs`, `Register.cshtml.cs`, `PaymentController`, `Contribute.cshtml` | (unset: off) | `true` | `false` |
| `ClientSideUsageLogging` | Switches page-view logging from server-side (`DMController`) to the client batch endpoint | `DMController`, `_head.cshtml` | (unset: off) | `true` | (unset) |
| `CustomerReminder` | Premium-upgrade reminder for signed-in non-premium users (also suppresses Google ads on that page) | `_head.cshtml` | (unset: off) | `true` | (unset) |
| `GoogleTagManager` | GTM container snippet | `_head.cshtml`, `_bs5-Layout.cshtml`, `_vue-Layout.cshtml` | (unset: off) | `false` | (unset) |
| `GoogleTags` | gtag.js / GA4 snippet | `_head.cshtml` | (unset: off) | `false` | (unset) |
| `PublicApi` | Registers the OpenIddict public-API foundation | `PublicApiServiceCollectionExtensions.AddPublicApiFoundation` | `false` | `false` | `false` |
| `SongPropertyCompression` | Whether song property logs are written compressed to Azure Search (reads handle both) | `AddM4dApplication` sets `m4dModels.SongPropertyCompression.Enabled` | `true` | `true` | `true` |
| `UsageLogging` | Server-side page-view logging, when client-side logging is off | `DMController` | (unset: off) | `true` | (unset) |

Two flags are **not** evaluated through `IFeatureManager`. `PublicApi` and
`SongPropertyCompression` are read as plain configuration values
(`FeatureManagement:PublicApi`, default `false`; `FeatureManagement:SongPropertyCompression`,
default `true`) once at startup, so they're fixed for the life of the process. `PublicApi` also
throws at startup if it's on in Production or with `PROD_DB` set; see
[plans/public-api-authorization](../plans/public-api-authorization.md). The
`m4d-vite-no-compression` launch profile turns compression off for manual comparison.

The GTM container ID (in `_head.cshtml` and the `noscript` iframes in `_bs5-Layout.cshtml` /
`_vue-Layout.cshtml`) and the GA4 measurement ID (in `_head.cshtml`) are hard-coded, not
configuration. Setup is in
[runbooks/gtm-ga4-setup](../runbooks/gtm-ga4-setup.md).

## Hosting, environment and database switches

| Key | Read by | Read | Source | Default / when missing |
| --- | --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Framework; `IsDevelopment()` branches throughout `M4dApplicationExtensions` | Startup | Pipeline app setting; launch profiles | `Production` |
| `SMOKE_TEST_MODE` | `Program.cs` | Startup | Env var | `false`. `true` serves a minimal diagnostic page and skips everything else. |
| `SELF_CONTAINED_DEPLOYMENT` | `AddM4dApplication`, `UseM4dPipeline` | Startup | Pipeline app setting | `false` (framework-dependent). See [hosting-and-identity](hosting-and-identity.md#deployment). |
| `HOME` | `AddM4dApplication` (self-contained only: data-protection key path) | Startup | App Service | Unset: platform default data protection |
| `DISABLE_HTTPS_REDIRECT` | `UseM4dPipeline` | Startup | `m4d-spotify` launch profile; sandbox appsettings | `false`. `true` skips HTTPS and `www` redirects (Spotify rejects `https://localhost`). |
| `ASPNETCORE_VITE` | `ConfigurationExtensions.UseVite` (`M4DHostEnvironmentExtension.cs`) | Startup | `*-vite` launch profiles | Not `"true"`: serve the built client from `wwwroot/vclient` instead of the Vite dev server |
| `AppConfig:Endpoint` | `AddM4dApplication` | Startup | `appsettings.json` (`https://music4dance.azconfig.io`); sandbox sets it empty | Empty: `AppConfiguration` marked unavailable, local config only. Ignored in Development. |
| `Configuration:Sentinel` | App Configuration refresh registration; logged by `UseM4dPipeline` and `StartupInitializationService`; shown on `/Admin/Diagnostics` | Refresh trigger | App Configuration (environment label) | No value: no refresh-all is ever triggered |
| `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` | `AddM4dApplication` (Development credential selection) | Startup | User secrets or env vars | Any missing: full `DefaultAzureCredential` chain |
| `AZURE_SQL_CONNECTIONSTRING` | `Program.cs`, `DbContext` factory, `DatabaseRecoveryService` | Startup and every `DbContext` resolution | Service Connector (App Service) | Falls through to the keys below |
| `PROD_DB` + `ProdConnectionString` | `Program.cs` (Development only), `DbContext` factory, `AdminController.RestoreDb`, `_head.cshtml` (`isProdDb` banner), `AddPublicApiFoundation` | Startup / per resolution / per request | `m4d-prod-db` launch profile + user secrets | `false` |
| `TEST_DB` + `TestConnectionString` | Same as `PROD_DB` (except the public API) | Same | `m4d-test-db` launch profile + user secrets | `false`. Requested without a connection string: ignored with a warning. |
| `ConnectionStrings:DanceMusicContextConnection` | `Program.cs`, `DbContext` factory, `DatabaseRecoveryService`; sandbox with `SANDBOX_USE_LOCALDB` | Same | `appsettings.json` (LocalDB `m4d`) | No connection string at all: `Database` unavailable, placeholder context |
| `ServiceHealth:DatabaseRetryInterval` | `DatabaseRecoveryService` constructor | Startup | `appsettings.json` (`00:01:00`) | 1 minute |
| `SANDBOX_USE_LOCALDB` | `m4d.Sandbox/Program.cs` | Startup | `m4d.Sandbox-localdb` launch profile | `false`: EF in-memory database |

The connection-string precedence and the `PROD_DB` / `TEST_DB` workflow are described in
[hosting-and-identity](hosting-and-identity.md#identity-and-secrets) and
[runbooks/provision-app-service](../runbooks/provision-app-service.md#local-development-access-production-database-via-azure-ad).

## Search indexes

| Key | Read by | Read | Source | Default |
| --- | --- | --- | --- | --- |
| Any top-level section with an `indexname` child (`SongIndexProd-3`, `SongIndexTest-4`, `SongIndexExperimental`, `PageIndex`, …), each with `endpoint` + `indexname` | `AddM4dApplication` registers a named `SearchClient` per section, plus one `SearchIndexClient` named `SongIndex` from the first `SongIndex*` section | Startup (skipped when `M4dApplicationOptions.ConfigureSearch` is false, as in the sandbox) | `appsettings.json` | None |
| `SongIndex*` sections whose `indexname` starts with `songs-` | `SearchServiceManager` (`m4dModels/SearchServiceInfo.cs`) builds the id/version table from `<id>-<version>` | Startup | `appsettings.json` | — |
| `SEARCHINDEX` | `SearchServiceManager` | Startup | Pipeline app setting; launch profiles | `SongIndexProd` |
| `SEARCHINDEXVERSION` | `SearchServiceManager` | Startup | Pipeline app setting; `m4d-next` profile | The build's `CodeVersion`. Never allowed below it; ignored when `SEARCHINDEX` is unset. |

What the ids and versions mean is in [search-index-versioning](../search/search-index-versioning.md).
`PageIndex` is the site page index used by `APIControllers/SearchController`.

## Secrets and third-party credentials

All of these are read once at startup or construction. In Azure they come from App Configuration /
Key Vault (see [hosting-and-identity](hosting-and-identity.md#identity-and-secrets)); locally from
user secrets. A missing value marks the matching service unavailable and registers a fallback
rather than failing startup; see [service-resilience](service-resilience.md).

| Key | Read by | When missing |
| --- | --- | --- |
| `Authentication:Google:ClientId` / `ClientSecret` | `AuthenticationBuilderExtensions.AddGoogleWithResilience` | `GoogleOAuth` unavailable, no Google login |
| `Authentication:Facebook:ClientId` / `ClientSecret` | `AddFacebookWithResilience` | `FacebookOAuth` unavailable |
| `Authentication:Spotify:ClientId` / `ClientSecret` | `AddSpotifyWithResilience`; also `CoreAuthentication` (via `SpotAuthentication`, client name `spotify`) for app tokens | Spotify login unavailable; app-token calls fail |
| `Authentication:AzureCommunicationServices:ConnectionString` | `ServiceCollectionExtensions.AddEmailSenderWithResilience` | `EmailService` unavailable; `NullEmailSender` |
| `Authentication:reCAPTCHA:SiteKey` / `SecretKey` | `AddReCaptchaWithResilience` | `ReCaptcha` unavailable; `NullReCaptchaSiteVerify` |
| `Authentication:Stripe:SecretKey`, `Authentication:StripeTest:SecretKey` | `PaymentController` constructor (sets `StripeConfiguration.ApiKey`) | Checkout fails. The `Test` key is used when `GlobalState.UseTestKeys` is true: on in Development, toggled at `/admin/toggletestkeys`. |
| `Authentication:RecomputeJob:Key` | `TokenRequirement` (cached in a static on first use) | Token-authenticated recompute/automation endpoints reject every call. See [spotify-playlist-automation](../music-services/spotify-playlist-automation.md). |
| `Authentication:AutoMapper:Key` | `AddM4dApplication` (`AddAutoMapper` license key) | AutoMapper runs unlicensed |

## Seed users (Development and sandbox)

`UserManagerHelpers.SeedData` reads `M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD`, `M4D_TEST_USER` /
`M4D_TEST_PASSWORD` and `M4D_EDITOR_USER` / `M4D_EDITOR_PASSWORD`. It runs after migrations in
Development, from `AdminController.RestoreDb`, and on every `m4d.Sandbox` start. A pair whose user
name is unset is skipped; a user name without a password throws. The sandbox's own
`appsettings.json` defines all three pairs (development-only accounts, described in
[contributor-test-environments](../dev-testing/contributor-test-environments.md)); for `m4d` they
go in user secrets.

## Behavior settings

| Key | Read by | Read | Code default | `appsettings.json` |
| --- | --- | --- | --- | --- |
| `RateLimiting:MaxRequestsPerWindow` | `RateLimitingOptions` (built in the `RateLimitingMiddleware` constructor) | Startup | `10` | `10` (sandbox `1000`) |
| `RateLimiting:WindowMinutes` | same | Startup | `1` | `1` |
| `RateLimiting:GlobalMaxRequestsPerWindow` | same | Startup | `100` | `100` (sandbox `1000`) |
| `RateLimiting:GlobalWindowMinutes` | same | Startup | `1` | `1` |
| `RateLimiting:CaptchaThresholdPercent` | same | Startup | `20` | `20` |
| `Configuration:BotFilter:ExcludeTokens` / `ExcludeFragments` / `BadFragments` (`;`-separated) | `SpiderManager` (`BotFilterInfo`) | Every check | Empty: no user agent is treated as a bot except an empty one | (unset; App Configuration) |
| `Configuration:Commerce:Enabled` | `CommerceController.IsCommerceEnabled` | Per request | `true` | (unset; sandbox `false`) |
| `Configuration:Commerce:FailLimit` | `CommerceController.GetCardFailLimit` | Per request | `5` | (unset) |
| `Configuration:Marketing` (`Enabled`, `Banner`, `Notice`, `Start`, `End`, `Product:Name`/`Link`/`Password`) | `GlobalState.SetMarketing` → `MarketingInfo` | **Once**, at registration | Missing: no marketing banner/notice | (unset) |
| `ServiceHealth:AdminNotifications` (`Enabled`, `Recipients`, `IncludeStackTrace`, `SenderAddress`) | `ServiceHealthNotifier` | When the notifier is built | `Enabled=false`, sender `donotreply@music4dance.net` | (unset) |
| `UsageTracking:Enabled` / `AnonymousThreshold` / `AnonymousBatchSize` / `AuthenticatedBatchSize` / `MaxQueueSize` | `_head.cshtml` → client `menuContext` | Per page | `true` / `3` / `5` / `1` / `100` | same values |
| `EngagementOffcanvas:Enabled` | `_head.cshtml` | Per page | `false` | `true` |
| `EngagementOffcanvas:ShowForAnonymous` / `ShowForLoggedIn` | same | Per page | `true` / `true` | `true` / `true` |
| `EngagementOffcanvas:FirstShowPageCount` / `RepeatInterval` | same | Per page | `2` / `5` | `2` / `5` |
| `EngagementOffcanvas:SessionDismissalTimeout` | same | Per page | `60` | `15` |
| `EngagementOffcanvas:Messages:Level1`/`Level2`/`Level3`/`LoggedInUpgrade`, `PremiumBenefits:Items`/`MoreText`/`CompleteListUrl`, `CtaUrls:Register`/`Login`/`Subscribe`/`Features` | same | Per page | Empty messages and items; built-in URLs | Full copy and URLs |

Rate limiting and the bot filter are explained in
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md); usage tracking in
[usage-tracking](../observability/usage-tracking.md); the engagement offcanvas in
[visitor-engagement-monetization](../pages/visitor-engagement-monetization.md); failure email in
[runbooks/configure-failure-email](../runbooks/configure-failure-email.md).

## Framework-consumed sections

These are read by libraries rather than app code:

- `Logging` (including `AzureAppServicesFile` / `AzureAppServicesBlob` provider overrides): log
  levels; see [logging-and-diagnostics](../observability/logging-and-diagnostics.md).
- `AllowedHosts`: `*`.
- `Vite` (`Base`, `PackageManager`, `PackageDirectory`, `IgnoreMissingAssets`, `Server:Port`,
  `Server:Https`): `Vite.AspNetCore`. App code also reads `Vite:Base` (default `vclient`) to create
  the build output folder.

## Test and tooling settings

Outside the app, `m4dModels.Tests` reads the `m4d` user secrets for
`Authentication:Spotify:ClientId` / `ClientSecret` (falling back to `M4D_SPOTIFY_CLIENT_ID` /
`M4D_SPOTIFY_CLIENT_SECRET`) and the `M4D_ARTIST_*` environment variables for the manual artist
splitter analyses. These only affect opt-in analysis tests.

## Future improvements

- **`PROD_DB` / `TEST_DB` are honored inconsistently.** `Program.cs` ignores them outside
  Development, but the `DbContext` factory, `_head.cshtml`, `AdminController.RestoreDb` and
  `AddPublicApiFoundation` read them in any environment.
- **Read-once settings don't follow the sentinel refresh.** `Configuration:Marketing`,
  `RateLimiting`, `ServiceHealth:DatabaseRetryInterval`, `Authentication:RecomputeJob:Key` and
  the registration-time secrets need a restart; binding them with `IOptionsMonitor` would let an
  App Configuration change apply live.
- **Defaults disagree between code and appsettings** for `EngagementOffcanvas:Enabled` (`false`
  vs `true`), `SessionDismissalTimeout` (`60` vs `15`) and `PremiumBenefits:MoreText`
  (`..and more!` vs `...and more!`).
- **Stale keys.** `/Admin/Diagnostics` still shows `Configuration:Registration:CaptchaEnabled`,
  which nothing else reads (the `Captcha` flag replaced it), and `appsettings.Development.json`
  sets `AppConfig:Endpoint`, which Development never uses.
- **GTM and GA4 IDs are hard-coded** in three views rather than configured.

## History

- 2024-02 (PR 462): Microsoft.FeatureManagement flags introduced.
- 2024-03 (PR 470): `SpiderManager` bot lists moved to `Configuration:BotFilter`.
- 2025-06 (#10): Most global settings converted to feature flags; `CustomerReminder` added in #13.
- 2025-12 (#95): Resilient registration: missing secrets mark a service unavailable instead of
  failing startup; `ServiceHealth:AdminNotifications` added.
- 2026-02 (#112): `ClientSideUsageLogging`, the `UsageTracking` section and the first
  `RateLimiting` keys.
- 2026-03 (#123): global rate limits and `CaptchaThresholdPercent`. (#130): `EngagementOffcanvas`
  section.
- 2026-06 (#180): `ServiceHealth:DatabaseRetryInterval`.
- 2026-07 (#212): `SongPropertyCompression` flag.
- 2026-08 (#246): `AZURE_*` service-principal credential readable from configuration (user
  secrets). (#250): `m4d.Sandbox` and its `appsettings.json`.
- 2026-09 (#254): `PublicApi` flag. (#278): `ArtistIndex` flag. (#280): feature flags selected by
  label (`LabelFilter.Null` + environment), fixing flags silently falling back to appsettings.
- 2026-10-01: This reference created.

## Related

- [hosting-and-identity](hosting-and-identity.md): App Configuration, Key Vault, identity, deployment app settings
- [service-resilience](service-resilience.md): what happens when a configured service is missing or down
- [search-index-versioning](../search/search-index-versioning.md): `SEARCHINDEX` / `SEARCHINDEXVERSION`
- [contributor-test-environments](../dev-testing/contributor-test-environments.md): sandbox configuration
- [runbooks/configure-failure-email](../runbooks/configure-failure-email.md)
- [runbooks/provision-app-service](../runbooks/provision-app-service.md)
