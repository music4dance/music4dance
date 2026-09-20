# Individual Artists and the Artist Index

**Status:** built and running against the **test** search index behind the `ArtistIndex` feature
flag. Production has not been rolled out yet — see [§11 Operations](#11-operations).

A song's `Artist` field is a credit string exactly as some music service wrote it: `Pitbull feat.
Ne-Yo`, `Duke Ellington and His Orchestra`, `Dolly Parton & Kenny Rogers`. That string is fine to
display and useless to browse: there was no way to ask "what else has Kenny Rogers done", because
his songs were spread across every credit string he happened to appear in.

This describes the machinery that derives a list of **individual artists** from each credit, stores
it alongside the credit, and uses it for artist pages and a browsable artist index.

Related: [artist-pages.md](artist-pages.md),
[song-internal-format.md](song-internal-format.md),
[search-index-versioning.md](search-index-versioning.md),
[playwright-e2e-testing.md](playwright-e2e-testing.md),
[meta-crawler-mitigation.md](meta-crawler-mitigation.md).

---

## 1. Overview

```
Artist credit string ──► ArtistSplitter ──► Artists (ordered list)
   "Pitbull feat. Ne-Yo"                     ["Pitbull", "Ne-Yo"]
```

The derived list is a **song property** (`Artists`), replayed from the property log like every
other, and an **index field** (`Artists`), a filterable/facetable/searchable string collection.

Four things write it:

| Writer | When | Precedence |
| ------ | ---- | ---------- |
| A person | Editing the song | Highest — never overwritten automatically |
| A music service (`batch-*`) | Import, from Spotify's structured credits | Beats the heuristic |
| `artist-bot` | Save hook, and the backfill job | Lowest |
| Nobody | Most songs | `EffectiveArtists` falls back to the cleaned credit |

The credit itself is never rewritten. `Artists` is additional, not a replacement, so the song page
still shows what the service actually said.

---

## 2. The `Artists` Property

`Song.Artists` is an ordered `IReadOnlyList<string>`, serialized into the property log as one
pipe-delimited scalar:

```
.Edit=	User=artist-bot|P	Time=…	Artists=Pitbull|Ne-Yo
```

One scalar rather than one property per artist, because the list is ordered (the primary artist
comes first), it is replaced as a unit, and a scalar gets the existing last-writer-wins replay
semantics for free.

### 2.1 Source precedence

`Song.ArtistsSource` is derived during replay, not stored:

| Source | Set by | Meaning |
| ------ | ------ | ------- |
| `User` | Any real user | Hand-corrected. The bot and the batch both skip the song. |
| `Service` | A `batch-*` pseudo user | From a service's structured credits. The bot skips it. |
| `Heuristic` | `artist-bot` | Derived. Safe to recompute and overwrite. |
| `None` | — | No list; `EffectiveArtists` returns the cleaned credit. |

`Song.UpdateArtists` is the single place that applies this, so the save hook and the batch job
cannot drift apart. It returns `false` — changing nothing — when the source is `User` or `Service`,
or when the new list equals the old one.

### 2.2 `EffectiveArtists`

```csharp
if (Artists != null) return Artists;
var credit = ArtistSplitter.CleanName(Artist);
return credit.Length == 0 ? [] : [credit];
```

Everything that displays or indexes artists reads `EffectiveArtists`, so a song with no derived
list still behaves like a one-artist song. A consequence worth remembering: **any full re-save
populates the index field**, whether or not the splitter found anything, which is why a restore
into a freshly built index arrives fully populated.

Songs with no credit at all produce an empty list and never match `Artists/any()`. There are 487 of
them, which is why a complete backfill reads 103,546 of 104,033 rather than 100%.

### 2.3 Credit changes invalidate

If a later edit changes the `Artist` credit, a derived list computed from the old credit is stale,
so replay clears it. Only a write that changes the *cleaned* credit counts — services routinely
rewrite `Artist` with an identical value, and treating those as changes would clear good lists
constantly.

### 2.4 Client parity

`m4d/ClientApp/src/models/Song.ts` replays the same rules, because the song editor applies edits
locally before saving. C# and TypeScript share one JSON file of replay cases
(`artists-replay-cases.json`) so the two implementations cannot drift.

---

## 3. The Splitter

`m4dModels/ArtistSplitter.cs`. Pure, synchronous, no I/O. `ArtistSplitter.Version` is bumped
whenever its behaviour changes, so a backfill can be re-run and the reports compared.

```csharp
ArtistSplit Split(string artist, string title = null, IArtistKnowledge knowledge = null)
```

It returns the artists, the `Rules` that fired, and any `Unresolved` separators it saw but declined
to split — the last of which is what drives tuning.

### 3.1 Rules that need no evidence

These are unambiguous markers:

- **`feat.` in the credit** — `Pitbull feat. Ne-Yo`, `ft.`, `w/`, `featuring`.
- **`feat.` in the title** — `Hold My Heart (feat. ZZ Ward)` credited to `Lindsey Stirling`. This is
  the single biggest source: 3,011 songs.
- **Semicolons** — classical credits, `Marcus Creed; RIAS Sinfonietta`.
- **Spaced slashes**, **comma lists**, and lists inside a feat. clause.

### 3.2 Rules that read the corpus

Ambiguous joins (`&`, `+`, `and`, `y`, `e`, `et`, `und`, `x`, `vs`, `with`) are band names as often
as they are collaborations, so most need evidence — an `IArtistKnowledge` lookup answering "does
this name have songs of its own?". In the batch this is built from a pass over the whole index; in
the save hook it is one facet query.

- **Leader and backing group** — a join followed by a possessive (`and His Orchestra`, `y Su
  Charanga`, `und sein Tanzorchester`) or by nothing but generic ensemble words (`with Orchestra`,
  `and Singers`) keeps **only the leader**, no evidence needed. `Duke Ellington and His Orchestra`
  → `[Duke Ellington]`. Anything credited after the group is split separately: `Teddy Wilson And
  His Orchestra With Billie Holliday` → `[Teddy Wilson, Billie Holliday]`.
- **Named ensembles** — a multi-word name containing an ensemble word (Orchestra, Philharmonic,
  Symphony, Sinfonietta, Choir, Trio, Collegium…) joined to another multi-word name always splits:
  `London Symphony Orchestra and Árpád Joó` → both.
- **One known side** — a join splits when either half has songs of its own, provided **both halves
  are multi-word**. One-word names collide with unrelated artists far too often (`Fitz and The
  Tantrums` would split on an unrelated "Fitz"). For article joins (`Bob Marley & The Wailers`) the
  known half must be the leader.
- **Both sides known** — `Dolly Parton & Kenny Rogers`.

### 3.3 What it deliberately refuses

- **Protected acts** — a curated list in code: `Simon & Garfunkel`, `Earth, Wind & Fire`, `Little
  Feat`, `Dimitri Vegas & Like Mike`, `Harry Connick, Jr.`
- **Act nouns after the join** — `& Sons`, `& The Gang`, `& Friends`.
- **An article on both sides** — `The Mamas & The Papas`.
- **Joins inside brackets** — `BnB (Blanco & Black)` is one act's alias.
- **Two single words outside a feat. clause** — `Rodrigo y Gabriela`, `Sonny & Cher`. Reported as
  `duo`, and the largest remaining unresolved category at 389 credits.
- **`Various` / `Various Artists`** — left exactly as they are. The artist index hides them from
  browsing. Asking a service for the real artists is a possible future pass.

### 3.4 Name cleanup

Every produced name goes through a final pass that strips a role marker, conjunction or
backing-group label the segmenting left in front of it (`Featuring Hilary Alexander`, `With Lester
Young`, `His Band Ross Mitchell`) and drops a name that is nothing but such a label (`His
Orchestra`, `Chorus`). Without it the browsable index accumulates entries that are not artists.

`ArtistSplitter.ArtistKey` folds case and diacritics (NFD, strip non-spacing marks) and is what
artist-page matching compares on. Note that it does **not** fold `Ø` — `LØLØ` and `Lolo` are
different keys.

---

## 4. Accuracy

Both harnesses are manual-only MSTest classes in `m4dModels.Tests`, excluded from `Server: Test`,
and read an `/Admin/IndexBackup` file named by `M4D_ARTIST_ANALYSIS_INDEX`. See
[§12 Analysis harnesses](#12-analysis-harnesses) for how to run them.

### 4.1 Coverage, against the 2026-09-16 backup

104,033 songs, 31,967 distinct credits. **7,538 songs (7.2%) split**, from 3,932 distinct credits,
yielding 32,545 distinct individual artists.

By rule: title feat. 3,011 · leader 2,082 · evidence 852 · one known side 657 · named ensemble 407 ·
artist feat. 359 · comma list 352 · semicolon 339 · slash 33.

`unresolved.tsv` holds 1,318 credits the splitter saw a separator in and declined to split; 904
excluding the `duo` label. Most are genuine act names or a leader-and-band credit whose leader has
no standalone songs.

### 4.2 Measured against Spotify

`ArtistSplitterGroundTruth` samples credits, fetches `GET /v1/tracks` in batches of 50, and compares
`track.artists[]` to the splitter's output. Against 1,600 credits:

- **Agreement runs 80–93% of credits per rule** (77–97% song-weighted), counting exact matches plus
  cases where Spotify names the same act at a different length (their `Duke Ellington & His
  Orchestra` against our `Duke Ellington`).
- **`(not split)` scores 97.4%** — we are not splitting things we shouldn't, which was the main risk.
- **The `extra` verdict is not error.** All 57 cases are Spotify being *less* complete: crediting
  only a track's lead artist, or one member of a comma list. Counting those as agreement puts most
  rules above 95%.
- **21 of 1,600 are real misses**, mostly Spotify counting a remixer as an artist.
- Most `differ` rows are name variants rather than splitting mistakes: our own typos, stylization
  (`P!nk`, `A$AP Ferg`), or a track id pointing at a different recording.

Three things that report gets right, worth preserving if it is ever rewritten:

1. **Two samples.** Precision is sampled per rule so popular credits can't dominate; recall is
   sampled separately across all credits, because the rule buckets over-represent exactly the
   credits recall is asking about. Both are seeded and reproducible.
2. **Names are compared more loosely than the index matches them** — punctuation, conjunctions and
   leading articles folded — so `Earth, Wind & Fire` and `Earth Wind And Fire` agree. Reconciling
   spellings is a separate problem; counting it here would bury real mistakes.
3. **A collaborator the credit never mentions is not a precision failure.** Those are reported as
   `incomplete` and excluded from the per-rule denominator.

### 4.3 The ceiling

In an unbiased 400-credit sample, Spotify lists 2+ artists for **27.8% of credits** — but **80% of
those credits contain no separator at all**. Spotify says `J Balvin | Chencho Corleone`; our credit
string says `J Balvin`, and no heuristic can recover the difference.

So the splitter is near its ceiling: it catches 18.9% of the collaborations Spotify knows about, and
about 20% is all a separator-based approach can reach. **Everything beyond that has to come from
service data, not a better heuristic** — see [§13](#13-known-limits-and-deferred-work).

---

## 5. Search Index Integration

### 5.1 The field

```csharp
new(Song.ArtistsField, SearchFieldDataType.Collection(SearchFieldDataType.String))
{
    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
}
```

It is in `SongIndex.BuildIndexFields()`, so any freshly built index has it, and it was added to
existing indexes **in place** via `CreateOrUpdateIndex` — no version migration. Azure allows adding
fields; existing documents read the new field as null.

Two Azure behaviours this design depends on, both confirmed against current docs:

- In `any()` lambdas over `Collection(Edm.String)`, only `eq` and `search.in` are allowed, combined
  with `or` — **no range operators**
  ([reference](https://learn.microsoft.com/en-us/azure/search/search-query-odata-collection-operators)).
  A–Z bucketing therefore cannot be done in the index, which is why [§9](#9-artist-index-page) uses
  an in-memory snapshot.
- A suggester **cannot take a field that already existed**: "you have to rebuild the index if you
  want to add them to a suggester"
  ([reference](https://learn.microsoft.com/en-us/azure/search/index-add-suggesters)). Adding
  `Artists` to the site-wide `songs` suggester therefore waits for the next versioned index —
  [#277](https://github.com/music4dance/music4dance/issues/277). The artist index page's own
  type-ahead doesn't wait on it and doesn't want it: it answers from the snapshot
  ([§9.4](#94-type-ahead)).

### 5.2 Capability detection

`SearchServiceInfo.HasFieldAsync(fieldName, isNext)` reads the live schema and caches the field-name
set for **10 minutes** (`SchemaCacheDuration`), so every instance notices a newly added field without
a restart. A failed read caches "absent" for one minute — the safe direction, since omitting a field
is harmless and writing an unknown one fails the whole batch.

Anything that changes a live schema drops the cache: `AddMissingIndexFields`, and
`CreateIndexAsync` / `CreateOrUpdateIndexAsync` / `DeleteIndexAsync`. That last group matters more
than it looks — **Reload the Index** resets an index and reloads it in one action, and without
invalidation the reload asks about the schema of the index it just deleted, then uploads 100K
documents that all omit `Artists`.

### 5.3 Two switches, and the trap

| Switch | Controls | Source |
| ------ | -------- | ------ |
| **Write** | `DocumentFromSong` includes `Artists` | `HasArtistsFieldAsync()` — on as soon as the field exists |
| **Read** | `FindArtist` matches individual artists; the index page and song-detail chips are live | The field exists **and** the `ArtistIndex` feature flag is on |

They are separate because between adding the field and finishing the backfill, most documents have
`Artists = null`. If artist pages switched over immediately they would lose nearly every song.

**The trap: `Upload` is a full document replace.** An instance whose schema cache still says
"absent" uploads documents *without* `Artists`, nulling values the backfill already wrote. Hence:
add the field, wait out the cache (or restart) before backfilling, and verify coverage afterwards.

### 5.4 Query path

`FindArtist(name, cruft, individualArtists)` does an analyzed **phrase search** on `Artists`, then
post-filters on `ArtistSplitter.ArtistKey` so matching is case- and diacritic-insensitive without a
second index field. The phrase search alone would also match a name inside a longer name (`Tony
Evans` inside `Tony Evans and His Orchestra`); the key comparison rejects those. If it finds
nothing it falls back to the old credit phrase search, so the feature degrades rather than breaking.

Results are capped at 1,000 songs. The most prolific artist in the corpus has 356.

---

## 6. Backfill and Admin Operations

`Admin/BatchArtists`, from **Admin → Initialization Tasks**, one set of buttons per index.

| Mode | Does |
| ---- | ---- |
| `Report` | Streams every song, computes what would change, writes nothing |
| `Apply` | Re-saves **every** song — what populates the field across an index |
| `ApplyChanged` | Re-saves only songs whose list changed — for re-runs after heuristic changes |

It runs as an `AdminMonitor` background task, logs the first 200 changes as `[before] -> [after]`,
and finishes with `Tried / Changed / HumanOrServiceLists`.

Every mode creates the `artist-bot` pseudo user if missing, which also activates the save hook — so
`Report` is read-only with respect to song data but not entirely without effect.

### 6.1 Streaming

It reuses `SongIndex.StreamAllSongsAsync`, which key-set paginates on `Modified desc, SongId desc`
(the `$skip` path fails past 100,000 rows, and the corpus is ~104K).

Writing while streaming that ordering is safe, for a reason worth writing down: the cursor walks
newest to oldest and each page asks for rows strictly *older* than it, so appending a bot edit sets
`Modified` to now and moves that song *above* the cursor, into the range already passed. It is
never revisited and never skipped. Unchanged songs re-uploaded by `Apply` keep their `Modified`
(`DocumentFromSong` copies it), so they don't move at all. The one gap is a *user* editing a song
the job hasn't reached, which moves it out of range — and the save hook covers that song anyway.

### 6.2 No partial merges

`Apply` re-saves whole documents rather than merging `{SongId, Artists}`. A merge path would be
cheaper for the ~93% of songs whose log doesn't change, but it is a second write path to keep
correct, and the full re-save is a known quantity that reuses **Reload All Songs**.

### 6.3 Verifying coverage

Initialization Tasks reports, per index, the count of documents matching `Artists/any()` against the
index total (`SongIndex.ArtistsCoverageAsync`), alongside whether the field is present and when this
instance will re-read the schema.

After a successful `Apply` the two should match except for the 487 songs with no credit. A larger
shortfall means the [§5.3](#53-two-switches-and-the-trap) trap: documents uploaded by an instance
that still thought the field was absent. Re-running `Apply` repairs it.

---

## 7. Save Hook

`SongIndex.UpdateArtists(IEnumerable<Song>)` runs on every save path (`SaveSong` delegates to
`SaveSongs`), so a song created or edited through the UI, the API, or an import gets its artists
derived immediately rather than waiting for the next batch.

Three deliberate properties:

- **Dormant until `artist-bot` exists.** Deploying the code changes nothing on its own. This also
  avoids history showing edits by a user the renderer doesn't know — `UserMapper.AnonymizeHistory`
  renders unknown users as `*UNAVAILABLE*` to anonymous visitors.
- **It never blocks a save.** Any failure is logged and swallowed; the batch job catches up.
- **Evidence comes from one facet query** (`GetArtistKnowledge`), and only for credits containing an
  ambiguous separator. Before the index field exists, new songs get evidence-free splits only.

Because re-splitting happens here, on the server, the client's own state after a save does not
reflect it. Anything verifying a derived list has to reload.

---

## 8. Song Details

`ArtistCredit.vue` renders the credit **verbatim** and overlays links: each stored artist is located
as a substring of the credit and only those spans become links. So `Duke Ellington and His
Orchestra` displays in full with just *Duke Ellington* linked. Names not found in the credit —
typically pulled from a title's `feat.` clause — are appended as `(with X, Y)`.

Matching is plain substring, longest-first is *not* applied, so a short name processed early can
land inside a longer word (`Sam` inside `Samantha`). Rare and cosmetic.

`ArtistsEditor.vue` lets `dbAdmin`, `canEdit` or the song's creator correct the list as tags, with
**Reset to automatic** (hand it back to the splitter) and **Don't split** (collapse to the credit).
The credit itself is editable by the same set; the title remains `dbAdmin`-only.

These are UI gates. The song PATCH endpoint is `[Authorize]` with no per-field role check, as it has
always been — history and attribution are the safety net.

---

## 9. Artist Index Page

`/song/artists`, gated on the `ArtistIndex` flag. Letter navigation, server-side search, and a
minimum-song-count toggle (default 2).

**A search that matches exactly one artist redirects to that artist's page** rather than rendering
an index holding a single link — `ArtistIndexModel.SoleSearchMatch` decides, `SongController.Artists`
acts on it. It's a search-only rule: a letter bucket that happens to hold one artist is still a
browse, and its listing is the answer. "One match" means one entry the page would have shown, so
the song-count threshold counts too — the same search can redirect with singles included and find
nothing without them. The redirect is built by hand as the lowercase `/song/artist?name=…` that
`artistPageUrl` emits everywhere else, not route-generated as `/Song/Artist?name=…`.

### 9.1 Why a snapshot

A–Z bucketing can't be expressed as an index filter ([§5.1](#51-the-field)), and faceting ~32,500
distinct values per request is not sensible. So `ArtistIndex.Build` groups a single streaming pass
over the corpus by `ArtistKey`, keeps the most common spelling of each, and sorts on a key with
leading articles stripped.

`Various Artists`, `Various`, `Unknown` and `VA` are excluded from browsing.

### 9.2 The cache

`ArtistIndexCache` holds one snapshot **per app instance**:

- Served from memory, or failing that from the snapshot on disk ([§9.6](#96-the-snapshot-on-disk)).
  Only when there is neither does a visitor wait for a build - up to 10s, and otherwise see "we're
  putting the artist index together".
- **Lifetime 6 hours**, stale-while-revalidate: once expired, the next visitor gets the stale
  snapshot immediately while a rebuild runs.
- Builds run in the background, one at a time, and **never fail a request**. Everything that can
  throw - including acquiring the transient service, which is what fails when the database is
  unreachable - is inside the build task. A failure leaves the existing snapshot in place and is
  not retried for `RetryAfterFailure` (5 minutes), so an outage doesn't turn every request into a
  fresh streaming pass.
- A build that returns **zero** artists is treated as a failure when a non-empty snapshot is
  already held. An index mid-rebuild answers every page with no results without erroring, and
  publishing that would blank the page and then write the blank to disk.
- `BatchArtists` invalidates it after `Apply`/`ApplyChanged`, so a backfill is visible at once on
  the instance that ran it. Other instances wait out their own 6 hours.

There is no timer: refresh is request-driven, so a new song can take up to 6 hours *and a visit* to
appear.

### 9.3 Crawlers and SEO

Splitting credits turns one artist page into ~32,500. Decisions:

- **No `sitemap.xml`.** A sitemap invites crawling rather than limiting it, so it is the wrong tool
  for a fan-out problem. The site has a user-facing site map page and no machine-readable one.
- **Thin artist pages ask not to be indexed, but to be followed.** Under
  `MinimumSongsToIndexArtist` (5) songs, `SongController.Artist` sets `ViewData["Robots"] =
  "noindex, follow"`: the page isn't worth indexing, the songs it links to are. That leaves about
  5,100 artist pages indexable out of 32,500.
- **That directive is gated on the flag**, along with the fan-out that justifies it. Ungated, it
  would de-index the thin pages that already exist — the ones whose whole-credit match finds fewer
  than 5 songs — as soon as the code deployed, with the feature still off. De-indexing is cheap to
  emit and slow to undo once a crawler has acted on it, so it waits for the flag like everything
  else.
- `_head.cshtml` takes a `ViewData["Robots"]` string for this. `ViewData["NoIndex"]` and the
  off-site host check still force `noindex, nofollow`, which is what staging and identity pages want.

A sitemap for the ~5,100 indexable pages is a reasonable later project; `robots.txt` has no
`Sitemap:` line to hook it to yet.

### 9.4 Type-ahead

The search box suggests artists as you type, from `GET /api/suggestion/artist?q=…&all=…`
(`SuggestionController.Artist` → `ArtistSuggest.vue`).

**It does not use an Azure suggester**, and deliberately so. Azure can only attach a suggester to a
field when the index is created ([§5.1](#51-the-field)), so the obvious implementation would have
cost a full index rebuild — and would have been worse:

| | Azure suggester on `Artists` | The snapshot |
| --- | --- | --- |
| Index rebuild | Required | None |
| What it suggests | Raw per-document field values | The entries the page can actually show |
| Spelling variants | Each one separately | Folded by `ArtistKey`, most common spelling wins |
| `Various Artists` | Suggested | Excluded, as on the page |
| Song counts | Not available | Included |

The decisive one is the second row: the index page browses the in-memory snapshot, so answering
from anything else lets it suggest a name whose search then finds nothing.

Costs and guards:

- **Never waits on a build.** `ArtistIndexCache.Current` returns whatever snapshot exists and kicks
  off a build if there isn't one, rather than blocking a keystroke for `FirstBuildWait`. No snapshot
  means no suggestions, which is invisible next to a 10-second hang.
- **Queries under 2 characters are refused server-side**, and results capped at 10 — a one-character
  substring matches most of the catalog and sorting it achieves nothing.
- **`all` is passed through** so suggestions can't offer artists the page's own filter would hide.
- Answers are as stale as the snapshot — up to 6 hours, exactly like the page.
- `JsonCamelCase`, not `Ok`: API controllers here serialize with `DefaultContractResolver`, which
  keeps C# casing.

Two traps worth keeping in mind if this is touched:

1. **Debounce the lookup, not the input.** `BFormInput`'s own `debounce` delays the model, so
   submitting straight after typing searches the *previous* value. `ArtistSuggest` debounces its
   fetch internally and lets the model update immediately.
2. **A `list` attribute changes the input's implicit ARIA role** from `searchbox` to `combobox`, so
   `getByRole("searchbox")` stops matching once type-ahead is wired up. The e2e fixture locates it
   by label instead.

Site-wide search autocomplete over `Artists` is a different problem and still needs the rebuild —
[#277](https://github.com/music4dance/music4dance/issues/277).

---

### 9.5 What it costs in memory

Measured against the 2026-09-16 backup (104,033 songs, 31,957 artists) by `ArtistIndexMemory`
([§12](#12-analysis-harnesses)). The site runs on a small instance, so these are worth knowing:

| | Server GC | Workstation GC |
| --- | --- | --- |
| Retained snapshot, per instance | 6.8 MB | 6.8 MB |
| **Working set added by a build** | **27.6 MB** | **4.6 MB** |
| Committed heap added by a build | 21.9 MB | 2.6 MB |
| Managed peak over idle | 45.4 MB | 21.8 MB |
| Allocated during a build | 225.6 MB | 225.6 MB |
| Collections (gen0/gen1/gen2) | 3/1/0 | 36/12/0 |
| Serialized as JSON | 0.5 MB | 0.5 MB |

**The 226 MB is churn, not footprint.** It is short-lived strings from `CleanName` and `ArtistKey`
plus one `Dictionary` per artist to count spellings, essentially all of which dies in gen0 - note
the zero gen2 collections in either mode. What reaches RSS is the working-set row, and a build runs
once per 6 hours on a background thread.

Only 2.3 MB of the snapshot is characters; the rest is per-object overhead - an entry object and
three strings (display name, match key, sort key) per artist. The keys are kept rather than
recomputed because `Search` runs over them on every keystroke.

`BuildAsync` **groups as the stream arrives**. It used to drain the whole stream into a list first,
holding every song's artist list at once - 13 MB, about twice the snapshot it was building, for no
benefit.

The GC mode matters more than anything in this code, and measuring this build is what turned it
up. Server GC trades memory for throughput: far larger gen0 budgets, far lazier return to the OS.
The Web SDK defaults it on, and on App Service it sizes those budgets against the host's memory
rather than the plan's 1.75 GB. **`m4d.csproj` now sets `<ServerGarbageCollection>false</ServerGarbageCollection>`**,
so the Workstation column is what production runs; the Server column is kept for the comparison
that motivated it. Both were measured on a many-core dev machine, where server GC allocates more
heaps than a 1-core B1 would, so treat the gap as directional rather than exact - the direction is
the documented one.

If GC pressure does need reducing here, collapsing the per-artist spelling `Dictionary` (most
artists have exactly one spelling) is the obvious next cut.

### 9.6 The snapshot on disk

`ArtistIndexFileManager` persists the snapshot beside `dance-environment.json`, and mirrors how
that file works:

| | |
| --- | --- |
| Runtime snapshot | `wwwroot/AppData/artist-index.json`, written after every successful build |
| Fallback in source control | `ClientApp/src/assets/content/artist-index-fallback.json`, copied to `wwwroot/content/` by m4d's `assets` build target |

Read order is runtime, then fallback. It buys three things:

- **A restart costs the first visitor nothing.** They get the previous snapshot immediately and a
  rebuild starts behind them, instead of waiting out a streaming pass or timing out into "still
  building".
- **A search outage costs freshness, not the page.** A failed build leaves whatever was loaded in
  place, so the index and its type-ahead keep answering from the last good pass.
- **A brand-new instance has something to serve**, which is what the checked-in fallback is for -
  a deploy to an App Service with an empty `AppData` still renders artist pages on its first
  request.

Details that matter:

- The file stores the **display name and song count** only. The match and sort keys are derived
  from the name, so leaving them out roughly halves the file and cannot drift from the code that
  derives them. It stores what was already grouped, so a reload re-sorts but never re-groups.
- **`Built` is kept, and round-trips in UTC.** Staleness is measured against `DateTime.UtcNow`, so
  an aged file is recognized as stale and triggers a rebuild on first use. Left to Newtonsoft's
  defaults the timestamp comes back as local time, which would shift the snapshot's age by the
  server's offset - silently, and only outside UTC.
- **Writes go to a temp file and are moved into place.** On a multi-instance app this file lives on
  a shared Azure Files volume, so two instances can finish builds at once; a move replaces it in
  one step where a direct write can be read half-finished.
- **`Invalidate` deletes it**, rather than only dropping the in-memory copy. After a backfill the
  persisted snapshot is wrong rather than merely old, and reloading it would put the pre-backfill
  artists straight back - and leave them there for the next instance to restart.
- **Anything unreadable is treated as a miss.** A snapshot is a cache; a corrupt one is worth no
  more than none.
- **Persistence is off in the sandbox** (gated on `ConfigureSearch`, like the dance stats file
  manager). The sandbox's `WebRootPath` is `m4d/wwwroot`, so persisting would have it trade
  snapshots with the real dev app - each reading back the other's catalog on its next start.
- **Writing is best-effort.** A read-only or full volume is logged and costs the next restart a
  slow first request; it never costs a build its result.

---

## 10. Artist Pages

With the flag on, `/song/artist?name=…` matches on individual artists
([§5.4](#54-query-path)) rather than the whole credit, so Kenny Rogers' page includes the songs
credited `Dolly Parton & Kenny Rogers`.

The page also shows **collaborators**, computed client-side from the returned songs'
`effectiveArtists` — no extra query — and links to the browsable index. Song lists elsewhere link
individual artists through the same `ArtistCredit` component.

---

## 11. Operations

### 11.1 First rollout, per environment

Do the test index first. All steps are on **Admin → Initialization Tasks** unless noted.

1. Deploy with `ArtistIndex` **off**. Nothing changes: the field isn't in the live index, and the
   save hook is dormant until `artist-bot` exists.
2. **Take an index backup** (`/Admin/IndexBackup`) — the rollback artifact.
3. **Add Missing Fields.** Read what it reports: it adds *every* missing field, not just `Artists`.
4. **Wait 10 minutes** (the schema cache) or restart, so every instance includes the field in
   uploads. The page shows the field state and when this instance last looked — but only for the
   instance serving the page.
5. **BatchArtists → Report.** Creates `artist-bot`, which also wakes the save hook. Sanity-check
   `Changed` against the offline analysis.
6. **BatchArtists → Apply.**
7. **Verify coverage** on the same page: populated should equal total minus the credit-less songs.
8. Turn `ArtistIndex` **on**. In production that is an Azure App Configuration flag and propagates
   within 5 minutes without a restart, per instance.

### 11.2 After a heuristic change

Bump `ArtistSplitter.Version`, re-run the analysis harness and diff the reports, then **Report** and
**ApplyChanged**. `Apply` is only needed to repair coverage — `ApplyChanged` skips songs whose log
doesn't change, which is exactly the set with a missing index value.

### 11.3 Rollback

Turn the flag off; artist pages revert to credit matching. Bad bot edits are corrected by re-running
with a fixed splitter — human and service lists are never touched. The unused field is harmless.

---

## 12. Analysis Harnesses

Manual-only, `[TestCategory("Manual")]`, in `m4dModels.Tests`. Both read an index backup; both share
`ArtistAnalysisCorpus` so they replay a backup line identically.

```pwsh
$env:M4D_ARTIST_ANALYSIS_INDEX = "C:/projects/music4dance/local/index-2026-09-16.txt"
$env:M4D_ARTIST_ANALYSIS_OUT   = "C:/projects/music4dance/local/artist-analysis"
dotnet test m4dModels.Tests -p:BaseOutputPath=local/build-out/ --filter "TestCategory=Manual"
```

| Harness | Writes |
| ------- | ------ |
| `ArtistSplitterAnalysis` | `summary.md`, `splits.tsv`, `unresolved.tsv`, `artists.tsv`, `case-variants.tsv` |
| `ArtistSplitterGroundTruth` | `spotify-accuracy.md`, `spotify-mismatches.tsv` |
| `ArtistIndexMemory` | Console only - the numbers in [§9.5](#95-what-it-costs-in-memory). Set `DOTNET_gcServer=0`/`1` to compare GC modes |
| `ArtistIndexSnapshot` | `m4d/ClientApp/src/assets/content/artist-index-fallback.json` ([§9.6](#96-the-snapshot-on-disk)) |

The Spotify sampler needs the network and reads `Authentication:Spotify:ClientId` / `:ClientSecret`
from m4d's user secrets (or `M4D_SPOTIFY_CLIENT_ID` / `M4D_SPOTIFY_CLIENT_SECRET`). It skips itself
when either the backup or the credentials are missing. A full run is ~35 API calls.

Review `unresolved.tsv` sorted by song count — the top few hundred credits carry most of the value.

`ArtistIndexSnapshot` rewrites a checked-in file, so run it deliberately: after a backfill that
materially changes the artist list, and not as part of a routine `TestCategory=Manual` sweep. It
dates the snapshot from the backup's own timestamp, not from when it ran, so the cache treats a
stale one as stale. It writes compact JSON, so run `yarn format` in `m4d/ClientApp`
afterwards - prettier owns the formatting of everything under `src/`, and would otherwise reformat
it on someone else's next run.

---

## 13. Known Limits and Deferred Work

- **Recall is the big one.** 80% of the collaborations Spotify knows about are invisible to any
  heuristic because the credit string never names them ([§4.3](#43-the-ceiling)). Re-enriching the
  backlog from Spotify is the highest-value follow-on by a wide margin, and a much better
  investment than further splitter rules.
- **Artists in the site-wide search suggester** waits for the next versioned index —
  [#277](https://github.com/music4dance/music4dance/issues/277). The artist index page has its own
  type-ahead, which needs no suggester ([§9.4](#94-type-ahead)); what's missing is typing an artist
  into the main search box and being completed there.
- **Name variants are not reconciled.** `Michael Bublé` / `Michael Buble` fold together via
  `ArtistKey`, but `P!nk` / `Pink` and `LØLØ` / `Lolo` do not. An alias table would plug into the
  same key→variants lookup.
- **Classical credits that glue an ensemble to a person with no separator** (`Württemberg
  Philharmonic Orchestra and Jorge Rotter Aldo Antognazzi`) need a name dictionary to split. They
  are 1–5 song credits.
- **`Various Artists`** is left whole; a service lookup could resolve some.
- **Not built:** an `ARTISTS` upload column, a CSV export column, public API DTOs, and `Artists` in
  server-side import paths (`Song.CreateFromTrack` for playlist/bulk import doesn't record service
  artist lists — the save hook still covers title `feat.` for those).
- **Crawler rules have not been reviewed** against the new URL fan-out
  ([meta-crawler-mitigation.md](meta-crawler-mitigation.md)).
- **A real artist entity** — ids, bios, images, MusicBrainz/Spotify links — remains possible: the
  `Artists` strings become the join key, and `ArtistKey` the lookup.
