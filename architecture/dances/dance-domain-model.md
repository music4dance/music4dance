# Dance Domain Model

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `DanceLib/` (`Dances.cs`, `DanceType.cs`, `DanceInstance.cs`, `TempoRange.cs`, `Meter.cs`),
`m4d/ClientApp/src/assets/content/dances.json`, `dancegroups.json`,
`m4dModels/DanceStatsManager.cs`, `m4dModels/DanceStatsInstance.cs`, `m4dModels/DanceStatsFileManager.cs`,
`m4d/Services/DanceStatsHostedService.cs`, `m4d/ClientApp/src/models/DanceDatabase/`

What a "dance" is in music4dance: the static dance library (types, styles, groups, tempo ranges,
organizations) defined in two JSON files, the C# and TypeScript object models built from them,
and `DanceStats`, the per-dance statistics layer computed from the song index and cached on disk.
For adding a dance, see [runbooks/add-a-dance](../runbooks/add-a-dance.md). For the cold-start
snapshot, see [runbooks/refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md).

## Source data

Two hand-maintained files under `m4d/ClientApp/src/assets/content/` are the single source of
truth for the dance library:

| File | Shape |
| --- | --- |
| `dances.json` | Array of dance types: `id` (three uppercase letters), `name`, `meter`, optional `blogTag`, `synonyms`, `searchonyms`, `validation`, and one or more `instances` |
| `dancegroups.json` | Array of groups: `name`, `id` (three letters, its own namespace alongside dance IDs), optional `blogTag`, `danceIds` |

There are 50 dance types and 8 groups today (Swing, Tango, Waltz, Foxtrot, Latin, Other,
Performance, Country). A dance can be in more than one group; `TGV` (Tango Vals) is in both Tango
and Waltz.

