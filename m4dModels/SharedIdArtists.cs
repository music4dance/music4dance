using System.Diagnostics;

namespace m4dModels;

/// <summary>
/// The songs' artists combined, for the shared-id review's Merge artist and Force merge artist:
/// every individual artist any of the songs names, once each, as a credit joined with " &amp; "
/// and as the individual artists. Confirmed when a Spotify or iTunes track the songs have (the
/// ones they share first) credits all of them; otherwise Missing is the ones the first track
/// found leaves out, and Via is that track's id. Reason says why nothing could be checked.
/// </summary>
public record SharedIdCombinedArtists(string Artist, IReadOnlyList<string> Artists, bool Confirmed = false,
    string Via = null, IReadOnlyList<string> Missing = null, string Reason = null);

public static class SharedIdArtists
{
    /// <summary>A bound on the lookups one check makes.</summary>
    public const int MaxLookups = 10;

    /// <summary>
    /// Every individual artist the songs name (FeaturedCleanup.IndividualArtists, each without a
    /// leader's backing group and under the name to use, ArtistAliases), once each, in the order
    /// the songs name them.
    /// </summary>
    public static IReadOnlyList<string> Combine(IReadOnlyList<Song> songs)
    {
        var knowledge = Knowledge(songs);
        return
        [
            .. songs.SelectMany(s => FeaturedCleanup.IndividualArtists(s, knowledge))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => ArtistAliases.PreferredName(ArtistSplitter.RemoveBackingGroup(a.Trim(), knowledge)))
                .DistinctBy(ArtistSplitter.ArtistKey)
        ];
    }

    /// <summary>
    /// The songs' artists combined (Combine), checked against their tracks unless
    /// <paramref name="force"/>: Confirmed as soon as one of them credits every artist.
    /// </summary>
    public static async Task<SharedIdCombinedArtists> Propose(IReadOnlyList<Song> songs,
        Func<string, Task<ServiceTrack>> lookup, bool force)
    {
        var artists = Combine(songs);
        var artist = string.Join(" & ", artists);
        if (force)
        {
            return new SharedIdCombinedArtists(artist, artists);
        }

        // The Spotify and iTunes ids, the ones more of the songs have first
        var ids = songs
            .SelectMany(s => s.GetExtendedPurchaseIds().Distinct().Select(id => (Id: id, s.SongId)))
            .Where(h => h.Id.StartsWith("S:") || h.Id.StartsWith("I:"))
            .GroupBy(h => h.Id)
            .OrderByDescending(g => g.Select(h => h.SongId).Distinct().Count())
            .ThenBy(g => g.Key[0] == 'S' ? 0 : 1).ThenBy(g => g.Key)
            .Select(g => g.Key)
            .Take(MaxLookups)
            .ToList();

        var knowledge = Knowledge(songs);
        (string Id, IReadOnlyList<string> Missing)? first = null;
        foreach (var id in ids)
        {
            ServiceTrack track;
            try
            {
                track = await lookup(id);
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"SharedIdArtists: couldn't look up {id}: {e.Message}");
                continue;
            }

            if (track == null || string.IsNullOrWhiteSpace(track.Name))
            {
                continue;
            }

            var credited = SharedIdOwners.TrackArtistKeys(track, knowledge);
            var missing = artists.Where(a => !credited.Contains(ArtistSplitter.ArtistKey(a))).ToList();
            if (missing.Count == 0)
            {
                return new SharedIdCombinedArtists(artist, artists, true, id, []);
            }

            first ??= (id, missing);
        }

        return first is { } found
            ? new SharedIdCombinedArtists(artist, artists, false, found.Id, found.Missing)
            : new SharedIdCombinedArtists(artist, artists, false,
                Reason: ids.Count == 0 ? "The songs have no Spotify or iTunes ids" : "None of the songs' tracks was found");
    }

    // The songs' artists, and the leaders of their groups, as evidence for splitting credits
    private static IArtistKnowledge Knowledge(IReadOnlyList<Song> songs) =>
        FeaturedCleanup.WithGroupLeaders(
            FeaturedCleanup.Knowing(songs.SelectMany(s => FeaturedCleanup.IndividualArtists(s))), songs);
}
