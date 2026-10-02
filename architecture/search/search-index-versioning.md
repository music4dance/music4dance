# Search Index Versioning

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4dModels/SearchServiceInfo.cs` (`SearchServiceManager`), `m4dModels/SongIndex.cs`,
`m4dModels/SongIndexNext.cs`, `m4dModels/SongFilter.cs`, `m4dModels/SongFilterNext.cs`,
`m4d/Controllers/AdminController.cs` (`UpdateSearchIdx`, `SetSearchIdx`, `CloneIdx`), `m4d/appsettings.json`

How music4dance makes breaking changes to the Azure AI Search schema. A new schema is tested in
isolation, then production is cut over to a freshly built index with near-zero downtime. The
mechanism was introduced in [PR #27](https://github.com/music4dance/music4dance/pull/27). The
step-by-step procedure is
[runbooks/search-index-breaking-migration](../runbooks/search-index-breaking-migration.md).

## Current state (2026-10-01)

- **`CodeVersion = 3`.** Production and test run the v3 schema (`songs-prod-3`, `songs-test-3`).
  Deployments set `SEARCHINDEXVERSION=3`. Values below `CodeVersion` are clamped up.
- **`-4` entries exist** in `appsettings.json` for both environments, so `HasNextVersion` is true and
  `/Admin` offers the "Update → songs-*-4" link. No v4 schema has been defined in
  `SongIndexNext` yet.
- **The v2 → v3 cleanup is incomplete.** The `-2` entries are still in `appsettings.json`, and two
  `// TODOIDX:` shims remain (see [Known issues](#known-issues)).

## Concepts

### Code Version vs. Config Version

| Term                                                      | Meaning                                                                                                                                                                                                                                 |
| --------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Code Version** (`SearchServiceManager.CodeVersion`)     | Hard-coded integer in source; the index schema version this build was compiled against. Currently **3**.                                                                                                                                |
| **Config Version** (`SearchServiceManager.ConfigVersion`) | Runtime integer; normally equals `CodeVersion`. When set to `CodeVersion + 1`, the app uses the _next_ schema. Controlled by the `SEARCHINDEXVERSION` environment variable. Values below `CodeVersion` are clamped up to `CodeVersion`. |
| **Next Version** (`SearchServiceManager.NextVersion`)     | `true` when `ConfigVersion > CodeVersion` — i.e. the app is actively using the newer schema.                                                                                                                                            |
| **HasNextVersion**                                        | `true` when the current `SearchServiceInfo` has a configuration entry for `CodeVersion + 1` **and** `NextVersion` is false — i.e. a next-version index exists but is not yet live.                                                      |

### Index Naming Convention

Azure AI Search index names are stored in `appsettings.json` using sections whose key encodes the
_environment_ and the _version_:

```
SongIndexProd-N   →  songs-prod-N    (production, version N)
SongIndexTest-N   →  songs-test-N    (test/staging, version N)
SongIndexExperimental → songs-experimental  (freeform; auto-treated as next version)
```

`SearchServiceManager` discovers all sections whose `indexname` starts with `songs-` and groups them
by their base name (`SongIndexProd`, `SongIndexTest`, …) and version suffix.

### `SongFilter` Versioning

When `NextVersion` is `true`, `SearchServiceManager.GetSongFilter()` returns a `SongFilterNext`
instance instead of a plain `SongFilter`. The two classes share almost all logic; the subclass
overrides only what differs between schema versions (currently just `DanceQuery`).

Always create `SongFilter` objects through `SearchService.GetSongFilter()` — **never** with
`new SongFilter(...)` directly. This ensures the correct subclass is returned for the active schema
version.

### `SongIndex` and `SongIndexNext`

Similarly, `SongIndex.Create()` returns a `SongIndexNext` for experimental/next-version contexts.
`SongIndexNext` overrides `BuildIndex()` (and any other methods that differ) to emit the new schema.

**`SongIndexNext` must override `IsNext => true`.** Every index operation (`ResetIndex`,
`BuildIndex`, `GetSearchClient`, `GetVersionedName`) routes to the correct versioned index name via
`IsNext`. Without this override, all operations silently target the old index (`songs-prod-N`
instead of `songs-prod-N+1`).

```csharp
public override bool IsNext => true;
```

**`dance_ALL/Tempo` is intentionally omitted.** The `dance_ALL` pseudo-field carries aggregate vote
data used for sort-by-popularity. There is no need for a `Tempo` sub-field there because
non-single-dance queries (including the "all dances" case) fall back to the top-level song `Tempo`
field. Populating `dance_ALL/Tempo` would just duplicate `song.Tempo` with no consumer.

