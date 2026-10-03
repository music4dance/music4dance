# Data Layer

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-02
**Code:** `m4dModels/DanceMusicContext.cs`, `m4dModels/DanceMusicCoreService.cs`,
`m4dModels/DanceMusicService.cs`, `m4dModels/Migrations/`, `m4d/.config/dotnet-tools.json`,
`m4d/Configuration/M4dApplicationExtensions.cs`, `m4d/Controllers/AdminController.cs`

How music4dance stores data: what lives in Azure SQL and what lives in the Azure AI Search
index, the EF Core context and the service façade in front of both, how schema migrations are
made and applied, and the admin backup and restore tools.

## Two stores

There is **no song table**. Songs moved out of SQL into the search index in 2016 (the "DBKill"
commits), and the index is now the system of record for the catalog.

| Store | Holds | Accessed through |
| --- | --- | --- |
| Azure SQL (`DanceMusicContext`) | Users, roles and logins (ASP.NET Identity), OpenIddict clients and tokens, dance descriptions and links, tag groups, saved searches, playlists, the activity log, the usage log | `DanceMusicContext` DbSets, `UserManager<ApplicationUser>` |
| Azure AI Search (`SongIndex`) | Every song: its full `SongProperty` log, compressed, plus the denormalized search fields built from it | `DanceMusicCoreService.SongIndex` / `GetSongIndex(id)` |

Some things that look like data come from neither store:

- Dance definitions (names, meters, tempo ranges, organizations) come from
  `dances.json` through `DanceLibrary`. The `Dances` table only holds the editable `Description`,
  `Modified` and `DanceLinks`, and `Dance.Info` (ignored by EF) joins the two by `Id`.
- Dance and tag statistics (`DanceStatsInstance`) are computed from the index
  (`DanceStatsManager.LoadFromAzure`), with a file cache for cold starts. See
  [runbooks/refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md).

The song format is in [song-internal-format](../songs/song-internal-format.md); index schemas and
versions are in [search-index-versioning](../search/search-index-versioning.md).

## `DanceMusicContext`

`DanceMusicContext` (in `m4dModels`) derives from `IdentityDbContext<ApplicationUser>`, so the
`AspNet*` Identity tables come with it. `ApplicationUser` adds profile and commerce columns to
`AspNetUsers`: `Region`, `Privacy`, `CanContact`, `ServicePreference`, `SubscriptionLevel` /
`SubscriptionStart` / `SubscriptionEnd`, `LifetimePurchased`, `FailedCardAttempts`, `HitCount`
and others. See [account-management](../users-admin/account-management.md).

| DbSet | Table | Notes |
| --- | --- | --- |
| `Dances` | `Dances` | `Id` max length 5 (the dance ID); `Info` ignored |
| `DanceLinks` | `DanceLink` | `Id` is a client-generated `Guid` (`ValueGeneratedNever`); FK `DanceId` |
| `TagGroups` | `TagGroups` | Key is `Key` (`value:Category`); self-referencing `PrimaryId` for tag rings; `Count`, `Value`, `Category`, `Children` ignored |
| `Searches` | `Searches` | `Query` required, `Filter` ignored; FK to `ApplicationUser`. See [saved-searches](../search/saved-searches.md) |
| `PlayLists` | `PlayLists` | See [playlist-management](../music-services/playlist-management.md) |
| `ActivityLog` | `ActivityLog` | FK to `ApplicationUser` |
| `UsageLog` | `UsageLog` | Length limits on `UsageId`, `Page`, `Query`, `Filter`, `Referrer`, `UserAgent`; indexes on `UserName` and `UsageId`. See [usage-tracking](../observability/usage-tracking.md) |

`OnModelCreating` also calls `UseOpenIddict()`, which maps the four `OpenIddict*` tables used by
the public API foundation ([plans/public-api-authorization](../plans/public-api-authorization.md)).

Other members worth knowing:

- **`CreateTransientContext()`** builds a new, independent context from the same connection
  string (or, under `UseInMemoryDatabase`, the same in-memory store name). It throws for any other
  provider. Long-running admin and playlist work uses it through
  `DanceMusicCoreService.GetTransientService()`, so it doesn't share the request's scoped context.
  Reading the in-memory store name needs an internal EF type, so that one line carries a scoped
  `#pragma warning disable EF1001`.
