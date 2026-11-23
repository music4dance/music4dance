# Artist Alias Fix-Up: What's Left

**Type:** Plan
**Status:** Partially implemented
**Last verified:** 2026-11-22
**Code:** `m4d/ClientApp/src/models/artist-aliases.json`, `m4dModels/ArtistAliases.cs`, `m4dModels/Song.cs` (`EffectiveArtists`, `HasArtistAlias`, `AppendNameEdit`), `m4dModels/ArtistSplitter.cs`, `m4d/Controllers/AdminController.cs` (`BatchArtists`), `m4d/ClientApp/src/models/Song.ts`

`ArtistAliases` names the spelling to use for an artist credited several ways ("Pink" is "P!nk",
"Ke$ha" is "Kesha"). Shipped 2026-11-22: the splitter, `Song.EffectiveArtists` (server and
client) and the artist page lookup read every individual artist under the name to use, the song
page links an artist on any of their spellings in the credit, and `BatchArtists ApplyChanged`
re-saves the songs that changes. The table is `m4d/ClientApp/src/models/artist-aliases.json`,
shared by both sides. The credit itself is left as the service wrote it. See
[individual-artists](../songs/individual-artists.md) § 2.2, § 2.4, § 3.4, § 5.4, § 6 and § 8.

## Running it

Restart the server, then run `BatchArtists` `ApplyChanged` (Admin → Initialization Tasks), first
on the test index and then on production. One pass does both fixes:

- Songs with no list a person or a service recorded get one from the splitter (version 4), by
  `artist-bot`: Kesha for a "Ke$ha" credit, and Andy Fortuna for "Andy Fortuna Productions" (the
  splitter's leader-and-group rule, which now includes "Productions").
- Songs whose recorded list names an alias keep their log, and are re-saved so the index field
  reads them under the name to use (`AliasesReindexed` in the summary).

Check a few artist pages afterwards (`?name=Ke$ha` and `?name=Kesha` should show the same songs)
and the artist index. Re-run `ApplyChanged` whenever rows are added to the table.

On the 2026-11-03 snapshot, before the shared-id merges, about 270 songs were credited under a
non-preferred spelling (the 101 Strings Orchestra spellings were 112 of them), plus 10 whose
recorded individual artists named one. Add the 59 Andy Fortuna Productions songs and the 5 of
John Upegui Productions and Steve Rice Productions. The other "Productions" credits (WinRon,
Ferran, Paxus and so on) have no leader credited alone, so they stay whole.

## What's left

### Optionally, rewrite credits

Rewriting `Artist` itself ("Ke$ha" → "Kesha" in the credit) makes the song list and search
results consistent. But it overrides what the service said, and it's what the review's merges
already do for the songs they touch. If you want it:

- Mark each alias row with whether its credits may be rewritten. A pure respelling (P!nk/Pink,
  Kesha/Ke$ha, Son By Four/Son By 4, the 101 Strings Orchestra spellings) can be. A row joining
  different entities should only fold individual artists. (Juan Luis Guerra and his band 4.40
  were taken out of the table on 2026-11-22 to keep them apart on artist pages.)
  A leader and their group is the splitter's job, not the alias table's (see Andy Fortuna
  above).
- Add a `Report`/`Apply` admin task that runs `ArtistAliases.PreferredCredit` over the songs
  `HasArtistAlias` finds. It writes the change as an edit by a new `alias-bot` pseudo user
  (`Song.AppendNameEdit`), so the history shows why it changed and a person's later edit wins.
- Skip songs whose `Artist` a real user set. That's the same precedence `UpdateArtists` keeps
  for `Artists`.
- Report first: log every `before -> after`, as `BatchArtists Report` does, and check the
  list before applying.

The add-time match ([add-song-matching-next](add-song-matching-next.md) § 1) reads the same
table, so the row marks serve it too.
