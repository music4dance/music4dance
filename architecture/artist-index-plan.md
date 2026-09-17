# Individual Artists and Artist Index — Planning Doc

> **Status: PLANNING (started 2026-09-16).** This is a working plan, not a description of shipped
> behavior. **Before this work is marked complete, turn this doc into an architecture doc**: drop
> the options we rejected, the open questions, and the phase checklists; describe what was actually
> built; and fold the artist-page changes into [artist-pages.md](artist-pages.md) (or replace it).
> Also update [song-internal-format.md](song-internal-format.md) with the new property and bot user.

Related docs: [artist-pages.md](artist-pages.md), [song-internal-format.md](song-internal-format.md),
[search-index-versioning.md](search-index-versioning.md),
[index-backup-streaming.md](index-backup-streaming.md),
[song-details-viewing-editing.md](song-details-viewing-editing.md),
[song-merge-algorithm.md](song-merge-algorithm.md), [meta-crawler-mitigation.md](meta-crawler-mitigation.md).

---

## 1. Problem

1. **One artist string per song.** `Song.Artist` holds the credit exactly as written, so
   collaborations show up as one opaque string:
   - `A & B`, `A and B`
   - `A, B & C`, `A, B, and C`
   - `A feat. B`, `A featuring B`, `A ft. B`
   - `A`, with `feat. B` / `(featuring B)` in the **title**

   The artist page (`/song/artist?name=`) does an exact phrase match against `Artist`
   (`SongIndex.FindArtist`), so `Dolly Parton` doesn't find `Dolly Parton & Kenny Rogers`.
2. **There's no artist index.** There's no way to browse artists. You can only land on an artist
   page by clicking an artist link.

## 2. Goals and Non-Goals

**Goals**

- Every song gets an ordered list of individual artists. For single-artist songs, the list is just
  `[Artist]`.
- The list is stored in the search index as a searchable, filterable, facetable string collection.
- Artist pages match on list membership, so a collaboration shows up on each collaborator's page.
- A browsable artist index page.
- The individual artists are shown on song details, and privileged users can edit them.
- The splitting heuristic can be measured on real data before rollout, improved, and re-run safely.
- The heuristic runs on newly created and edited songs.
- **The code works whether or not the index has the new field**, so the field can be added to the
  live v3 indexes without an index-version migration.

**Non-goals (for this project)**

- A real artist entity: IDs, bios, images, MusicBrainz or Spotify artist IDs. See §14 for how this
  design leaves room for one later.
- Artist roles beyond "first listed = primary". We keep the order but don't store
  primary/featured/conductor roles.
- Rewriting `Title` to strip `feat. X`.
- Merging spelling variants ("Beyoncé" vs. "Beyonce") beyond simple normalization. Aliases are a
  possible Phase 5 (§12).

---

## 3. Review of the Proposed Plan

Here is the original plan, point by point:

| # | Proposal | Assessment |
| - | -------- | ---------- |
| 1 | Keep `Artist`, add a heuristically derived artist array | **Agree.** `Artist` is the credit as the recording presents it. Service lookup (`TitleArtistMatch` / `SoftArtistMatch`), merge-candidate matching, CSV export/upload, and display all depend on it. Keep it as the credit and treat the new list as the identity/index layer. |
| 2 | Single-artist songs: the array is just `[Artist]` | **Agree.** Do this at index time, not by writing properties (§4.3). |
| 3 | Make it searchable + facetable | **Agree, and make it filterable too.** Exact-match artist pages need `Artists/any(a: a eq '...')`, which requires `IsFilterable`. |
| 4 | A new song property per unique derived artist, absent when there's no split | **Agree that it should be persisted and absent when trivial. Counter-proposal on the shape:** use one ordered list-valued scalar property instead of one property per artist (§4.2). With one property per artist, re-running an improved heuristic means diffing and removing old entries, and it's hard to tell human entries from bot entries. |
| 5 | Show on song details; editable by privileged users | **Agree.** Proposed: `canEdit` + `dbAdmin` can edit, with a "reset to automatic" action (§10). |
| 6 | Measure the heuristic against production before committing | **Strongly agree.** Run it offline against an index backup file, not the live DB, and use Spotify's structured artist lists as a ground-truth sample (§6). |
| 7 | Add the field in Azure, then batch-populate. Test index first. | **Agree, with two gaps.** (a) *Every* index document needs the field, not only split songs, and a full-corpus batch has to use key-set pagination because `BatchProcess`/`StreamAll` hit the 100K `$skip` limit (corpus ≈104K). (b) The *write* path should turn on when the field exists, but the *read* path (artist pages, index) should wait until the backfill is done, or artist pages will silently drop songs during rollout (§7.4). |
| 8 | Re-runnable when the heuristic improves | **Agree.** Version the heuristic and make the job idempotent: only write when the result changes, and never override a human (§4.4, §8). |
| 9 | Run on new songs | **Agree, plus:** also re-run when `Artist` or `Title` is edited, and define what happens to an existing list when `Artist` changes (§4.5). |
| 10 | Tolerate the field being present or absent | **Agree.** Detect the field by reading the index schema, and watch for one trap: an app instance that doesn't know about the field can wipe it on upload (§7.4). |

**What the original plan doesn't cover** (all addressed below):

- **Staleness.** If someone edits `Artist` after a list was stored, the list is wrong, and the
  pseudo-user rule would keep bots from fixing a human-entered list.
- **Precedence** between human edits, service data, and the heuristic.
- **Band names that contain separators.** "Simon & Garfunkel", "Earth, Wind & Fire",
  "Mumford & Sons", "Peter, Paul and Mary", "Wisin y Yandel", "Glenn Miller and His Orchestra".
  Naive splitting is actively harmful for these.
- **`Last, First` credits.** `Song.CleanArtistString` already un-sorts comma forms on import, so
  commas are ambiguous.
- **Better data we already have.** Spotify's track API returns an `artists` array, and
  `SpotifyService.ParseTrackResults` throws away everything after `artists[0]`.
- **Case and variant fragmentation.** Facets and `eq` filters are case-sensitive.
- **Crawlers.** A browsable index links to tens of thousands of artist pages.
- **Things Azure can't do additively.** A new field can't be added to the existing `songs`
  suggester without a rebuild.
- **Merge, history display, the TS `Song` replay, the sandbox index, upload/export formats.**

---

## 4. Data Model Decisions

