# Per-Dance Tempo — Implementation Plan

**Issue**: [#15 Enable having a different tempo per dance on a song](https://github.com/music4dance/music4dance/issues/15)

## Motivation

Some songs genuinely work at different tempos for different dances — the classic example is Slow
Foxtrot vs. Viennese Waltz, but even a fast Lindy Hop / Charleston combo alongside a slow Foxtrot
is possible. The feature allows editors to store a per-dance override tempo alongside the existing
song-level tempo.

---

## Design Decisions

### Per-dance tempo lives on `DanceRating`, not on `Song`

`DanceRating` already carries the dance-specific weight (votes) and tags. Adding an optional
`decimal? Tempo` field there is the natural extension. The song-level `Tempo` remains the
canonical value when no per-dance override is set.

### Azure AI Search schema is a breaking change

Azure AI Search complex-type fields cannot have new sub-fields added after index creation. Because
each dance is stored as a `dance_{id}` complex field, adding a `Tempo` sub-field requires a new
index schema version (version 3). This is implemented through the existing
[Search Index Versioning](search-index-versioning.md) mechanism.

### Fallback strategy for tempo filtering/sorting

Azure AI Search cannot fall back from a sub-field to a top-level field in a single OData
expression. The issue notes this constraint. The chosen strategy is:

> **On migration, clone the song-level `Tempo` into every dance rating that has a vote.**

This ensures `dance_{id}/Tempo` is always populated for existing data. New songs get the same
treatment at creation time. The sort/filter then unconditionally uses `dance_{id}/Tempo` when a
single dance is selected and we are in the next-version schema.

### Tempo Semantics Rules

#### Table 1 — Write semantics (what each token does to in-memory model)

| Token                | Pre-condition          | `song.Tempo` after | `dr.Tempo` (named dance) after | Rationale                                                                                                                                                                                                |
| -------------------- | ---------------------- | ------------------ | ------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Tempo=120`          | any                    | → 120              | unchanged                      | Standard song-level set                                                                                                                                                                                  |
| `Tempo=` (empty)     | any                    | → null             | unchanged                      | Explicit clear; dances lose inherited value                                                                                                                                                              |
| `Tempo:CHA=128`      | `song.Tempo` has value | unchanged          | CHA → 128                      | Override one dance only                                                                                                                                                                                  |
| `Tempo:CHA=128`      | `song.Tempo` is null   | → 128 (inferred)   | CHA → 128                      | **Promote**: dance tempo becomes song tempo _by inference in `LoadProperties`_; explicit dance override is preserved so other users can freely change `song.Tempo` later without disturbing CHA's intent |
| `Tempo:CHA=` (empty) | any                    | unchanged          | CHA → null                     | Remove override; CHA reverts to inheriting `song.Tempo`                                                                                                                                                  |
| `Tempo:CHA=` (empty) | `song.Tempo` is null   | unchanged          | CHA → null                     | CHA loses its tempo entirely (no fallback)                                                                                                                                                               |

**Promote decision**: inference happens inside `LoadProperties`/`loadProperties` replay, not in the UI. This preserves the semantic that the user is explicitly expressing a _dance_ preference, not a song preference — leaving other users free to set `song.Tempo` independently.

#### Table 2 — Index propagation (`DocumentFromSong`, `dance_{id}/Tempo = dr.Tempo ?? song.Tempo`)

| `song.Tempo`   | `dr.Tempo` (CHA) | `dr.Tempo` (SLS) | `dance_CHA/Tempo` | `dance_SLS/Tempo` |
| -------------- | ---------------- | ---------------- | ----------------- | ----------------- |
| 120            | null             | null             | 120 (inherited)   | 120 (inherited)   |
| 120            | 128              | null             | 128 (override)    | 120 (inherited)   |
| null           | null             | null             | null              | null              |
| 128 (inferred) | 128 (explicit)   | null             | 128 (explicit)    | 128 (inherited)   |

Note row 4: once `song.Tempo` is inferred as 128, any dance without an explicit override (e.g. SLS) **inherits 128**, not null. The fallback `dr.Tempo ?? song.Tempo` applies at index time.

#### Table 3 — Permission rules

| Actor                                       | `Tempo=value`        | `Tempo=` (clear) | `Tempo:DanceId=value`          | `Tempo:DanceId=` (clear) | Overwrite another human's value |
| ------------------------------------------- | -------------------- | ---------------- | ------------------------------ | ------------------------ | ------------------------------- |
| Algorithm / pseudo (`batch-*`, `tempo-bot`) | ✅ if no human value | ❌               | ✅ if no human value for dance | ❌                       | ❌                              |
| `canTag` role (privileged)                  | ✅                   | ✅               | ✅                             | ✅                       | ✅                              |
| Song creator                                | ✅                   | ✅               | ✅                             | ✅                       | Own values only                 |
| Other authenticated user                    | ✅ if no human value | ❌               | ✅ if no human value for dance | ❌                       | ❌                              |
| Anonymous                                   | ❌                   | ❌               | ❌                             | ❌                       | ❌                              |

"Human value exists" = last writer of `Tempo` / `Tempo:DanceId` was a non-pseudo user (tracked via `isUserModified`).

#### Table 4 — UX gaps to address

| Rule                         | Required UX                                                                                                     |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `Tempo=` clears song tempo   | "Clear tempo" action in song editor (canTag only)                                                               |
| `Tempo:CHA=` clears override | Dance tempo input already supports empty → clear; but UI should visually distinguish "not set" from "inherited" |
| Promote inference            | No UI change needed; handled server- and client-side in property replay                                         |

---

## Work Breakdown

### Phase 1 — Data model

**`m4dModels/DanceRating.cs`**

- Add `[DataMember] public decimal? Tempo { get; set; }`

**`m4dModels/Song.cs`** (serialization)

- In `Song.Create` (the tab-delimited parser), recognise `Tempo:{DanceId}=value`:
  - Look up the dance rating by ID; set its `Tempo`.
- Modify the handler for `Tempo=value`:
  - As before, set `song.Tempo = value`.
  - Also, for every `DanceRating` whose current `Tempo == null` OR whose `Tempo == old song tempo`,
    set `rating.Tempo = value`. (This keeps ratings that have been split from drifting.)
- In the serializer (wherever the song is emitted back to the triplet format), emit
  `Tempo:{dr.DanceId}=value` lines for every `DanceRating` where `dr.Tempo != song.Tempo`
  (i.e., only write the override lines, not the defaults).

**Tests (`m4dModels.Tests` or `DanceTests`):**

- Song with one dance, no override → dance rating tempo equals song tempo after parse.
- Song with two dances, one override → only the overridden dance differs.
- Re-setting song tempo propagates to non-overridden dances only.

---

### Phase 2 — Index schema (breaking change, version 3)

**`m4dModels/SongIndexNext.cs`**

Override `BuildIndex()`. In the base implementation, `IndexFieldFromDanceId` creates each dance
complex field. The override adds a `Tempo` sortable/filterable double sub-field:

```csharp
protected override SearchField IndexFieldFromDanceIdNext(string id)
{
    var field = base.IndexFieldFromDanceId(id); // gets Votes, TempoTags, StyleTags, OtherTags
    field.Fields.Add(new SearchField(Song.TempoField, SearchFieldDataType.Double)
    {
        IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = false
    });
    return field;
}
```

_(Exact approach depends on whether `SearchField.Fields` is mutable in the SDK; if not, rebuild the
field from scratch.)_

Override `DocumentFromSong` to populate `dance_{id}/Tempo`:

```csharp
doc[BuildDanceFieldName(dr.DanceId)] = new Dictionary<string, object>
{
    { Votes,     dr.Weight },
    { Song.TempoField, CleanNumber((float?)(dr.Tempo ?? song.Tempo)) }, // ← new
    { TempoTags, oneTempo.ToArray() },
    { StyleTags, oneStyle.ToArray() },
    { OtherTags, oneOther.ToArray() }
};
```

Bump `SearchServiceManager.CodeVersion` from `2` to `3` in `SearchServiceInfo.cs`
only when production cutover is complete.

Add to `appsettings.json` (already present, just confirm):

```json
"SongIndexProd-3": { "endpoint": "...", "indexname": "songs-prod-3" },
"SongIndexTest-3": { "endpoint": "...", "indexname": "songs-test-3" }
```

**Tests:**

- `BuildIndex()` produces a `dance_{id}` complex field that contains a `Tempo` sub-field.
- `DocumentFromSong` populates `dance_{id}/Tempo` from the override when set, falling back to song
  tempo when not.

---

### Phase 3 — Filter and sort changes

**`m4dModels/DanceQueryNext.cs`**

Override `ODataSort` to sort by per-dance tempo when appropriate:

```csharp
public override IList<string> ODataSort(string order)
{
    var dances = DanceLibrary.Dances.Instance.ExpandGroups(Dances).ToList();
    if (dances.Count == 0)
        return [$"dance_ALL/Votes {order}"];
    return [$"dance_{dances[0].Id}/Votes {order}"];
    // Note: tempo sort is handled by SongFilterNext.ODataSort (see below)
}
```

**`m4dModels/SongFilterNext.cs`** (or in `SongFilter.ODataSort` gated on `IsNext`)

Override the tempo sort case — when sorting by `Tempo` and the filter targets a single dance, use
`dance_{id}/Tempo`:

```csharp
public override IList<string> ODataSort
{
    get
    {
        var sort = SongSort;
        if (sort.Id == SongSort.Tempo && IsSingleDance)
        {
            var dances = DanceQuery.ExpandedDances.ToList();
            if (dances.Count == 1)
                return [$"dance_{dances[0].Id}/{Song.TempoField} {(sort.Descending ? "desc" : "asc")}"];
        }
        return base.ODataSort;
    }
}
```

**Tempo filter (OData `where` clause)**

In `SongFilter.GetOdataFilter` (or `SongFilterNext` override), when `TempoMin`/`TempoMax` are set
**and** `IsSingleDance` is true, emit:

```
dance_{id}/Tempo ge 120.0 and dance_{id}/Tempo le 140.0
```

instead of:

```
Tempo ge 120.0 and Tempo le 140.0
```

This is a breaking-change-only path so can live entirely in `SongFilterNext`.

**Tests:**

- Verify `ODataSort` produces `dance_CHA/Tempo asc` when a single-dance Cha Cha filter is sorted
  by tempo in next-version mode.
- Verify it falls back to `Tempo asc` for multi-dance or no-dance filters.
- Verify tempo OData filter uses the dance sub-field for single-dance filters.

---

### Phase 4 — UI: song editor

**Location**: the manual song editor (both admin and user-facing). The `Tempo` field currently
shows/edits the song-level tempo. With this change:

- The song-level tempo field remains at the top.
- Below each dance rating, show the dance-specific tempo (pre-filled with the song tempo if no
  override is set).
- Editing a dance tempo produces a `Tempo:DanceId=value` serialisation token in the edit block.
- If a dance tempo is cleared (set to empty), the override is removed and it reverts to the song
  tempo.

#### Interaction fix: keep the clicked control visible while entering edit mode

The edit-entry icons can trigger a layout shift when the editor opens. The implementation now
uses selector-based edit targeting so each entry point can request the exact control to focus after
re-render.

- `requestEdit` now accepts a selector string from the initiating control (song tempo, song tags,
  dance row controls) instead of branching on source IDs in `SongCore`.
- After DOM update, `SongCore` scrolls to the selector target and applies focus using VueUse
  focus handling.
- Dance row entry points (including dance-tag pencil) now route to the dance tempo input selector
  for that same dance row so the editor target is predictable.

#### Visual fix: distinguish inherited tempo from explicit tempo

Inherited tempo now renders with muted/italic placeholder styling and numeric-first text.

- Dance tempo inherited placeholder uses `NNN (inherited)` and widened controls.
- Song tempo uses `NNN (inferred)` only for the true corner case where song tempo was promoted from
  dance tempo replay with no explicit song-level `Tempo=` token.
- Song tempo and song length controls were widened to improve readability without changing behavior.

**Serialization token on edit**: Follow the same pattern used for dance-specific tags
(`Tag+:DanceId=...`). The edit block passed to `SongIndex.EditSong` should include the
`Tempo:DanceId=value` tokens.

**Tests:**

- Client-side: the tempo input in the dance-rating section emits the correct token.
- Client-side: selector propagation/focus targeting is verified from dance controls into `SongCore`.
- Client-side: dance-tag edit entry emits a dance-tempo selector for the same dance row.
- Client-side: inferred placeholder is shown only for true dance-promoted song tempo.
- Server-side: `EditSong` round-trips the per-dance tempo correctly.

---

### Phase 5 — Migration

When `UpdateIndex` runs (see [search-index-versioning.md](search-index-versioning.md)):

1. `SongIndexNext.DocumentFromSong` automatically clones song tempo into every dance rating that
   has no override (by using `dr.Tempo ?? song.Tempo`).
2. No extra migration step is needed for the data — the `UploadIndex` path handles it.

To verify before running production migration:

1. Use the `m4d-next` launch profile (`SEARCHINDEXVERSION=3`, `SEARCHINDEX=SongIndexTest`).
2. Run `Admin/CloneIdx?id=SongIndexTest` to populate `songs-test-3` from the current test index.
3. Browse song/dance pages and verify tempos are populated correctly.
4. Run the client and server test suites.

---

### Phase 6 — Clean up `TODOIDX` items (after production migration)

The following `// TODOIDX:` items were already pending; they should be cleaned up as part of this
migration since they all belong to the same schema transition:

| File                     | Item                      | Cleanup                                       |
| ------------------------ | ------------------------- | --------------------------------------------- |
| `m4dModels/SongIndex.cs` | `DanceTagsInferred` field | Remove from `BuildIndex()` and all references |
| `m4dModels/Song.cs`      | `TitleHashField`          | Remove from `BuildIndex()` and all references |
| `m4dModels/Song.cs`      | `FailedLookup` comment    | Follow the inline note                        |

---

## Implementation Status

| Phase                       | Status         | Notes                                                                                                 |
| --------------------------- | -------------- | ----------------------------------------------------------------------------------------------------- |
| Phase 1 — Data model        | ✅ Complete    | `DanceRating.Tempo`, unified `Tempo` token with optional `:DanceId` qualifier                         |
| Phase 2 — Index schema (v3) | ✅ Complete    | `SongIndexNext.BuildIndex/DocumentFromSong`                                                           |
| Phase 3 — Filter / sort     | ✅ Complete    | Single concrete dance tempo filter/sort uses `dance_{id}/Tempo`; groups fallback to top-level `Tempo` |
| Phase 4 — UI: song editor   | ✅ Complete    | Per-dance tempo input, selector-based focus targeting, inherited/inferred visual treatment            |
| Phase 5 — Migration         | ⏳ Pending Ops | Production cutover (`UpdateSearchIdx`), then bump `CodeVersion`                                       |
| Phase 6 — TODOIDX cleanup   | ⏳ Pending Ops | Post-production cleanup and old index removal                                                         |

Focused verification completed:

- Server: `PerDanceTempoTests` + `SongFilterTests` pass (26/26).
- Client: song editor selector/focus and inferred/inherited tempo tests pass (44/44 in focused run).
  Full server test suite: **498 passed, 1 skipped, 0 failed**.

---

## Manual Testing Required (Before Merging)

### Prerequisites

1. Launch the app with the `m4d-next` profile (sets `SEARCHINDEXVERSION=3`).
2. The profile should point to `SongIndexTest` so you're working against the test index.

### Creating the v3 test index and running the migration

This is the same process as production migration — a test run for it.

1. Launch with the **normal dev profile** (`m4d-vite`, no `SEARCHINDEXVERSION`). This puts the app in `HasNextVersion` mode: current = `songs-test-2`, next = `songs-test-3`.
2. Go to **Admin/InitializationTasks** and click **"Update SongIndexTest → songs-test-3"**.
   - This resets `songs-test-3`, streams all songs from `songs-test-2`, and uploads them to `songs-test-3` using `SongIndexNext.DocumentFromSong` (which populates `dance_{id}/Tempo` for every song).
   - After completion the app automatically switches to `NextVersion=true` for the rest of that session.
3. Switch to the **`m4d-next` profile** and verify at **Admin/Diagnostics** that **Active Index** shows `songs-test-3`.

### Test scenarios

Before opening or merging the PR, also run the client build verification:

1. From `m4d/ClientApp`, run `yarn build`.
2. Confirm the build completes successfully, which includes the client TypeScript type check.

**A. Tempo filter uses per-dance tempo (single dance)**

1. Navigate to the song list filtered to a single dance (e.g. Cha Cha).
2. Apply a tempo range filter (e.g. 116–120 BPM).
3. In browser dev tools, check the API request includes `dance_CHA/Tempo ge 116` in the OData filter.
4. Verify results make sense (songs with Cha Cha tempos in range appear).

**B. Tempo sort uses per-dance tempo (single dance)**

1. Navigate to the song list filtered to a single dance (e.g. Rumba).
2. Sort by tempo ascending.
3. Verify the API request includes `dance_RMB/Tempo asc` as the sort field.
4. Verify results are ordered by effective Rumba tempo.

**C. Multi-dance falls back to song-level tempo**

1. Navigate to the song list filtered to two dances.
2. Apply tempo filter or sort.
3. Verify the API uses the top-level `Tempo` field, not `dance_{id}/Tempo`.

**D. Set a per-dance tempo override on a song**

Verify:

- Setting a per-dance tempo via the song editor emits `Tempo:DanceId=value`.
- The override is visible in the song editor (pre-filled with song tempo if no override).
- Clearing the override reverts to song tempo.

---

## Summary Checklist

### Schema / Index

- [x] `SongIndexNext.BuildIndex()` — add `Tempo` sub-field to every `dance_{id}` complex field
- [x] `SongIndexNext.DocumentFromSong()` — populate `dance_{id}/Tempo`
- [ ] Bump `CodeVersion` to `3` in `SearchServiceManager` _(deferred — bump only after v3 index is live in production)_
- [ ] Confirm `SongIndexProd-3` and `SongIndexTest-3` entries in `appsettings.json`

### Data Model

- [x] `DanceRating.Tempo` property (nullable decimal)
- [x] `Song.Create` parser: `Tempo:{DanceId}=` token
- [ ] `Song.Tempo` setter: propagate to un-overridden dance ratings _(deferred — migration via `DocumentFromSong` fallback is sufficient for now)_
- [x] Client/server replay semantics: `Tempo:{DanceId}` supported with promote inference
- [ ] Serializer: emit `Tempo:{DanceId}=` only when tempo differs from song _(deferred; current append-history replay is canonical)_

### Query

- [x] `SongFilterNext.ODataSort`: use `dance_{id}/Tempo` when single-dance + tempo sort
- [x] `SongFilterNext.GetOdataFilter`: use `dance_{id}/Tempo` for single-dance tempo range filter
- [x] `SongFilter.GetOdataFilter`: same single-dance behavior for base filter path

### UI

- [x] Song editor: per-dance tempo field visible and editable
- [x] Song editor: empty = inherit from song tempo
- [x] Edit serialisation produces correct `Tempo:{DanceId}=` token

### Migration & Cleanup

- [ ] `m4d-next` profile tested against `songs-test-3` (see Manual Testing section above)
- [ ] `TODOIDX` items removed post-production migration
- [ ] Old `SongIndexProd-2` / `SongIndexTest-2` sections cleaned from `appsettings.json` and indices deleted from Azure

### Tests

- [x] `DanceRating.Tempo` serialisation round-trip
- [x] `SongFilterNext.ODataSort` for tempo + single dance
- [x] `SongFilterNext.GetOdataFilter` for tempo range + single dance
- [x] `SongIndexNext.DanceTempoSubField` constant and OData path consistency
- [x] Song tempo propagation/inference behavior for replay path
- [x] Song editor/list client behavior tests
- [x] Selector propagation test from dance edit controls into `SongCore`
- [x] Dance tag edit maps to dance-tempo selector for row-local focus target
- [x] `dance_ALL/Tempo` override marker (`DocumentFromSong_DanceAllTempo_*` tests)

---

## Phase 7 — `dance_ALL/Tempo` as a tempo-override marker field

### Motivation (Phase 7)

Editors want to search for songs that have at least one per-dance tempo override (e.g. to audit or
review them). Azure AI Search cannot express "any `dance_{id}/Tempo` differs from the top-level
`Tempo`" in a single OData filter — it has no way to compare two fields of the same document
against each other.

### Approach

`dance_ALL` is a synthetic, schema-only complex field (it has no corresponding `DanceRating`) that
already aggregates votes/tags across all dances on a song. It already carries the `Tempo`
sub-field in the schema (added as part of Phase 2, since `dance_ALL`'s shape is built by the same
`IndexFieldFromDanceId` helper as every other dance), but `DocumentFromSong` previously always left
it `null`.

`DocumentFromSong` (`m4dModels/SongIndex.cs`) now sets `dance_ALL/Tempo` to the song's top-level
`Tempo` whenever **any** `DanceRating` has an explicit `Tempo` override that differs from
`song.Tempo`. If no dance overrides the song tempo, `dance_ALL/Tempo` stays `null`.

This makes `dance_ALL/Tempo ne null` a single-field, indexable proxy for "this song has at least
one per-dance tempo override" — no field-to-field comparison required.

### OData filter

To find songs with at least one dance tempo override, in RawSearch or the Azure portal:

```text
dance_ALL/Tempo ne null
```

Combine with other filters as usual, e.g. restricted to Cha Cha overrides specifically:

```text
dance_ALL/Tempo ne null and dance_CHA/Tempo ne null
```

### Tests (Phase 7)

`m4dModels.Tests/PerDanceTempoTests.cs`:

- `DocumentFromSong_DanceAllTempo_IsNull_WhenNoOverride`
- `DocumentFromSong_DanceAllTempo_IsSongTempo_WhenAnyDanceOverrides`

Exposed via a `TestSongIndex.CallDocumentFromSong` wrapper since `DocumentFromSong` is `protected`.
