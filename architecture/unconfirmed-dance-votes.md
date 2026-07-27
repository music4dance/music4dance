# Unconfirmed Dance Votes

## Overview

A dance rating whose current weight traces back entirely to an **unconfirmed vote source** — a
user account (currently just `dgsnure`, the Spotify-playlist auto-import account) whose votes are
applied in bulk by an automated process rather than a person judging the song — is excluded from
default search results, the same way a song with no dance categorization at all is
(`CruftFilter.NoDances`, [[song-filter]]).

The data isn't deleted or hidden entirely: it's still visible on the song's detail page, still
counted in `DanceTags`, and can be brought back into search results via the "Not confirmed by a
dancer" option on Advanced Search, or the equivalent Raw Search checkbox. Only the *default*
search path treats an unconfirmed-only dance as absent.

This is implemented **without any Azure Search index schema change** — no new field, no
`SongIndexNext`/`CodeVersion` migration ([[search-index-versioning]]). It reuses two fields that
already existed (`dance_{id}/Votes`, `dance_ALL/Votes`) via a sentinel value.

---

## Where "unconfirmed source" is not `IsPseudo`

`ApplicationUser`/`ModifiedRecord.IsPseudo` (`m4dModels/ModifiedRecord.cs:31`) marks
**batch/system accounts** (`batch-a`, `batch-s`, `batch-i`, `tempo-bot`, `automerge`) for two
unrelated purposes: display formatting (`DecoratedName`) and exemption from the ±1-per-user-per-dance
vote cap (`Song.TryGetCappedDelta`, `m4dModels/Song.cs:4322`). A batch account is trusted automation
acting on behalf of the system — its votes aren't "unconfirmed," they're just uncapped.

Unconfirmed-source status is a separate, orthogonal concept — **vote trust**, not
automation-vs-human — tracked by its own hardcoded registry right next to `TryGetCappedDelta`
(`Song.cs:4350-4361`):

```csharp
private static readonly HashSet<string> s_unconfirmedVoteSources =
    new(StringComparer.OrdinalIgnoreCase) { "dgsnure" };

private static bool IsUnconfirmedSource(string user)
{
    return user != null && s_unconfirmedVoteSources.Contains(user);
}
```

`dgsnure` is itself a pseudo user, but its username doesn't start with `batch` and isn't
`tempo-bot`, so it is **not** exempt from `TryGetCappedDelta`'s ±1-per-dance cap — a single
unconfirmed source can't currently push a dance's weight past 1 through repeated votes. That
changes only if a future unconfirmed source is also given batch-style cap exemption.

By the time `IsUnconfirmedSource` is checked, the `|P` pseudo-flag suffix has already been
stripped: `ModifiedRecord`'s constructor splits `"dgsnure|P"` into `UserName = "dgsnure"` and
`IsPseudo = true` (`ModifiedRecord.cs:13-20`) before `user` is ever read, so the registry only
ever needs to hold bare usernames.

Adding another unconfirmed source is a one-line change to `s_unconfirmedVoteSources`. If the
roster grows past a handful of names, or needs to change without a deploy, promote it to config or
an admin-editable list — not needed at the current scale.

---

## Attribution: `DanceRating.IsUnconfirmedOnly`

There's no persisted per-voter breakdown of a dance's vote weight — `DanceRating.Weight`
(`m4dModels/DanceRating.cs:67`) is only ever a net aggregate `int`, and songs aren't stored in SQL
at all (they live as Azure Search documents whose `PropertiesField` holds a compressed, ordered log
of `SongProperty` edits). What *is* available is that ordered log, which is already replayed with
per-user attribution every time a `Song` is materialized — that's how the ±1-per-user cap works
today. Both replay paths were extended to also track, per dance, how much of the current net weight
came from **confirmed** (non-unconfirmed-source) voters, and stamp a transient flag once the replay
finishes:

- `Song.LoadProperties` (`m4dModels/Song.cs:1570`) — the path every `Song.Create`/`Song.Load` goes
  through, i.e. every read and every save.
- `Song.SetRatingsFromProperties` (`Song.cs:4263`) — a second, near-duplicate implementation of the
  same replay+cap logic used by an admin "recompute ratings" action
  (`SongController.cs`). The duplication predates this feature; both copies needed the same
  extension.