### 4.1 Naming

| Layer | Name | Notes |
| ----- | ---- | ----- |
| Song property (log) | `Artists` | Parallels `Artist`, the same way `Title`/`Tempo` share a name across layers. |
| C# `Song` | `IReadOnlyList<string> Artists` (explicit, may be null) and `EffectiveArtists` (never null) | `EffectiveArtists` = explicit list, else `[Artist]`, else `[]`. |
| Index field | `Artists` | `Collection(Edm.String)` |
| TS `PropertyType` | `artistsField = "Artists"` | |
| Bot user | `artist-bot` (written as `artist-bot\|P`) | Same pattern as `tempo-bot`. |

`Artist` vs. `Artists` is a one-letter difference, which invites typos in code and in the log. The
alternative is `IndividualArtists` (or `Performers`). **Recommendation: `Artists`.** The C# and TS
constants make the typo risk manageable, and the field shows up in facets and API output, where
`Artists` reads best. We should settle this before Phase 1. It's painful to rename once the name is
in song logs.

(Avoid "ArtistCredit". In MusicBrainz that term means the *combined* credit string, which is the
opposite of this field.)

### 4.2 Property Shape: Ordered List Scalar (counter-proposal)

**Recommended:** a single scalar property whose value is a pipe-delimited ordered list:

```text
Artists=Dolly Parton|Kenny Rogers
```

- **Scalar semantics:** the last effective write wins, exactly like `Title`/`Artist`, including the
  existing bot-override rule (a pseudo write is ignored once a real user has set the field).
- **Ordered:** the first entry is the primary artist. That matters for display and for
  "A feat. B".
- **Pipe-delimited,** like tag lists. `|` is disallowed inside names; the splitter and editor
  replace it with `/`.
- **Empty value** (`Artists=`) clears the explicit list, reverting to automatic (§4.4).
- A single-element explicit list (`Artists=Simon & Garfunkel`) is meaningful: someone asserted
  "this is one act, don't split it". It is different from an absent property.

**Rejected: `Artist+`/`Artist-` set semantics (one property per artist).**

- To re-run an improved heuristic, you'd have to diff the old and new sets and emit removals.
- It's hard to reason about when a human removed a bot-added artist and the bot later re-adds it.
- There's no natural ordering.
- There's no clean "reset to automatic".

**Rejected: indexed properties (`Artists:00`, `Artists:01`)** like albums. That's a lot of
machinery (`BuildAlbumInfo`-style grouping) with no benefit over a single list value.

### 4.3 Absent vs. Present

| Log state | `Song.Artists` | `EffectiveArtists` / index `Artists` |
| --------- | -------------- | ------------------------------------ |
| No `Artists` property | `null` | `[Artist]` (or `[]` if `Artist` is empty) |
| `Artists=A\|B` | `[A, B]` | `[A, B]` |
| `Artists=Simon & Garfunkel` | `[Simon & Garfunkel]` | same (explicit "don't split") |
| `Artists=` (latest effective write) | `null` | `[Artist]` |

The heuristic writes a property **only when its output differs from `[Artist]`**. Most songs never
get one.

### 4.4 Precedence: Human > Service > Heuristic

Three sources can supply a list:

1. **A human edit** (`canEdit`/`dbAdmin` on song details, admin tools). Written as the real user.
2. **Service data.** Spotify's `track.artists[].name`, written by `batch-s|P` during service
   lookup/enrichment.
3. **The heuristic.** Written by `artist-bot|P`.

Sources 2 and 3 are both pseudo users, so the existing scalar rule protects human edits for free.
To keep the heuristic from overriding Spotify's structured data, `artist-bot` skips a song whose
current effective `Artists` was last written by a `batch-*` service user, unless `Artist` has
changed since then (§4.5). Replay tracks this with a small `ArtistsSource` enum
(`None | Heuristic | Service | User`).

**"Reset to automatic"** means a human writes `Artists=`. During replay, an empty write by a real
user also clears the "user-modified" flag for `Artists`, so the bot can take over again. That is a
small, deliberate exception to the general scalar rule, and it must be implemented identically in
C# (`Song.LoadProperties`) and TS (`Song.fromHistory`).

### 4.5 Staleness Rule When `Artist` Changes

Suppose a song has `Artists=Dolly Parton|Kenny Rogers` and someone edits `Artist` to `Kenny Rogers`.
The list is now wrong. And if a human wrote the list, the bot can never fix it.

**Rule:** during replay, an **effective** write to `Artist` resets `Artists` to `null` and clears
its source and user-modified flag. "Effective" means one that isn't ignored by the pseudo rule.
After that:

- The song falls back to `[Artist]` until something writes a new list.
- The save hook (§9) re-runs the heuristic in the same request, so a split `Artist` gets a fresh
  `artist-bot` list right away.
- If a human changes `Artist` and `Artists` in the same edit block, the editor must emit `Artist`
  **before** `Artists`. Otherwise the reset wipes the human's list. Add a unit test for this
  ordering.

A `Title` change does **not** reset the list, so a human-curated list survives a title typo fix.
The save hook still re-runs the heuristic on title changes, because `feat.` may have been added to
or removed from the title. The pseudo rule keeps that re-run from overriding a human.

### 4.6 Persist Heuristic Output vs. Derive at Index Time (considered)

We seriously considered a counter-counter-proposal: **don't persist heuristic output at all.**
Compute it inside `DocumentFromSong` and persist only human and service lists.

| | Persist (`artist-bot` edits) — **recommended** | Derive at index time |
| - | ---- | ---- |
| Client song lists and details | Work as-is: the client rebuilds `Song` from history (`SongListModel.Histories`), so `Artists` is just there | Needs a server side channel (`songId → artists`) in every song-list model, or a TS port of the heuristic that can drift |
| Audit / explainability | Visible in the history log ("Artist Bot") | Needs a separate diagnostic |
| Re-running an improved heuristic | Batch job appends blocks only where output changed | Just a reindex |
| Log growth | One small block on maybe 5–15% of songs per heuristic version that changes their result | None |
| Sandbox / backups / exports | Self-contained | Must re-derive |
| Consistency with the codebase | Matches `tempo-bot` and `batch-s` | New pattern |

The client-rebuilds-from-history design settles it: **persist.** Log growth is bounded because the
job only writes when the result changes. Pick back up the derive-at-index-time option only if Phase
0 shows the heuristic would touch a large share of the corpus on every revision.

