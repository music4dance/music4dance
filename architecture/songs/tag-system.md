# Tag System

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-03
**Code:** `m4dModels/TagGroup.cs`, `m4dModels/TagManager.cs`, `m4dModels/TaggableObject.cs`,
`m4dModels/TagList.cs`, `m4dModels/TagSummary.cs`, `m4dModels/TagQuery.cs`,
`m4dModels/DanceMusicCoreService.cs` (Tags region), `m4d/Controllers/TagController.cs`,
`m4d/APIControllers/TagController.cs`, `m4d/ClientApp/src/models/Tag.ts`,
`m4d/ClientApp/src/models/TagList.ts`, `m4d/ClientApp/src/models/TagQuery.ts`,
`m4d/ClientApp/src/helpers/TagEnvironmentManager.ts`, `m4d/ClientApp/src/components/TagListEditor.vue`

Tags are free-form labels that users and bots attach to a song, or to one dance rating on a song.
This doc covers the vocabulary (categories and tag groups), the in-memory tag cache, how tags reach
the search index, the admin tag tools, and the client classes and components. The wire format of
the `Tag+` / `Tag-` / `DeleteTag` properties in a song's history is in
[song-internal-format](song-internal-format.md) (section 3.2) and isn't repeated here.

## Tag keys and categories

A tag key is `value:Category`, for example `Jazz:Music`, `4/4:Tempo` or `International:Style`.
Lists of tags are pipe-delimited (`Pop:Music|Wedding:Other`). In a search query each tag can carry
a `+` / `-` qualifier, and a summary can carry a count (`Jazz:Music:12`).

| Category | Where it's valid | Search index field | UI name / icon |
| --- | --- | --- | --- |
| `Music` | Song | `GenreTags` | "musical genre", `music-note-list` |
| `Tempo` | Song and dance rating | `TempoTags` | "tempo", `clock` |
| `Other` | Song and dance rating | `OtherTags` | "other", `tag` |
| `Style` | Dance rating only | `dance_{id}/StyleTags` | "style", `briefcase` |
| `Dance` | Song (generated) | `DanceTags` | "dance", `award` |

- **Server validation.** `TaggableObject.VerifyTags` checks the category against the object's
  `ValidClasses`. For `Song` that's `dance, music, tempo, other`. For `DanceRating` it's
  `style, tempo, other`. Unknown or missing categories become `Other`, and the category is
  title-cased.
- **Client validation.** `Tag.getSongValidCategories()` / `Tag.getDanceValidCategories()` and
  `Tag.filterByContext` mirror these lists, without `Dance`. The categories a user may *add* come
  from `Song.categories` (`Tempo`, `Other`, `Music`) and `DanceRating.categories` (`Style`,
  `Tempo`, `Other`).
- **`Dance` tags** are written as a side effect of dance voting (`Foxtrot:Dance`, or
  `!Foxtrot:Dance` for a down-vote). Users don't edit them as tags. The client hides them from
  tag lists and the tag database. See [song-internal-format](song-internal-format.md) and
  [dance-family-voting](dance-family-voting.md) for how `Style` tags are set by family voting.
