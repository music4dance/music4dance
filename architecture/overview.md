# System Overview

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `music4dance.sln`, `m4d/Program.cs`, `m4d/Configuration/M4dApplicationExtensions.cs`,
`m4d/Controllers/DMController.cs`, `m4d/ClientApp/vite.config.ts`, `m4dModels/DanceMusicService.cs`

The map of the whole system: which projects exist, how a request is served, where data lives and
which external services the site depends on. Each section is deliberately short and links to the
doc that covers the area in depth. Start here if you're new to the codebase.

## Projects

`music4dance.sln` holds these projects. Arrows are project references.

```text
DanceLib  <--  m4dModels  <--  m4d  <--  m4d.Sandbox
                   ^            ^             |
                   |            +-- SelfCrawler
             m4dModels.Sandbox  <-------------+
```

| Project | Folder | What it is |
| --- | --- | --- |
| `DanceLibrary` | `DanceLib/` | Pure domain library with no I/O: dances, dance groups, `Tempo`, `TempoRange`, `Meter`, competition categories, dance filtering and validation. |
| `m4dModels` | `m4dModels/` | Data and business logic: `Song` and its property log, `DanceMusicContext` (EF Core), `DanceMusicService`, `SongIndex` (Azure AI Search), `SongFilter`, music-service clients (`SpotifyService`, `ITunesService`, `AmazonService`), `DanceStatsManager`. |
| `m4d` | `m4d/` | The ASP.NET Core web app: MVC controllers (`Controllers/`), JSON API controllers (`APIControllers/`), Razor views, ASP.NET Identity UI (`Areas/Identity/`), middleware, hosted services, and the Vue client in `m4d/ClientApp/`. |
| `m4dModels.Sandbox` | `m4dModels.Sandbox/` | In-memory stand-ins for external services (`SongIndexLocal`, `LocalSearchServiceManager`, `LocalDanceStatsFileManager`, `SandboxServiceFactory`) plus seed data in `TestData/`. Used by the sandbox host and by the test projects. |
| `m4d.Sandbox` | `m4d.Sandbox/` | A host that runs the real `m4d` app with no Azure, SQL Server or OAuth dependencies. See [contributor-test-environments](dev-testing/contributor-test-environments.md). |
| `DanceLibrary.Tests`, `m4dModels.Tests`, `m4d.Tests` | `DanceTests/`, `m4dModels.Tests/`, `m4d.Tests/` | MSTest suites. See [testing-patterns](dev-testing/testing-patterns.md). |
| `SelfCrawler` | `SelfCrawler/` | Manual-only Selenium crawler that checks the live site and writes the site-search page index. Never run in CI. |
| `architecture` | `architecture/` | Documentation-only project, so these docs show up in Visual Studio. It has no build output. |

Outside the solution:

- `m4d/ClientApp/`: Vue 3 + TypeScript client, built with Vite and Yarn.
- `e2e/`: Playwright tests that run against `m4d.Sandbox`. See
  [playwright-e2e-testing](dev-testing/playwright-e2e-testing.md).
