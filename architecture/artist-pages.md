# Artist Pages (`artist/App.vue`)

## Overview

The Artist page shows every song whose `Artist` field exactly matches a given string, along with a
per-dance breakdown of how many of those songs are rated for each dance. It's a read-only,
anonymous-accessible page reached at `/song/artist?name={artist}` — most often via an artist-name
link rendered elsewhere in the app (song tables, song detail, album page).

There is no artist entity in the domain model — an "artist page" is just a filtered song list keyed
off the literal string stored in `Song.Artist`. Two artists credited together (`"Dolly Parton"` vs.
`"Dolly Parton & Kenny Rogers"`) are unrelated strings as far as this page is concerned; see
[[song-filter]] for the separate, tokenized search path that can approximate a broader match.

## Server-Side Wiring

- **Route**: no `[Route]` attribute — resolved by the default MVC route
  (`{controller=Home}/{action=Index}/{id?}`, `m4d/Configuration/M4dApplicationExtensions.cs:871-873`)
  to `SongController.Artist(string name)`. `name` is a plain query-string parameter, not a path
  segment or route value, so the URL is always `/song/artist?name=...` (or `/song/artist/?name=...`).
- **Controller action** — `m4d/Controllers/SongController.cs:878-897`:

  ```csharp
  [AllowAnonymous]
  public async Task<ActionResult> Artist(string name)
  {
      ...
      var model = await ArtistViewModel.Create(name, Mapper, DefaultCruftFilter(), Database);
      return Vue3($"Artist: {name}", $"Songs for dancing by {name}", "artist", model, danceEnvironment: true);
  }
  ```

  `"artist"` selects the Vue3 page bundle (`m4d/ClientApp/src/pages/artist/App.vue`);
  `danceEnvironment: true` makes the generic `Vue3.cshtml` host view emit `window.danceDatabaseJson`
  so the client can compute the per-dance breakdown without a second round trip.

- **View model** — `ArtistViewModel.Create` (`m4d/ViewModels/ArtistViewModel.cs:12-27`) does the
  actual lookup:

  ```csharp
  var list = (await dms.SongIndex.FindArtist(name, cruft)).Take(500);
  ```

  capped at 500 songs, filtered by the caller's default cruft setting (hides withdrawn/cruft songs
  for anonymous/non-admin users).

- **Song lookup — exact/phrase match, not substring** — `SongIndex.FindArtist` /
  `FindByField` (`m4dModels/SongIndex.cs:1274-1287`):

  ```csharp
  public virtual async Task<IEnumerable<Song>> FindArtist(string name, CruftFilter cruft = CruftFilter.NoCruft)
      => await FindByField(Song.ArtistField, name, "dance_ALL/Votes desc", cruft);

  public async Task<IEnumerable<Song>> FindByField(string field, string name, string sort = null, CruftFilter cruft = CruftFilter.NoCruft)
  {
      var options = new SearchOptions();
      options.SearchFields.Add(field);
      options.OrderBy.Add(sort);
      return await SongsFromAzureResult(await DoSearch($"\"{name}\"", options, cruft));
  }
  ```

  The name is quoted and searched only against the `Artist` field with Azure Cognitive Search's
  default **Simple** query type, so a quoted string is a phrase match after analysis — effectively
  exact-string matching. There is no `Contains`/`LIKE`/wildcard behavior here: `"Dolly Parton"` will
  not match a song credited to `"Dolly Parton & Kenny Rogers"`.

## Client-Side Rendering

- `m4d/ClientApp/src/pages/artist/App.vue` parses the server-serialized model
  (`TypedJSON.parse(model_, ArtistModel)`, line 12) — the songs are already fetched server-side;
  there is no client-side data fetch for the song list itself.
- `ArtistModel` (`m4d/ClientApp/src/models/ArtistModel.ts:5-7`) extends `SongListModel` and adds
  `artist: string`.
- The song list renders via the shared `SongTable` component
  (`m4d/ClientApp/src/components/SongTable.vue`), with the `artist` column hidden since every row
  shares the same value (`App.vue:69-76`).
- **Per-dance breakdown links**: `App.vue:44-49` builds, for each dance the returned songs are rated
  for, a link to the tokenized/Lucene search path instead of the exact-match path:

  ```ts
  KeywordQuery.fromParts(new Map([["Artist", artist]]));
  ```

  fed into a `SongFilter` and linked as `/song/filtersearch?filter=...`. This is the one place the
  Artist page already departs from exact matching — see [[song-filter]] for what that search type
  actually does (word/token-level match via Azure Lucene `QueryType.Full`, still not a raw
  character-substring match).

## Where Artist-Page Links Are Generated

Several components link to `/song/artist?name={artist}` from elsewhere in the site; these are the
templates for any new artist-related link:

- `m4d/ClientApp/src/components/SongTable.vue:273-275` — `artistRef(song)`
- `m4d/ClientApp/src/pages/song/components/SongCore.vue:114-117` — `artistLink` computed
- `m4d/ClientApp/src/pages/album/App.vue:13` — `artistRef` computed

All three build the URL manually (`` `/song/artist/?name=${encodeURIComponent(song.artist)}` ``)
since it's a plain query string, not a `SongFilter`-encoded route — the "don't hand-build filter
strings" rule in `CLAUDE.md` applies to `SongFilter`/`KeywordQuery`/`DanceQueryItem`/tag strings,
not this simple one-parameter route.

## Known Limitation

Because matching is exact-string on the literal `Artist` field, the Artist page cannot find
collaborations, featured-artist credits, or alternate creditings of the same performer. The
tokenized `filtersearch` path (`KeywordQuery.fromParts` → `Artist:(...)` Lucene query) used for the
per-dance breakdown links is the closest existing broader-match mechanism in the codebase, and is
the natural building block for a "search all songs mentioning this artist" link. See
[[song-filter]] for how that query type is compiled and what it does and doesn't match.