### `// TODOIDX:` markers

Code that must be **removed** once a migration completes is tagged `// TODOIDX:`, so the
compatibility shims are easy to find and delete after cutover.

## Local development profiles

Launch profiles in `m4d/Properties/launchSettings.json`:

| Profile | `SEARCHINDEX` | `SEARCHINDEXVERSION` | When to use |
| --- | --- | --- | --- |
| `m4d-vite` (and `m4d-build`, `m4d-vite-no-compression`, `m4d-spotify`) | `SongIndexTest` | unset, so `CodeVersion` | Normal development against the current test index |
| `m4d-experimental` | `SongIndexExperimental` | auto +1 | Freeform experiments; no migration needed |
| `m4d-next` | `SongIndexTest` | `3` | Meant to be `CodeVersion + 1`. **Stale:** must be bumped to the next version before it selects the next schema |
| `m4d-prod-db` | `SongIndexProd` | unset | Reproduce a production bug against the production index |
| `m4d-test-db` | `SongIndexTest` | unset | Integration testing against the test index |

## Testing breaking changes

Use `DanceMusicTester` in the server tests. The `ISearchServiceManager` there is a mock; make it
return `SongFilter.Create(nextVersion: true, …)` and `NextVersion = true` to exercise next-version
behavior:

```csharp
mockSearchService
    .Setup(m => m.GetSongFilter(It.IsAny<string>()))
    .Returns<string>(s => SongFilter.Create(/* nextVersion */ true, s));
mockSearchService.Setup(m => m.NextVersion).Returns(true);
```

Integration tests that need a real Azure index use the `m4d-next` profile and the test index.

## Architecture Diagram

```
                        SEARCHINDEX env var
                               │
                    ┌──────────▼──────────┐
                    │ SearchServiceManager │
                    │  DefaultId           │  ConfigVersion == CodeVersion  → current schema
                    │  CodeVersion = N     │  ConfigVersion == CodeVersion+1 → next schema
                    │  ConfigVersion       │
                    └────────┬────────────┘
                             │
              ┌──────────────┼──────────────────┐
              │                                 │
    ┌─────────▼──────────┐          ┌───────────▼──────────┐
    │   SongIndex         │          │   SongIndexNext       │
    │  BuildIndex() vN    │          │  BuildIndex() vN+1      │
    │  IsNext = false     │          │  IsNext = true        │
    └─────────┬──────────┘          └───────────┬──────────┘
              │                                 │
    ┌─────────▼──────────┐          ┌───────────▼──────────┐
    │   SongFilter        │          │   SongFilterNext      │
    │  (OData filters vN) │          │  (OData filters vN+1)   │
    └────────────────────┘          └──────────────────────┘

Azure AI Search indices (appsettings.json, as of 2026-10-01):
  songs-prod-3, songs-test-3   ← current (CodeVersion 3)
  songs-prod-4, songs-test-4   ← next-version slots, configured
  songs-prod-2, songs-test-2   ← v2 leftovers; cleanup step not yet done
  songs-experimental           ← freeform (always IsNext = true)
```

## Known issues

- **Leftover `TODOIDX` shims** from the v2 → v3 migration:
  - `Song.TitleHashField` (`Song.cs`)
  - `SongIndex.DanceTagsInferred` (`SongIndex.cs`)

  Remove them from the schema and every reference.
- **`SongIndexProd-2` / `SongIndexTest-2` are still configured**, and the old indexes may still
  exist in Azure AI Search.
- **The `m4d-next` profile's `SEARCHINDEXVERSION` is stale** (`3`, which equals `CodeVersion`).

## History

- PR #27: Versioning mechanism introduced.
- 2026: v2 → v3 migration (per-dance tempo: `dance_{id}/Tempo` fields), cut over with
  `UpdateSearchIdx`. During that cutover `CodeVersion` stayed at 2 and was bumped afterwards; the
  runbook now reflects that order.
- 2026-08: `-4` index entries added to `appsettings.json`.
- 2026-10-01: The production migration steps moved to a runbook. The old step order, which bumped
  `CodeVersion` *before* cutover, was corrected.

## Related

- [runbooks/search-index-breaking-migration](../runbooks/search-index-breaking-migration.md)
- [index-backup-streaming](index-backup-streaming.md): how `UpdateSearchIdx` copies every song
- [song-filter](song-filter.md): `SongFilter` / `SongFilterNext` and OData generation
- [song-internal-format](../songs/song-internal-format.md): index storage compression
