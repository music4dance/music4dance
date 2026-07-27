# Unconfirmed Dance Votes: Design

## Problem

There is currently one user account (`dgsnure`, a pseudo user driving Spotify-playlist
auto-import — with more sources like it expected over time) whose dance votes are lower-confidence
than a typical human curator's: they're applied in bulk by an automated process rather than a
person actually judging the song. We don't want to purge this data — it's still a useful signal and
a starting point for human correction — but a song whose **only** dance categorization comes from
an unconfirmed source shouldn't clutter default search results, the same way a song with **no**
dance categorization at all doesn't today (`CruftFilter.NoDances`, [[song-filter]]).

This doc proposes treating "only unconfirmed votes" as a new kind of cruft, filtered out by default
and opt-in visible via the existing `CruftFilter`/"Bonus content" mechanism — **without adding any
field to the Azure Search index schema**, by reusing two fields (`dance_{id}/Votes`,
`dance_ALL/Votes`) that already exist and are already nullable/sentinel-friendly.

**Public-facing wording**: "unconfirmed" (not "low quality") throughout UI text — e.g. Advanced
Search's new checkbox reads **"Not confirmed by a dancer"** — decided below. This doc uses
"unconfirmed" in code-facing names too, for consistency with the UI and to keep the concept
value-neutral (a source can move from unconfirmed to confirmed over time as real users vote).

---

## Terminology: this is not `IsPseudo`

`ApplicationUser`/`ModifiedRecord.IsPseudo` (`m4dModels/ModifiedRecord.cs:31`) already exists, but
it means something different: it marks **batch/system accounts** (`batch-a`, `batch-s`, `batch-i`,
`tempo-bot`, `automerge`) for two unrelated purposes — display formatting (`DecoratedName`) and
exemption from the ±1-per-user-per-dance vote cap (`TryGetCappedDelta`, `Song.cs:4291-4317`, and
its near-duplicate in `LoadProperties`, `Song.cs:1604`). A batch account is trusted automation
acting on behalf of the system (tempo correction, merges) — its votes aren't "unconfirmed," they're
just uncapped.

The account this doc is about is a *different* axis: **vote trust**, not automation-vs-human. It
needs its own concept — call it an **unconfirmed source** — orthogonal to `IsPseudo`. A future
unconfirmed source could be a regular (non-batch, capped) account; a batch account could remain
fully trusted. Don't conflate the two lists.

`dgsnure` itself *is* pseudo (`IsPseudo = true`), which matters for a different reason: it means
`dgsnure`'s votes may already be exempt from the ±1 cap today, or may not — `TryGetCappedDelta`'s
current check is `user.StartsWith("batch")` / `user == "tempo-bot"`, neither of which matches
`dgsnure`. Confirm during implementation whether `dgsnure` is capped at ±1/dance today or exempted
some other way; if it's currently capped, that limits how much volume a single unconfirmed source
can contribute per dance, which is still useful context for judging how urgent this feature is.
Either way, this is exactly the scenario the feature targets: an uncapped or high-volume pseudo
account can dominate a dance's vote weight without a single real dancer ever weighing in.

**Registry**: follow the exact precedent already in the code (`TryGetCappedDelta`'s
`user == null || user.StartsWith("batch") || user == "tempo-bot"`, `Song.cs:4297`) — a small
hardcoded static check:

```csharp
private static readonly HashSet<string> s_unconfirmedVoteSources =
    new(StringComparer.OrdinalIgnoreCase) { "dgsnure" };

private static bool IsUnconfirmedSource(string user) =>
    user != null && s_unconfirmedVoteSources.Contains(user);
```

