# Add-Song Matching: What to Bring Over From the Shared-Id Review

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-11-21
**Code:** `m4dModels/SongIndex.cs` (`MergeFromTitle`, `FindMatchingSong`, `MatchSongs`), `m4d/Utilities/MusicServiceManager.cs` (`CreateSong`), `m4dModels/SharedIdMatcher.cs`, `m4dModels/FeaturedCleanup.cs`, `m4dModels/ArtistAliases.cs`

Clearing the shared-id queue showed which differences between two copies of a song are
harmless and which ones mean different recordings. The title match used when songs are added
(`SongIndex.MergeFromTitle`) knows none of this yet. It treats "Pink" and "P!nk" as different
artists, and "Song - Remastered 2011" as a different title from "Song". For an import with only
one candidate, it also ignores length. See
[song-merge-algorithm](../songs/song-merge-algorithm.md) § Matching new songs by title for the
side-by-side comparison.

The shared-id review can be generous because a shared id has already linked the songs and a
person checks what isn't exact. The add-time match has neither. So bring over the parts that
make names *compare* alike, which are safe on their own. Bring the looser tiers over only as far
as length and the service's own data back them up.

## Proposed changes, most value first

### 1. Compare artists the way the `Exact` tier does

In `MergeFromTitle`, compare credits with `SharedIdMatcher`'s artist key in place of
`CreateTitleHash(Artist)`: each credit under its preferred name (`ArtistAliases.PreferredCredit`)
and without the leader's backing group (`ArtistSplitter.RemoveBackingGroup`, with the catalog's
artist knowledge). Then the 330-odd songs credited under a non-preferred alias in the
2026-11-03 snapshot stop collecting duplicates, as does every "X and His Orchestra" credit.

- Keep the blank-credit rule as it is. It's how a title-only upload row finds its song.
- Get the artist knowledge for the two credits the way the save hook does
  (`SongIndex.GetArtistKnowledge`, one small query for the names the credit leaves in doubt), not
  `BuildArtistKnowledge`, which reads the whole index.

### 2. Compare titles without version notes, and search by the cleaned title

Run `SharedIdMatcher.WithoutVersionNotes` on both titles before `CreateTitleHash`, so
"- Remastered", "(2011 Remaster)" and "- Single Version" no longer make a new song. The
full-text search (`SongsFromTitle`) should also search the cleaned title. Today a long
"- Remastered 2011" suffix can push the plain title out of the top 50.

**Don't** bring over `WithoutQualifiers` ("- Radio Edit", "- Live", "- Acoustic") as it stands.
The review groups those "for now", while the length check guards Merge all. At add time, a
qualifier difference should match only when the lengths are close too (see 3).

### 3. Make length count for every match type

Bring `SharedIdMatcher.LengthTolerance` (15 seconds) to the add-time match:

- A `Weak` match, where one candidate is merged by imports whatever its length, is rejected when
  both lengths are known and further apart than the tolerance.
- The `Length` match's 5 seconds can widen to the same 15. The review found that a service's
  lengths for one recording often differ by 5 to 10 seconds of fade or silence.
- A qualifier-only title difference (from 2) matches only within the tolerance.

Tempo, which can outweigh length in the review (`TemposAgree`), usually isn't known at add time.
It could be rechecked after `UpdateAudioData` for an add by link, but that's not worth doing
until 1 to 3 are in.

This is also where `MergeManager.FilterLength` (the admin merge candidates' Level 3, ±20 s of the
median) and `LengthTolerance` should become one constant.

### 4. Name the merged song the way the review does

When a match is found only because of 1 or 2 (alias, backing group, version note), the existing
song keeps its own title and artist, which is fine. But a new song that brings a feat. credit
("X feat. Y" onto "X") has nowhere to put Y. Run `FeaturedCleanup.Propose` after
`AdditiveMerge`, and apply it with the same music4dance edit the review's Merge all tasks use.
Then the featured artist lands in the individual artists, and a remaster note doesn't
overwrite a clean title.

### 5. Use the track the user picked

An add by link or search has the service track in hand. Spotify's artist list is the
same evidence that **Merge artist** and the `FewerArtists` confirmation look up, and it comes
free here:

- Record it as the song's `Artists` (a `Service`-sourced list). This is a known gap in
  [individual-artists](../songs/individual-artists.md) § 13: `Song.CreateFromTrack` doesn't
  record service artist lists.
- With it, a candidate credited to the lead artist alone ("X" for a track by X and Y) can match
  as the `FewerArtists` tier does, when the lengths agree.

### 6. Later: queue the near misses instead of guessing

A candidate that matches only as a `Close` or `FewerArtists` tier without confirmation shouldn't
be merged automatically. But it shouldn't be lost either. It could go into a review queue like
`SharedIdCandidates`, with a `Title match` source, so the same page sorts it out. That queue is
keyed on shared ids today, so it needs a design of its own. Do it only if 1 to 5 still leave
duplicates turning up.

## Order and testing

Steps 1 to 3 change one method (`MergeFromTitle`) and its caller's accept rule. Each can ship
alone, with `MergeFromTitle` cases in `m4dModels.Tests` and `MusicServiceManagerTests`
(`m4d.Tests`) cases for the differences the review
found. Before shipping 1 and 2, run them against the latest snapshot:

- How many existing songs would now match another existing song?
- Those pairs are what the add-time matcher would have merged. Review a sample by hand, the way
  the Preview merges page was used.

Steps 4 and 5 touch the add path's edits and want an integration test through `CreateSong`.

## Risks

- **A wrong merge is worse than a duplicate.** A duplicate can be merged later in one step. A
  wrong merge has to be found first, and then an admin unmerges it from the song page
  (song-merge-algorithm § 4. Unmerging), after which the two songs are kept apart.
  Every loosening above is paired with a length check for that reason.
- **Aliases that aren't just spellings.** A row that joins different entities (as "Juan Luis
  Guerra" and his band "Juan Luis Guerra 4.40" did until it was taken out) suits a cluster that
  already shares an id, but it's a stronger claim when matching by title alone. Mark such rows in
  `ArtistAliases` so the add path can skip them (see [artist-alias-fixup](artist-alias-fixup.md)).
