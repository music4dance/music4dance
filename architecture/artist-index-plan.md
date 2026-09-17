# Individual Artists and Artist Index — Planning Doc

> **Status: IMPLEMENTED ON BRANCH, NOT ROLLED OUT (2026-09-16).** Phases 0-4 are built on
> `feature/artist-index` and fully tested locally, all behind the `ArtistIndex` feature flag
> (default off). Nothing has touched a live Azure index yet - see §0 for what was built, where it
> departs from the plan below, and the rollout runbook.
>
> **Before this work is marked complete, turn this doc into an architecture doc**: drop
> the options we rejected, the open questions, and the phase checklists; describe what was actually
> built; and fold the artist-page changes into [artist-pages.md](artist-pages.md) (or replace it).
> Also update [song-internal-format.md](song-internal-format.md) with the new property and bot user.

Related docs: [artist-pages.md](artist-pages.md), [song-internal-format.md](song-internal-format.md),
[search-index-versioning.md](search-index-versioning.md),
[index-backup-streaming.md](index-backup-streaming.md),
[song-details-viewing-editing.md](song-details-viewing-editing.md),
[song-merge-algorithm.md](song-merge-algorithm.md), [meta-crawler-mitigation.md](meta-crawler-mitigation.md).

---

## 0. Implementation Status (2026-09-16)

### 0.1 What's Built

| Phase | Commit(s) | Contents |
| ----- | --------- | -------- |
| 0 | `Add ArtistSplitter heuristic and index-backup analysis harness` | `m4dModels/ArtistSplitter.cs`, `ArtistKnowledge.cs`; unit tests; manual `ArtistSplitterAnalysis` harness |
| 1 | `Add Artists song property with replay rules in C# and TypeScript` | `Song.Artists` / `ArtistsSource` / `EffectiveArtists` / `UpdateArtists`; TS `Song.artists`; shared JSON replay cases; history viewer; `artist-bot` user |
| 2 | `Index individual artists and use them for artist pages` | Artists index field, schema detection (`SearchServiceInfo.HasFieldAsync`), save hook, `FindArtist` switch, Admin **Add Missing Fields** and **BatchArtists**, sandbox parity |
| 3 | `Show and edit individual artists on song details`, `Link individual artists in song lists and show collaborators`, `Add browsable artist index at /song/artists` | `ArtistCredit`, `ArtistsEditor`, SongTable links, artist page collaborators, `/song/artists` |
| 4 | `Capture Spotify's structured artist credits` | `ServiceTrack.Artists`; add-song-from-track records multi-artist credits |
| - | `Harden the artist save hook and artist index build` | Review fixes (see §0.2) |

Test status at the last commit: m4dModels.Tests 592 passed, m4d.Tests 228 passed, Vitest 964 passed.
`yarn type-check` clean; `yarn lint` reports only errors that already existed on `main`.

### 0.2 Departures From the Plan Below

These supersede the corresponding sections; fold them in when converting to an architecture doc.

- **No confidence tiers (§5.1, §5.3).** Evidence-free rules (feat, title feat, `;`, spaced ` / `,
  comma lists, feat-clause lists of full names) always apply. Ambiguous separators (`&`, `+`, `and`,
  `y`, `e`, `et`, `und`, `x`, `vs`, `with`) split only with evidence, so there's no separate gate.
  `ArtistSplit` reports `Rules` and `Unresolved` instead of a confidence.
- **Protected lists live in code (§5.4).** They're in `ArtistSplitter.cs` (`ProtectedActs`,
  `GroupWords`, `Articles`, `Fillers`), not JSON. Any change still bumps `ArtistSplitter.Version`.