**On the `|P` suffix** — no need to encode it. By the time `user` is available at the call site
(`Song.cs:1595-1596`, `currentModified = new ModifiedRecord(prop.Value); user = currentModified.UserName;`),
`ModifiedRecord`'s constructor has already split `"dgsnure|P"` into `UserName = "dgsnure"` and
`IsPseudo = true` (`ModifiedRecord.cs:13-20`) — `user` is always the bare name. `IsUnconfirmedSource`
should compare against `"dgsnure"` only, and does so independent of `IsPseudo`, consistent with
"Terminology" above (the two flags are orthogonal, so matching shouldn't require both).

This needs no DI, no config file, no schema/migration — consistent with how the codebase already
hardcodes the batch/tempo-bot exemption list right next to the logic that uses it. Confirmed
acceptable for now (hardcoded is fine); promote to config or an admin-editable list later if the
roster grows past a handful of names or needs to change without a deploy.

---

## Where per-user, per-dance attribution actually lives today

`DanceRating.Weight` (`m4dModels/DanceRating.cs:67`) is only ever a net aggregate `int` — there is
no persisted per-voter breakdown, and no SQL table for songs/votes at all (songs live entirely as
Azure Search documents, `PropertiesField` holding a compressed, ordered log of `SongProperty`
edits — see [[song-properties-compression]]). But that ordered log **is** the source of truth, and
it's already replayed with per-user attribution every time a `Song` is materialized:

- `Song.LoadProperties` (`m4dModels/Song.cs:1570`, called from every `Song.Create`/`Song.Load`
  path — i.e. every read and every save) tracks the current `user` from `UserField`/`UserProxy`
  properties as it walks the log, and calls `TryGetCappedDelta(drd, user, userDanceContributions,
  out var effective)` (`Song.cs:1604`) before applying each `DanceRatingField` delta — this is what
  enforces the ±1-per-real-user cap per dance today.
- `Song.SetRatingsFromProperties` (`Song.cs:4247`) is a second, near-identical implementation of
  the same replay+cap logic, used by an admin "recompute ratings" action
  (`SongController.cs:1538`) rather than the normal load path. The two are already duplicated (the
  comment at `Song.cs:4255-4256` acknowledges this) — this doc's change has to be made in **both**
  places, which is a good forcing function to finally extract the shared logic into one method both
  call, but that refactor is optional/separable from the feature itself.

Both places already have exactly what's needed: they know, for every `DanceRatingField` delta, both
the dance and the user who cast it (post-cap). Extending them to also classify each contributor as
unconfirmed or not, and roll that up per dance, requires no new data source — just more bookkeeping
in a loop that already exists.

### Proposed extension

Alongside the existing `userDanceContributions` dictionary (used for the cap), track a second,
smaller one — net weight contributed by **confirmed** (non-unconfirmed) users, per dance:

```csharp
var confirmedNet = new Dictionary<string, int>(); // danceId -> net weight from confirmed users

// ...inside the DanceRatingField case, after TryGetCappedDelta produces `effective`:
if (TryGetCappedDelta(drd, user, userDanceContributions, out var effective))
{
    var del = SoftUpdateDanceRating(effective);
    if (!IsUnconfirmedSource(user))
    {
        confirmedNet[effective.DanceId] = confirmedNet.GetValueOrDefault(effective.DanceId) + effective.Delta;
    }
    if (del != null) { drDelete.Add(del); }
}
```

Then, once the log has been fully replayed and the usual zero/negative cleanup has run (`Song.cs:
1789-1799` / the equivalent in `SetRatingsFromProperties`), stamp each surviving `DanceRating`:

```csharp
foreach (var dr in DanceRatings)
{
    dr.IsUnconfirmedOnly = confirmedNet.GetValueOrDefault(dr.DanceId) <= 0;
}
```

Because a surviving `DanceRating.Weight` is always `> 0` (any rating that would go `<= 0` is
already removed — `Song.cs:2904-2907`, `Song.cs:4208-4211`), `confirmedNet <= 0` for a surviving
rating can only mean every unit of its positive weight traces back to unconfirmed contributors
(possibly netted against a real user's downvote — still "no genuine positive signal").

### New field on `DanceRating`

```csharp
// m4dModels/DanceRating.cs
// Computed during property replay (LoadProperties / SetRatingsFromProperties); never
// persisted — no [DataMember], so it's excluded from any DataContract-based serialization
// and doesn't round-trip through SongProperties.
public bool IsUnconfirmedOnly { get; set; }
```

`DanceRating` isn't exposed through any client-facing view model today (confirmed — no
`m4d/ViewModels` type maps it), so there's no accidental leakage to worry about, but keep it
un-annotated (no `[DataMember]`) defensively so it can never be picked up if that changes.

---

## Index encoding — reusing existing fields, no schema change

`SongIndex.DocumentFromSong` (`m4dModels/SongIndex.cs:1806`) is where a `Song` becomes the Azure
Search document. Two edits, both reusing fields that already exist:

### 1. Per-dance `Votes` sentinel

```csharp
// SongIndex.cs:1909, inside the `foreach (var dr in song.DanceRatings)` loop
doc[BuildDanceFieldName(dr.DanceId)] = new Dictionary<string, object>
{
    { Votes, dr.IsUnconfirmedOnly ? -1 : dr.Weight },   // was: dr.Weight
    ...
};
```

`-1` is a safe, collision-free sentinel: `Votes` for a real rating is always `>= 0` today (see
above), and it's confirmed nowhere in the codebase does a legitimate dance ever carry a negative
weight in the index (`EditDanceRating`/`SoftUpdateDanceRating` remove the rating instead of letting
it go negative). By default (cruft bit unset) this makes `dance_{id}/Votes ge {threshold}` — the
clause `DanceQuery.GetODataFilter` already emits for every dance-scoped query (`DanceQuery.cs:151`)
— naturally exclude unconfirmed-only dances from specific-dance search. Bringing them back on opt-in
is handled in "New `CruftFilter` bit" below — it's a small, deliberate addition to `DanceQuery`, not
left out.

### 2. `dance_ALL/Votes` aggregate

```csharp
// SongIndex.cs:1928
var all = song.DanceRatings.Where(dr => !dr.IsUnconfirmedOnly).Sum(dr => dr.Weight); // was: .Sum(...)
doc["dance_ALL"] = new Dictionary<string, object>
{
    { Votes, all == 0 ? null : all },   // unchanged — reuses the existing null-when-empty rule
    ...
};
```

This is the piece that answers "does this song have **any** real dance categorization at all,
regardless of which dance" — the browse/aggregate-level equivalent of the per-dance case above —
using the field that already exists for exactly this kind of "has an overall rating" signal
(`dance_ALL/Votes` is already `null` for a song with zero dance ratings; now it's also `null` for a
song whose only ratings are unconfirmed-only). No new field.

Side effect (desirable, not a regression): default "Sort by Dance Rating" — which, with 2+ or no
dances selected, sorts by `dance_ALL/Votes desc` (`DanceQuery.cs:186`) — will rank an
unconfirmed-only song's rating as absent rather than boosting it on fabricated confidence, and the
existing numeric-sort-guard (`(dance_ALL/Votes ne null) and (dance_ALL/Votes ne 0)`,
`SongFilter.cs:724-725`) already drops null values from that sort for free.

### `DanceTags` — deliberately left untouched

The `Song.DanceTags` collection (`SongIndex.cs:1884`, driven by `TagSummary`, independent of
`DanceRating.Weight`) keeps listing the dance whether or not its votes are unconfirmed-only. This
is intentional: it's what the user asked for ("don't want to completely remove them... they do
provide some value") — the dance still shows up on the song's detail page, in tag search, and in
`CruftFilter.NoDances`'s `DanceTags/any()` check (a song with only an unconfirmed-only dance still
counts as "categorized" for that older, coarser cruft bit). Only the *vote-weight* signal is
suppressed, via the two `Votes` fields above.

