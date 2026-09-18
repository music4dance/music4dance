# Artist Pages (`artist/App.vue`)

## Overview

The Artist page shows every song by a given artist, along with a per-dance breakdown of how many of
those songs are rated for each dance. It's a read-only, anonymous-accessible page reached at
`/song/artist?name={artist}` — most often via an artist-name link rendered elsewhere in the app
(song tables, song detail, album page).

There is still no artist entity in the domain model — an "artist page" is a filtered song list keyed
off a string. **What that string is matched against depends on the `ArtistIndex` feature flag:**

| Flag | Matches |
| ---- | ------- |
| Off | The whole `Artist` credit, as a phrase. `"Dolly Parton"` does not match `"Dolly Parton & Kenny Rogers"`. |
| On | The song's **individual artists** — the derived `Artists` list — so collaborations, featured credits and leader-of-a-band credits all appear. |

How that list is derived, stored and matched is covered in
[individual-artists.md](individual-artists.md); this document covers the page itself.

## Server-Side Wiring

- **Route**: no `[Route]` attribute — resolved by the default MVC route
  (`{controller=Home}/{action=Index}/{id?}`) to `SongController.Artist(string name)`. `name` is a
  plain query-string parameter, not a path segment, so the URL is always `/song/artist?name=...`.
- **Controller action** — `m4d/Controllers/SongController.cs`:

  ```csharp
  [AllowAnonymous]
  public async Task<ActionResult> Artist(string name)
  {
      ...
      var artistIndex = await FeatureManager.IsEnabledAsync(FeatureFlags.ArtistIndex);
      var model = await ArtistViewModel.Create(
          name, Mapper, DefaultCruftFilter(), Database, artistIndex);

      if (artistIndex && model.Histories.Count < MinimumSongsToIndexArtist)
      {
          ViewData["Robots"] = "noindex, follow";
      }

      return Vue3($"Artist: {name}", $"Songs for dancing by {name}", "artist", model, danceEnvironment: true);
  }
  ```

  `"artist"` selects the Vue3 page bundle (`m4d/ClientApp/src/pages/artist/App.vue`);
  `danceEnvironment: true` makes the generic `Vue3.cshtml` host view emit `window.danceDatabaseJson`
  so the client can compute the per-dance breakdown without a second round trip.

  Splitting credits turned one artist page into roughly 32,500, most of them a single song. Pages
  under five songs ask crawlers to follow but not index them — see
  [individual-artists.md §9.3](individual-artists.md#93-crawlers-and-seo).

- **View model** — `ArtistViewModel.Create` (`m4d/ViewModels/ArtistViewModel.cs`) does the lookup,
  capped at 500 songs and filtered by the caller's default cruft setting (hides withdrawn/cruft
  songs for anonymous/non-admin users).

- **Song lookup** — `SongIndex.FindArtist`:

  ```csharp
  public virtual async Task<IEnumerable<Song>> FindArtist(string name,
      CruftFilter cruft = CruftFilter.NoCruft, bool individualArtists = false)
  {
      if (individualArtists && await HasArtistsFieldAsync())
      {
          var songs = await FindIndividualArtist(name, cruft);
          if (songs.Count > 0)
          {
              return songs;
          }
      }

      return await FindByField(Song.ArtistField, name, "dance_ALL/Votes desc", cruft);
  }
  ```

  `FindIndividualArtist` runs an analyzed phrase search against the `Artists` collection and then
  post-filters each song on `ArtistSplitter.ArtistKey`, so matching is case- and
  diacritic-insensitive and a name inside a longer name (`Tony Evans` within `Tony Evans and His
  Orchestra`) is rejected. Finding nothing falls through to the credit search, so the page degrades
  rather than breaking — which is also what happens for every song the backfill hasn't reached.

  In that fallback the name is quoted and searched only against the `Artist` field with Azure AI
  Search's default **Simple** query type, so a quoted string is a phrase match after analysis —
  effectively exact-string matching, with no `Contains`/`LIKE`/wildcard behaviour.

## Client-Side Rendering

- `m4d/ClientApp/src/pages/artist/App.vue` parses the server-serialized model
  (`TypedJSON.parse(model_, ArtistModel)`) — the songs are already fetched server-side; there is no
  client-side fetch for the song list.
- `ArtistModel` (`m4d/ClientApp/src/models/ArtistModel.ts`) extends `SongListModel` and adds
  `artist: string`.
- The song list renders via the shared `SongTable` component. With the flag **off** the `artist`
  column is hidden, since every row shares the same value; with it **on** the column stays, because
  rows now differ — a collaboration shows its full credit with each individual artist linked.
- **Collaborators**: with the flag on, the page lists the other artists appearing on these songs,
  computed client-side from the returned songs' `effectiveArtists` (`collaborators` in
  `ArtistNames.ts`) — no extra query — and links on to the browsable index at `/song/artists`.
- **Per-dance breakdown links**: for each dance the returned songs are rated for, a link to the
  tokenized/Lucene search path rather than the exact-match path:

  ```ts
  KeywordQuery.fromParts(new Map([["Artist", artist]]));
  ```

  fed into a `SongFilter` and linked as `/song/filtersearch?filter=...`. See [[song-filter]] for
  what that search type actually does (word/token match via Azure Lucene `QueryType.Full`, still
  not a raw character-substring match).

## Where Artist-Page Links Are Generated

Use `artistPageUrl(name)` from `m4d/ClientApp/src/models/ArtistNames.ts` for new links rather than
building the query string by hand.

- `m4d/ClientApp/src/components/ArtistCredit.vue` — renders a credit with each individual artist
  linked inside it, falling back to linking the whole credit when the flag is off or the song has
  no derived list. This is what song tables and song detail use.
- `m4d/ClientApp/src/components/SongTable.vue` — `ArtistCredit` when the flag is on, `artistRef`
  otherwise.
- `m4d/ClientApp/src/pages/album/App.vue` — still builds its own URL for the album's credit.

## Known Limitations

- **Only what the credit named.** Individual artists come from the credit string and the title's
  `feat.` clause, so a collaborator neither mentions is invisible — about 80% of the collaborations
  Spotify knows about, see
  [individual-artists.md §4.3](individual-artists.md#43-the-ceiling).
- **Spelling variants are separate artists.** `ArtistKey` folds case and diacritics, so
  `Michael Bublé` and `Michael Buble` share a page; `P!nk` and `Pink` do not.
- **Capped** at 1,000 songs by the individual-artist query and 500 by the view model. The most
  prolific artist in the corpus has 356.
- With the flag **off**, the original limitation stands in full: exact-string matching on `Artist`
  finds no collaborations, featured credits or alternate creditings at all.