- **Search naming.** The search index calls the `Music` category `Genre`, so
  `TagQuery.TagFromFacetId` / `TagFromClassName` (C# and TS) map `GenreTags` back to `Music`.

## Tag groups (primary / alias rings)

`TagGroup` (table `TagGroups`, keyed by `Key`) is one entry in the site-wide tag vocabulary:

| Member | Meaning |
| --- | --- |
| `Key` | `value:Category`; `Value` and `Category` are derived from it |
| `Count` | Number of references (filled from search facets, not stored meaningfully in SQL) |
| `PrimaryId` | Key of the *primary* tag this one is an alias of, or null |
| `Primary` / `Children` | Navigation links, rebuilt in memory by `TagManager.SetTagMap` |
| `Modified` | Timestamp used by backups (`SerializeTags` with `from`) |

A primary and its aliases form a "ring". `GetPrimary()` walks `Primary` links to the root, and
`DanceMusicCoreService.GetTagRing(tag)` returns the primary for any key (or a transient
`TagGroup` if the key is unknown). Ring resolution is applied in three places:

1. **On write.** `TaggableObject.AddTags` / `RemoveTags` / `ChangeTags` run the tags through
   `ConvertToRing`, so the object's `TagSummary` only ever holds primary keys. The raw `Tag+`
   property in the song history keeps whatever the user typed.
2. **On query.** `TagQuery.GetODataFilter(dms)` expands the include and exclude lists with
   `dms.GetTagRings` before building OData, so a search for an alias matches songs indexed under
   the primary. See [song-filter](../search/song-filter.md) for the OData shapes per category.
3. **On cleanup.** The `X` action of `Song.CleanupProperties` (`RemoveTagRing`) rewrites the
   `Tag+` / `Tag-` properties themselves to primary keys. `T` (`FixDuplicateTags`) drops tags
   that resolve to the same ring.

`TagGroup.TagEncode` / `TagDecode` make a key URL-safe (`:` becomes `-p`, `/` becomes `-s`, space
becomes `-w`, `-` becomes `--`, other characters become `-xx` hex). The MVC tag pages use them for
their `id` route values.

## The tag cache: `TagManager`

`TagManager` holds `TagMap`, a case-insensitive `ConcurrentDictionary<string, TagGroup>`. It lives
on `DanceStatsInstance.TagManager` and is exposed as `DanceMusicCoreService.TagManager` /
`TagMap`.

- **Build.** `DanceBuilder.Build` calls `TagManager.BuildTagManager(dms, GlobalFacets, source)`.
  This seeds the map from the SQL `TagGroups` rows (de-duplicated case-insensitively by
  `CleanTagGroups`, with collisions recorded in `TagManager.Duplicates`), then asks the search
  index for facets on `GenreTags`, `TempoTags`, `OtherTags`, `dance_ALL/StyleTags`,
  `dance_ALL/TempoTags` and `dance_ALL/OtherTags` (up to 10,000 values each). Each facet value
  becomes or updates a `TagGroup` with its facet `Count`. So the SQL table only has to hold tags
  with ring links. Every other tag is discovered from the index.
- **Persistence.** The map is serialized as `tagGroups` in the dance-stats snapshot (the
  `DanceStatsInstance` JSON constructor rebuilds a `TagManager` from it). The cold-start fallback
  and sandbox stats files carry it the same way. See
  [song-internal-format](song-internal-format.md) and
  [refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md).
- **Live updates.** When a user adds or removes a tag, `TaggableObject.DoUpdate` calls
  `UpdateTagGroups`, which does `FindOrCreateTagGroup(tag).Count += 1` (or `-= 1`) on the shared
  map. New tags therefore show up in the map immediately. Their counts drift until the next stats
  rebuild, and nothing deletes groups that reach zero (there's a TODO about a sweep).
- **Per-dance tag summaries.** `DanceBuilder.LoadSongs` also builds each dance's
  `DanceStats.SongTags` and `DanceTags` (`TagSummary`s from that dance's facets). The dance
  details page renders these as two `TagCloud`s.

`TagSummary` is the counted form (`Tag:Count|Tag:Count`, sorted). `TagList` is the uncounted
form, with the `+` / `-` qualifier helpers (`ExtractAdd`, `ExtractRemove`, `AddMissingQualifier`,
`Subtract`, `Normalize`). `TagCount` parses one entry. `TagAccumulator` merges summaries.

## Storage on songs and dance ratings

`Song` and `DanceRating` both derive from `TaggableObject`, which owns a `TagSummary` and a
comment list. During song load, `Tag+` / `Tag-` / `DeleteTag` properties go through
`Song.AddObjectTags` / `RemoveObjectTags` / `ForceDeleteTag`. These pick the song or (when the
property has a dance qualifier, `Tag+:CHA`) the matching `DanceRating`. `GetUserTags(user)`
replays the property log to find the tags one user currently has on the object. `AddTags` and
`ChangeTags` use it so a user's repeat of a tag doesn't count twice.

In the search document (`SongIndex.DocumentFromSong`):

- song-level `Music`, `Tempo`, `Other` and `Dance` tags go to `GenreTags`, `TempoTags`,
  `OtherTags` and `DanceTags`.
- each dance rating's `Tempo`, `Style` and `Other` tags go to `dance_{id}/TempoTags`,
  `StyleTags` and `OtherTags`. Their union goes to `dance_ALL/...`.

A `TagQuery` without the `^` prefix matches a tag on either the song or `dance_ALL`. With `^` it
matches song tags only (see [song-filter](../search/song-filter.md)).

## Server endpoints

### `m4d/Controllers/TagController.cs` (MVC)

| Action | Access | What it does |
| --- | --- | --- |
| `Index` | anonymous | Tag Cloud page: `Vue3("tag-index", danceEnvironment, tagEnvironment)` |
| `List` | `dbAdmin` | Razor table of `Database.OrderedTagGroups` (all groups in the cache), with Edit / Details / Delete links and a link to `/song/tags` |
| `Details` | `dbAdmin` | Razor view of one `TagGroup` |
| `Edit` (GET/POST) | `dbAdmin` | Rename a tag (`newKey`) **or** change its `PrimaryId`, not both at once |
| `Delete` (GET/POST) | `dbAdmin` | Delete a group. Only allowed for aliases (`PrimaryId` set); otherwise returns 406 |
| `CleanupTags` | `dbAdmin` | Removes SQL `TagGroups` rows that aren't in the cache or have no ring links (`IsConected`) |

Edit calls `DanceMusicCoreService.RenameTag` (finds or creates the new key and makes it the
primary) or `SetPrimaryTag` (the primary must already exist). Both go through `UpdateTag`, which:

1. builds the OData filter for the old tag (`FilterFromTag`),
2. updates the in-memory ring (`TagManager.SetPrimary`, which also moves the alias's count onto
   the primary) and adds or updates both rows in SQL (`AddTagsToDatabase`),
3. calls `SongIndex.UpdateFromFilter`, which re-indexes every song that matched the old tag. The
   songs are reloaded from their property logs, so their summaries are rebuilt with the new
   primary. Then it saves changes.

The admin landing page `/Admin/Tags` (`Views/Admin/Tags.cshtml`) links to the cloud, the list, and
`AzureFacets` dumps of the raw index facets.

### `m4d/APIControllers/TagController.cs` (`GET api/tag`)

Returns `{ key, count }` for every non-`Dance` primary tag (cached for 600 seconds). The same data
is embedded in pages (see below). Nothing in the client, the e2e suite or the rest of the repo
calls this endpoint today.

### Song list tag actions

`SongController.Tags`, `AddTags` and `RemoveTags` (`/song/tags?tags=...` etc.) set or adjust
`Filter.Tags` with `TagList` operations and run the search. The admin tag list links to `Tags`.

## Getting the vocabulary to the client

Pages that need the tag vocabulary pass `tagEnvironment: true` to `Vue3()` (`TagController.Index`,
and in `SongController` the song details, Advanced Search, Augment and Merge pages). `DanceMusicController.BuildEnvironment3` fills
`ViewData["TagDatabase"]` from `DanceStatsInstance.GetJsonTagDatabse()`: non-`Dance` primary tags
as `{ key, count }`, cached in a static string. `Views/Shared/_environmentWriter.cshtml` writes it
to `window.tagDatabaseJson`. The cache is cleared by `/Admin/ClearSongCache`
(`DanceMusicController.ClearJsonCache`).

On the client, `safeTagDatabase()` (`helpers/TagEnvironmentManager.ts`) parses it once into a
`TagDatabase`. `TagDatabase.addTag` records tags the user creates in this session in
`sessionStorage["incremental-tags"]`, and `TagLoader` merges them back on the next page load, so a
new tag is offered again before the server stats catch up.

## Client models

| Class | File | Role |
| --- | --- | --- |
| `Tag`, `TagBucket`, `TagCategory`, `TagContext` | `models/Tag.ts` | One tag (`key`, `count`). `value` / `category` getters, `fromString` / `fromParts` / `fromKey` / `fromDanceId`, negation (`!value`), category icon/variant lookup (`Tag.tagInfo`), context filtering. `TagBucket.bucketize` ranks tags into 10 size buckets for the cloud |
| `TagList` | `models/TagList.ts` | Pipe-delimited list. `tags`, `Adds` / `Removes` (qualifier split), `add` / `remove` / `find`, `getByCategory`, `voteFromTags`, human descriptions |
| `TagQuery` | `models/TagQuery.ts` | Search-side wrapper: `^` flag (`excludeDanceTags`), `tagList`, `addTag(key, include)`, `description` / `shortDescription` |
| `TaggableObject` | `models/TaggableObject.ts` | Base for client `Song` and `DanceRating`: `tags`, `currentUserTags`, `comments`, `categories`, `modifier` (`""` or `:{danceId}`) |
| `TagHandler` | `models/TagHandler.ts` | Context for a clicked tag: builds the include/exclude, "filter current list" / "list all" links (`getAvailableOptions`), dance-scoped when `danceId` is set |
| `TagMatrix` | `models/TagMatrix.ts` | Dance x tag count table for the wedding page (server: `DanceController.BuildWeddingTagMatrix`) |

As CLAUDE.md says, build and parse tag strings with these classes (`Tag.fromParts`,
`item.tagQuery.tagList.tags`, `DanceQueryItem`), never by hand.

## Tag UI

| Component | Used by | Purpose |
| --- | --- | --- |
| `TagListEditor` | `SongCore.vue`, `DanceDetails.vue` (song page) | Edit tags on a song or one dance rating. See below |
| `TagCategorySelector` + `TagSelector` | `TagListEditor`, Advanced Search `TagQuerySelector` | Searchable picker over the tag database, grouped by category. Typing a new value offers "+value" in each allowed category |
| `TagList.vue` / `TagButton` | `TagListEditor`, `SongTable`, `DanceModal` | Read-only chips. A check mark shows the current user's tags |
| `TagButtonOther` | `TagListEditor` | Chip that adds (or, with `is-delete`, removes) someone else's tag |
| `TagModal` | `SongTable`, `TagCloud`, `SongCore` | Menu of `TagHandler` filter/list links for a clicked tag |
| `TagCloud` | `tag-index` page, `dance-details` page | Size-bucketed cloud with per-category toggles and a strictness slider |
| `TagViewer` | `SongPropertyViewer` (song history) | One tag in a history row, struck through if no longer active |
| `TagIcon` | tag and dance buttons | Maps `Tag.tagInfo` icon names to `unplugin-icons` components |

`TagListEditor` is the only place users change tags. Adding or removing one of your own tags
through the selector, or adopting another user's tag, queues a `Tag+` / `Tag-` property (plus
`:danceId` for a dance rating) on the `SongEditor`. New keys go into the session tag database.
Users with `canEdit` also get a "Remove Tags" row for other users' tags. Other signed-in users get
"Remove System Tags" for tags last touched by a bot (`systemTagKeys`). The permission matrix and
save flow are in [song-details-viewing-editing](song-details-viewing-editing.md).

## Backup, restore and seed data

`DanceMusicService.SerializeTags` writes `Category\tValue\tPrimaryId\tModified` lines after a
`+++++TAGSS+++++` header. `BackupDatabase` and `BackupTail` include it. `LoadTags` reads the same
format (from `AdminController.ReloadDatabase` and the sandbox's embedded `test-tags.txt` via
`SandboxServiceFactory`) and rebuilds `Primary` / `Children` links.

## Tests

- Server: `m4dModels.Tests/TagTests.cs`, `TagQueryTests.cs`, `TagFormatTests.cs`.
- Client: `models/__tests__/TagList.test.ts`, `TagHandler.test.ts`;
  `components/__tests__/TagListEditor.test.ts`, `TagCloud.test.ts`, `TagViewer.test.ts`;
  `pages/tag-index/__tests__/tag-index.test.ts`. Tests load the vocabulary from
  `src/assets/content/tags.json` through `TestHelpers`.
- E2E: `e2e/tests/tagging.spec.ts` adds and removes a tag on a seeded sandbox song.

## Future improvements

- `GET api/tag` has no callers. Either drop it or switch pages to it instead of embedding
  `tagDatabaseJson`.
- `AdminController.BackupTail` assigns `playlists = Database.SerializeTags(true, from)`. It looks
  like it should be `SerializePlaylists`.
- `TagController.CleanupTags` calls `RemoveRange(delete)` inside a `foreach` over `delete`. That
  works, but it's redundant.
- Live `TagGroup.Count` updates drift and zero-count groups are never swept (TODO in
  `TaggableObject.UpdateTagGroups`).
- `UpdateTagGroups` counts the user's raw tags while the summary stores ring primaries, so an
  alias's in-memory count can rise again after `SetPrimary` zeroes it.
- Naming debt: `GetJsonTagDatabse`, `TagGroup.IsConected`, `TagSummary.MassageeTag`, and the
  `showModal` stub in `useTagButton.ts`.

## History

- PR 248 / 252 / 255 / 256 (Dec 2021): tag-system cleanup; embedded `TagDatabase` in pages.
- PR 449 / 497 (2024): Advanced Search and song details ported to Vue 3, including the tag
  editors.
- #44 (2025-08): tag handling refactored out of `SongFilter` into `TagQuery` (C# and TS).
- #46 (2025-09): dance-specific tags made first class (`dance_{id}/...Tags`, `TagHandler`
  dance-scoped options); #48 follow-up fixes.
- #150 (2026-04): automated tags removed from song history; "Remove System Tags" added.
- This doc was created on 2026-10-01 (architecture coverage gap #4).
- 2026-10-03: `Details` requires `dbAdmin` (it had no attribute); `TagControllerAuthorizationTests`
  checks every action but `Index`.

## Related

- [song-internal-format](song-internal-format.md): `Tag+` / `Tag-` / `DeleteTag` property format
- [song-details-viewing-editing](song-details-viewing-editing.md): tag editing UI, permissions
- [dance-family-voting](dance-family-voting.md): `Style` tags from family voting
- [song-filter](../search/song-filter.md): `TagQuery` wire format and OData mapping
- [bulk-operations](../users-admin/bulk-operations.md): bulk tag changes via `SongModifier`
