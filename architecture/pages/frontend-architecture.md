# Frontend Architecture

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4d/ClientApp/vite.config.ts`, `m4d/ClientApp/src/pages/`, `m4d/Controllers/DMController.cs` (`Vue3()`),
`m4d/Views/Shared/Vue3.cshtml`, `m4d/Views/Shared/_vue-Layout.cshtml`, `m4d/Views/Shared/_head.cshtml`,
`m4d/ClientApp/src/components/PageFrame.vue`, `m4d/ClientApp/src/helpers/GetMenuContext.ts`

music4dance is a multi-page application (MPA). Routing, authorization and data loading stay in
ASP.NET Core MVC controllers; each page is then rendered by its own small Vue 3 app, built by
Vite as a separate entry point. There's no client-side router and no SPA shell. This doc follows
one request from the controller to the mounted Vue app, describes the shared client code, and
ends with how to add a page.

For running the client locally (`yarn build` vs. the `yarn dev` hot-reload server), see
[contributor-setup](../dev-testing/contributor-setup.md). For client test patterns, see
[testing-patterns](../dev-testing/testing-patterns.md).

## Request flow at a glance

```
GET /home/tempi
  HomeController.Tempi(...)
    -> Vue3("Tempos", ..., "tempo-list", model, danceEnvironment: true)   (DMController)
       sets UseVue = V3, fills ViewData["DanceDatabase"], returns View("Vue3", VueModel)
  Views/_ViewStart.cshtml        UseVue == V3  -> Layout = _vue-Layout.cshtml
  Views/Shared/Vue3.cshtml       <div id="app">, var model_ = {...},
                                 <script type="module" vite-src="/src/pages/tempo-list/main.ts">
  Views/Shared/_vue-Layout.cshtml
    <head>  _head.cshtml          var menuContext = {...}
    <body>  _environmentWriter    var danceDatabaseJson = {...}, var tagDatabaseJson = [...]
  Browser loads the tempo-list bundle
    generated main.ts             createApp(BApp > App.vue).mount('#app')
    App.vue                       reads model_ / safeDanceDatabase(), renders <PageFrame>
```

## Server side

### The `Vue3()` helper

`DanceMusicController` (`m4d/Controllers/DMController.cs`) is the base class for the site's MVC
controllers. Its protected `Vue3()` method is how almost every page action returns a Vue page:

```csharp
protected ActionResult Vue3(string title, string description, string name,
    object model = null, string helpPage = null,
    bool danceEnvironment = false, bool tagEnvironment = false,
    string script = null, bool preserveCase = false)