### 4.7 Normalization of Individual Names

Each individual name is normalized the same way everywhere (splitter, editor, index writer):

- Trim; collapse internal whitespace; normalize Unicode to NFC.
- Strip wrapping brackets and quotes left over from splitting (`(feat. X)` → `X`).
- Un-sort `Last, First` **only** when the whole credit is a single sorted name (reuse
  `Song.Unsort` logic).
- De-duplicate case-insensitively within one song, keeping the first spelling.
- **Don't** change case or strip diacritics in the stored value. Display fidelity matters, and
  case-variant merging is handled in §11.4.

---

## 5. The Heuristic (`ArtistSplitter`)

### 5.1 Shape

A pure, dependency-free static class in `m4dModels` with no `DanceMusicCoreService` and no
Azure dependency, so it can run in unit tests, in an offline analysis harness over a backup file,
and in the save pipeline.

```csharp
public static class ArtistSplitter
{
    public const int Version = 1; // bump on any behavior change

    public static ArtistSplit Split(string artist, string title, IArtistKnowledge knowledge = null);
}

public record ArtistSplit(
    IReadOnlyList<string> Artists,   // normalized, ordered, deduped; never empty if artist non-empty
    IReadOnlyList<string> Rules,     // rule ids that fired, e.g. "feat", "amp", "comma-list", "title-feat", "protected-act"
    SplitConfidence Confidence);     // High | Medium | Low — Low results are NOT auto-applied
```

`IArtistKnowledge` is optional corpus evidence (§5.5). Without it, the splitter only applies the
High-confidence rules.

### 5.2 Rule Catalog (initial, to be tuned in Phase 0)

Rules run in order. Separator matching is case-insensitive and requires surrounding whitespace
unless noted.

| Id | Pattern | Example | Confidence | Notes |
| -- | ------- | ------- | ---------- | ----- |
| `feat` | `feat.`, `feat`, `ft.`, `ft`, `featuring`, `(feat. X)`, `[feat. X]` | `Pitbull feat. Ne-Yo` | **High** | The left side is primary. The right side can itself be a list: `A feat. B & C`. |
| `title-feat` | `(feat. X)` / `(featuring X)` / `- feat. X` in **title** | Title `Sway (feat. Michael Bublé)` | **High** | Append X after the artist-side results. Don't modify the title. |
| `with` | ` with ` | `Frank Sinatra with Count Basie` | Medium | Must not fire on "X with His Orchestra" (see `protected-act`). |
| `x` / `vs` | ` x `, ` X `, ` vs. `, ` vs ` | `Kygo x Whitney Houston` | Medium | ` x ` needs lowercase-x-between-capitalized-names checks. |
| `amp` | ` & `, ` + ` | `Dolly Parton & Kenny Rogers` | **Medium → needs evidence** | Main false-positive source ("Simon & Garfunkel"). |
| `and` | ` and `, ` y `, ` e `, ` et `, ` und ` | `Brooks and Dunn` | **Low → needs evidence** | Localized "and"s are very ambiguous ("Wisin y Yandel", "Rosa e Marco"). |
| `comma-list` | `A, B & C`, `A, B, and C`, `A, B` | `Marc Anthony, Cheo Feliciano` | Low → needs evidence | Conflicts with `Last, First`; see `sorted-name`. |
| `slash` / `semicolon` | `/`, `;` | `Artist A / Artist B` | Medium | Some imports use these as list separators. |
| `sorted-name` | Exactly one comma, both sides one or two words, no other separators | `Parton, Dolly` | — | Un-sort rather than split. |
| `protected-act` | Curated list plus patterns: `and His Orchestra`, `& His Orchestra`, `& The <X>`, `and the <X>`, `& Sons`, `& Friends`, `Orchestra`, `Ensemble`, `Quartet`, `Trio` | `Bob Marley & The Wailers`, `Hootie & the Blowfish` | — | Blocks a split of that span. The curated list lives in a checked-in JSON file (§5.4). |
| `various` | `Various Artists`, `Various`, `VA` | | — | Produces `[]`. The song is left out of per-artist pages but keeps its `Artist` display. |

**Classical credits** ("Berliner Philharmoniker, Herbert von Karajan") are a known gray area. The
recommendation is to split them, since each is a legitimate "artist" to browse by. Confirm in Phase
0 how common they are in this catalog; likely rare.

### 5.3 Confidence Gate

- **High** results are applied automatically.
- **Medium** results are applied automatically **if** Phase 0 precision ≥ the target (§6.4) for
  that rule. Otherwise they need corpus evidence.
- **Low** results are applied only with corpus evidence (§5.5). Otherwise they're left unsplit and
  listed in the "ambiguous" report for manual review or a curated list.

### 5.4 Curated Data Files

Checked in under `m4dModels/Resources/artists/` (or next to `dances.json` if it's also needed
client-side):

- `protected-acts.json`: full act names that must never be split ("Simon & Garfunkel",
  "Earth, Wind & Fire", "Crosby, Stills, Nash & Young", "Peter, Paul and Mary", "Tyler, the Creator",
  "Wisin y Yandel", "Brooks & Dunn", "Mumford & Sons", …). Seeded from Phase 0's "never seen split"
  report.
- `forced-splits.json` (optional): credit → explicit list overrides that are hard to express as
  rules.

Changing either file means bumping `ArtistSplitter.Version`.

### 5.5 Corpus Evidence (`IArtistKnowledge`)

The strongest ambiguity signal is **whether each part appears as a standalone `Artist` elsewhere in
the catalog.**

- `Dolly Parton & Kenny Rogers`: both parts have many standalone songs → split.
- `Simon & Garfunkel`: neither "Simon" nor "Garfunkel" is a standalone artist → don't split.
- `Ike & Tina Turner`: "Tina Turner" is standalone, "Ike" isn't → don't split. That's the right call.

Rule: split an ambiguous separator when **every** part (after normalization) has at least *N*
standalone songs (start with N = 1 and tune), or appears in a curated/Spotify-sourced list.

Where the knowledge comes from:

- **Offline (Phase 0 / backfill):** build a `Dictionary<normalizedName, count>` from all `Artist`
  values in the backup file or a streaming pass.