The `m4d.csproj` `assets` target copies everything under `ClientApp/src/assets/` into `wwwroot/`
on every build, so the server reads `wwwroot/content/dances.json`. Never edit the `wwwroot` copy.
The client gets the same files in two ways: injected by the server at page render (see
[Client loading](#client-loading)), and imported directly by client tests
(`src/helpers/LoadTestDances.ts`).

The same folder also holds `NDCA.csv`, `DanceSport.csv`, `UCWDC.csv`, `WORLDCDF.csv` and
`ACDA.csv`. They're reference tables of each organization's published tempos. No code reads
them; the tempos that matter are copied into `dances.json` by hand.

### Instances, styles and organizations

Each entry in `instances` is one way of dancing the dance, keyed by `style`. The styles in use
are `International Standard`, `International Latin`, `American Smooth`, `American Rhythm`,
`Country`, `Social` and `Performance`. An instance has:

- `tempoRange` (`min`/`max`): the general range for that style.
- `organizations`: the bodies that sanction it. In use: `DanceSport`, `NDCA` (ballroom), and
  `UCWDC`, `WORLDCDF`, `ACDA` (country western).
- `exceptions`: per-organization overrides, each an `organization` plus its own `tempoRange`.
  For example, International Standard Slow Waltz is 84-90 generally but exactly 87 for NDCA.
- `competitionGroup` (`Ballroom` or `Country`) and `competitionOrder` (1-5 for a dance in the
  round; 0 or omitted for an extra). Only competition instances set these.

The instance ID is the dance ID plus the first letter of the style's first word (`SWZI`,
`SWZA`, `SWZC`). That means a dance can't have two instances whose styles start with the same
letter.

### Tempo units

`tempoRange` values in `dances.json` are **beats per minute**. Slow Waltz is 84-90, which is
28-30 measures per minute in 3/4. The UI shows MPM by dividing by the meter's numerator
(`TempoRange.mpm(numerator)` in TypeScript), and song tempos (`Song.Tempo`, per-dance
`DanceRating.Tempo`) are BPM too. The `TempoRange` class comment in `DanceLib/TempoRange.cs` says
MPM. That comment is out of date.

Dances with no fixed meter or tempo (Performance and Pattern dances) use `meter: 1/1` and a
`1-500` range. The client treats any range with `min < 10` and `max > 499` as "any tempo"
(`TempoRange.isInfinite`).

### Validation rules

13 dance types carry a `validation` block (`doubleTempoIfBelow`, `halveTempoIfAbove`,
`flagInvalidMeters`) used to correct half/double-time tempos on imported songs. It belongs to the
dance type, not an instance. See [tempo-validation-rules](../songs/tempo-validation-rules.md).

## Server model: `DanceLib`

`DanceLib/` (assembly `DanceLibrary`) has no dependencies on the rest of the app. It's
deserialized with Newtonsoft.Json, using `[JsonConstructor]` constructors.

| Class | Role |
| --- | --- |
| `Dances` | The library. `Dances.Load(typesJson, groupsJson)` builds one, and `Dances.Reset(instance)` swaps the process-wide `Dances.Instance` under a lock. Exposes `AllDanceTypes`, `AllDanceInstances`, `AllDanceGroups`, `AllDances` (all three kinds), `NonPerformanceDanceTypes`, `DanceFromId`, `DanceFromName`, `FromIds`, `FromNames`, `ExpandGroups`, and `GetAllDanceWordsUpper()` (names, synonyms and searchonyms, used by remix detection in `Song.cs`) |
| `DanceObject` | Abstract base: `Id`, `Name`, `Meter`, `TempoRange`, `BlogTag`, `Synonyms`, `Searchonyms`, `CleanName` / `SeoFriendly` (the kebab-case URL slug) |
| `DanceType` | A dance. `TempoRange` is the union of its instances' ranges; `Organizations` is the union of its instances' organizations, or `["Unaffiliated"]` when none; `Groups` is filled in from `dancegroups.json`; `Validation` holds the tempo rules |
| `DanceInstance` | One style of a dance. `Id` and `Name` are derived (`"International Slow Waltz"`); `ReduceExceptions(orgs)` returns a copy whose range is the union of the matching organizations' exceptions |
| `DanceException` | Per-organization tempo override |
| `DanceGroup` | Group of dance objects (`DanceIds` resolved to `Members`). Its `TempoRange` is the union of its members' ranges, and its `Meter` is the first member's |
| `CompetitionCategory` / `CompetitionGroup` | Built while the library loads from instances that set `competitionGroup`: one category per style (`Round` ordered by `competitionOrder`, plus `Extras`). They're held in static dictionaries, which `Dances.Load` clears and refills |
| `Meter`, `Tempo`, `TempoType`, `TempoRange` | Immutable value types. `Tempo` parses strings like `"120"`, `"120 BPM"`, `"30 MPM 3/4"` and converts between BPM, MPM and BPS; `TempoRange` validates `0 < min <= max <= 1000` and provides `Include`, `Contains` and `CalculateDelta(Percent)` |
| `DanceFilter`, `DanceOrder` | Style / organization / group / meter filtering and tempo-distance ranking (`Dances.FilterDances`). On the server these are only exercised by `DanceTests`; the live pages use the TypeScript equivalents |
| `SongDuration`, `DurationType` | Duration parsing used by song import |

`Dances.Instance` is a static singleton. Most code reaches the library through it. The DB entity
`m4dModels/Dance.cs` also has a `Dance.DanceLibrary` static property, used by
`api/dances` (`DancesController`), that captures `Dances.Instance` once, when the `Dance` type is
first used.

## Database: the `Dance` table

The JSON defines what dances exist. The `Dances` table (`m4dModels/Dance.cs`, `DanceCore`) only
stores editable content for each ID: `Description`, `Modified`, and `DanceLinks` (external
links for the dance). Editors with the `dbAdmin` role update it through
`PATCH api/dances/{id}` (`DancesController.Patch` → `DanceMusicCoreService.EditDance`), which
then calls `DanceStatsManager.ReloadDances` to copy the new text into the in-memory stats.

## `DanceStats`: per-dance statistics

`DanceStatsInstance` (`m4dModels/DanceStatsInstance.cs`) combines the library with data from
the song index and database:

- `Dances` and `Groups`: one `DanceStats` each, with `SongCount`, `MaxWeight` (the top song's
  vote weight), `SongTags` / `DanceTags` (`TagSummary` facets), `SongIds` of the top 10 songs,
  plus `Description`, `DanceLinks` and `SpotifyPlaylist` mirrored from the database. Group
  stats are aggregated from their children's.
- `CachedSongs`: the top songs in serialized form, held in a `SongCache`. `SongIndex` pushes
  edited songs into it (`UpdateSong`), and `IndexUpdater` drains the queue (`DequeueSongs`) when
  it writes to Azure Search.
- `TagGroups`: the full tag vocabulary, through a `TagManager`. `DanceMusicCoreService.TagManager`
  reads it from here.
- `Map`: `DanceStats` by ID, built in `FixupStats`. `FromId` and `FromName` (by SEO slug) look
  dances up for controllers.

### Building it

`DanceStatsInstance.BuildInstance` uses `DanceBuilder` (or `DanceBuilderNext` against the next
index schema; see [search-index-versioning](../search/search-index-versioning.md)). It:

1. builds a `TagManager` from global tag facets,
2. gets per-dance song counts from the `DanceTags` facet,
3. creates a `DanceStats` for every group and type, copying description and links from the DB,
4. runs one search per dance (sorted by `Dances`, top 10, with that dance's tag facets) for top
   songs, `MaxWeight` and tag summaries.

`FixupStats` then builds `Map`, restores top songs from the cache, rolls up group stats, copies
`SpotifyFromSearch` playlist IDs onto dances by name, and creates a placeholder `Dance` row (and
index entry) for any dance that has no description yet. That last step is how a newly added
dance gets its DB row. If the database is down, it skips the DB steps and keeps the stats it
has.

### Caching and startup

`DanceStatsManager` (singleton `IDanceStatsManager`) owns the current instance.
`DanceStatsFileManager(appRoot)`, with `appRoot` = `wwwroot`, does the file I/O:

| Method | Reads / writes |
| --- | --- |
| `GetDances` / `GetGroups` | `wwwroot/content/dances.json`, `dancegroups.json` |
| `GetStats` | `wwwroot/AppData/dance-environment.json` (runtime cache), else `wwwroot/content/dance-environment-fallback.json` (checked-in snapshot), else `null` |
| `WriteStats` | `wwwroot/AppData/dance-environment.json` |

`DanceStatsHostedService` runs `DanceStatsManager.Initialize` at startup: it loads from the file
cache (`LoadFromAppData`) if there is one, and otherwise builds from the index and database
(`LoadFromAzure`). Both paths call `InitializeDanceLibrary`, which reloads `dances.json` and
`dancegroups.json` into `Dances.Instance`. If initialization throws, the service logs it, marks
`Database` unavailable, and the app starts in degraded mode. See
[service-resilience § Degraded behavior](../infrastructure/service-resilience.md#degraded-behavior).

`LoadFromAzure` always writes the result back to the runtime cache. It runs when:

- `ClearCache(dms, fromStore: true)` is called. Callers include `/Admin/ClearSongCache` (which
  also clears the controllers' JSON caches; see below), `GET api/recompute/songstats` (called by
  the Logic App every 6 hours; see
  [spotify-playlist-automation](../music-services/spotify-playlist-automation.md)), and several
  admin and bulk-load actions in `AdminController`, `SongController` and `DanceMusicService`.
- `ClearCache(dms, fromStore: false)` re-reads the file cache instead; `MergeManager` uses it.
- `/Admin/ReloadDances` only re-copies descriptions and links from the DB (`ReloadDances`). It
  doesn't re-read the JSON files or pick up new dances.

The sandbox host and tests swap in `LocalDanceStatsFileManager` (`m4dModels.Sandbox`), which
reads `test-dances.json`, `test-groups.json` and `dancestatistics.txt` from embedded resources
and never writes. See [contributor-test-environments](../dev-testing/contributor-test-environments.md).

## Client loading

Pages that need dances pass `danceEnvironment: true` to `Vue3(...)`. `DMController.BuildEnvironment3`
then builds a JSON object `{ dances, groups, metrics }`: the two content files verbatim, plus
`DanceStatsInstance.GetMetrics()` (song count and max weight per dance). It caches the string in
the static `s_danceDatabaseCache`, but only when stats are available, so a degraded start retries
on the next request. `_environmentWriter.cshtml` writes it into the page as
`window.danceDatabaseJson`, and `/Admin/ClearSongCache` clears it through
`DanceMusicController.ClearJsonCache()`.

On the client, `safeDanceDatabase()` (`src/helpers/DanceEnvironmentManager.ts`) parses it once
with TypedJSON into a `DanceDatabase` and memoizes it on `window.danceDatabase`. The TypeScript
classes in `src/models/DanceDatabase/` mirror the C# ones (`DanceType`, `DanceInstance`,
`DanceGroup`, `DanceException`, `TempoRange`, `Meter`, `DanceFilter`, `DanceOrder`) and add
client-only helpers:

- `DanceDatabase`: `danceFromId`, `instanceFromId`, `fromSynonym`, `metricsFromId` /
  `getSongCount`, `styles`, `organizations`, `filter` (with `DanceFilter`, used by the
  [Tempo List](../pages/tempo-list-page.md)), and `filterTempo` (used by the
  [Tempo Counter](../pages/tempo-counter-page.md)).
- `DanceType.filteredTempo(styles, organizations)` and `DanceInstance.filteredTempo`: the tempo
  range after applying organization exceptions (used by `CompetitionCategoryTable.vue`).
- `DanceType.styleFamilies` / `DanceInstance.styleFamily`: used by
  [dance family voting](../songs/dance-family-voting.md).
- `DanceType.validationRange`: the plausible range from the validation thresholds.

Two other endpoints expose dance data. `GET api/dances` returns the non-performance dance types,
and `GET api/danceenvironment` returns the `DanceEnvironment` shape (sparse stats plus groups).
No client code in `m4d/ClientApp/src` calls `api/danceenvironment`.

## Testing

- `DanceTests/` (project `DanceLibrary.Tests`) covers `Meter`, `Tempo`, `TempoRange`,
  `DanceFilter`, durations and validation against `TestData/test-dances.json` / `test-groups.json`.
  `ProductionDanceDataTests` loads the real `dances.json` and `dancegroups.json` to catch data
  errors.
- Client tests in `src/models/DanceDatabase/__tests__/` load the real content files plus
  `src/assets/content/metrics.json` through `LoadTestDances.ts`.

## Future improvements

- `DanceLib/TempoRange.cs` describes ranges as MPM, but the data is BPM. Its validation message
  says "less than 250" while the check allows up to 1000.
- `TempoRange.formatMPM` in TypeScript calls `bpm()` instead of `mpm()`. Nothing calls it today.
- `Dance.DanceLibrary` captures `Dances.Instance` once, so `api/dances` doesn't see a library
  reloaded by `/Admin/ClearSongCache`. It should read `Dances.Instance` directly.
- `Dance.Update` (the dance-list text loader) writes a changed link URL into `Description`
  instead of `Link`.
- `DMController.BuildEnvironment` sets `ViewData["DanceEnvironment"]`, which no view reads.
  `DanceEnvironmentController` has no client callers. Both could be removed.
- `DanceGroup` has a `Meter` only because `DanceObject` requires one (an `INT-TODO` in the code).
  `Dances.cs` has a `NEXTSTEPS` note about auditing the static singleton and making
  `FilterDances` return a new `Dances`.
- `DanceStatsInstance.FixupStats` double-counts group tag summaries (`TODO` in the code).
- `LoadFromAzure` ends with `UpdateAzureIndex(null, dms)`, commented as saving new tag types,
  but `UpdateAzureIndex` returns immediately when passed `null`.
- The organization CSVs in `assets/content/` aren't read by anything. Either generate exceptions
  from them or move them out of the shipped assets.

## History

- Azure DevOps PR 425 / PR 454: dance library cleanup and modernized C# dance filtering.
- Azure DevOps PR 455: TypeScript dance database updated to match.
- #59: Country western competition dances and organizations (`UCWDC`, `WORLDCDF`, `ACDA`).
- #67: `styleFamilies` / `styleFamily` on the TypeScript model, for dance family voting.
- #95: Service resilience. `DanceStatsHostedService` degrades instead of failing startup;
  `dance-environment-fallback.json` added as the cold-start snapshot.
- #109, #235, #242: `DanceValidation` tempo rules, moved to the dance type and extended (13
  dance types carry them today).
- #161: Pattern dance (`meter: 1/1`, open tempo range).
- #217: Tempo List fixes; `validationRange` on the TypeScript `DanceType` / `DanceInstance`.
- #232: `/Admin/ExportDanceFallback` for refreshing the snapshot.
- 2026-10-01: This doc created (coverage gap 3).

## Related

- [runbooks/add-a-dance](../runbooks/add-a-dance.md): adding a dance type or group
- [runbooks/refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md)
- [service-resilience](../infrastructure/service-resilience.md): degraded startup and file fallback
- [tempo-validation-rules](../songs/tempo-validation-rules.md): per-dance tempo correction
- [dance-family-voting](../songs/dance-family-voting.md): style families when voting
- [tempo-list-page](../pages/tempo-list-page.md) and [tempo-counter-page](../pages/tempo-counter-page.md):
  client-side filtering by style, organization and tempo
- [song-internal-format](../songs/song-internal-format.md): dance ratings and the
  `dance-environment.json` format
- [spotify-playlist-automation](../music-services/spotify-playlist-automation.md): the
  `songstats` recompute job
