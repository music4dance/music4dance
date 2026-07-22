# SongFilter: Query Model, Wire Format, and the Advanced Search Pipeline

## Overview

`SongFilter` is the compact, serializable query state that drives every song search on the
site — dance selection, keywords, tags, tempo/length range, user/vote scoping, sort order, and
purchase-service filtering. It exists in **parallel implementations** that must stay in sync:

| Layer  | File                                                                  |
| ------ | --------------------------------------------------------------------- |
| Server | `m4dModels/SongFilter.cs` (+ `SongFilterNext`, the v2-schema variant) |
| Client | `m4d/ClientApp/src/models/SongFilter.ts`                              |

Both serialize to and parse the same hyphen-delimited cell string (below), so a filter built by
the client and a filter parsed by the server for the same query string produce equivalent query
objects. This document covers the filter's wire format, the sub-query classes that give each field
meaning, how the **Advanced Search** page (`m4d/ClientApp/src/pages/advanced-search/App.vue`)
builds one from form state, and how the server turns it into an Azure Search OData filter and
`SearchOptions`.

It intentionally does **not** re-document `SongSearch`'s orchestration (premium gating, user
anonymization, vote/edited-by post-filters) or the controller-to-Vue results rendering — see
[[song-search-service]] and [[song-search-results]] for those. This doc is the layer _between_ the
form and that pipeline: how the query itself is represented and compiled. It also covers
`CustomSearchController` (below), a second, independent producer/consumer of `SongFilter` that
builds canned raw filters for the Holiday/Halloween/Broadway pages rather than reading one off an
Advanced Search form.

---

## Wire Format

`SongFilter.ToString()` (server) / `.query` (client) produce a single hyphen-separated string:

```
[v2-]action-dances-sortorder-searchstring-purchase-user-tempomin-tempomax[-lengthmin-lengthmax]-page-tags-level
```

- **Version**: a `v2-` prefix (checked case-insensitively) adds the `lengthMin`/`lengthMax` cells
  after `tempoMax`. Without it, length filtering isn't representable — `SongFilter.Create(bool