- `scripts/`: maintenance scripts (blog sitemap updates, line-ending checks, upload preparation).
- `azure-pipelines.yml`: the deployment pipeline. See [hosting-and-identity](infrastructure/hosting-and-identity.md#deployment).

## Composition root

`m4d/Program.cs` only works out the SQL connection string (honoring `PROD_DB` / `TEST_DB` in
Development) and handles `SMOKE_TEST_MODE`. Everything else lives in two extension methods in
`m4d/Configuration/M4dApplicationExtensions.cs`:

- `AddM4dApplication` registers services: the shared Azure credential, App Configuration, one
  named `SearchClient` per configuration section that has an `indexname` key, `DanceMusicContext`,
  ASP.NET Identity, Google / Facebook / Spotify OAuth, email, reCAPTCHA, the public-API
  foundation, `ISearchServiceManager`, `IDanceStatsManager`, the security trackers,
  `ArtistIndexCache`, `IBackgroundTaskQueue` and the hosted services (`BackgroundQueueHostedService`,
  `DanceStatsHostedService`, `StartupInitializationService`).
- `UseM4dPipeline` maps the health endpoints, builds the middleware pipeline and routes, and runs
  EF migrations synchronously before the app starts serving.

`m4d.Sandbox` calls the same two methods with `M4dApplicationOptions { ConfigureDatabase = false,
ConfigureSearch = false }` and registers its in-memory replacements itself, so both hosts share one
startup path. External dependencies are registered so that a failure is recorded in
`ServiceHealthManager` instead of stopping startup (OAuth, email and reCAPTCHA through
`...WithResilience` wrappers; SQL, Search and App Configuration through fallbacks); see
[service-resilience](infrastructure/service-resilience.md). The startup order and the Azure
identity model are in [hosting-and-identity](infrastructure/hosting-and-identity.md).

## Request flow

### Middleware

In order, as `UseM4dPipeline` builds it:

1. `/health/startup` and `/health/ready`, mapped before any middleware.
2. Outside Development: an exception-logging wrapper, then `UseAzureAppConfiguration()` if App
   Configuration registered successfully.
3. The exception handler (`/Error`, `/Error/{0}`) and HSTS, or the developer exception page and
   the Vite dev server in Development.
4. HTTPS redirection, and a rewrite of `music4dance.net` to `www.music4dance.net`. Both are skipped
   when `DISABLE_HTTPS_REDIRECT` is set.
5. Static files: `MapStaticAssets()`, or `UseStaticFiles()` in self-contained deployments.
6. HTTP logging, then routing.
7. `Http4xxTrackingMiddleware`, which counts 4xx responses by URL. See
   [bot-and-abuse-defense](security/bot-and-abuse-defense.md).
8. `DatabaseRecoveryService.TriggerRecoveryIfNeeded()`.
9. Forwarded headers, then authentication.
10. `RateLimitingMiddleware`, for the Identity endpoints.
11. The cache-control middleware: anonymous HTML gets `public, max-age=300`, authenticated HTML
    gets `no-store`.
12. Authorization, then response caching.
13. A redirect of any path containing `/blog` to `https://music4dance.blog`.
14. Routes: the `dances/...` routes to `DanceController`, the default
    `{controller=Home}/{action=Index}/{id?}` route, and Razor Pages (the Identity area).

### Page requests (MVC + Vue)

Almost every page is a Vue app rendered into a thin Razor shell:

1. An MVC controller deriving from `DanceMusicController` (`m4d/Controllers/DMController.cs`)
   handles the request. Its `OnActionExecutionAsync` builds `UserMetadata` and, unless the
   `ClientSideUsageLogging` flag is on, records server-side usage
   ([usage-tracking](observability/usage-tracking.md)).
2. The action gathers a model and returns `Vue3(title, description, name, model, ...)`.
3. `Vue3()` renders `Views/Shared/Vue3.cshtml` inside `_vue-Layout.cshtml`. The shell writes:
   - `menuContext`: the user, roles, subscription level, service health, the antiforgery token
     and client feature settings (`_head.cshtml`);
   - `model_`, the page model serialized as camel-case JSON (`_jsonCamelCase.cshtml`);
   - `danceDatabaseJson` / `tagDatabaseJson` when the action asks for the dance or tag environment
     (`_environmentWriter.cshtml`). The dance environment combines `dances.json`,
     `dancegroups.json` and the `DanceStats` metrics, and is cached in a static field;
   - a `<script type="module">` for `/src/pages/{name}/main.ts`, resolved through the Vite manifest.
4. There are no `main.ts` files on disk. The `AutoEndpoints` plugin in `vite.config.ts` makes one
   Vite entry for each `src/pages/*/App.vue` and generates its `main.ts`, which mounts `App.vue`
   inside bootstrap-vue-next's `BApp` on `#app`.
5. The page's `App.vue` wraps its content in `PageFrame` (site navigation, footer, status banner)
   and reads the globals through helpers such as `getMenuContext()`.

A few admin and legacy pages are still plain Razor views. They use `_bs5-Layout.cshtml`, which
`_ViewStart.cshtml` picks when `UseVue` isn't `V3`. The Identity pages under
`m4d/Areas/Identity/Pages/` are Razor Pages.

### API requests

Controllers in `m4d/APIControllers/` are `[ApiController]`s routed at `api/[controller]` and
return JSON (Newtonsoft, default contract resolver). Vue pages call them for anything that happens
after the page loads: voting and editing songs, tag and dance lookups, service-track lookup,
playlists, usage-log batches and site search. Most derive from `DanceMusicApiController`
(`m4d/APIControllers/DMApiController.cs`). `RecomputeController` (`/api/recompute/{id}`) and two
MVC actions on `PlayListController` (`UpdateBatch`, `UpdateBatchStatus`) accept a shared token
instead of a user cookie (`TokenRequirement.Authorize`), so Azure Logic Apps can call them on a
schedule; see
[spotify-playlist-automation](music-services/spotify-playlist-automation.md).

### Background work

`IBackgroundTaskQueue` / `BackgroundQueueHostedService` run long admin operations off the request
thread. `DanceStatsHostedService` loads the dance statistics at startup, and
`StartupInitializationService` does the first App Configuration refresh after the app starts
accepting requests.

## Data stores

| Store | What lives there | Accessed through |
| --- | --- | --- |
| **Azure SQL** (`DanceMusicContext`, an `IdentityDbContext<ApplicationUser>`) | Users, roles, logins and claims (ASP.NET Identity); `Dances` and `DanceLinks` (descriptions and links); `TagGroups`; saved `Searches`; `PlayLists`; `ActivityLog`; `UsageLog`; OpenIddict tables for the public API. **No songs.** | EF Core. Migrations are in `m4dModels/Migrations/` and are applied at startup. |
| **Azure AI Search**, service `music4dance` | The song catalog. Each song is one document whose `Properties` field holds the full append-only property log ([song-internal-format](songs/song-internal-format.md)), the system of record for songs. There's one index per environment and schema version: `SongIndexProd-N`, `SongIndexTest-N`, `SongIndexExperimental`. | `SongIndex`, obtained from `DanceMusicService` / `ISearchServiceManager`. See [search-index-versioning](search/search-index-versioning.md). |
| **Azure AI Search**, service `m4d` | `PageIndex`, the site-page index behind site search. SelfCrawler writes it. | `api/search` (`APIControllers/SearchController.cs`). |
| **App content files** (`m4d/ClientApp/src/assets/content/`, copied to `wwwroot/content/` by the `assets` build target) | `dances.json`, `dancegroups.json`, `tags.json`, organization tempo CSVs, `blogmap.txt` / `helpmap.txt`, and the cold-start fallbacks `dance-environment-fallback.json` and `artist-index-fallback.json`. | `DanceStatsFileManager`, `ArtistIndexFileManager`, `IFileProvider`. |
| **Runtime cache files** (`wwwroot/AppData/`) | `dance-environment.json` (the computed dance stats) and the artist-index snapshot, so a restart doesn't need a full index pass. | `DanceStatsFileManager`, `ArtistIndexFileManager`. |
| **In memory** (per instance; production runs one instance) | `DanceStats`, `ArtistIndexCache`, the static dance/tag environment JSON caches, the security trackers, `ServiceHealthManager` state. | Singletons. |

Both App Service apps (production and test) share the SQL server and the search services, with
separate databases and indexes. See [hosting-and-identity](infrastructure/hosting-and-identity.md#environments).

## External services

| Service | Used for | Where |
| --- | --- | --- |
| Azure App Service (Linux) | Hosting, production `msc4dnc` and test `m4d-test` | [hosting-and-identity](infrastructure/hosting-and-identity.md) |
| Azure App Configuration + Key Vault | Settings, secrets and feature flags (Microsoft.FeatureManagement; names in `m4d/Utilities/FeatureFlags.cs`) | `AddM4dApplication`, `StartupInitializationService` |
| Azure SQL, Azure AI Search | Data stores, above | `DanceMusicContext`, `SongIndex` |
| Azure Communication Services | Outgoing email (account confirmation, password reset, admin failure alerts) | `m4d/Services/EmailSender.cs` |
| Azure Logic Apps | Scheduled calls to the token-authorized API endpoints | [spotify-playlist-automation](music-services/spotify-playlist-automation.md) |
| Spotify Web API | Track lookup, audio features (tempo), playlists; also an OAuth login provider | `SpotifyService`, `MusicServiceManager` ([music-service-api-calls](music-services/music-service-api-calls.md)) |
| Apple iTunes Search API | Track lookup and purchase links | `ITunesService` |
| Amazon | Search links only; there's no API call | `AmazonService` ([music-service-integration](music-services/music-service-integration.md)) |
| Google, Facebook | OAuth login providers | `m4d/Configuration/AuthenticationBuilderExtensions.cs` |
| Google reCAPTCHA v2 | CAPTCHA on Identity forms under attack | `AddReCaptchaWithResilience` ([bot-and-abuse-defense](security/bot-and-abuse-defense.md)) |
| Stripe | Premium subscriptions and donations, through Stripe Checkout | `PaymentController` (its base class `CommerceController` holds the commerce settings checks) |
| WordPress.com (`music4dance.blog`) | Blog and help articles; the site links to them through `blogmap.txt` / `helpmap.txt` and redirects `/blog` paths there | [blog-help-sitemap](pages/blog-help-sitemap.md) |
| Google Tag Manager / GA4, AdSense | Analytics and ads, injected in `_head.cshtml` | [runbooks/gtm-ga4-setup](runbooks/gtm-ga4-setup.md) |

The public API for third-party apps (DanzQ: OpenIddict, under `m4d/PublicApi/`) is registered but
stays off unless the `PublicApi` feature flag is on. See
[plans/public-api-authorization](plans/public-api-authorization.md).

## Where to go next

| If you're working on... | Read |
| --- | --- |
| Songs: storage format, editing, merging, voting | [songs/](README.md#songs-data-model-editing-voting) |
| Search, filters, the index | [search/](README.md#search) |
| Spotify / Apple / Amazon, playlists | [music-services/](README.md#music-services-and-playlists) |
| Accounts, admin pages, bulk edits | [users-admin/](README.md#users-and-admin) |
| Deploying, Azure resources, startup, outages | [infrastructure/](README.md#infrastructure-and-deployment) |
| Running the app locally, writing tests | [contributor-setup](dev-testing/contributor-setup.md), [testing-patterns](dev-testing/testing-patterns.md) |

## Future improvements

- `Program.cs` still carries a TODO about adding an EF design-time `DbContext` factory.
- `ForwardedHeadersOptions` doesn't restrict `KnownProxies` / `KnownNetworks` (noted in
  `UseM4dPipeline`).
- The `/blog` redirect matches `/blog` anywhere in the path, not just as a prefix.
- The remaining areas without a doc are listed in the README's
  [Coverage gaps](README.md#coverage-gaps).

## History

- 2023-11 (PR 421): First pages converted to Vue 3 / Vite, starting the one-entry-per-page client.
- 2026-08 (#250): Startup moved out of `Program.cs` into `M4dApplicationExtensions`, shared with
  the new `m4d.Sandbox` host.
- 2026-09 (#254): DanzQ public-API foundation added behind the `PublicApi` flag.
- 2026-09 (#267): Playwright e2e suite against `m4d.Sandbox`.
- 2026-10: This overview written.

## Related

- [hosting-and-identity](infrastructure/hosting-and-identity.md): environments, deployment, Azure identity, startup sequence
- [service-resilience](infrastructure/service-resilience.md): what happens when a dependency is down
- [contributor-test-environments](dev-testing/contributor-test-environments.md): the sandbox host and its in-memory services
- [song-internal-format](songs/song-internal-format.md): how a song is stored
- [music-service-integration](music-services/music-service-integration.md): the music-service registry