Both track a `confirmedNet` dictionary (danceId → net weight from confirmed voters) alongside the
existing per-user cap dictionary, incrementing it with the post-cap `effective.Delta` whenever the
voting user is *not* an unconfirmed source. After the replay (and the usual removal of any rating
whose weight settled at or below zero), every surviving `DanceRating` is stamped:

```csharp
foreach (var dr in DanceRatings)
{
    dr.IsUnconfirmedOnly = confirmedNet.GetValueOrDefault(dr.DanceId) <= 0;
}
```

Because a surviving `DanceRating.Weight` is always `> 0` (a rating that would go to zero or below is
removed rather than kept — `Song.cs:2904-2907`, `Song.cs:4208-4211`), `confirmedNet <= 0` for a
surviving rating can only mean every unit of its positive weight traces back to unconfirmed
contributors — possibly netted against a real user's downvote, which still counts as "no genuine
positive signal."

```csharp
// m4dModels/DanceRating.cs:85
// Computed during property replay (LoadProperties / SetRatingsFromProperties); never
// persisted - no [DataMember], so it's excluded from DataContract serialization and
// doesn't round-trip through SongProperties.
public bool IsUnconfirmedOnly { get; set; }
```

`DanceRating` isn't exposed through any client-facing view model, so this flag never leaves the
server.

---

## Index encoding: reusing existing fields

`SongIndex.DocumentFromSong` (`m4dModels/SongIndex.cs:1816`) is where a `Song` becomes the Azure
Search document.

**Per-dance `Votes` sentinel** (`SongIndex.cs:1927`):

```csharp
doc[BuildDanceFieldName(dr.DanceId)] = new Dictionary<string, object>
{
    { Votes, dr.IsUnconfirmedOnly ? -1 : dr.Weight },
    ...
};
```

`-1` is a safe, collision-free sentinel — `Votes` for a real rating is always `>= 0` (see above).
This alone makes `dance_{id}/Votes ge {threshold}` — the clause `DanceQuery.GetODataFilter` emits
for every dance-scoped query (`DanceQuery.cs:155`) — exclude unconfirmed-only dances from any
specific-dance search by default.

**`dance_ALL/Votes` aggregate** (`SongIndex.cs:1945-1946`):

```csharp
// Excludes unconfirmed-only dances, so "does this song have any confirmed dance rating
// at all" can be answered with "dance_ALL/Votes ne null" - see CruftFilter.UnconfirmedDances.
var all = song.DanceRatings.Where(dr => !dr.IsUnconfirmedOnly).Sum(dr => dr.Weight);
doc["dance_ALL"] = new Dictionary<string, object>
{
    { Votes, all == 0 ? null : all },
    ...
};
```

This answers "does this song have any confirmed dance rating at all, regardless of which dance" —
reusing the field's existing null-when-empty behavior rather than adding a new one. One side
effect: default "Sort by Dance Rating" (`dance_ALL/Votes desc` when 2+ or no dances are selected,
`DanceQuery.cs:186`) ranks an unconfirmed-only song's rating as absent instead of boosting it on
fabricated confidence, and the existing numeric-sort-guard
(`(dance_ALL/Votes ne null) and (dance_ALL/Votes ne 0)`, `SongFilter.cs:724-725`) already drops
null values from that sort.

**`DanceTags` is untouched.** The `Song.DanceTags` collection (`SongIndex.cs`, driven by
`TagSummary`, independent of `DanceRating.Weight`) still lists the dance whether or not its votes
are unconfirmed-only — it still shows on the song's detail page, in tag search, and satisfies the
older, coarser `CruftFilter.NoDances`'s `DanceTags/any()` check. Only the *vote-weight* signal is
suppressed.

---

## `CruftFilter.UnconfirmedDances`

```csharp
// m4dModels/CruftFilter.cs
[Flags]
public enum CruftFilter
{
    NoCruft = 0x00,
    NoPublishers = 0x01,
    NoDances = 0x02,
    UnconfirmedDances = 0x04,
    AllCruft = 0x07
}
```

`AllCruft` includes the new bit because `SongIndex`'s full-index backup stream passes it explicitly
to bypass all cruft filtering (`DoSearch(searchString, parameters, CruftFilter.AllCruft)`,
`SongIndex.cs`, used by `BackupIndexStreamingAsync`) — every index backup/rebuild needs to see
unconfirmed-only songs too.

