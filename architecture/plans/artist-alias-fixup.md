# Artist Alias Fix-Up

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-11-21
**Code:** `m4dModels/ArtistAliases.cs`, `m4dModels/Song.cs` (`EffectiveArtists`, `UpdateArtists`, `AppendNameEdit`), `m4dModels/ArtistSplitter.cs`, `m4d/Controllers/AdminController.cs` (`BatchArtists`), `m4d/Services/ArtistIndexCache.cs`

`ArtistAliases` names the spelling to use for an artist credited several ways ("Pink" is "P!nk",
"Lady Antebellum" is "Lady A"). Today only the shared-id review reads it, to compare credits and
to name a merged song. Everywhere else the spellings are still different artists: the artist
index lists both, each artist page shows only its own songs, and searching for one misses the
other. This was flagged as deferred work in
[individual-artists](../songs/individual-artists.md) § 13 ("Name variants are not reconciled").

## How much there is

Counted on the 2026-11-03 snapshot, before the shared-id merges (song credits whose whole credit,
or its primary artist before a feat. clause, is a non-preferred spelling):

| Preferred | Other spellings (songs) | Preferred spelling (songs) |
| --- | --- | --- |
| 101 Strings Orchestra | 112 (seven spellings) | 38 |
| Andy Fortuna | 59 | 54 |
| El Gran Combo | 49 | 11 |
| Ke$ha | 31 | 20 |
| Juan Luis Guerra 4.40 | 24 | 28 |
| Michael Paul Kramer | 17 | 31 |
| Lady A | 14 | 25 |
| P!nk | 9 | 93 |
| Sasha Alex Sloan | 8 | 5 |
| Son By Four, Yambao, Village People, Bobby V., Café Latino | 7 | 16 |

That's about 330 songs, plus 10 whose individual artists (`Artists`) name a non-preferred
spelling. Where a row's first name isn't the most common spelling (El Gran Combo, Andy Fortuna,
Sasha Alex Sloan, Ke$ha), check that it's the name you want shown. The artist has used "Kesha"
since 2014, for example.

## Two separate fixes

A song has two artist fields ([individual-artists](../songs/individual-artists.md) § 1):
`Artist`, the credit exactly as a service wrote it, and `Artists`, the individual artists that
artist pages and the artist index use. They need different treatment.

### 1. Individual artists: fold aliases when they're read (recommended first)

Apply `ArtistAliases.PreferredName` to each name where individual artists are produced:

- `Song.EffectiveArtists`, for both a recorded `Artists` list and the cleaned-credit fallback.
- The splitter's final name pass (`ArtistSplitter`, § 3.4), so a freshly split list is already
  preferred. Bump `ArtistSplitter.Version`.
- The artist page's lookup (`SongIndex.FindIndividualArtist`, which compares `ArtistKey`s), so `/song/artist?name=Kesha` finds
  the Ke$ha songs, and old links keep working.

This writes nothing to the property log. A credit stays as the service wrote it, which is the
rule individual artists were designed around. User and service lists aren't overwritten either:
they're read under the preferred name. Only the search index has to catch up:

- Re-save the songs whose `Artists` index field holds a non-preferred spelling. Add a
  `BatchArtists` mode, `Aliases`, that filters
  `Artists/any(a: search.in(a, '<every non-preferred spelling>', '|'))`. It's a few hundred
  songs, so it runs in a minute rather than an hour, and it re-saves them with `UpdateAzureIndex`
  as `ApplyChanged` does.
- Rebuild the artist index snapshot afterwards (`ArtistIndexCache`), the way
  [runbooks/artist-index-operations](../runbooks/artist-index-operations.md) does after a backfill.
- Re-run the `Aliases` mode whenever rows are added to the table. Keeping it a filtered search
  keeps it cheap.

**Client parity.** `Song.ts` replays `Artists` too (§ 2.4), so the song page would still show
"Kesha" until the client knows the table. Move the aliases into a JSON file that both C# and
TypeScript read, as `artists-replay-cases.json` already is for the replay rules, and apply it
in the client's effective artists. Until then the page links to `/song/artist?name=Kesha`,
which still works once the artist page folds the name.

### 2. Credits: rewrite only where asked (optional, after 1)

Rewriting `Artist` itself ("Kesha" → "Ke$ha" in the credit) makes the song list and search
results consistent. But it overrides what the service said, and it's what the review's merges
already do for the songs they touch. If you want it:

- Mark each alias row with whether its credits may be rewritten. A pure respelling (P!nk/Pink,
  Ke$ha/Kesha, Son By Four/Son By 4, the 101 Strings Orchestra spellings) can be. A row joining
  different entities (Juan Luis Guerra and his band 4.40, Andy Fortuna and Andy Fortuna
  Productions) should only fold individual artists.
- Add a `Report`/`Apply` admin task that runs `ArtistAliases.PreferredCredit` over the songs
  the `Aliases` filter finds. It writes the change as an edit by a new `alias-bot` pseudo user
  (`Song.AppendNameEdit`), so the history shows why it changed and a person's later edit wins.
- Skip songs whose `Artist` a real user set. That's the same precedence `UpdateArtists` keeps
  for `Artists`.
- Report first: log every `before -> after`, as `BatchArtists Report` does, and check the
  list before applying.

## Order

1. Decide the preferred spelling for each row, and mark the rows that join different entities.
2. Fold aliases in `EffectiveArtists`, the splitter and the artist page lookup, with tests.
3. Add the `BatchArtists` `Aliases` mode. Run it on the test index, check the artist index and a
   few artist pages, then run it on production.
4. Move the table to shared JSON and fold it on the client.
5. Only if wanted: the credit rewrite, `Report` then `Apply`.

The add-time match ([add-song-matching-next](add-song-matching-next.md) § 1) reads the same
table, so the row marks from step 1 serve it too.