---

## New `CruftFilter` bit — and bringing unconfirmed votes back for a specific dance

```csharp
// m4dModels/CruftFilter.cs
[Flags]
public enum CruftFilter
{
    NoCruft = 0x00,
    NoPublishers = 0x01,
    NoDances = 0x02,
    UnconfirmedDances = 0x04,  // NEW
    AllCruft = 0x07,           // was 0x03 — MUST include the new bit
}
```

**`AllCruft` must be updated.** It's not just "everything OR'd together" cosmetically — `SongIndex`'s
full-index backup stream passes it explicitly to bypass all cruft filtering
(`DoSearch(searchString, parameters, CruftFilter.AllCruft)`, `SongIndex.cs:2065`, used by
`BackupIndexStreamingAsync`, which every index migration/rebuild depends on, [[search-index-versioning]]).
If `AllCruft` isn't extended to include the new bit, a full backup would silently start skipping
unconfirmed-only songs.

### The aggregate/browse case — `AddCruftInfo`

`SongIndex.AddCruftInfo` (`SongIndex.cs:1471`) gets a third clause, symmetric with the existing two:

```csharp
if ((cruft & CruftFilter.UnconfirmedDances) != CruftFilter.UnconfirmedDances)
{
    if (extra.Length > 0) { _ = extra.Append(" and "); }
    _ = extra.Append("dance_ALL/Votes ne null");
}
```