- **Online (save hook):** the index. `Artist` isn't filterable today, so use a cheap exact phrase
  search `FindByField(Artist, part)` with `Size=1` (the same query `FindArtist` runs now), or, once
  `Artists` is populated, `Artists/any(a: a eq 'part')` with `$top=0`, `$count=true`. A small
  memory cache (LRU, ~10K names) keeps the save path fast. Only needed when an ambiguous rule
  fires.

Evidence makes the result depend on catalog contents, so re-running later can change a result.
That's acceptable: runs are idempotent and never override humans. For reproducibility, the backfill
report records the evidence counts it used.

### 5.6 Test Vectors

A checked-in `ArtistSplitterCases.json` of `{ artist, title, knowledge?, expected, rules }` cases,
seeded from Phase 0 findings (including every false positive we fix). An MSTest data-driven test
runs them. Every heuristic change adds cases.

---

## 6. Phase 0 — Measure the Heuristic on Real Data

### 6.1 Input

**Don't run analysis against the live production index.** Use the existing export:

1. `/Admin/IndexBackup` on production → download `index-YYYY-MM-DD.txt` into `local/` (gitignored).
   Format: [song-internal-format.md §12.2](song-internal-format.md). One plain-text song log per line,
   ~104K lines.
2. The harness parses each line with `SongProperty.Load` and takes the effective `Title`/`Artist`
   using the same scalar rules. Either use `Song.Create` with a lightweight service, or add a
   minimal "replay scalars only" helper if full `Song` loading needs too much of
   `DanceMusicCoreService`.

### 6.2 Harness

Recommended: a **manual-only MSTest class** in `m4dModels.Tests` with a `[TestCategory("Manual")]`
filter excluded from `Server: Test`, or a tiny `tools/ArtistAnalysis` console project. It reads the
backup path from an env var and writes reports into `local/artist-analysis/`:

| Report | Contents |
| ------ | -------- |
| `summary.md` | Totals: songs, distinct `Artist` strings, songs split, distinct individual artists before/after, counts per rule and per confidence tier |
| `separators.tsv` | Every separator token found (including ones no rule handles), with frequency and 3 examples each |
| `splits.tsv` | `artist \| title \| result \| rules \| confidence \| evidence counts \| song count` grouped by distinct credit, sorted by song count |
| `ambiguous.tsv` | Low/Medium results not auto-applied, by frequency (drives the protected-acts list and rule tuning) |
| `never-standalone.tsv` | Credits containing a separator where no part appears standalone (probable band names) |
| `case-variants.tsv` | Individual names that differ only by case/diacritics/"The " prefix (input to §11.4) |
| `diff-vN-vN+1.tsv` | When comparing two splitter versions: every credit whose result changed |

Review the reports sorted by song count. The top few hundred distinct credits probably cover most of
the value.

### 6.3 Ground Truth from Spotify (optional but recommended)

Most songs carry Spotify track IDs (`Purchase:NN:SS`). For a **stratified random sample** of
credits containing separators (for example 500 per rule), fetch `GET /v1/tracks?ids=` (50 IDs per
call, so ~10–20 calls per 500 songs) and compare `track.artists[].name` to the splitter output.

- **Precision per rule:** of the songs we split, how many match Spotify's artist count and names
  (after normalization).
- **Recall:** of the songs Spotify lists with 2+ artists, how many we split.
- Spotify isn't perfect ("Bob Marley & The Wailers" is one artist there, which matches what we
  want; orchestras vary). Treat it as a strong signal, not an oracle.

This runs in the harness with dev Spotify credentials, reusing the `SpotifyService` auth plumbing.
It needs the network, so keep it in the manual category.

### 6.4 Exit Criteria for Phase 0

To agree on before moving on. Suggested targets:

- **High** rules: ≥ 99% precision on the sample.
- **Medium** rules enabled without evidence only if ≥ 97% precision; otherwise gate them on evidence.
- No **protected act** in the top 1,000 credits by song count is split.
- The `ambiguous.tsv` head has been reviewed and the protected-acts list seeded.

---

## 7. Search Index Changes

### 7.1 Field Definition

```csharp
new(ArtistsField, SearchFieldDataType.Collection(SearchFieldDataType.String))
{
    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
}
```

- **Filterable:** needed for `Artists/any(a: a eq 'Kenny Rogers')`. In `any` lambdas over string
  collections, OData supports `eq` and `search.in`, not range operators. *(Verify against current
  Azure AI Search docs; this determines whether A–Z bucketing can be done in-index. §11 assumes it
  can't.)*
- **Facetable:** "top artists for this search or dance", collaborator lists, cheap counts.
- **Searchable:** so keyword search matches individual names. Don't add it to the `Default` scoring
  profile's `TextWeights` at first, because `Artist` already carries weight 10 and double-counting
  would skew ranking. Revisit after testing.

Add it to `SongIndex.BuildIndex()` so any freshly built index (test resets, `songs-*-4`,
experimental) includes it natively.

### 7.2 Adding the Field to Existing Indexes (no version bump)

Azure AI Search allows **adding** new fields to an existing index in place (`CreateOrUpdateIndex`).
Existing documents read the new field as `null`. So no v3→v4 migration is needed.

- New admin action `Admin/AddIndexFields` (`dbAdmin`), following the existing pattern in
  `SongIndex.UpdateIndex(IEnumerable<string> dances)`: get the index, add any fields from
  `BuildIndex()` that are missing (top-level only), then `CreateOrUpdateIndexAsync`. Make it
  generic ("add missing fields") so the next additive field reuses it.
- Takes an index id, so it can run against `SongIndexTest` first.

**What can't be done in place** (defer to the next versioned index, see
[search-index-versioning.md](search-index-versioning.md)):

- Adding `Artists` to the existing `songs` **suggester**. Suggesters can only include fields that
  existed when the suggester was created. *(Verify.)* Artist autocomplete on the index page doesn't
  need the suggester anyway (§11.3).
- Changing attributes on the existing `Artist` field.

Scoring profiles *can* be updated in place if we later want `Artists` weights.

### 7.3 Capability Detection

Add to `SearchServiceInfo` (per index name, per `isNext`):

```csharp
Task<bool> HasFieldAsync(string fieldName, bool isNext);   // cached
void InvalidateSchemaCache();
```

- Lazily calls `GetIndexAsync` and caches the field-name set. Suggested TTL: 10 minutes, so all App
  Service instances notice a newly added field without a restart.
- If the schema can't be read, assume **absent**. That is the safe direction for the write path (see
  the trap below) and the read path falls back.