nextVersion, ...)` picks `SongFilterNext` vs `SongFilter` based on
  `ISearchServiceManager.NextVersion` (index schema version), not client choice.
- **Cell escaping**: literal `-` inside a cell (e.g. a dance-group tag name) is escaped as `\-`
  before joining, then unescaped after splitting — both client and server use the same substitute
  character trick (server: ``→now `~`; client: ``) to avoid corrupting on a stray
  hyphen. A cell value of `.` or empty means "not set" (`ReadCell`/`readCell` both special-case it).
- **Trimming**: trailing `.`/`-` cells are stripped from the output — an all-default filter
  serializes to `""` (or literally `"index"`, which is also collapsed to empty).
- This string is what appears in the `filter` query-string parameter on every search-driving route
  (`/song/filtersearch?filter=...`, `/song/azuresearch?filter=...`, etc.) and is what
  `GetFilterFromContext` (`m4d/Controllers/DMController.cs`) reads to rehydrate the ambient
  `Filter` on every request — see [[song-search-results]] for that side.

### Raw filters

When `Filter.IsRaw` (action is `azure+raw*` or `customsearch`), the cells are reinterpreted: `Dances`
holds a literal OData filter string, `User` holds Azure `SearchFields`, `SortOrder` holds literal
OData sort clauses, and `Tags` holds free-form flags (e.g. `singleDance`) instead of a tag query.
`RawSearch` (`m4dModels/RawSearch.cs`) is the strongly-typed form model this round-trips through —
`new RawSearch(songFilter)` unpacks a raw `SongFilter` back into named fields for the Raw Search
admin form, and `RawSearch.GetAzureSearchParams(pageSize)` builds `SearchOptions` directly from
those fields (no OData synthesis — the filter _is_ already OData). `RawDanceQuery` gives raw
filters a best-effort `DanceQueryItem` view for display purposes only (regex-extracts a single
dance name out of the OData `DanceTags/...` clause) — it can't represent arbitrary raw queries, just
the common single-dance case produced by `SongFilter.CreateCustomSearchFilter` (see
"CustomSearchController: Canned Raw Filters" below) and admin Raw Search tooling. Advanced Search
never produces a raw filter; this section exists because `SongFilter` is shared machinery.

---

## Sub-Query Classes

Each non-scalar `SongFilter` field is a mini query language of its own, parsed from its cell string
on demand (a `SongFilter` property getter, not cached). Every one of these has a matching
client (`m4d/ClientApp/src/models/*.ts`) and server (`m4dModels/*.cs`) implementation with the same
parsing rules, so the client can preview a filter's `.description` and build its query string
without a round-trip, while the server independently re-derives the same structure to build the
Azure query.

| Field          | Class (server / client)                                               | Cell syntax                                                                                           | Responsibility                                                                                                                                                                                                                                                                   |
| -------------- | --------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `Dances`       | `DanceQuery` / `DanceQuery.ts`                                        | `AND,` prefix (optional) + comma-separated `DanceQueryItem` values, e.g. `AND,WCS+2,CHA-1\|GenreTags` | Which dances, combined `and`/`or`; each item can carry a vote threshold and a dance-scoped `TagQuery`                                                                                                                                                                            |
| —              | `DanceQueryItem` / `DanceQueryItem.ts`                                | `{danceId}[+\|-]{threshold}[\|{tagQuery}]` — regex `^([a-zA-Z0-9]+)([+-]?)(\d*)\|?(.*)?$`             | One dance selection: id, vote threshold (`+n`/`-n`, default `1`), optional nested tag filter scoped to that dance                                                                                                                                                                |
| `Tags`         | `TagQuery` / `TagQuery.ts`                                            | `^` prefix (optional, "exclude dance_ALL tags") + `TagList` syntax (`+`/`-` qualified, `              | `-delimited)                                                                                                                                                                                                                                                                     | Song-level (and optionally dance-level) tag include/exclude, classified into Music/Style/Tempo/Other with different OData shapes per class |
| `User`         | `UserQuery` / `UserQuery.ts`                                          | `[+\|-]{userName}\|[l\|h\|d\|x\|a]`                                                                   | Identity/named-user scoping: include vs. exclude, and a modifier — `l`=liked, `h`=disliked/blocked, `d`=up-voted, `x`=down-voted, `a`=any opinion. See [[song-search-service]] for how `d`/`x` get diverted to `VoteSearch`'s in-memory path since votes aren't OData-filterable |
| `SearchString` | `KeywordQuery` / `KeywordQuery.ts`                                    | plain text, or `` `Field:(value) ... `` (Lucene, leading backtick) for per-field search               | Free-text search; a leading backtick switches Azure `QueryType` to `Full` (Lucene) and enables per-field (`Title:`/`Artist:`/`Albums:`) syntax via `KeywordQuery.Update`/`Fields`                                                                                                |
| `SortOrder`    | `SongSort` / `SongSort.ts`                                            | `{fieldId}[_asc\|_desc]`                                                                              | Sort field + direction; `fieldId` must be one of a fixed allow-list (`s_directional`/`s_intrinsic`/`s_numerical` in the server class) or it's silently dropped                                                                                                                   |
| `Purchase`     | (no class — `SongFilter.SplitPurchase`/`splitPurchase` static helper) | service-code letters, optionally split by a literal `N`: `{include}[N{exclude}]`                      | Which `MusicService` codes (`I`=ITunes, `S`=Spotify, `R`=ISRC/admin-only, etc.) a song must/must-not be available on                                                                                                                                                             |

`TempoMin`/`TempoMax`, `LengthMin`/`LengthMax`, `Page`, and `Level` (a `CruftFilter` bitmask — "not
in a publisher catalog" / "not categorized by dance") are plain scalar cells with no sub-class.

**Always go through these classes to build or parse a filter field** — this is the same rule as
[[../CLAUDE.md]]'s "Filter / Tag Construction" guidance, and this table is the map of which class
owns which field. `DanceQueryItem.fromValue`/`FromValue` and `TagQuery`'s `TagList` parsing in
particular have escaping/regex edge cases (threshold sign, `|`-delimited nested tag queries) that
are easy to get subtly wrong by hand.

---

## Building a Filter: The Advanced Search Page

`AdvancedSearchForm` (`m4d/Controllers/SongController.cs`) serves the `advanced-search` Vue page
(`m4d/ClientApp/src/pages/advanced-search/App.vue`) with `danceEnvironment: true, tagEnvironment:
true` so the dance/tag databases are available client-side. This is reached via the main menu
(`/song/advancedsearchform`), not through `SongFilter`'s own routing.

> There is also a legacy `AdvancedSearch` GET action (`SongController.cs`, individual
> `searchString`/`dances`/`tags`/... query parameters) that mutates the ambient `Filter` field-by-field
> and calls `DoAzureSearch()` directly. It predates the Vue form and isn't linked from the current
> UI — the Vue page below builds and submits a complete filter string itself rather than posting
> discrete fields.

### Initialization

On load, `getQueryFilter()` reads the ambient `?filter=` query param (if the page was reached via
"edit this search") and parses it with `SongFilter.buildFilter()`. Each form control is seeded from
a parsed sub-query:

```ts
const filter = getQueryFilter();
const danceQueryInit = new DanceQuery(filter.dances);
const danceQueryItems = ref(danceQueryInit.danceQueryItems);
const danceConnector = ref(danceQueryInit.isExclusive ? "all" : "any");
...
const userQueryInit = new UserQuery(filter.user);
const user = ref(userQueryInit.userName ?? context.userName ?? "");
...
const purchaseParts = SongFilter.splitPurchase(filter.purchase);
const services = ref(purchaseParts.include ? purchaseParts.include.split("") : []);
```

### Assembly

A `songFilter` computed property rebuilds a fresh `SongFilter` from current form state on every
change (`m4d/ClientApp/src/pages/advanced-search/App.vue:88-121`):

```ts
const songFilter = computed(() => {
  const danceQuery = DanceQuery.fromParts(
    danceQueryItems.value.map((t) => t.toString()),
    danceConnector.value === "all",
  );
  const userQuery = UserQuery.fromParts(
    computedActivity.value ? computedActivity.value : undefined,
    isAnonymous.value ? user.value : displayUser.value,
  );
  const filter = new SongFilter();
  filter.action = "advanced";
  filter.searchString = keyWords.value;
  filter.dances = danceQuery.query;
  filter.sortOrder = SongSort.fromParts(
    sortId.value ?? undefined,
    sortDirection.value,
  ).query;
  filter.user = userQuery.query;
  filter.purchase = SongFilter.joinPurchase(
    services.value,
    excludeServices.value,
  );
  filter.tempoMin = tempoMin.value === 0 ? undefined : tempoMin.value;
  filter.tempoMax = tempoMax.value >= 400 ? undefined : tempoMax.value;
  filter.lengthMin = lengthMin.value === 0 ? undefined : lengthMin.value;
  filter.lengthMax = lengthMax.value >= 400 ? undefined : lengthMax.value;
  filter.tags = excludeDanceTags.value
    ? `^${tagString.value}`
    : tagString.value;
  filter.level = level ? level : undefined;
  return filter;
});
```

Notable details for anyone adding a new advanced-search field:

- **Every field composition goes through the sub-query class's own builder** —
  `DanceQuery.fromParts`, `UserQuery.fromParts`, `SongSort.fromParts`, `SongFilter.joinPurchase` —
  never manual string concatenation of the filter cell itself.
- **Range fields use sentinel-at-default, not the literal value**: `tempoMin`/`tempoMax` only
  populate the filter when they differ from the slider's full range (`0`/`400`); same for
  `lengthMin`/`lengthMax` (`0`/`600`, though the assembly code's upper check is `>= 400`, a
  pre-existing mismatch with the slider's declared `600` max — worth confirming before relying on
  length-max filtering at the top of the range).
- `songFilter.value.description` is rendered live in a `BAlert` above the submit button — this is
  the same `Description` getter documented in the sub-query table, giving the user a plain-English
  preview before they search.
- The `showDiagnostics` query param (`?showDiagnostics=1`) reveals a raw dump of every ref plus
  `songFilter` and `songFilter.query`, useful for debugging a filter that isn't matching what the
  form shows.

### Submission

```ts
async function onSubmit(): Promise<void> {
  ...
  const query = songFilter.value.encodedQuery;
  window.history.replaceState(null, "", window.location.pathname + `?filter=${query}`);
  window.location.href = `${loc.origin}/song/filtersearch?filter=${query}`;
}
```

`encodedQuery` is `encodeURIComponent(this.query)` — the full wire-format string from above. This
is a hard navigation (`window.location.href`, not an API call): the browser round-trips to
`SongController.FilterSearch`, which re-enters the standard filter-driven search pipeline
documented in [[song-search-results]] (`DoAzureSearch()` → [[song-search-service]] →
`FormatSongList()` → `song-index` Vue page). `FilterSearch` itself only special-cases an explicit
`page` query param (pagination, see [[song-search-results]]'s "Pagination Convention"); otherwise it
re-parses whatever `Filter` the query string already encodes and searches unchanged.

---

## From `SongFilter` to Azure `SearchOptions`

Once the server has a rehydrated `Filter` (via `GetFilterFromContext` → `SongFilter.Create`/parsed
constructor), two independent things are derived from it before the query reaches Azure:

### 1. The OData filter — `SongFilter.GetOdataFilter(DanceMusicCoreService dms)`

```csharp
public virtual string GetOdataFilter(DanceMusicCoreService dms)
{
    var usingSingleDanceTempo = SingleDanceId != null &&
        (TempoMin.HasValue || TempoMax.HasValue || SongSort.Id == SongSort.Tempo);
    var tempoFieldPath = usingSingleDanceTempo
        ? $"dance_{SingleDanceId}/{SongIndex.DanceTempoSubField}"
        : Song.TempoField;
    return BuildOdataFilter(dms, tempoFieldPath);
}
```

`BuildOdataFilter` ANDs together, in order, whichever of these are non-null:

1. A numeric-sort guard (`(field ne null) and (field ne 0)`) when sorting by a numeric field, so
   unset values don't cluster at one end.
2. `DanceQuery.GetODataFilter(dms)` (or `RawDanceQuery`'s passthrough for raw filters) — per-dance
   `dance_{id}/Votes ge|le {threshold}` clauses, ORed within an expanded dance group and combined
   `and`/`or` across selected dances per `IsExclusive`. Each dance's own `TagQuery` (from
   `DanceQueryItem.TagQuery`) is folded in here via `TagQuery.GetODataFilterForDanceField`.
3. `UserQuery.ODataFilter` — `Users/any(t: t eq '{user}|{modifier}')`-shaped clauses (`null` for
   identity queries, which are resolved to a concrete user elsewhere — see [[song-search-service]]).
4. Tempo range, using the single-dance-tempo field path from above when applicable so a per-dance
   tempo override (rather than the song's base tempo) is what gets range-checked.
5. Length range (`Song.LengthField`).
6. `ODataPurchase` — service-availability clauses built from `Purchase`'s include/exclude split.
7. `TagQuery.GetODataFilter(dms)` — song-level tag clauses, with tag-ring expansion
   (`dms.GetTagRings`) so a tag search also matches its synonyms/related tags.
8. A comments-only guard (`Comments/any()`) when `SortOrder` is the comments sort.

`DanceMusicCoreService` is threaded through for tag-ring expansion and dance-tag OData generation —
it's why `GetOdataFilter` takes a service reference rather than being a pure function of the filter
alone.

### 2. Sort, paging, and query mode — `SongIndex.AzureParmsFromFilter(SongFilter filter, int?

pageSize)`

```csharp
public SearchOptions AzureParmsFromFilter(SongFilter filter, int? pageSize = null)
{
    pageSize ??= 25;
    if (filter.IsRaw)
    {
        return new RawSearch(filter).GetAzureSearchParams(pageSize);
    }
    var order = filter.ODataSort;
    var odataFilter = filter.GetOdataFilter(DanceMusicService);
    var useLucene = filter.KeywordQuery.IsLucene;
    var ret = new SearchOptions
    {
        QueryType = useLucene ? SearchQueryType.Full : SearchQueryType.Simple,
        SearchMode = useLucene ? SearchMode.All : SearchMode.Any,
        Filter = odataFilter,
        IncludeTotalCount = true,
        Size = pageSize,
        Skip = ((filter.Page ?? 1) - 1) * pageSize,
    };
    ret.OrderBy.AddRange(order);
    return ret;
}
```

`filter.ODataSort` (on `SongFilter`) has its own small dispatch: a dance-vote sort with a single
selected dance sorts by that dance's tempo sub-field instead of the base `Tempo` field when the sort
id is `Tempo`; a `Dances` sort defers to `DanceQuery.ODataSort`/`RawDanceQuery.ODataSort` (per-dance
`Votes` field, or the aggregate `dance_ALL/Votes` when multiple/no dance is selected); a `Comments`
sort maps to `Modified` (there's no dedicated comment-timestamp field); everything else uses
`SongSort.OData` directly.

This is also the point where raw filters diverge completely — `RawSearch.GetAzureSearchParams` uses
the filter's `Dances` cell as a literal OData string and skips `BuildOdataFilter` entirely, since a
raw query's "filter" _is_ already Azure OData syntax by construction.

### 3. Handoff to `SongSearch`

`AzureParmsFromFilter`'s output (`SearchOptions`) plus `filter.SearchString` and `filter.CruftFilter`
are what `SongIndex.Search()` passes to `DoSearch()` → `Client.SearchAsync<SearchDocument>(...)` —
the actual Azure REST call. This is normally reached through `SongSearch.Search()`, which wraps it
with premium gating, user-query anonymization, and search logging before calling it — see
[[song-search-service]] for that full sequence, including the `VoteSearch`/`PostSearch` in-memory
fallback used when the query includes vote scoping that Azure can't filter on directly.

---

## Results Rendering

Once `SongSearch.Search()` returns a `SearchResults`, `SongController.FormatSongList()` converts it
to a `SongListModel` and renders the `song-index` Vue page — this is the part [[song-search-results]]
already documents in full (`SongHistory` anonymization, `SongFilterSparse`, `SearchRequestDiagnostics`
for the `DiagRole`, pagination via `SongFooter`). One filter-specific detail worth calling out here:
`SongFilterSparse` (`m4dModels/SongFilter.cs`, top of file) is a flat DTO with the same field names
as `SongFilter` (`AutoMapper`-mapped both directions via `SongFilterProfile`) — it's what actually
serializes into the page's JSON model for the client `song-index` page to read back into a
`SongFilter.ts` instance (via `TypedJSON.parse`) and use for follow-up requests (sort-order links,
pagination, "search within these results"), without re-exposing any server-only computed properties.

---

## CustomSearchController: Canned Raw Filters

`CustomSearchController` (`m4d/Controllers/CustomSearchController.cs`) serves the Holiday,
Halloween, and Broadway "seasonal music" pages (`/customsearch?name=holiday|halloween|broadway`,
optionally `&dance={danceName}&page={n}`) — canned SEO landing pages, not user-driven search. It's
a second, independent producer of `SongFilter` and a second, independent consumer of `SongSearch`,
parallel to (but not routed through) the Advanced Search / `SongController` pipeline documented
above.

### Building the filter

```csharp
Filter = Database.SearchService.GetSongFilter().CreateCustomSearchFilter(name, dance, page);
```

`SongFilter.CreateCustomSearchFilter` (`m4dModels/SongFilter.cs`) hand-builds a **raw** filter —
there's no form, so there's nothing to compose from sub-query classes:

```csharp
public virtual SongFilter CreateCustomSearchFilter(string name = "holiday", string dance = null, int page = 1)
{
    var holidayFilter = name.ToLowerInvariant() switch
    {
        "halloween" => "OtherTags/any(t: t eq 'Halloween')",
        "holiday" or "christmas" =>
            "(OtherTags/any(t: t eq 'Holiday') or GenreTags/any(t: t eq 'Christmas' or t eq 'Holiday')) " +
            "and OtherTags/all(t: t ne 'Halloween')",
        "broadway" => "GenreTags/any(t: t eq 'Broadway') or ... 'Show Tunes' ... 'Musicals' ...",
        _ => throw new Exception($"Unknown holiday: {name}"),
    };
    // dance == null: danceSort = "dance_ALL/Votes desc"
    // dance != null: danceFilter = DanceTags/any(...), danceSort = "dance_{id}/Votes desc"
    var odata = string.IsNullOrWhiteSpace(dance) ? holidayFilter : $"{danceFilter} and ({holidayFilter})";

    return new SongFilter(
        new RawSearch
        {
            ODataFilter = odata,
            SortFields = danceSort,
            Page = page,
            Flags = danceFilter == null ? "" : "singleDance"
        },
        "customsearch"
    );
}
```

`name` selects one of three hardcoded OData clauses (matched against `OtherTags`/`GenreTags`,
independent of the `TagQuery` tag-ring machinery used elsewhere — these tag names are literal, not
resolved through `dms.GetTagRings`). An optional `dance` name adds a `DanceTags/any(...)` clause and
switches the sort to that dance's own vote field; without a dance, everything sorts by aggregate
`dance_ALL/Votes`. The `"singleDance"` flag is what lets `RawDanceQuery.SingleDance` (see "Raw
filters" above) recognize the dance-scoped variant for display purposes. The resulting `SongFilter`
has `Action = "customsearch"`, which is why `IsRaw` is true for it (`SongFilter.IsRaw` explicitly
checks for the `customsearch` action, not just `azure+raw*`) — it goes through
`RawSearch.GetAzureSearchParams`, not `BuildOdataFilter`, same as any other raw filter.

### Search execution

The controller constructs `SongSearch` itself, directly, rather than going through
`SongController.DoAzureSearch()`:

```csharp
var results = await new SongSearch(
    Filter, UserName, IsPremium(), SongIndex, UserManager, TaskQueue, ServiceHealth).Search();
```

This still goes through the full `SongSearch.Search()` sequence from [[song-search-service]] —
premium gating, user-query resolution, `LogSearch`, and the same error handling for an unavailable
Azure client — but the surrounding bot-filter check (`SpiderManager.CheckAnySpiders`, guarded by
`!Filter.IsEmptyBot`) and the `IsSearchAvailable()`/`InvalidOperationException`/`RedirectException`
handling are each **reimplemented locally** in `CustomSearchController.Index` rather than reused
from `SongController.DoAzureSearch()` — the two controllers don't share a base method for this,
only the same helpers (`IsSearchAvailable`, `IsSearchServiceError`, `HandleSearchServiceError`) off
`ContentController`. Keep this in mind if `DoAzureSearch`'s error handling changes — the same fix
likely needs to land here too.

### Rendering

Results render through a **separate** model and Vue page, not `song-index`:

- `CustomSearchModel` (`m4d/ViewModels/SongListModel.cs`) extends `SongListModel` with `Name`,
  `Description` (a hardcoded per-occasion string, e.g. `"'Halloween'"` or `"'Broadway' or 'Broadway
And Vocal' or 'Musical' or 'Show Tunes'"`), `Dance`, and `PlayListId`.
- `Filter` still round-trips through the same `SongFilterSparse`/`AutoMapper` mapping described
  under "Results Rendering" above (`Mapper.Map<SongFilterSparse>(Filter)`), so the client-side
  `SongFilter.ts` reconstruction and pagination links work identically to the main search pipeline.
- `PlayListId` is looked up separately (`Database.PlayLists` by name/`PlayListType.SpotifyFromSearch`)
  and rendered by a `SpotifyPlayer` embed on the `custom-search` Vue page
  (`m4d/ClientApp/src/pages/custom-search/App.vue`) when present — see [[playlist-management]] for
  how those per-dance seasonal playlists get created and kept in sync.
- The `custom-search` page itself (title/breadcrumbs, a dance-scoped vs. all-dances blurb,
  `CustomSearchDanceChooser` for switching dances, `CustomSearchHelp` for the zero-result case) has
  its own layout distinct from `song-index`'s `SongLibraryHeader`/`SearchHeader` split, but reuses
  the same `SongTable`/`SongFooter`/`AdminFooter` components.

### Other entry points

- `SongController.HolidayMusic(occassion, dance, page)` is a permanent redirect
  (`RedirectToActionPermanent("Index", "CustomSearch", ...)`) — the route this doc's
  `song-search-results.md` companion lists as "not actually handled here." It exists for backward
  compatibility with old `/song/holidaymusic` links.
- `SiteMapInfo.cs` lists the three canned URLs (`customsearch?name=holiday|halloween|broadway`) as
  crawlable sitemap entries — these are meant to be indexed, unlike most filter-driven search URLs.
- `PlayListController`'s bulk-create flow (`m4d/Controllers/PlayListController.cs`, around line 534)
  calls the same `CreateCustomSearchFilter(occassion, ds.DanceName)` to build the search each
  per-dance seasonal Spotify playlist is populated from — a third caller of the same filter builder,
  for playlist sync rather than page rendering. See [[playlist-management]] for that flow.

---

## Related Code

| File                                                               | Purpose                                                                                                                                                   |
| ------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `m4dModels/SongFilter.cs`                                          | `SongFilter`, `SongFilterSparse`, `SongFilterProfile`, wire-format parse/serialize, `GetOdataFilter`/`BuildOdataFilter`, `Description`/`ShortDescription` |
| `m4d/ClientApp/src/models/SongFilter.ts`                           | Client mirror: parse/serialize, `description`, `url`, `isSimple`/`isDefault`                                                                              |
| `m4dModels/DanceQuery.cs`, `DanceQueryItem.cs` / `.ts` equivalents | Dance selection sub-query and OData generation                                                                                                            |
| `m4dModels/RawDanceQuery.cs` / `RawDanceQuery.ts`                  | Best-effort dance view over a raw OData filter                                                                                                            |
| `m4dModels/TagQuery.cs` / `TagQuery.ts`                            | Tag include/exclude sub-query, OData per tag class                                                                                                        |
| `m4dModels/UserQuery.cs` / `UserQuery.ts`                          | User/vote scoping sub-query                                                                                                                               |
| `m4dModels/KeywordQuery.cs` / `KeywordQuery.ts`                    | Free-text/Lucene search sub-query                                                                                                                         |
| `m4dModels/SongSort.cs` / `SongSort.ts`                            | Sort field + direction sub-query                                                                                                                          |
| `m4dModels/RawSearch.cs`                                           | Raw-filter form model, `GetAzureSearchParams`                                                                                                             |
| `m4dModels/SearchServiceInfo.cs`                                   | `ISearchServiceManager.GetSongFilter` — picks `SongFilter` vs `SongFilterNext` by index schema version                                                    |
| `m4dModels/SongIndex.cs`                                           | `AzureParmsFromFilter`, `AddCruftInfo`, `DoSearch`, `Search`                                                                                              |
| `m4d/ClientApp/src/pages/advanced-search/App.vue`                  | Advanced Search form: builds a `SongFilter` from UI state, submits via hard navigation to `FilterSearch`                                                  |
| `m4d/Controllers/SongController.cs`                                | `AdvancedSearchForm`, `FilterSearch`, legacy `AdvancedSearch`, `HolidayMusic` redirect; see [[song-search-results]] for the rest of the action surface    |
| `m4d/Services/SongSearch.cs`                                       | Orchestration layer consuming the built `SearchOptions` — see [[song-search-service]]                                                                     |
| `m4d/Controllers/CustomSearchController.cs`                        | Holiday/Halloween/Broadway canned pages: builds the raw filter, calls `SongSearch` directly, renders `custom-search`                                      |
| `m4d/ViewModels/SongListModel.cs`                                  | `CustomSearchModel` (`SongListModel` + `Name`/`Description`/`Dance`/`PlayListId`)                                                                         |
| `m4d/ClientApp/src/pages/custom-search/App.vue`                    | Vue page for canned seasonal search results, distinct from `song-index`                                                                                   |
| `m4d/Controllers/PlayListController.cs`                            | Bulk-create flow reuses `CreateCustomSearchFilter` to populate seasonal Spotify playlists — see [[playlist-management]]                                   |