Same bit semantics as the existing two: bit **unset** (default, `NoCruft = 0`) → the restrictive
clause is added → unconfirmed-only songs are hidden from a general/keyword browse. Bit **set** →
clause omitted → they're included in the browse.

### The specific-dance case — this is the core use case, and it's simple

Turns out threading the opt-in into a per-dance query isn't the complexity risk originally flagged
— `SongFilter` already owns both `CruftFilter` and the call site that builds the per-dance clause,
so there's no piping problem, just one parameter and one extra `or`:

```csharp
// DanceQuery.cs — GetODataFilter gains a parameter
public virtual string GetODataFilter(DanceMusicCoreService dms, bool includeUnconfirmed = false)
{
    ...
    var subFilters = matches.Select(d =>
    {
        var danceField = $"dance_{d.Id}";
        var voteClause = $"{danceField}/Votes {(item.Threshold > 0 ? "ge" : "le")} {Math.Abs(item.Threshold)}";
        if (includeUnconfirmed)
        {
            voteClause = $"({voteClause} or {danceField}/Votes eq -1)";
        }
        var filterParts = new List<string> { voteClause };
        ...
```

```csharp
// SongFilter.cs:760 — GetDanceODataFilter already has CruftFilter (SongFilter.cs:248) on `this`
private string GetDanceODataFilter(DanceMusicCoreService dms)
{
    return IsRaw
        ? RawDanceQuery?.GetODataFilter(dms)
        : DanceQuery?.GetODataFilter(dms, CruftFilter.HasFlag(CruftFilter.UnconfirmedDances));
}
```

With the checkbox on, selecting "West Coast Swing" now emits
`(dance_wcs/Votes ge 1 or dance_wcs/Votes eq -1)` — exactly "bring back the unconfirmed votes for
this dance," which is the stated core use case.