### The aggregate/browse case — `AddCruftInfo`

`SongIndex.AddCruftInfo` (`SongIndex.cs:1471`) has a third clause alongside `Purchase/any()` and
`DanceTags/any()`:

```csharp
if ((cruft & CruftFilter.UnconfirmedDances) != CruftFilter.UnconfirmedDances)
{
    if (extra.Length > 0) { _ = extra.Append(" and "); }
    _ = extra.Append("(not DanceTags/any() or dance_ALL/Votes ne null)");
}
```

The `not DanceTags/any() or ...` guard keeps this clause independent of `NoDances`: a song with no
dance tags at all also has a null `dance_ALL/Votes` (there's nothing to aggregate), so without the
guard this clause would re-exclude exactly the uncategorized songs that the `NoDances` bit is
supposed to opt back in. The guard lets those through unconditionally and reserves the
`dance_ALL/Votes ne null` check for songs that *do* have dance tags but whose only votes are
unconfirmed.

Same bit semantics as the other two: bit **unset** (default) → the restrictive clause is added →
unconfirmed-only songs are hidden from a general/keyword browse. Bit **set** → clause omitted →
they're included.

### The specific-dance case — `DanceQuery.GetODataFilter`

Selecting a specific dance and opting in also brings back that dance's unconfirmed-only matches.
`SongFilter` already owns both `CruftFilter` (`SongFilter.cs:248`) and the call site that builds the
per-dance clause, so this is just a parameter and an extra `or`:

```csharp
// DanceQuery.cs:124
public virtual string GetODataFilter(DanceMusicCoreService dms, bool includeUnconfirmed = false)
{
    ...
    var voteClause =
        $"{danceField}/Votes {(item.Threshold > 0 ? "ge" : "le")} {Math.Abs(item.Threshold)}";
    if (includeUnconfirmed)
    {
        voteClause = $"({voteClause} or {danceField}/Votes eq -1)";
    }
    ...
```

```csharp
// SongFilter.cs:760
private string GetDanceODataFilter(DanceMusicCoreService dms)
{
    return IsRaw
        ? RawDanceQuery?.GetODataFilter(dms)
        : DanceQuery?.GetODataFilter(dms, CruftFilter.HasFlag(CruftFilter.UnconfirmedDances));
}
```

With the checkbox on, selecting "West Coast Swing" emits
`(dance_wcs/Votes ge 1 or dance_wcs/Votes eq -1)`.

**Known limitation**: the sentinel only records *that* a dance's votes are unconfirmed-only, not
their magnitude — the real weight lives only in the in-memory `Song`, never in the index. So
`eq -1` can't respect a threshold: `WCS+1` and `WCS+3` both become `(... ge {n} or ... eq -1)`, and
*any* unconfirmed-only match for that dance is included once the box is checked, regardless of the
requested threshold. This is the tradeoff of not adding a schema field.

Negative-threshold dance queries (`Votes le n`) also match `Votes = -1` even with the cruft bit
unset, since `-1 le n` is true for any `n >= -1` — a "weakly/not rated" query correctly includes a
dance with zero genuine votes.

`RawDanceQuery`/raw filters don't get this treatment — an admin building a raw OData filter
(`RawSearch`, [[song-filter]]'s "Raw filters" section) already has full manual control and can write
`dance_wcs/Votes eq -1` directly.

### Admin Raw Search checkbox

```csharp
// m4dModels/RawSearch.cs
[Display(Name = @"Exclude songs with unconfirmed-only dance votes")]
public bool ExcludeUnconfirmedDances
{
    get => CruftFilter.HasFlag(CruftFilter.UnconfirmedDances);
    set => CruftFilter = value
        ? CruftFilter | CruftFilter.UnconfirmedDances
        : CruftFilter & ~CruftFilter.UnconfirmedDances;
}
```

Rendered in `m4d/Views/Song/RawSearchForm.cshtml` alongside `ExcludePublishers`/`ExcludeDances`.
(Note: as with those two, the property name and its `HasFlag` semantics read backwards from each
other — `true` means the bit that *stops* the restriction from being added, i.e. cruft is
*shown*. Pre-existing quirk in the admin form, not specific to this feature.)

The `Song/RawSearch` GET action model-binds with a restrictive allow-list
(`SongController.cs:438-439`) rather than binding the whole `RawSearch` model, so a new checkbox
property has to be added to that `[Bind("...")]` list explicitly or it silently posts as its
default (`false`) and can never be toggled on from the form:

```csharp
[Bind(
    "SearchText,ODataFilter,SortFields,SearchFields,Description,IsLucene,ExcludePublishers,ExcludeDances,ExcludeUnconfirmedDances")]
RawSearch rawSearch
```

### Advanced Search checkbox

`m4d/ClientApp/src/pages/advanced-search/App.vue`'s "Bonus content:" checkbox group has a third
option alongside "Not found in any publisher catalog" / "Not categorized by dance":

```html
<BFormCheckbox value="U">Not confirmed by a dancer</BFormCheckbox>
```

wired to bit `4` in the `level` cell (`filter.level`), the same generic int cell `Level`/`CruftFilter`
has always used — no wire-format change was needed. "Not confirmed by a dancer" was chosen
deliberately over wording that names the mechanism ("automatic playlist import") or uses
value-laden language ("low quality") — it stays accurate and reads correctly for any future
unconfirmed source, not just `dgsnure`.

---

## Rollout

This was a pure data-content change, not a schema migration — no new field, so none of the
`SongIndexNext`/`CodeVersion` apparatus ([[search-index-versioning]]) was needed. Existing index
documents needed one re-serialize-and-reupload pass (the same `BackupIndexStreamingAsync` +
`UploadIndex`/`SaveSongs` building blocks used for index migrations, targeting the same
index/version rather than a new one) to pick up the new `Votes`/`dance_ALL.Votes` values for
already-unconfirmed-only dances; every save since deploy computes them automatically via
`DocumentFromSong`.

A pre-existing, cruder mechanism served a similar purpose before this feature: `SongController`'s
`NewMusic` action explicitly excluded any song `dgsnure` had ever touched
(`Filter.User = new UserQuery("dgsnure", false).Query`) from the New Music page. That's been
removed — the new mechanism supersedes it and is strictly more precise, since it only suppresses
dance ratings that are actually unconfirmed-only rather than every song the account ever edited
(including ones with genuine confirmed dance votes from other users).

---

## Related Code

| File | Purpose |
| --- | --- |
| `m4dModels/DanceRating.cs` | `Weight` (aggregate), `IsUnconfirmedOnly` |
| `m4dModels/Song.cs` | `LoadProperties`, `SetRatingsFromProperties`, `TryGetCappedDelta`, `IsUnconfirmedSource`, `s_unconfirmedVoteSources` |
| `m4dModels/ModifiedRecord.cs` | `IsPseudo` — a different, orthogonal concept, see "Where 'unconfirmed source' is not `IsPseudo`" above |
| `m4dModels/SongIndex.cs` | `DocumentFromSong`, `AddCruftInfo`, `BackupIndexStreamingAsync`/`UploadIndex` |
| `m4dModels/CruftFilter.cs` | `UnconfirmedDances` bit, `AllCruft` |
| `m4dModels/DanceQuery.cs` | Per-dance OData (`Votes ge/le`), `includeUnconfirmed` opt-in clause |
| `m4dModels/SongFilter.cs` | `CruftFilter`, `GetDanceODataFilter` — where the opt-in threads through |
| `m4dModels/RawSearch.cs` | `ExcludeUnconfirmedDances` admin checkbox shim over `CruftFilter` |
| `m4d/Views/Song/RawSearchForm.cshtml` | Renders the admin checkbox |
| `m4d/ClientApp/src/pages/advanced-search/App.vue` | "Bonus content" checkboxes, `level` bitmask |
| `m4dModels.Tests/UnconfirmedDanceVoteTests.cs` | Attribution, index encoding, `AddCruftInfo`, `DanceQuery`/`SongFilter` opt-in coverage |
| [[song-filter]] | `CruftFilter`/`Level` field, `AddCruftInfo`, wire format |
| [[song-search-service]] | `VoteSearch`/`PostSearch` — unaffected by this feature (they reconstruct real `Song` objects and never touch the index sentinel) |
| [[search-index-versioning]] | Why this feature didn't need it |