- `AddIndexFields` invalidates the cache on the instance that ran it.
- Tests: mock both states. `SongIndexLocal` (sandbox) always reports present and implements the
  query in memory (§13).

### 7.4 Two Switches: Write Capability vs. Read Enablement

| Switch | Controls | Source |
| ------ | -------- | ------ |
| **Write** | `DocumentFromSong` includes `[ArtistsField] = song.EffectiveArtists` | `HasFieldAsync("Artists")`. On as soon as the field exists. |
| **Read** | `FindArtist` uses `Artists/any(...)`; artist index page and song-details artist chips are live | Field exists **and** the feature flag `ArtistIndex` (`m4d.Utilities.FeatureFlags` / Azure App Configuration) is on |

Why two: once the field is added but before the backfill finishes, most documents have
`Artists = null`. If artist pages switched immediately, they'd lose nearly every song. The flag is
turned on per environment **after** that environment's backfill is verified.

**The trap: `Upload` is a full replace.** `UpdateAzureIndex` uses `IndexDocumentsBatch.Upload`,
which replaces the whole document. An instance whose schema cache still says "absent" will upload
documents *without* `Artists`, nulling a value the backfill already wrote. Mitigations, all cheap:

1. Add the field, then **wait at least one cache TTL** (or restart the app) before starting the
   backfill.
2. The backfill is idempotent. A final "verify" pass (§8.4) finds and re-populates `null`
   documents.
3. Never include `Artists` in the document when the field is absent. Azure rejects unknown fields
   on upload with a 400, which would fail the whole batch.

### 7.5 Write Path

`DocumentFromSong` (`m4dModels/SongIndex.cs`) gets, behind the write switch:

```csharp
[ArtistsField] = song.EffectiveArtists.ToArray()
```

`DocumentFromSong` is currently synchronous and called from LINQ `Select`s (`UpdateAzureIndex`,
`UploadIndex`, `FixupUser`). Resolve the capability **once per batch** before building documents
(for example `var includeArtists = await HasFieldAsync(...)`) and pass it in, rather than making
`DocumentFromSong` async.

### 7.6 Query Paths

- `FindArtist(name)`: read switch on → `search=*`,
  `Filter = $"Artists/any(a: a eq '{EscapeOData(name)}')"`, same sort
  (`dance_ALL/Votes desc`), cruft filter, cap. Read switch off → today's phrase search on `Artist`.
  `EscapeOData` doubles single quotes. Check whether a helper already exists near
  `search.in(SongId, ...)`.
- `Select` lists (e.g. `LoadLightSongsStreamingAsync`) must not name `Artists` unless the write
  switch is on, because Azure rejects selecting a nonexistent field. Nothing needs it there today.
- Facet requests (`AddAzureCategories`) must not name `Artists` unless the field exists.

---

## 8. Backfill / Re-run Job

### 8.1 Why not `BatchProcess`

`SongController.BatchProcess` streams via `StreamAll`, which pages with `$skip` and fails past
100,000 rows ([song-internal-format.md §11.3](song-internal-format.md)). The corpus is ~104K. Build
on the composite key-set pagination in `BackupIndexStreamingAsync` / `LoadLightSongsStreamingAsync`
(`Modified desc, SongId desc`).

Better, and useful beyond this project: **refactor `BatchProcess` to use key-set streaming.**
Otherwise note the limitation and give this job its own loop.

**Watch out:** key-set pagination on `Modified` while the job itself changes `Modified` (appending
bot edits) can skip or repeat rows. Use one of these:

- Snapshot all song IDs first (a light key-set pass selecting only `SongId`), then process the
  snapshot in chunks via `FindSongs(ids)`.
- Or paginate on `SongId` alone (it's the key, so it's sortable/filterable) instead of `Modified`.

**Recommendation:** paginate on `SongId asc`. It's simpler and stable under modification.

### 8.2 Per-Song Logic

```text
split = ArtistSplitter.Split(song.Artist, song.Title, knowledge)
desired = (split.Confidence passes gate) ? split.Artists : [song.Artist]
if song.ArtistsSource is User or Service (and not stale):   skip log change
else if desired == [song.Artist] and song.Artists is null:  no log change
else if desired sequence-equals song.EffectiveArtists:      no log change
else: append .Edit / User=artist-bot|P / Time=now / Artists=<desired or empty>
index: full Upload if log changed; else partial MergeDocuments {SongId, Artists=EffectiveArtists}
       (partial merge skipped when the index value already equals EffectiveArtists)
```

- A partial **merge** (`IndexDocumentsBatch.Merge`) of just `{SongId, Artists}` is much cheaper
  than rebuilding the whole document. It's the right tool to populate the field on the ~90% of
  songs whose log doesn't change.
- When the heuristic *no longer* splits a song that `artist-bot` previously split, the job writes
  `Artists=` (clear) as `artist-bot`. That only works because the bot, not a human, owns the current
  value.
- Should dance stats be updated for bot edits (`stats.UpdateSong`)? Probably not needed, since stats
  don't use artists. Confirm this doesn't bloat `SongCache`.

### 8.3 Controls

Admin page/action `Admin/BatchArtists` (`dbAdmin`), with parameters:

- `index` (default: current; run `SongIndexTest` first)
- `mode`: `Report` (no writes; produces the same TSVs as Phase 0, from the live index) |
  `IndexOnly` (partial merges only, no log changes) | `Apply`
- `count` (cap for trial runs), `startAfterSongId` (resume)
- `minConfidence`

It runs as an `AdminMonitor` background task like the existing batches, logging progress and totals
per rule, and writes a before/after log like `BatchProcess`'s `log` option.

### 8.4 Verification Pass

After `Apply`: `$count` of `Artists/any()` vs. `$count` of `Artist ne null`. `Artist` isn't
filterable today, so compare against total non-null-title documents instead, which is also fine.
Also spot-check the top 200 credits by song count. Only then enable the `ArtistIndex` flag.

### 8.5 Re-running After Heuristic Changes

Bump `ArtistSplitter.Version`, run `Report` and diff against the previous report, then `Apply`. The
bot never overrides human/service lists, and it only writes where the output changed.

---

## 9. New and Edited Songs (Save Hook)

### 9.1 Where

