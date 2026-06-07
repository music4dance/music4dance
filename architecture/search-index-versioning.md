# Search Index Versioning and Breaking-Change Migration

## Overview

The search index versioning system allows breaking changes to the Azure AI Search schema to be tested
in isolation and then rolled out to production with near-zero downtime. It was introduced in
[PR #27](https://github.com/music4dance/music4dance/pull/27).

## Current Rollout Status (Per-Dance Tempo)

- The v3 schema and query behavior for per-dance tempo are implemented in code (`SongIndexNext` / filter paths).
- Production cutover is still an operational step: run `UpdateSearchIdx`, validate, then bump `CodeVersion`.
- Until that cutover is complete, `CodeVersion` remains `2` and next-version behavior is activated via `SEARCHINDEXVERSION`.

---

## Concepts

### Code Version vs. Config Version

| Term                                                      | Meaning                                                                                                                                                                            |
| --------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Code Version** (`SearchServiceManager.CodeVersion`)     | Hard-coded integer in source; the index schema version this build was compiled against. Currently **2**.                                                                           |
| **Config Version** (`SearchServiceManager.ConfigVersion`) | Runtime integer; normally equals `CodeVersion`. When set to `CodeVersion + 1`, the app uses the _next_ schema. Controlled by the `SEARCHINDEXVERSION` environment variable.        |
| **Next Version** (`SearchServiceManager.NextVersion`)     | `true` when `ConfigVersion > CodeVersion` — i.e. the app is actively using the newer schema.                                                                                       |
| **HasNextVersion**                                        | `true` when the current `SearchServiceInfo` has a configuration entry for `CodeVersion + 1` **and** `NextVersion` is false — i.e. a next-version index exists but is not yet live. |

### Index Naming Convention

Azure AI Search index names are stored in `appsettings.json` using sections whose key encodes the
_environment_ and the _version_:

```
SongIndexProd-2   →  songs-prod-2    (production, version 2)
SongIndexProd-3   →  songs-prod-3    (production, version 3 — next)
SongIndexTest-2   →  songs-test-2    (test/staging, version 2)
SongIndexTest-3   →  songs-test-3    (test/staging, version 3 — next)
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
`IsNext`. Without this override, all operations silently target the old index (`songs-prod-2`
instead of `songs-prod-3`).

```csharp
public override bool IsNext => true;
```

**`dance_ALL/Tempo` is intentionally omitted.** The `dance_ALL` pseudo-field carries aggregate vote
data used for sort-by-popularity. There is no need for a `Tempo` sub-field there because
non-single-dance queries (including the "all dances" case) fall back to the top-level song `Tempo`
field. Populating `dance_ALL/Tempo` would just duplicate `song.Tempo` with no consumer.

### `// TODOIDX:` Markers

Code that must be **removed** once the migration to the next index version is complete is tagged with
`// TODOIDX:` comments. These exist so that compatibility shims are easy to find and clean up after
cutting over production.

Current `TODOIDX` items (as of version 2 → 3 migration):

| File           | Symbol                    | Action                                            |
| -------------- | ------------------------- | ------------------------------------------------- |
| `SongIndex.cs` | `DanceTagsInferred` field | Remove field from index schema and all references |
| `Song.cs`      | `TitleHashField`          | Remove field from index schema and all references |
| `Song.cs`      | `FailedLookup` clean-up   | See inline comment                                |

---

## Local Development Profiles

Launch profiles in `m4d/Properties/launchSettings.json` cover the most common combinations:

| Profile            | `SEARCHINDEX`           | `SEARCHINDEXVERSION`    | When to use                                                                             |
| ------------------ | ----------------------- | ----------------------- | --------------------------------------------------------------------------------------- |
| `m4d-vite`         | `SongIndexTest`         | _(unset → CodeVersion)_ | Normal development against the current test index                                       |
| `m4d-experimental` | `SongIndexExperimental` | _(auto +1)_             | Quick freeform experiments — no migration needed                                        |
| `m4d-next`         | `SongIndexTest`         | `3` (CodeVersion+1)     | Test a _specific_ next-version schema against the test index before going to production |
| `m4d-prod-db`      | `SongIndexProd`         | _(unset)_               | Reproduce a production bug against the production index                                 |
| `m4d-test-db`      | `SongIndexTest`         | _(unset)_               | Integration testing against the test index                                              |

---

## Implementing a Breaking Change

### Step 1 — Define the new schema in `SongIndexNext`

Override `IsNext`, `BuildIndex()`, and any other methods in `SongIndexNext.cs`. **`IsNext => true`
is mandatory** — it controls which versioned index name is used by every operation in the base
class. Without it, `ResetIndex`, `UploadIndex`, and the search client all silently target the old
index.

```csharp
public override bool IsNext => true;
```

Override `BuildIndex()` to emit the new Azure AI Search index schema. The base
`SongIndex.BuildIndex()` continues to emit the old schema for backwards compatibility until the
migration is complete.

### Step 2 — Add any filter logic in `SongFilterNext`

If the new schema requires different OData filter expressions, override the relevant methods/
properties in `SongFilterNext.cs`. Keep `SongFilter` generating the old expressions so the old
index continues to work.

### Step 3 — Add compatibility shims with `TODOIDX` markers

If the running code needs to support _both_ schemas simultaneously (e.g., reading a field that
exists only in the old schema), add a guard and tag it:

```csharp
// TODOIDX: Remove DanceTagsInferred once index is updated to version 3
if (!SongIndex.IsNext)
{
    // ... old behaviour
}
```

### Step 4 — Bump `CodeVersion`

In `SearchServiceManager`, increment `CodeVersion` from `N` to `N+1`.

Add the new index name entries to `appsettings.json`:

```json
"SongIndexProd-3": { "endpoint": "...", "indexname": "songs-prod-3" },
"SongIndexTest-3": { "endpoint": "...", "indexname": "songs-test-3" }
```

(The old `-2` entries remain until the migration is complete and the old index is deleted.)

### Step 5 — Test with `m4d-next` profile

Run the application with the `m4d-next` launch profile. This points at `SongIndexTest` but with
`SEARCHINDEXVERSION` set to the new version, so `NextVersion = true` and the app uses
`SongIndexNext` + `SongFilterNext`.

The `songs-test-3` Azure index must exist; create and populate it by running
`Admin → UpdateSearchIdx` (or `CloneIdx` followed by `SetSearchIdx`).

### Step 6 — Validate and prepare production migration

Once you are satisfied with the behaviour on the test index:

1. Confirm `songs-prod-3` is provisioned in Azure AI Search (it can be empty at this point).
2. Merge the feature branch to `main` and deploy to the staging / production App Service with
   `SEARCHINDEXVERSION` still _unset_ (so the current schema remains live).
3. Verify the deployment is healthy on the current schema.

---

## Production Migration Runbook

> **Prerequisites**: The new `songs-prod-N+1` index name has been provisioned in Azure AI Search
> and added to `appsettings.json`. The code for the new schema is deployed to production with
> `SEARCHINDEXVERSION` still unset.

### Step 1 — Navigate to the Admin Diagnostics page

`/Admin/Diagnostics` — confirm the current index and version are as expected.

### Step 2 — Run `UpdateSearchIdx`

`GET /Admin/UpdateSearchIdx` (requires `showDiagnostics` role).

What happens internally (`DanceMusicCoreService.UpdateIndex`):

1. Posts a site-wide banner: _"We are upgrading infrastructure …"_
2. Creates/resets `songs-prod-N+1` via `SongIndexNext.ResetIndex()`.
3. Streams all songs from the current index using `BackupIndexStreamingAsync()` (no 100 K limit).
4. Uploads the backup to the new index via `UploadIndex()`.
5. Calls `SearchService.RedirectToUpdate()`, setting `ConfigVersion = CodeVersion + 1` → `NextVersion = true`.
6. Clears the stats cache and reloads from Azure.
7. Clears the banner.

### Step 3 — Verify

Browse the site. Spot-check song search, dance pages, and tag filters. Check `/Admin/Diagnostics`
to confirm the active index is now `songs-prod-N+1`.

### Step 4 — Clean up `TODOIDX` items

Remove all `// TODOIDX:` compatibility shims from the codebase. Run all tests.

### Step 5 — Remove the old index section from `appsettings.json`

Delete the `SongIndexProd-N` entry (e.g. `SongIndexProd-2`) from `appsettings.json` and from
Azure AI Search. Keep the `N+1` entry as the new current version.

### Step 6 — Bump `CodeVersion` (if not already done)

If `CodeVersion` was bumped in Step 4 of _Implementing a Breaking Change_, this is already done.
Re-deploy so the constant matches the live index.

---

## Rollback

If the migration fails or the new index behaves incorrectly:

1. Call `Admin/SetSearchIdx?id=SongIndexProd` (or the specific old-version id) to switch back to
   the previous index immediately — no redeploy required.
2. Dance stats will reload automatically.
3. Investigate the issue, fix, and re-run `UpdateSearchIdx` when ready.

---

## Testing Breaking Changes

Use `DanceMusicTester` in the server-side test suite. The `SearchServiceManager` used in tests is
created via `Mock<ISearchServiceManager>` and wired to return `SongFilter.Create(nextVersion, ...)`.
To test next-version behaviour, pass `nextVersion: true`:

```csharp
mockSearchService
    .Setup(m => m.GetSongFilter(It.IsAny<string>()))
    .Returns<string>(s => SongFilter.Create(/* nextVersion */ true, s));

mockSearchService
    .Setup(m => m.NextVersion)
    .Returns(true);
```

Integration tests that require the actual Azure index should use the `m4d-next` launch profile
and the test index. Unit tests should use mocks.

---

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
    │  BuildIndex() v2    │          │  BuildIndex() v3      │
    │  IsNext = false     │          │  IsNext = true        │
    └─────────┬──────────┘          └───────────┬──────────┘
              │                                 │
    ┌─────────▼──────────┐          ┌───────────▼──────────┐
    │   SongFilter        │          │   SongFilterNext      │
    │  (OData filters v2) │          │  (OData filters v3)   │
    └────────────────────┘          └──────────────────────┘

Azure AI Search indices:
  songs-prod-2   ← current production (old schema)
  songs-prod-3   ← next production    (new schema, populated during UpdateSearchIdx)
  songs-test-2   ← current test
  songs-test-3   ← next test
  songs-experimental ← freeform (always IsNext = true)
```