```

| Parameter | Effect |
| --- | --- |
| `title`, `description` | Become `ViewData["Title"]` / `ViewData["Description"]`. `_head.cshtml` renders `<title>` (appending " - Music4Dance: Shall we dance...to music?" unless the title starts with `music4dance:`) and `<meta name="description">`. |
| `name` | The page folder under `m4d/ClientApp/src/pages/`. `Vue3.cshtml` turns it into the entry `/src/pages/{name}/main.ts`. |
| `model` | Serialized into the global `model_` (see below). A `string` model has its single quotes escaped and is emitted as a quoted JS string; anything else is emitted as a JSON object literal. |
| `helpPage` | Sets `HelpPage`, which `View()` copies into `ViewData["Help"]`. `_head.cshtml` turns it into `menuContext.helpLink` (`https://music4dance.blog/music4dance-help/{helpPage}/`). |
| `danceEnvironment`, `tagEnvironment` | Call `BuildEnvironment3()` to put the dance and/or tag database into `ViewData` (see [Dance and tag databases](#dance-and-tag-databases)). |
| `script` | Name of a Razor partial rendered before `<div id="app">`. Only the home page uses it (`_fbLike`). |
| `preserveCase` | Serialize `model` with its C# property casing instead of camelCase. |

`Vue3()` also sets the controller's `UseVue` property to `UseVue.V3`, and the overridden `View()`
copies it into `ViewData["UseVue"]`. `Views/_ViewStart.cshtml` uses that to pick the layout:
`_vue-Layout.cshtml` for Vue pages, `_bs5-Layout.cshtml` for everything else. Controllers that
are mostly Vue (`SongController`, `DanceController`, `CustomSearchController`) set
`UseVue = UseVue.V3` up front and set it back to `UseVue.No` in the actions that return a plain
Razor view. `ReturnError()` always uses `UseVue.No`.

A controller doesn't have to name the page with a literal. Song search results use
`SongFilter.VueName` (`m4dModels/SongFilter.cs`), which returns `new-music` for the
`newmusic` action and `song-index` for everything else.

### `Vue3.cshtml` and the Vite tag helpers

`m4d/Views/Shared/Vue3.cshtml` is the single host view for every Vue page. It:

- computes `entry = "/src/pages/{Name}/main.ts"`;
- emits `<link rel="stylesheet" vite-href="@entry">` in the `Styles` section and
  `<script type="module" vite-src="@entry">` in the `Scripts` section;
- renders the model through the `_jsonCamelCase` partial as `var model_ = ...;`;
- if the action put `ViewData["SearchRequestDiagnostics"]` (song search, diagnostics role only),
  renders it the same way as `var searchRequestDiagnostics_ = ...;`;
- renders the empty `<div id="app"></div>` that Vue mounts into.

`vite-href` / `vite-src` come from the `Vite.AspNetCore` package (registered with
`services.AddViteServices()` in `M4dApplicationExtensions.AddM4dApplication`, tag helpers imported in
`Views/_ViewImports.cshtml`). They resolve the entry through `IViteManifest`, which reads
`wwwroot/vclient/.vite/manifest.json` from the last `yarn build`. When `ASPNETCORE_VITE=true`
(`ConfigurationExtensions.UseVite()`, set by the `*-vite` launch profiles) and the environment is
Development, the pipeline calls `app.UseViteDevelopmentServer(true)` and the tags point at the
Vite dev server instead. The relevant config lives under `Vite` in `appsettings.json` (`Base:
vclient`, `PackageManager: yarn`, `PackageDirectory: ClientApp`, `IgnoreMissingAssets: true`)
and `appsettings.Development.json` (`Server:Port: 7237`, `Server:Https: true`, which
`vite.config.ts` also imports so both sides agree on the port).

`AddM4dApplication` creates `wwwroot/vclient` at startup if it's missing, because `IViteManifest`
throws on a missing directory. With `IgnoreMissingAssets`, a page with no build output still
renders, but the script tag is skipped and Vue never mounts, so the page is blank.

### Layouts

`_vue-Layout.cshtml` is deliberately thin: outside dev-server mode it links the shared
`style.css` manifest entry, then the page's `Styles` section, `_head.cshtml`, the Google Tag
Manager `<noscript>` (behind `FeatureFlags.GoogleTagManager`), the view body,
`_environmentWriter.cshtml`, and the page's `Scripts` section. All visible chrome (menu, footer,
breadcrumbs) comes from `PageFrame` in the Vue app.

`_bs5-Layout.cshtml` serves the remaining Razor views (Identity area, admin forms, error pages).
It renders its own breadcrumbs and footer in Razor, but it also mounts the `header` page
(`src/pages/header/App.vue`, a `MainMenu` plus `ServiceStatusBanner`) into its own
`<div id="app">`, so Razor pages get the same Vue navigation menu as Vue pages.

### `menuContext`: per-request user and site state

`_head.cshtml` is included by both layouts and writes one global, `var menuContext = {...}`. It
carries what every page needs and nothing page-specific:

- user: `userName`, `userId` (empty unless the user's privacy setting allows it), `roles`,
  `level`, `hitCount`, `started`, `expiration`, `customerReminder`;
- site: `helpLink`, `indexId` (search index abbreviation), `isTestDb`, `isProdDb`,
  `marketingMessage`, `artistIndex` (feature flag), `googleAdsActive`;
- `xsrfToken`: the antiforgery request token, used for all client POSTs;
- service health: `searchHealthy`, `databaseHealthy`, `configurationHealthy` (see
  [service-resilience](../infrastructure/service-resilience.md#client-status-banner));
- `useClientSideTracking` and `usageTracking` (see [usage-tracking](../observability/usage-tracking.md));
- `engagementConfig`, emitted only when the engagement prompts aren't suppressed (see
  [visitor-engagement-monetization](visitor-engagement-monetization.md)).

Because `menuContext` embeds the antiforgery token, HTML caching interacts with it: anonymous
pages are browser-cacheable for five minutes, and a replayed page can carry a token whose cookie
is gone. See the cache-control notes in
[hosting-and-identity](../infrastructure/hosting-and-identity.md#request-pipeline-notes) and
[usage-tracking](../observability/usage-tracking.md).

### Dance and tag databases

Pages that need the dance catalog or the tag list ask for them with `danceEnvironment` /
`tagEnvironment`. `BuildEnvironment3()` then sets:

- `ViewData["DanceDatabase"]`: a JSON object `{ dances, groups, metrics }` built from
  `wwwroot/content/dances.json`, `wwwroot/content/dancegroups.json` and
  `DanceStats.GetMetrics()`;
- `ViewData["TagDatabase"]`: `DanceStats.GetJsonTagDatabse()`.

Both strings are cached in static fields on `DanceMusicController`, but only when `DanceStats`
was available (so a degraded start retries on the next request). `AdminController.ClearSongCache`
calls `DanceMusicController.ClearJsonCache()` to drop them. `_environmentWriter.cshtml` emits
whichever is present as `var danceDatabaseJson = ...` and `var tagDatabaseJson = ...`.

## Client side

### One Vite entry per page

`m4d/ClientApp/vite.config.ts` contains a local plugin, `AutoEndpoints`, that does the
per-page wiring:

- In its `config` hook it globs `src/pages/*/App.vue` and adds one Rollup input per folder,
  keyed by folder name, pointing at `src/pages/{name}/main.ts`.
- Those `main.ts` files **don't exist on disk**. The plugin's `resolveId` / `load` hooks generate
  each one from a template: import `@/scss/styles.scss` and the bootstrap-vue-next CSS, wrap the
  page's `App.vue` in bootstrap-vue-next's `BApp`, `createApp(...)`, and `app.mount('#app')`.
- Per-page Vue plugins are configured in the plugin's argument. Today only `dance-details` has
  one (`VueShowdownPlugin` from `vue-showdown`, flavor `vanilla`).

So a folder under `src/pages/` with an `App.vue` is a page entry, with no other registration.
There are 34 today, one of which (`header`) is the menu-only app used by `_bs5-Layout.cshtml`.

Other build settings that matter:

| Setting | Value | Why |
| --- | --- | --- |
| `base` | `/vclient` | Matches `Vite:Base`; assets are served from `wwwroot/vclient`. |
| `build.outDir` | `../wwwroot/vclient` | Output goes straight into the ASP.NET web root. |
| `build.manifest` | `true` | `IViteManifest` needs it to map entries to hashed files. |
| `build.cssCodeSplit` | `false` | All CSS goes into one stylesheet that both layouts link. |
| `manualChunks` | `bsvn`, `showdown`, `vue` | Splits the big `node_modules` dependencies into shared chunks the browser can cache across pages. |
| `resolve.alias` | `@` → `./src` | Used by every import in the client. |

`unplugin-vue-components` (`Components(...)`) auto-registers every `.vue` file under any
`components` folder (`src/**/components/**/*.vue`), plus bootstrap-vue-next components and
`unplugin-icons` icons, so pages use `<PageFrame>`, `<BButton>` or `<IBiCheckCircleFill />`
without importing them. The generated `components.d.ts` is gitignored. `vite-plugin-mkcert`
provides the HTTPS certificate for the dev server, and `vite-plugin-inspect` writes build
inspection output to `.vite-inspect`.

### Reading the server data

Page code reads the globals the Razor views wrote:

- **`model_`**: declared per page (`declare const model_: ...`). Most pages turn it into a typed
  model with `TypedJSON.parse(model_, SomeModel)`. The models in `src/models` are decorated with
  `typedjson`'s `@jsonObject` / `@jsonMember`. Some pages, such as `tempo-list`, use the object
  directly.
- **`menuContext`**: always read through `getMenuContext()` (`src/helpers/GetMenuContext.ts`),
  which wraps `window.menuContext` in a `MenuContext` instance (`src/models/MenuContext.ts`) once
  and caches it in a module-level singleton. `MenuContext` adds role helpers (`isAdmin`,
  `isPremium`, `canTag`, `canEdit`, `isBeta`, `hasRole()`), `isAuthenticated`,
  `daysToExpiration`, `getAccountLink()`, and `axiosXsrf`, an axios instance that sends the
  `RequestVerificationToken` header. `getAxiosXsrf()` is a shortcut for the same instance.
- **Dance database**: `safeDanceDatabase()` (`src/helpers/DanceEnvironmentManager.ts`) parses
  `window.danceDatabaseJson` into a `DanceDatabase` (`src/models/DanceDatabase/`) and caches it
  on `window.danceDatabase`. It throws if the page wasn't rendered with `danceEnvironment`.
- **Tag database**: `safeTagDatabase()` (`src/helpers/TagEnvironmentManager.ts`) does the same
  with `window.tagDatabaseJson` and `TagDatabase`.
- **`searchRequestDiagnostics_`**: only `song-index` reads it, and checks `typeof` first because
  it's usually absent.

Because these globals are fixed for the life of the page, they're read once at setup time. That's
safe for the globals themselves, but values derived from component *props* still need
`computed()`. See the MPA reactivity rule in `CLAUDE.md`.

### `PageFrame`

`src/components/PageFrame.vue` is the site chrome, and every page's `App.vue` wraps its template
in it (`<PageFrame id="app" :title="...">`). It renders:

- `MainMenu` with the `MenuContext`;
- `ServiceStatusBanner`, driven by `useServiceHealth()` (polling starts in `onMounted`);
- optional breadcrumbs (`breadcrumbs: BreadCrumbItem[]` prop, `src/models/BreadCrumbItem.ts`,
  which also exports shared trails such as `infoTrail`);
- the `title` as an `<h1>`, then the page content inside `PageLoader`;
- the footer, whose Help link is `menuContext.helpLink`.

It also emits `loaded` from `onMounted`. Its `loaded` computed is hard-coded to `true` (see
[Future improvements](#future-improvements)).

### Shared code

| Folder | What's in it |
| --- | --- |
| `src/components/` | Cross-page components: chrome (`PageFrame`, `MainMenu`, `PageLoader`, `ServiceStatusBanner`), song tables and voting (`SongTable`, `DanceVote`, `TagListEditor`, …), modals, engagement UI. Page-only components live in `src/pages/{name}/components/`. |
| `src/models/` | Typed client models. Many mirror server view models (`SongListModel`, `SongDetailsModel`, `PlaylistModel`, …). Others are client implementations of server-side formats: `SongFilter`, `DanceQueryItem`, `Tag`, `TagList`, `SongProperty`. The filter/tag rules in `CLAUDE.md` apply to these. `DanceDatabase/` holds the dance catalog types. |
| `src/composables/` | `useServiceHealth`, `useUsageTracking`, `useEngagementOffcanvas`, `useUrlQuerySync` (two-way URL query sync, see [tempo-list-page](tempo-list-page.md#shareable-urls)), `useDropTarget` (see [drop-target-lookup](../songs/drop-target-lookup.md)), `useSongSelector`, `useTagButton`. |
| `src/helpers/` | Global-data accessors (`GetMenuContext`, `DanceEnvironmentManager`, `TagEnvironmentManager`, `DanceLoader`, `TagLoader`), small utilities (`StringHelpers`, `timeHelpers`, `LinkHelpers`, …), and test support (`TestPageSnapshot`, `TestHelpers`, `TestDatabase`, `LoadTestDances`). |
| `src/scss/` | `styles.scss`, imported by every generated entry. |

### Page tests

Each page folder has a `__tests__` folder whose test usually calls
`testPageSnapshot(App, model, menuContext)` (`src/helpers/TestPageSnapshot.ts`). It sets
`window.model_` and `window.menuContext` the way the Razor view would, mounts the app with
bootstrap-vue-next, stubs `MainMenu`, and snapshots the HTML. `setupTestEnvironment()` (`src/helpers/TestHelpers.ts`)
also fills `window.danceDatabaseJson` and `window.tagDatabaseJson` from the checked-in
`src/assets/content` JSON, so `safeDanceDatabase()` / `safeTagDatabase()` work in tests. See
[testing-patterns](../dev-testing/testing-patterns.md) for the rest.

## Adding a page

1. **Client.** Create `m4d/ClientApp/src/pages/{name}/App.vue` using `<script setup lang="ts">`,
   with the template wrapped in `<PageFrame id="app" :title="...">`. That's all the build needs;
   `AutoEndpoints` picks it up on the next `yarn build` or dev-server start (restart `yarn dev` so
   the new input is added).
2. **Model.** If the page needs server data, add a C# view model (usually under `m4d/ViewModels/`)
   and a matching TypeScript class or interface, either next to the page or in `src/models/` if
   it's shared. Declare `declare const model_: ...` in `App.vue` and parse it with `TypedJSON`
   if it's a class. Keep property names camelCase on the client (the default serializer
   camelCases) unless you pass `preserveCase: true`.
3. **Server.** In a controller derived from `DanceMusicController`, return
   `Vue3(title, description, "{name}", model, helpPage: ..., danceEnvironment: ..., tagEnvironment: ...)`.
   Ask for the dance or tag database only if the page or its components call `safeDanceDatabase()`
   / `safeTagDatabase()`; they add a sizable payload to the HTML.
4. **Calls back to the server.** Use `getAxiosXsrf()` (or `menuContext.axiosXsrf`) for POSTs to antiforgery-protected
   endpoints.
5. **Test.** Add `src/pages/{name}/__tests__/{name}.test.ts` with a `testPageSnapshot` test and,
   if needed, a sample model.
6. **Links.** Add the page to `MainMenu` or the site map if it should be discoverable. For the
   blog/help site map, see [blog-help-sitemap](blog-help-sitemap.md).

## Future improvements

- `PageFrame.vue` has `// INT-TODO: Rethink dance/tag environmnet loading`, and its `loaded`
  computed always returns `true`, so `PageLoader` never shows a loading state.
- `UseVue` still has a `V2` value, left over from removing Vue 2 (#8). Nothing sets it.
- The older `BuildEnvironment()` in `DMController.cs` writes `ViewData["DanceEnvironment"]`,
  which nothing reads. Its tag branch still works, because it uses the same `TagDatabase` key.
- `MenuContextInterface` declares `isLocal`, `isTest` and `isProduction`, but `_head.cshtml`
  never sets them. The admin "Production / Test / Local" buttons in `SongCore.vue` therefore always
  show. The Test button also points at `m4d-linux.azurewebsites.net`, not the current `m4d-test`
  app.
- The generated `main.ts` template imports `createBootstrap` but never calls it.
- Both layouts carry `INT-TODO`s: move the inline `.list-clean` styles to a stylesheet, and stop
  loading Bootstrap's JS from the jsDelivr CDN with no fallback.

## History

- 4a6d35ae (PR 421, 2023-11): first batch of pages converted to Vue 3 + Vite.
- 4a1c78b7 (PR 492, 2024-08): song-table pages moved to Vue 3 + bootstrap-vue-next.
- 6eaa97a1 (PR 501, 2024-08): component auto-registration (`unplugin-vue-components`).
- 76e228db (PR 541, 2025-02): Vite 6.
- #8 (2025-06): Vue 2 and its layout path removed; `Vue3()` is now the only Vue entry point.
- #130 (2026-03): `engagementConfig` added to `menuContext`.
- #184 (2026-06): `searchRequestDiagnostics_` global for song search diagnostics.
- #260 (2026-09): `wwwroot/vclient` created at startup so a checkout without a client build doesn't 500.
- 2026-10-01: this doc created.

## Related

- [contributor-setup](../dev-testing/contributor-setup.md): building the client, hot reload with `yarn dev`
- [testing-patterns](../dev-testing/testing-patterns.md): Vitest patterns, `MenuContext` in tests
- [service-resilience](../infrastructure/service-resilience.md): health flags in `menuContext`, status banner
- [usage-tracking](../observability/usage-tracking.md): client-side page-view tracking settings in `menuContext`
- [visitor-engagement-monetization](visitor-engagement-monetization.md): `engagementConfig`
- [admin-pages](../users-admin/admin-pages.md): admin pages built on `Vue3()`
- [tempo-list-page](tempo-list-page.md): a worked example of one page end to end
