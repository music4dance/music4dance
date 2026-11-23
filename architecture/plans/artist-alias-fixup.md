# Artist Alias Fix-Up: Rewriting Credits

**Type:** Plan
**Status:** Proposed (optional). Folding aliases into individual artists shipped 2026-11-22.
**Last verified:** 2026-11-22
**Code:** `m4d/ClientApp/src/models/artist-aliases.json`, `m4dModels/ArtistAliases.cs` (`PreferredCredit`), `m4dModels/Song.cs` (`HasArtistAlias`, `AppendNameEdit`), `m4d/Controllers/AdminController.cs` (`BatchArtists`)

`ArtistAliases` names the spelling to use for an artist credited several ways ("Pink" is "P!nk",
"Ke$ha" is "Kesha"). The individual artists, the artist pages and the song page's credit links
already read every alias under the name to use, from `artist-aliases.json`, which the server and
the client share. See [individual-artists](../songs/individual-artists.md) § 2.2, § 2.4, § 3.4,
§ 5.4, § 6 and § 8. Running it after a table change is in
[artist-index-operations](../runbooks/artist-index-operations.md) § After an alias change.

The credit itself (`Artist`) is still left as the service wrote it. This plan is what rewriting it
would take.

## Rewriting credits

Rewriting `Artist` ("Ke$ha" → "Kesha" in the credit) makes the song list and search results
consistent. But it overrides what the service said, and it's what the review's merges already do
for the songs they touch. If you want it:

- Mark each alias row with whether its credits may be rewritten. A pure respelling (P!nk/Pink,
  Kesha/Ke$ha, Son By Four/Son By 4, the 101 Strings Orchestra spellings) can be. A row joining
  different entities should only fold individual artists. (Juan Luis Guerra and his band 4.40
  were taken out of the table on 2026-11-22 to keep them apart on artist pages.)
  A leader and their group is the splitter's job, not the alias table's ("Andy Fortuna
  Productions" keeps its credit and has Andy Fortuna as its individual artist).
- Add a `Report`/`Apply` admin task that runs `ArtistAliases.PreferredCredit` over the songs
  `HasArtistAlias` finds. It writes the change as an edit by a new `alias-bot` pseudo user
  (`Song.AppendNameEdit`), so the history shows why it changed and a person's later edit wins.
- Skip songs whose `Artist` a real user set. That's the same precedence `UpdateArtists` keeps
  for `Artists`.
- Report first: log every `before -> after`, as `BatchArtists Report` does, and check the
  list before applying.

The add-time match ([add-song-matching-next](add-song-matching-next.md) § 1) reads the same
table, so the row marks serve it too.