There are many save paths: song-details PATCH/PUT, Add Song (`Augment`), bulk upload/`CommitCatalog`,
merge, service lookups (`batch-s`), `AdminEditSong`, `CorrectTempoSong`, and others. **They mostly
pass through `SongIndex.SaveSongs` / `SaveSongsImmediate`.** Options:

- **A. Central hook in `SaveSongs`/`SaveSongsImmediate`** (recommended). Before indexing, for each
  song where `Artist` or `Title` changed in this save (or `Artists` is stale per §4.5), run the
  splitter and append an `artist-bot` block if needed. Opt-out parameter for paths that must not
  mutate logs.
- **B. Explicit calls in each edit path.** More precise, easy to miss one.

`UploadIndex` / `LoadIdx` (backup restore) and `UpdateIndex` (version migration) **must not** run
the hook, because a restore should reproduce logs exactly. They don't go through `SaveSongs` today;
keep it that way.

**Detecting "changed in this save":** compare the effective `Artist`/`Title` before and after the
edit, where the old `Song` is available (`EditSong`/`UpdateSong` have `song` and `edit`). Where it
isn't (bulk create), treat new songs as changed. A cheap rule that also works: run the splitter
whenever `ArtistsSource` is `None` or `Heuristic`. It's idempotent, so running too often only costs
CPU and, for ambiguous rules, an evidence lookup.

### 9.2 Ordering Within the Log

The `artist-bot` block is appended **after** the user's block, with the same or later `Time`. The
user's block keeps its `Edited` timestamp semantics, since bots are pseudo and don't touch `Edited`.

### 9.3 Service Data at Creation

When a song is created or enriched from a Spotify track, capture `track.artists[].name` as
`ServiceTrack.Artists` (a new `string[]`). If it has 2+ entries, emit
`Artists=<names>` as part of the `batch-s` block. The heuristic then defers to it (§4.4). Same idea
for iTunes/Apple if their payloads expose structured artists; check `ITunesService`.

### 9.4 Client Preview

When a user edits `Artist` in the song editor, show a **suggested** split right away via
`GET /api/song/splitartist?artist=&title=`, which returns an `ArtistSplit`. Show it as "will be
saved as: A · B". The user can accept it (nothing to emit; the server hook does it) or edit the
chips (emits a human `Artists` property, after `Artist`). This avoids porting the heuristic to TS.

---

## 10. Song Details: Display and Editing

### 10.1 TS Model

- `PropertyType.artistsField = "Artists"` (`m4d/ClientApp/src/models/SongProperty.ts`).
- `Song.fromHistory` replays `Artists` with the §4.2–§4.5 rules (scalar, pipe list, empty clears,
  reset on effective `Artist` write, pseudo precedence). **Shared test vectors:** write the replay
  cases once as JSON and run them from both MSTest and Vitest, so the C# and TS replay can't drift.
- `Song.artists` (explicit or undefined), `Song.effectiveArtists`, `Song.artistsSource`.
- `SongHistory` algorithmic-user list: add `artist-bot`, with display name "Artist Bot".
- C#: `Song.cs` `TryGetCappedDelta` and similar `user == "tempo-bot"` checks should use a shared
  `IsAlgorithmicUser` helper that includes `artist-bot`. Grep for `tempo-bot` in C# and TS.

### 10.2 Display

In `SongCore.vue`, keep the heading `Title by <Artist>`. When `effectiveArtists` differs from
`[artist]`:

- **Preferred:** render the `Artist` string with each individual name that occurs as a substring
  hyperlinked to its own artist page ("Dolly Parton & Kenny Rogers" with two links). Names that
  aren't found in the string (e.g. from `title-feat`) are appended as "with X".
- **Fallback, simpler:** a small "Artists:" line of `BBadge`/link chips under the title.

When the read switch is off (the server exposes a `features.artistIndex` flag in the page model or
menu context), keep today's single link.

Consider the same substring-linking in `SongTable.vue` (`artistRef`) and `album/App.vue`. Share one
helper that returns `{ text, href? }[]` segments.

### 10.3 Editing

- **Who:** `canEdit` or `dbAdmin`. Today `Artist` itself is `dbAdmin`-only (plus the creator) in
  `SongCore.vue`. Should the creator also be allowed? Proposed: yes, same as `Artist`.
- **Control:** `BFormTags` (bootstrap-vue-next) for add/remove, with drag-or-arrow reordering (order
  matters: first = primary). Plus buttons:
  - **Reset to automatic** → emits `Artists=` (only shown when `artistsSource == User`)
  - **Don't split** → emits `Artists=<Artist>` (a single explicit entry)
- **Validation:** no `|`; trimmed; non-empty entries; ≤ ~20 entries; each ≤ 200 chars.
- The editor emits `Artists` after any `Artist` change in the same block (§4.5).
- **Admin merge page** (`song-merge`): treat `Artists` like the other scalar fields. See §13.

---

## 11. Artist Index Page

### 11.1 URL and Routing

- `/song/artists` → `SongController.Artists()` → Vue page `artist-index` (wrapped in `PageFrame`).
- Optional `?letter=A` / `?q=` query parameters; bookmarkable.
- Keep `/song/artist?name=` as the artist page URL. Existing links and bookmarks keep working.

### 11.2 Data Source: Cached Aggregate (recommended) vs. Live Facets

**Live facets** (`facet=Artists,count:N,sort:value`): easy, but

- facets can't be filtered by value prefix, so there's no in-index A–Z bucketing (§7.1 caveat);
- large facet counts (~40K+ distinct artists, to be measured in Phase 0) are expensive for every
  page view;
- they're case-sensitive, so variants fragment (§11.4).

**Cached aggregate (recommended):** build an `ArtistIndexSnapshot` roughly daily, in the same
lifecycle as dance stats (`DanceStatsManager` reload):

- One key-set streaming pass selecting `SongId, Artists` with the standard cruft filter (skip
  cruft/unconfirmed songs, so the counts match what anonymous users see). Alternatively, one facet
  request with a very high count. Try both in Phase 3 and pick the faster.
- Aggregate by **grouping key** (§11.4) → `{ key, displayName, songCount }`. The display name is the
  most common spelling.
- Sort key: strip a leading "The "/"A "/"El "/"La "…, fold diacritics, uppercase. Anything not
  starting with A–Z goes in the `#` bucket.
- Store in memory. Optionally persist the JSON alongside dance stats, so a cold start doesn't need a
  full pass and the sandbox can ship a small snapshot.