**One real tradeoff, not a complexity concern**: the sentinel only records *that* a dance's votes
are unconfirmed-only, not their magnitude (that's the cost of not adding a schema field — the real
weight only lives in the in-memory `Song`, never in the index). So `eq -1` can't respect a
threshold — `WCS+1` and `WCS+3` both become `(... ge {n} or ... eq -1)`, and *any* unconfirmed-only
match for that dance is included once the box is checked, regardless of the threshold you set. In
practice this is probably fine (there's no real per-vote magnitude behind an unconfirmed rating to
threshold against anyway — it's binary, "an auto-import matched this dance" or not), but worth
being explicit about since it's a genuine information loss versus a schema change, not an
implementation shortcut.

`RawDanceQuery`/raw filters are **not** given this treatment — raw filters are hand-built OData by
an admin (`RawSearch`, [[song-filter]]'s "Raw filters" section) who already has full manual control
and can write `dance_wcs/Votes eq -1` directly if needed.

### `RawSearch.cs` admin checkbox

```csharp
[Display(Name = @"Exclude songs with unconfirmed-only dance votes")]
public bool ExcludeUnconfirmedDances
{
    get => CruftFilter.HasFlag(CruftFilter.UnconfirmedDances);
    set => CruftFilter = value
        ? CruftFilter | CruftFilter.UnconfirmedDances
        : CruftFilter & ~CruftFilter.UnconfirmedDances;
}
```

(Note: the existing two `RawSearch` properties are named "Exclude..." but bound the opposite way
round from what the name implies — `HasFlag` true is actually the "*don't* exclude, i.e. show
cruft" state, per `AddCruftInfo` above. That's a pre-existing naming quirk in the admin-only form,
not something this doc needs to fix, but worth keeping the new property consistent with its
siblings rather than "fixing" it in isolation.)

---

## Advanced Search UX

`m4d/ClientApp/src/pages/advanced-search/App.vue`'s "Bonus content:" checkbox group
(`App.vue:627-635`) already renders the two existing bits as opt-in checkboxes:

```html
<BFormGroup id="bonuses-group" class="mx-2 mb-2" label="Bonus content:" label-for="bonuses">
  <BFormCheckboxGroup id="bonuses" v-model="bonuses">
    <BFormCheckbox value="P">Not found in any publisher catalog</BFormCheckbox>
    <BFormCheckbox value="D">Not categorized by dance</BFormCheckbox>
    <BFormCheckbox value="U">Not confirmed by a dancer</BFormCheckbox> <!-- NEW -->
  </BFormCheckboxGroup>
  ...
</BFormGroup>
```

`computeBonuses()`/the `songFilter` computed (`App.vue:161-167`, `308-317`) get the third bit:

```ts
if (bonuses.value.indexOf("P") !== -1) level = 1;
if (bonuses.value.indexOf("D") !== -1) level += 2;
if (bonuses.value.indexOf("U") !== -1) level += 4; // NEW
```

No wire-format change: `SongFilter.level` (`m4d/ClientApp/src/models/SongFilter.ts:47,131`) is
already a generic int cell — this is purely a UI/bitmask change on both ends, nothing new to parse
or serialize.

"Not confirmed by a dancer" was chosen over alternatives ("Not yet confirmed by the community", "Only
matched by automatic playlist import") specifically because it doesn't name the mechanism
(Spotify/auto-import) — it'll keep reading correctly once other unconfirmed sources exist beyond
`dgsnure`, and it avoids "low quality" or similar value-laden language while staying accurate: the
one true fact being encoded is "no dancer has confirmed this."

---

## Rollout — a data-content change, not a schema migration

This deliberately sidesteps the whole `SongIndexNext`/`CodeVersion` apparatus documented in
[[search-index-versioning]] — no new field is added to `BuildIndex()`, so none of that machinery
(index version bump, `SEARCHINDEXVERSION`, dual-schema `TODOIDX` shims, `UpdateSearchIdx` cutover)
is needed. This is the main practical payoff of the "no schema change" constraint.

What **is** needed: every document already sitting in the live index was serialized under the old
logic and still holds real (non-sentinel) `Votes`/`dance_ALL.Votes` values for any unconfirmed-only
dance — they won't reflect the new suppression until re-serialized. After deploying the code change:

- Re-run the existing "stream every song, re-run `DocumentFromSong`, re-upload" backfill pattern —
  the same building block `BackupIndexStreamingAsync` + `UploadIndex`/`SaveSongs` already provides
  for index migrations (`AdminController.cs` around `IndexBackup`/`UpdateSearchIdx`,
  `SongIndex.cs:889-894`), just targeting the **same** index/version rather than a new one. No
  index reset, no name change, no version bump.
- New saves/edits after deploy get correct values automatically — `DocumentFromSong` always runs on
  the current `Song` state, so this is self-healing for any song touched after rollout even without
  the backfill; the backfill just makes existing untouched songs correct immediately rather than
  gradually.

---

## Edge cases & interactions

- **`VoteSearch`/`PostSearch` (user-specific "did I vote for this dance" queries, [[song-search-service]])
  are unaffected.** Those reconstruct real `Song` objects from the property log and read actual
  `DanceRating`/vote state in memory — they never touch the index's `Votes` sentinel, so a user's
  own real vote history is never misrepresented by this change.
- **Negative-threshold dance queries** (`DanceQueryItem`'s `-n` syntax, `Votes le n`,
  `DanceQuery.cs:151`) will also match an unconfirmed-only dance's `Votes = -1` even with the cruft
  bit unset, since `-1 le n` is true for any `n >= -1`. This reads as semantically correct — a
  "weakly/not rated" query should include a dance with zero genuine votes — but call it out
  explicitly since it wasn't the primary target of this feature.
- **Opt-in for a specific dance shows all-or-nothing, not threshold-scoped**, per the tradeoff
  described above — checking "Not confirmed by a dancer" while filtering on "West Coast Swing +3"
  will include any unconfirmed-only WCS match, not just ones that would have hit a weight of 3.

---

## Implementation checklist

| File | Change |
| --- | --- |
| `m4dModels/DanceRating.cs` | Add transient `IsUnconfirmedOnly` bool (no `[DataMember]`) |
| `m4dModels/Song.cs` | `LoadProperties` (~1570) and `SetRatingsFromProperties` (~4247): add `IsUnconfirmedSource` check, `confirmedNet` tracking, post-loop stamping. Confirm whether `dgsnure` is already covered by `TryGetCappedDelta`'s batch-exemption check or still capped at ±1/dance today. Consider extracting the now-triple-duplicated cap/attribution logic into one shared helper both call. |
| `m4dModels/SongIndex.cs` | `DocumentFromSong` (~1806): per-dance `Votes` sentinel (~1911), `dance_ALL` aggregate (~1928). `AddCruftInfo` (~1471): new clause. |
| `m4dModels/CruftFilter.cs` | Add `UnconfirmedDances = 0x04`; bump `AllCruft` to `0x07` |
| `m4dModels/DanceQuery.cs` | `GetODataFilter`: new `includeUnconfirmed` parameter, `or {danceField}/Votes eq -1` clause |
| `m4dModels/SongFilter.cs` | `GetDanceODataFilter` (~760): pass `CruftFilter.HasFlag(CruftFilter.UnconfirmedDances)` through to `DanceQuery.GetODataFilter` |
| `m4dModels/RawSearch.cs` | New `ExcludeUnconfirmedDances` checkbox-bool property |
| `m4d/ClientApp/src/pages/advanced-search/App.vue` | New "Bonus content" checkbox ("Not confirmed by a dancer") + bitmask wiring |
| Tests | `m4dModels.Tests/DanceRatingCapTests.cs` (extend for the new attribution logic, both `LoadProperties` and `SetRatingsFromProperties` paths), `SongIndex` doc-building tests (assert `-1` sentinel and `dance_ALL/Votes` null-when-unconfirmed-only), `DanceQueryTest.cs` (assert the `or ... eq -1` clause appears only when the cruft bit is set), `CruftFilter`/`AddCruftInfo`/`RawSearch` tests for the new bit |
| Backfill | One-time admin re-serialize-and-reupload pass over the live index after deploy (existing `BackupIndexStreamingAsync`/`UploadIndex` building blocks — no version bump) |

## Related Code

| File | Purpose |
| --- | --- |
| `m4dModels/DanceRating.cs` | `Weight` (aggregate), new `IsUnconfirmedOnly` |
| `m4dModels/Song.cs` | `LoadProperties`, `SetRatingsFromProperties`, `TryGetCappedDelta`, `EditDanceRating` |
| `m4dModels/ModifiedRecord.cs` | Existing `IsPseudo` — a different concept, see "Terminology" above |
| `m4dModels/SongIndex.cs` | `DocumentFromSong`, `AddCruftInfo`, `BackupIndexStreamingAsync`/`UploadIndex` |
| `m4dModels/CruftFilter.cs` | The bitmask this doc adds a member to |
| `m4dModels/RawSearch.cs` | Admin Raw Search checkbox shim over `CruftFilter` |
| `m4dModels/DanceQuery.cs` | Per-dance OData (`Votes ge/le`) — gains the `includeUnconfirmed` opt-in clause |
| `m4dModels/SongFilter.cs` | `CruftFilter`, `GetDanceODataFilter` — where the opt-in threads through |
| `m4d/ClientApp/src/pages/advanced-search/App.vue` | "Bonus content" checkboxes |
| [[song-filter]] | `CruftFilter`/`Level` field, `AddCruftInfo`, wire format |
| [[song-search-service]] | `VoteSearch`/`PostSearch` — confirmed unaffected |
| [[search-index-versioning]] | Why this change doesn't need it |