- **Joins, as of splitter v2 (2026-09-17, after reviewing `unresolved.tsv`) (§5.2):**
  - *Leader and backing group:* a join followed by a possessive ("and His Orchestra", "y Su
    Charanga", "und sein Tanzorchester", "& All His Stars") or by nothing but generic ensemble
    words ("with Orchestra", "and Singers") keeps only the leader, with no evidence needed:
    "Duke Ellington and His Orchestra" becomes `[Duke Ellington]`. Anything credited after the
    group is split on its own ("Teddy Wilson And His Orchestra With Billie Holliday").
  - *Named ensembles:* a multi-word name containing an ensemble word (Orchestra, Philharmonic,
    Symphony, Sinfonietta, Choir, Ensemble, Trio, Collegium, ...) joined with another multi-word
    name always splits: "London Symphony Orchestra and Árpád Joó".
  - *One known side:* a join splits when either half is a standalone artist, as long as both halves
    are multi-word. One-word names collide with unrelated artists too often ("Fitz and The
    Tantrums"). For article joins ("Bob Marley & The Wailers"), the known half must be the leader.
  - *Still protected:* act nouns after the join ("& Sons", "& The Gang", "& Friends"), article on
    both sides ("The Mamas & The Papas"), joins inside brackets ("BnB (Blanco & Black)"), and two
    single words outside a feat clause ("Rodrigo y Gabriela", reported as `duo`).
  - `Last, First` un-sorting was dropped: the corpus has almost no sorted names, and `, Jr.`/`, Sr.`
    suffixes are protected.
  - "Various"/"Various Artists" are left alone by the splitter (a possible future pass could ask
    Spotify for the real artists). The artist index page still leaves them out of browsing.
- **Replay: `batch-*` is always a service (§4.4).** Songs added from a track in the client log the
  service block as plain `batch-s` (no `|P`), so replay classifies `batch-*` accounts as `Service`
  either way.
- **Replay: an unchanged credit doesn't invalidate (§4.5).** Services routinely re-write `Artist`
  with the same value, so only a write that changes the cleaned credit clears `Artists`.
- **`Song.UpdateArtists` is synchronous and doesn't reload.** It appends the block and sets the
  in-memory state directly (tested to match a fresh replay), so callers' unsaved in-memory changes
  survive the save hook.
- **Backfill uses the existing `SongIndex.StreamAllSongsAsync` (§8.1).** It already pages
  `Modified desc` with key-set pagination and is documented as safe to write back while
  streaming (a re-saved song moves above the cursor). No partial `Merge` uploads: **Apply** re-saves
  every song in full, like **Reload All Songs**, and **ApplyChanged** re-saves only changed songs.
- **Artist page matching (§11.4, §7.6).** Instead of expanding keys to variants, `FindArtist` does
  an analyzed phrase search on `Artists` (case-insensitive) and then keeps songs whose
  `EffectiveArtists` contain the name by `ArtistSplitter.ArtistKey` (case- and
  diacritic-insensitive). No second index field. It falls back to the old credit phrase search
  when that finds nothing.
- **Online evidence (§5.5).** The save hook records which names the splitter would ask about and
  fetches evidence with one facet query:
  `Artists/any(a: search.in(a, 'n1|n2', '|')) and SongId ne '<self>'`, faceting `Artists`. That
  only runs for credits with ambiguous separators, and only once the field exists. Before that,
  new songs get evidence-free splits only; the batch job catches up.
- **Collaborators are computed client-side (§12)** from the returned songs' `effectiveArtists`, so
  there's no facet query.
- **Artist index (§11).** Unknown/Various are excluded, the default minimum is 2 songs, "Most
  popular artists" (top 100) is shown when no letter is picked, and search is server-side over the
  snapshot. `ArtistIndexCache` always builds in the background: the first request waits up to 10s
  and otherwise the page says the index is being built.
- **Spotify capture (§9.3)** is wired only into the client add-song-from-track flow
  (`SongHistory.fromTrack`). That flow folds the service's edits into the creating user's block
  (existing behavior), so these lists are recorded as `User`, not `Service`. The server-side
  `Song.CreateFromTrack` paths (playlist/bulk import) don't record `Artists` yet, but the save hook
  covers title "feat." for them.
- **Not done:** `ARTISTS` upload column, CSV export column, sitemap/crawler decisions (§11.5),
  navigation links to `/song/artists` (only the artist page links to it), sandbox fixture refresh,
  Diagnostics display of the field/flag, public API DTOs.

### 0.3 Phase 0 Findings (index backup 2026-09-16)

**Splitter v2 (current):** 7,538 songs (7.2%) split from 3,932 distinct credits. By rule: title
feat 3,011 · leader 2,082 · evidence 852 · one known side 657 · named ensemble 407 · artist feat
359 · comma list 352 · semicolon 339 · slash 33. `unresolved.tsv` went from 2,468 credits (v1) to
1,318, and 904 of those aren't single-word duos. Most of the rest are genuine act names ("Belle &
Sebastian") or leader-and-band credits where the leader has no songs of their own ("Nathaniel
Rateliff & The Night Sweats").

A final pass over each produced name (added after the §6.3 sample) strips a role marker,
conjunction or backing-group label the segmenting left in front of it ("Featuring Hilary
Alexander", "With Lester Young", "His Band Ross Mitchell") and drops a name that is nothing but
such a label ("His Orchestra", "Chorus"). That removed every junk entry the corpus produced; the
seven that remain are songs whose entire `Artist` field is one of those strings, where falling back
to the credit is correct.

**Splitter v1** (kept for comparison in `local/artist-analysis-v1/`):

From `local/artist-analysis/summary.md` (regenerate with the harness; see §6.2):

- 104,033 songs; 31,967 distinct credits; **4,431 songs (4.3%) split** from 2,838 distinct credits;
  32,434 distinct individual artists (case-insensitive).
- By rule: title feat 3,011 · evidence 803 · artist feat 359 · comma list 352 · semicolon
  (classical credits) 339 · slash 33 · partial evidence in feat clauses 22.
- Title "feat." is by far the biggest source. In the artist field, `&`/`and`/`y`/`und` are mostly
  band names ("Tony Evans & His Orchestra", "David Calzado y su Charanga Habanera"), which is why
  those need evidence.
- Evidence splits sampled by hand look very precise. Leader-and-band credits where the band is
  also a standalone artist ("Gloria Estefan & Miami Sound Machine", "Michael Franti & Spearhead")
  do split, which is arguably right for browsing.
- Known misses (left unsplit, fixable by hand): collaborations where neither half is a standalone
  artist ("Walter Laird & Nico Gomez", "Vienna State Opera Orchestra & Felix Prohaska").
- Case and diacritic variants are common ("Michael Bublé"/"Michael Buble", "Céline Dion"/"Celine
  Dion", `case-variants.tsv` has 535 groups), so key-based artist matching was pulled into the
  first cut.
**Spotify ground-truth sample (§6.3), run 2026-09-17** — 1,600 distinct credits, from
`local/artist-analysis/spotify-accuracy.md`:

- **Agreement per rule runs 80-93% of credits** (77-97% song-weighted), counting exact matches plus
  the cases where Spotify names the same act at a different length ("Duke Ellington & His
  Orchestra" against our "Duke Ellington"). `(not split)` scores 97.4%, so we are not splitting
  things we shouldn't.
- **The `extra` column is not error.** All 57 sampled cases are Spotify being less complete than we
  are: it credits only the lead artist of a `feat.` track (17 cases where our own title names the
  collaborator), only one member of a comma list ("DJ Khaled, T-Pain, Ludacris, Rick Ross & Snoop
  Dogg" → just DJ Khaled), or only the leader of a leader-and-band credit ("Tommy James & The
  Shondells"). Counting those as agreement puts most rules above 95%.
- **Only 21 of 1,600 credits are real misses**, and most of those are Spotify counting a remixer as
  an artist. The 62 `differ` rows are overwhelmingly name variants, not splitting mistakes: our own
  typos ("Antony Santos" for "Anthony Santos"), stylization Spotify spells differently ("P!nk",
  "A$AP Ferg", "LØLØ", "98º" with a masculine ordinal for our degree sign), words in the wrong
  order in our data ("Sweat Blood & Tears"), and tracks whose Spotify id points at a different
  recording. Note that `Ø` survives the NFD fold in `ArtistKey`, which matters for §11.4.
- **The one structural weakness is classical credits that glue an ensemble to a person with no
  separator** ("Württemberg Philharmonic Orchestra and Jorge Rotter Aldo Antognazzi", where the
  right side is two people). Splitting these needs a name dictionary, and they are 1-5 song
  credits, so they are left alone.
- **Recall is the real finding.** In an unbiased 400-credit sample, Spotify lists 2+ artists for
  27.8% of credits (32.8% of songs), but **80% of those credits contain no separator at all** —
  our `Artist` string simply never mentions the collaborator (Spotify says "J Balvin | Chencho
  Corleone"; we have "J Balvin"). A separator-based heuristic can therefore only ever reach about a
  fifth of the collaborations Spotify knows about, which it does: 18.9% of those credits, 9.1%
  song-weighted. **Everything beyond that has to come from service data (§9.3), not a better
  heuristic.**

### 0.4 Rollout Runbook

All steps are admin actions on **Admin → Initialization Tasks** unless noted. Do the test index
first, then production.

1. Deploy the branch with `ArtistIndex` **off**. Nothing changes: the field isn't in the live index,
   so documents don't include it, and the save hook stays dormant until the `artist-bot` pseudo user
   exists (history would otherwise show an unknown user to anonymous visitors).
2. **Take an index backup** (`/Admin/IndexBackup`). It's the rollback artifact.
3. **Add Missing Fields** for the index. Adds `Artists` in place.
4. **Wait at least 10 minutes** (the schema cache lifetime), or restart the app, so every instance
   includes the field in uploads (§7.4 trap). Initialization Tasks now reports the field as present
   and shows when the serving instance last looked.
5. **BatchArtists → Report.** Creates the `artist-bot` pseudo user, which also activates the save
   hook for newly saved songs. Review the logged changes (first 200, in the app log) and the
   completion totals on Admin Status. It builds knowledge from a light pass over the whole index,
   then streams every song without writing any.
6. **BatchArtists → Apply.** Appends bot edits and re-saves every song, which populates `Artists`
   everywhere.
7. Verify: spot-check a few songs' history (Artist Bot entries) and search for a collaborator.
8. Turn `ArtistIndex` **on** (Azure App Configuration feature flag). Check an artist page for a
   collaborator (e.g. "Kenny Rogers"), song details links and editing, and `/song/artists` (the
   first visit may say it's building).
9. After heuristic changes: bump `ArtistSplitter.Version`, run the analysis harness, then
   **BatchArtists → Report**, then **ApplyChanged**.

Rollback: turn the flag off. Bad bot edits are corrected by re-running with a fixed splitter
(human/service lists are never touched). The unused field is harmless.

### 0.5 Open Items Needing a Decision

- Whether to chase the recall ceiling above: re-enriching the ambiguous backlog from Spotify
  (§6.3 / the last item in §19) is now the highest-value follow-on, and is a bigger win than any
  further heuristic work.
- §18 questions 3-10 still stand. In particular: whether "Various Artists" credits should be
  browsable, whether artist pages belong in the sitemap, and whether to add a nav link.
- Whether `Artist` editing should also open up to `canEdit` (currently `dbAdmin`/creator, while
  `Artists` is `dbAdmin`/`canEdit`/creator).
- Whether to also record Spotify's artist list in server-side import paths (§0.2).

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

### 6.3 Ground Truth from Spotify

`ArtistSplitterGroundTruth.CompareToSpotify` (manual-only, same project). Most songs carry a Spotify
track id (`Purchase:NN:SS`), so it samples distinct credits, fetches `GET /v1/tracks?ids=` 50 at a
time, and compares `track.artists[].name` to the splitter output. Results are in §0.3; reports are
`spotify-accuracy.md` and `spotify-mismatches.tsv`.

```pwsh
$env:M4D_ARTIST_ANALYSIS_INDEX = "C:/projects/music4dance/local/index-2026-09-16.txt"
$env:M4D_ARTIST_ANALYSIS_OUT   = "C:/projects/music4dance/local/artist-analysis"
dotnet test m4dModels.Tests -p:BaseOutputPath=local/build-out/ --filter "TestCategory=Manual"
```

Credentials come from m4d's user secrets (`Authentication:Spotify:ClientId` / `:ClientSecret`), so
there is nothing to set up if the site runs locally; `M4D_SPOTIFY_CLIENT_ID` /
`M4D_SPOTIFY_CLIENT_SECRET` override them. The test skips itself when either the backup or the
credentials are missing. It needs the network, which is why it stays in the manual category. A full
run is ~35 API calls and about 12 seconds.

Three things the harness gets right that a naive comparison wouldn't, and that are worth preserving
if it is ever rewritten:

1. **Two samples, not one.** Precision is sampled per rule (up to `M4D_ARTIST_TRUTH_SAMPLE`, default
   150, distinct credits per rule, so popular credits can't dominate); recall is sampled separately
   across all credits (`M4D_ARTIST_TRUTH_OVERALL`, default 400), because the rule buckets
   over-represent exactly the credits recall is asking about. Both are seeded
   (`M4D_ARTIST_TRUTH_SEED`) so a re-run of the same splitter version reproduces the sample.
2. **Names are compared more loosely than the index matches them.** Punctuation, conjunctions and
   leading articles are folded, so "Earth, Wind & Fire" and "Earth Wind And Fire" agree. Name
   variants are §11.4's problem; counting them here would bury real splitting mistakes.
3. **A collaborator our `Artist` string never mentions is not a precision failure.** Those are
   reported as `incomplete` and excluded from the per-rule denominator, because no heuristic could
   have found them. Conflating the two is what made the first cut of this report useless.

Spotify isn't an oracle. It models "Duke Ellington & His Orchestra" as one artist, often credits
only a track's lead artist, and sometimes counts a remixer as an artist. Treat it as a strong
signal.

### 6.4 Exit Criteria for Phase 0

Targets set before the sample existed:

- **High** rules: ≥ 99% precision on the sample.
- **Medium** rules enabled without evidence only if ≥ 97% precision; otherwise gate them on evidence.
- No **protected act** in the top 1,000 credits by song count is split.
- The `ambiguous.tsv` head has been reviewed and the protected-acts list seeded.

**How they came out.** The 99%/97% thresholds turned out not to be measurable against Spotify,
because most disagreement isn't a splitting mistake: Spotify names acts at a different length,
credits fewer artists than the credit string does, and spells names its own way. Measured as
"agreement or Spotify being less complete", every rule is above 95%; measured strictly, 80-93%
(§0.3). Real misses are 21 of 1,600 sampled credits, and no protected act in the sample was split.

That is good enough to roll out. The judgement being made here is that a wrong entry in the
`Artists` array is cheap — it is one extra name on an artist page, correctable by hand or by a
re-run — whereas the alternative is no artist index at all. Precision per rule is not the
number that should gate the next round of work; recall is, and the ceiling on recall is the
`Artist` string itself, not the heuristic.

---

## 7. Search Index Changes

### 7.1 Field Definition

```csharp
new(ArtistsField, SearchFieldDataType.Collection(SearchFieldDataType.String))
{
    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
}
```

- **Filterable:** needed for `Artists/any(a: a eq 'Kenny Rogers')`. **Confirmed:** in `any` lambdas
  over `Collection(Edm.String)`, Azure allows only comparisons with `eq` or `search.in`, combined
  with `or` — no range operators ([OData collection operator
  reference](https://learn.microsoft.com/en-us/azure/search/search-query-odata-collection-operators)).
  So A–Z bucketing cannot be done in-index, which is the assumption §11 is built on.
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

- Adding `Artists` to the existing `songs` **suggester**. **Confirmed:** "If you try to create a
  suggester using preexisting fields, the API disallows it... you have to rebuild the index if you
  want to add them to a suggester", because prefixes are generated at indexing time and existing
  fields are already tokenized ([Configure a
  suggester](https://learn.microsoft.com/en-us/azure/search/index-add-suggesters)). Artist
  autocomplete on the index page doesn't need the suggester anyway (§11.3).
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
- **Admin → Initialization Tasks shows, per index, whether `Artists` is present and when this
  instance will re-read the schema**, plus the `ArtistIndex` flag state
  (`SongIndex.ArtistsFieldStatusAsync` / `SearchServiceInfo.SchemaCacheExpiry`). That turns step 4
  of the runbook from a blind wait into something readable. It reflects only the instance serving
  the page; others can still lag by up to the cache duration.
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

- [x] `ArtistSplitter` + curated JSON files + test vectors + unit tests (lists ended up in code, §0.2)
- [x] Analysis harness (manual category) reading a `local/` index backup; reports in
      `local/artist-analysis/`
- [x] Spotify ground-truth sampler (`ArtistSplitterGroundTruth`, manual; findings in §0.3)
- [x] Iterate rules until the §6.4 exit criteria are met; record decisions in this doc

### Phase 1 — Model and Replay (dormant)

- [x] `Artists` property constants (C# + TS), replay rules (§4), `EffectiveArtists`, `ArtistsSource`
- [x] Shared C#/TS replay test vectors
- [x] `artist-bot` user: display name, algorithmic lists (no shared `IsAlgorithmicUser` helper; the
      dance vote cap still exempts only `batch*`/`tempo-bot`, which doesn't matter since artist-bot
      never votes)
- [x] History display of `Artists`
- [x] Merge handling (no code needed: merges replay every source song's log)
- Ships dormant: nothing writes `Artists` yet.

### Phase 2 — Index Capability and Backfill

- [x] `SearchServiceInfo.HasFieldAsync` + cache, with field/flag state on Admin → Initialization
      Tasks
- [x] `ArtistsField` in `BuildIndex()`; conditional inclusion in `DocumentFromSong`
- [x] `Admin/AddIndexFields`
- [x] Batch job `Admin/BatchArtists` with `Report | Apply | ApplyChanged` (reuses
      `StreamAllSongsAsync`; no partial merges, resume, or automated verify pass, §0.2)
- [x] Feature flag `ArtistIndex` (read switch) + `FindArtist` switch + fallback
- [x] Save hook (§9) behind the write switch
- **Rollout, test index:** add field → wait for cache TTL/restart → `Report` → review →
  `Apply` → verify → flag on → smoke test.
- **Rollout, production:** repeat. Take a fresh `/Admin/IndexBackup` **before** `Apply` as the
  rollback artifact.

### Phase 3 — UI

- [x] Song details display (substring links) + editing (`canEdit`/`dbAdmin`/creator). The split
      preview endpoint was not built: the save hook applies the split on save.
- [x] Artist page: collaborators panel, artist column visibility
- [x] Artist index snapshot + `/song/artists` page + search endpoint
- [ ] Crawler/sitemap decisions (§11.5) - needs a decision
- [ ] Link from nav/footer/site map (only the artist page links to the index so far)

### Phase 4 — Better Sources

- [x] Spotify `artists[]` capture on create/enrich (§9.3)
- [ ] Re-enrich the backlog from Spotify via a batch. §6.3 showed this is where the remaining
      collaborations are: 80% of the credits Spotify lists with 2+ artists contain no separator for
      any heuristic to find.

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