- Size estimate: 50K entries × ~40 bytes ≈ 2 MB in memory. Send the client only the requested
  letter.

### 11.3 UI

- A–Z + `#` letter bar (`BNav` pills). Selecting a letter lists artists with song counts in a
  multi-column list, paginated or virtualized if a letter has thousands.
- **Minimum songs filter:** default "2+ songs", with a toggle to show all. This keeps the list
  meaningful and reduces crawl surface.
- **Search box:** client-side prefix/substring filter over the loaded letter, plus a server
  endpoint `GET /api/artist/search?q=` over the snapshot (substring, diacritic-insensitive) for
  cross-letter search. No suggester needed.
- **Optional "Top artists"** header: top 50 by song count, from the snapshot.
- Links go to `/song/artist?name=<displayName>`.

### 11.4 Case and Variant Grouping

Individual names may differ only in case, diacritics, or a leading "The". Proposal:

- A **grouping key** (lowercase, diacritics folded, whitespace collapsed; *not* "The"-stripped by
  default, because "The The" and "Weeknd" vs. "The Weeknd" are riskier than they look) is used
  **only** in the snapshot aggregation and for artist page lookup.
- **Artist page lookup** by key requires either
  - a second index field `ArtistKeys` (filterable only, computed in C#, not an Azure normalizer), or
  - using the snapshot to expand a key into all its display variants, then
    `Artists/any(a: search.in(a, 'v1|v2|v3', '|'))`.
- **Recommendation:** the snapshot expansion (no extra field). `ArtistKeys` is the fallback if the
  variant lists get large.

A future alias table ("Beyonce" ≡ "Beyoncé Knowles") would plug into the same key→variants expansion
(Phase 5).

### 11.5 Crawlers and SEO

The index page creates crawlable links to every artist page. Artist pages already run
`CheckSpiders()`. Before launch, decide:

- Whether artist pages go in the sitemap (probably only those with ≥ N songs).
- Whether the index uses `rel="nofollow"` for low-count artists.
- Whether the rate-limit/crawler rules in [meta-crawler-mitigation.md](meta-crawler-mitigation.md)
  and [distributed-attack-mitigation.md](distributed-attack-mitigation.md) cover the new URL fan-out.

---

## 12. Artist Page Changes

- `FindArtist` switches to `Artists/any(...)` behind the read switch (§7.6). A collaboration now
  appears on each collaborator's page.
- **Keep the `artist` column visible** in `SongTable` when any row's `Artist` differs from the page
  artist. It's currently hidden because all rows share the value, which is no longer true.
- **Collaborators panel:** facet `Artists` filtered to this artist's songs → "Also appears with:
  Kenny Rogers (12), …", excluding the artist themself. One cheap facet query.
- **Per-dance breakdown links** (`artist/App.vue`) currently build
  `KeywordQuery.fromParts(new Map([["Artist", artist]]))`, a token search. Options:
  1. Leave as is (it's broader and still works).
  2. Add an `Artists` exact-match term to `SongFilter` so the links match the page exactly. That's a
     filter-string format change (`SongFilter`, `SongFilterNext`, TS `SongFilter`, and the
     "use the class library" rule in `CLAUDE.md`).
  **Recommendation:** option 1 for now; option 2 as a follow-up if the mismatch is noticeable.
- **Empty result with read switch on:** fall back to the legacy phrase search, so an artist name
  that only exists as an unsplit credit (e.g. a heuristic miss) still returns something.

---

## 13. Other Touch Points

| Area | Change |
| ---- | ------ |
| `Song.cs` constants / `ScalarFields` | `ArtistsField = "Artists"`. **Don't** add it to `ScalarFields`/`ScalarProperties` blindly: those drive reflection-based loading and `modified` checks that assume a simple type. Handle `Artists` explicitly in `LoadProperties` (like `TempoField`). |
| `Song.LoadProperties` | Parse the pipe list; apply the §4.2–§4.5 rules; track `ArtistsSource`. |
| `Song.CreateLightSong` | No change (doesn't need artists). |
| Song merge (`SongMerge.cs`, `MergeSongs`, [song-merge-algorithm.md](song-merge-algorithm.md)) | Merge is scalar-like: prefer the human-sourced list; else re-run the splitter on the merged `Artist`. Add to the merge UI's scalar field set. |
| `Song.CheckPropertiesInternal` / cleanup | Validate the `Artists` format. `CleanupProperties` could collapse multiple consecutive `artist-bot` blocks. |
| History display (`SongHistoryLog.vue`, `SongHistoryViewer.vue`) | Render the `Artists` property as a chip list; user display name "Artist Bot". |
| `SongHistory.ts` `isAlgorithmic` / `humanOnly` | Include `artist-bot`. |
| Upload format ([SongUploadFormat.md](SongUploadFormat.md), `Song.cs` header map) | Optional `ARTISTS` column (pipe-delimited) that emits a human-attributed `Artists` property. |
| CSV export (`Song.cs` export header strings) | Optional `Artists` column at `Global` level. Low priority. |
| `SpotifyService.ParseTrackResults` / `ServiceTrack` | Capture all `artists[].name` (§9.3). |
| `m4dModels.Sandbox/SongIndexLocal.cs` | Implement `FindArtist` over the in-memory songs using `EffectiveArtists`, and report the field as present. Today it doesn't override `FindArtist`/`FindByField`; verify what the sandbox artist page does now. |
| Sandbox fixture (`dancestatistics.txt`) | Optionally run the splitter over `cachedSongs` so sandbox contributors see split artists. Also a small artist-index snapshot. |
| Public API ([public-api-authorization.md](public-api-authorization.md)) | If song DTOs are exposed, add `artists` (additive). |
| `SongIndex` diagnostics (`ViewBag.AzureIndexInfo = BuildIndex()`) | Show "Artists field: present/absent" and flag state on `/Admin/Diagnostics`. |
| Usage logging / GTM | Track artist index usage ([gtm-tracking-guide.md](gtm-tracking-guide.md)). |

---

## 14. Future: Artist Entity (out of scope, keep possible)

This design doesn't block a real artist table later (SQL `Artists` with id, canonical name, aliases,
Spotify/MusicBrainz ids, bio). The upgrade path is:

- the grouping key / alias expansion (§11.4) becomes a lookup in that table;
- `Artists` stays as names in the log (human-readable, like tags), optionally with a parallel
  `ArtistIds` index field.

`TagGroup` (primary tag + synonyms) is a precedent in this codebase for a curated canonicalization
table.

---

## 15. Phased Rollout

Each phase is a separate PR unless noted. **Always test index first, then production.**

### Phase 0 — Heuristic and Analysis (no production changes)

- [ ] `ArtistSplitter` + curated JSON files + test vectors + unit tests
- [ ] Analysis harness (manual category) reading a `local/` index backup; reports in
      `local/artist-analysis/`
- [ ] Optional Spotify ground-truth sampler
- [ ] Iterate rules until the §6.4 exit criteria are met; record decisions in this doc

### Phase 1 — Model and Replay (dormant)

- [ ] `Artists` property constants (C# + TS), replay rules (§4), `EffectiveArtists`, `ArtistsSource`
- [ ] Shared C#/TS replay test vectors
- [ ] `artist-bot` user: display name, algorithmic lists, `IsAlgorithmicUser` helper
- [ ] History display of `Artists`
- [ ] Merge handling
- Ships dormant: nothing writes `Artists` yet.

### Phase 2 — Index Capability and Backfill

- [ ] `SearchServiceInfo.HasFieldAsync` + cache + diagnostics display
- [ ] `ArtistsField` in `BuildIndex()`; conditional inclusion in `DocumentFromSong`
- [ ] `Admin/AddIndexFields`
- [ ] Key-set (`SongId`) batch job `Admin/BatchArtists` with `Report | IndexOnly | Apply`, partial
      merges, resume, and a verify pass
- [ ] Feature flag `ArtistIndex` (read switch) + `FindArtist` switch + fallback
- [ ] Save hook (§9) behind the write switch
- **Rollout, test index:** add field → wait for cache TTL/restart → `Report` → review →
  `Apply` → verify → flag on → smoke test.
- **Rollout, production:** repeat. Take a fresh `/Admin/IndexBackup` **before** `Apply` as the
  rollback artifact.

### Phase 3 — UI

- [ ] Song details display (substring links) + editing (`canEdit`/`dbAdmin`/creator) + split preview
      endpoint
- [ ] Artist page: collaborators panel, artist column visibility
- [ ] Artist index snapshot + `/song/artists` page + search endpoint
- [ ] Crawler/sitemap decisions (§11.5)
- [ ] Link from nav/footer/site map

### Phase 4 — Better Sources

- [ ] Spotify `artists[]` capture on create/enrich (§9.3)
- [ ] Optional: re-enrich the ambiguous backlog from Spotify via a batch

### Phase 5 — Canonicalization (optional)

- [ ] Case/diacritic variant grouping in the lookup (§11.4)
- [ ] Alias table, if variant noise warrants it

### Wrap-up

- [ ] **Convert this doc to an architecture doc** (see the banner at the top); update
      `artist-pages.md` and `song-internal-format.md`
- [ ] Next versioned index (`songs-*-4`): add `Artists` to the suggester; remove any `TODOIDX`
      shims added here

---

## 16. Rollback

- **UI / read path:** turn off the `ArtistIndex` flag → artist pages revert to the phrase search on
  `Artist` and the index page hides. No deploy.
- **Bad heuristic writes:** re-run the job with a fixed heuristic, which writes corrected values or
  `Artists=` clears as `artist-bot`. Human/service lists are never affected. Worst case: restore the
  pre-`Apply` index backup via `/Admin/LoadIdx`. That loses any user edits made since the backup,
  so it's a last resort.
- **The index field itself:** a field can't be removed from an Azure index in place. Leaving it
  unused and `null` is harmless. It goes away at the next versioned rebuild if it's dropped from
  `BuildIndex()`.

---

## 17. Testing Plan

**Server (MSTest)**

- `ArtistSplitter` data-driven cases (every rule, protected acts, sorted names, title feat, unicode,
  empty/various, dedupe, `|` sanitization).
- Replay: absent/present/empty/single-explicit; human vs. `artist-bot` vs. `batch-s` precedence;
  reset on `Artist` change; same-block `Artist` then `Artists`; title change doesn't reset;
  "reset to automatic" re-enables the bot. Use the serialized `Song.Create(".Create=\tUser=...")`
  format per [testing-patterns.md](testing-patterns.md).
- `DocumentFromSong` with the field present/absent; no `Artists` key when absent.
- `FindArtist` filter string with the switch on/off, and quote escaping (`O'Connor`).
- Batch job per-song decision table (§8.2), including the `IndexOnly` partial merge document shape.
  Use `TestSongIndex` to verify upload vs. merge calls.
- Save hook: runs for edit/create/merge paths; does **not** run for `UploadIndex`/`LoadIdx`.

**Client (Vitest)**

- `Song.fromHistory` shared replay vectors.
- Artist substring-link segmenter (overlaps, case, names not in the string).
- Artists editor emits properties in the right order; "Reset to automatic" and "Don't split".
- Artist index page letter bucketing, min-count filter, search.

**Manual / E2E**

- Test-index rollout rehearsal (§15 Phase 2) end to end, including the stale-cache trap: run an
  instance with the field absent in its cache, edit a song, and confirm the verify pass catches and
  fixes it.
- Playwright: artist index → artist page → song details → collaborator link.

---

## 18. Open Questions

1. **Name:** `Artists` vs. `IndividualArtists` / `Performers` (§4.1). Decide before Phase 1.
2. **Who can edit `Artists`?** `canEdit` + `dbAdmin` + creator? And should `Artist` itself move from
   `dbAdmin` to `canEdit` at the same time?
3. **Medium/Low rule policy** after Phase 0 numbers: evidence threshold N, and whether `and`/`y`/`e`
   splits ever apply without evidence.
4. **Classical credits** (orchestra, conductor, soloist): split or not?
5. **"Various Artists"** → `[]` (excluded from artist pages), or kept as a pseudo-artist?
6. **Minimum song count** on the artist index by default (1 or 2), and sitemap inclusion.
7. **Grouping key:** fold "The " prefixes or not (§11.4).
8. **Should `BatchProcess` be refactored** to key-set pagination as part of this work? It would
   benefit every batch job.
9. **Collaborator-aware `SongFilter` term** (§12 option 2): needed, or is token search good enough?
10. **Azure facts to verify** before relying on them: string-collection lambda operator support;
    suggester field-addition restriction; facet cost at ~50K distinct values; whether phrase
    queries can match across collection element boundaries (affects searchable `Artists`).