- **`AutoDetectChangesEnabled`** wraps the change tracker's flag. `LoadSearches` turns it off for
  bulk imports.
- **`SaveChangesAsync`** traces the error and rethrows. It does nothing else.
- **`LoadDances()`** loads `Dances` with their `DanceLinks`.

### Registration and lifetime

`AddM4dApplication` registers the context with `AddDbContext` (scoped) when
`M4dApplicationOptions.ConfigureDatabase` is true, which it is everywhere except `m4d.Sandbox`:

- SQL Server, with `EnableRetryOnFailure` (5 retries, 30s max delay) and a 60s command timeout
  for Azure SQL cold starts.
- The connection string is **re-read from `IConfiguration` on every context resolution**, so a
  connection string changed while the app is running reaches new contexts. The precedence is in
  [hosting-and-identity § Identity and secrets](hosting-and-identity.md#identity-and-secrets).
- With no connection string, or if registration throws, it registers a placeholder SQL Server
  context so DI still resolves, and marks `Database` unavailable. See
  [service-resilience](service-resilience.md).

Identity uses `AddEntityFrameworkStores<DanceMusicContext>()`.

| Environment | Database |
| --- | --- |
| Local | LocalDB `m4d` (the `DanceMusicContextConnection` default in `appsettings.json`) |
| Test (`m4d-test`) | `music4dance_test` on Azure SQL server `n8a541qjnq` |
| Production (`msc4dnc`) | `music4dance` on the same server |

`m4d.Sandbox` and many server tests use `UseInMemoryDatabase` instead; see
[contributor-test-environments](../dev-testing/contributor-test-environments.md).

## `DanceMusicService`: the façade

Controllers don't use the context directly for most work. They get a `DanceMusicService` named
`Database`, which wraps the scoped context, the search service and the stats manager.
`DanceMusicService` is **not** registered in DI. `DanceMusicController` and
`DanceMusicApiController` construct one per request from their injected dependencies, and so do
`DanceStatsHostedService`, `DatabaseRecoveryService` and `m4d.Sandbox`.

Two classes split the work:

- **`DanceMusicCoreService`** (no `UserManager`): `Context` and DbSet pass-throughs,
  `SaveChanges()`, `SongIndex` / `GetSongIndex(id, isNext)`, `MergeManager`, dance editing
  (`EditDance`), tag rings (`GetTagRings`, `RenameTag`, `SetPrimaryTag`), playlists
  (`AddPlaylist`, `UpdatePlayList`), index copies (`CloneIndex`, `UpdateIndex`), cache clearing,
  a per-instance user cache (`FindUser`), and the role-name constants (`EditRole`, `DbaRole`,
  `DiagRole`, `PremiumRole`, …). `GetTransientService()` returns another `DanceMusicCoreService`
  on a transient context.
- **`DanceMusicService`** adds `UserManager<ApplicationUser>` and everything that needs it: the
  `Load*` / `Serialize*` methods behind backup and restore (below), `FindOrAddUser`,
  `ChangeUserName`, `AddPseudoUser` and `MergeUsers`.

Song reads and writes go through `SongIndex` (`FindSong`, `UpdateSong`, `SaveSong`,
`DeleteSong`, `AdminEditSong`, …), not through EF.

## Migrations

**Where they live.** Migrations are in `m4dModels/Migrations/`, next to the context, with the
snapshot in `DanceMusicContextModelSnapshot.cs`. No `MigrationsAssembly` override or design-time
factory exists; `m4d` is the startup project.

| Migration | Change |
| --- | --- |
| `20191123233600_CreateSchema` | Baseline: Identity tables, `Dances`, `DanceLink`, `PlayLists`, `TagGroups`, `Searches` |
| `20211128222327_ActivityLog` | `ActivityLog` table |
| `20211223033025_CardTracking` | `FailedCardAttempts`, `LifetimePurchased` on `AspNetUsers` |
| `20240224015103_UsageLog` | `UsageLog` table, `HitCount` on `AspNetUsers` |
| `20240311190320_UsageLogReferral` | `Referrer` on `UsageLog` |
| `20260326002120_SearchMostRecentPage` | `MostRecentPage` on `Searches` |
| `20260831134022_DanzQApiFoundation` | The four `OpenIddict*` tables |

**Tooling.** `dotnet-ef` is a local tool pinned in `m4d/.config/dotnet-tools.json`. There's no
manifest at the repo root, so run it from `m4d/`:

```sh
cd m4d
dotnet tool restore
dotnet ef migrations add <Name> --project ../m4dModels --startup-project .
dotnet ef database update --project ../m4dModels --startup-project .
```

The tool version is bumped with the NuGet packages (the `update-nuget` skill). The design-time
host prints a `HostAbortedException` dump that isn't an error; see
[contributor-setup § Create the database](../dev-testing/contributor-setup.md#create-the-database).

**How they're applied.** The app migrates its own database at startup, in every environment.
`UseM4dPipeline` runs `MigrateAsync()` **synchronously, before `app.Run()`**, so the schema exists
before any hosted service touches it:

- It uses a dedicated context **without** `EnableRetryOnFailure`, so the migrator can reach its
  create-database path when a local database has been deleted, instead of retrying the failed
  connection.
- It suppresses `PendingModelChangesWarning`.
- On success it marks `Database` healthy and, in Development, runs
  `UserManagerHelpers.SeedData` (roles, plus admin, test and editor users when their
  `M4D_*_USER` settings exist).
- On failure it marks `Database` unavailable and keeps starting.
- It's **skipped when `PROD_DB` or `TEST_DB` is in effect**, so a local session pointed at a
  shared database never changes that database's schema.

So test and production get new migrations when a build containing them is deployed and starts:
test against `music4dance_test`, production against `music4dance`. Nothing in
`azure-pipelines.yml` runs migrations.

**Guard rail.** `m4dModels.Tests/PublicApiSchemaTests.cs` asserts that the snapshot matches the
model (`HasPendingModelChanges()` is false) and that the OpenIddict migration generates reversible
SQL. A model change without a migration fails that test, even though startup would ignore it.

**Rolling back** is `dotnet ef database update <PreviousMigration>`, then removing the migration
and redeploying. The design-time host uses the same connection-string precedence as the app, so
check which database you're pointed at first.

## Backup and restore

### Azure SQL

The repo has no code or pipeline for SQL backups. Production relies on Azure SQL's own automated
backups; the retention settings aren't recorded here.

### Admin backup files

`/Admin/UploadBackup` (`dbAdmin`) is the admin page for these tools. Backups are tab-delimited
text, written to `wwwroot/AppData/` (`EnsureAppData`) and returned as a download. Sections start
with marker lines (`+++++DANCES+++++`, `+++++TAGSS+++++`, `+++++PLAYLISTS+++++`,
`+++++SEARCHES+++++`, `+++++SONGS+++++`), and the user section starts with its column header
(`UserId\tUserName\tRoles\t…`).

| Action | Role | What it does |
| --- | --- | --- |
| `BackupDatabase` | `showDiagnostics` | Writes `backup-YYYY-MM-DD.txt` with the user, dance, tag, playlist and search sections (`Serialize*`), each switchable by a query flag. **No songs**: the `songs` flag is ignored and the song section is commented out. |
| `IndexBackup` | `showDiagnostics` | Writes `index-YYYY-MM-DD.txt` from the song index, optionally filtered. This is the song backup. See [index-backup-streaming](../search/index-backup-streaming.md). |
| `BackupTail` / `BackupDelta` | `showDiagnostics` | Intended as recent-change and "everything except these song IDs" exports. Both are broken; see [Future improvements](#future-improvements). |
| `ExportCsv` | `showDiagnostics` | A CSV of songs that have samples and dance tags, through `PlaylistExport`. |
| `ReloadDatabase` (POST) | `dbAdmin` | Uploads a backup file and loads whichever sections it finds (below). |
| `LoadIdx` (POST) | `dbAdmin` | Uploads an index backup into a chosen index: reset and reload, or (`reset=false`) update in place. Reloads stats if it targeted the default index. |
| `CloneIdx` | `dbAdmin` | Streams the default index into another one (`CloneIndex`). |
| `RestoreDatabase` / `Reseed` | `dbAdmin` | Migrate to the latest schema and run `SeedData` / run `SeedData` only. Linked from `/Admin/InitializationTasks`. |

`ReloadDatabase` has three modes, picked by the submit button:

- **Reload**: if the file has user, dance and tag sections, it first empties the schema by
  migrating to `"0"` and back up (`RestoreDb(delete: true)`), then loads everything. Migrating
  down replaced `EnsureDeleted()`, which intermittently failed with a misleading login error on
  LocalDB. The schema wipe is skipped when `PROD_DB` or `TEST_DB` is set.
- **Update**: loads the SQL sections incrementally, and applies song lines through
  `UpdateSongs`, which creates, updates, merges or deletes songs in the index.
- **Admin**: like Update, but songs go through `SongIndex.AdminEditSong`.

`FixupUser`, `LoadUsage` and `LoadUsageFromAppData` are narrower loaders on the same page; usage
loading is covered in [usage-tracking](../observability/usage-tracking.md).

## Future improvements

- **`BackupTail` and `BackupDelta` are broken.** They call the async `SerializeSongs` without
  awaiting it, so the song section is the `Task`'s type name. `BackupTail` also writes tags where
  playlists belong (`SerializeTags` twice). `SerializeSongs` ignores its `from` and `exclusions`
  arguments. `BackupDelta`'s parameter is `file`, but the form posts `FileUpload`.
- **The "Backup tail" `IndexBackup` links** on `UploadBackup` pass `count` and `from`, which
  `IndexBackup` doesn't accept, so they produce full backups.
- **"Reload" mode doesn't load songs.** `LoadSongs` builds `Song` objects but never saves them
  to the index. Songs are restored with `LoadIdx` instead.
- **Unused parameters:** `BackupDatabase`'s `songs` and `useLookupHistory` parameters only
  affect the file name.
- **Connection-string precedence is computed twice**, in `Program.cs` and in the `AddDbContext`
  lambda, and only `Program.cs` limits `PROD_DB` / `TEST_DB` to Development.
- **Document SQL backup retention** (point-in-time restore window, long-term retention) for the
  production and test databases.

## History

- 2016-07: Songs moved out of SQL into the search index ("DBKill", c51febb0 through c59df95b).
- 2020-03: .NET Core conversion; current `DanceMusicContext` and migrations baseline (PR 14).
- 2020-03: `dotnet-ef` pinned as a local tool in `m4d/.config/dotnet-tools.json` (PR 23).
- 2021-11 / 2021-12: `ActivityLog` table (PR 243) and card-tracking columns (PR 253).
- 2024-02 / 2024-03: `UsageLog` table (PR 464) and `Referrer` column (PR 472).
- 2025-12: Database health tracking and startup migration failure handling (#95); placeholder
  context when no connection string is configured (#97).
- 2026-03: `MostRecentPage` on `Searches` (#142).
- 2026-05: Startup migration context made retry-free, with `PendingModelChangesWarning`
  suppressed (#172).
- 2026-07: `RestoreDb` empties the schema by migrating to `"0"` instead of `EnsureDeleted()` (#207).
- 2026-08: Startup composition moved into `M4dApplicationExtensions`, `ConfigureDatabase` option
  and `CreateTransientContext` in-memory support for `m4d.Sandbox` (#250).
- 2026-09: OpenIddict tables and schema tests (#254); EF1001 suppression scoped to one line (#295).
- 2026-10-01: This doc created. The stale "migrations run in a background `Task.Run`" note and
  the migration commands moved here from [saved-searches](../search/saved-searches.md).
- 2026-10-02, #326 (`af5513b8`): `RestoreDatabase` and `Reseed` require `dbAdmin` again (they had
  been `[AllowAnonymous]`); the no-op `UpdateDatabase` action removed.

## Related

- [hosting-and-identity](hosting-and-identity.md): environments, connection-string precedence, startup sequence
- [service-resilience](service-resilience.md): `Database` health, placeholder context, `DatabaseRecoveryService`
- [index-backup-streaming](../search/index-backup-streaming.md): the song-index side of backup and restore
- [search-index-versioning](../search/search-index-versioning.md): song index names and schema versions
- [song-internal-format](../songs/song-internal-format.md): the song line format in backups
- [contributor-setup](../dev-testing/contributor-setup.md): creating a local database
- [account-management](../users-admin/account-management.md): Identity, users, merges
