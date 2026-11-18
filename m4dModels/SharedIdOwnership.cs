using System.Diagnostics;

namespace m4dModels;

public enum SharedIdOwnerStatus
{
    // The id is one song's: it comes off the others
    Owned,
    // No song is the track: it comes off every song
    Bad,
    // Nothing is proposed
    Unsure
}

/// <summary>
/// What SharedIdOwnership proposes for one id the songs share: Song is the owner (Owned only),
/// Reason says why in a few words, and Via is, for an ISRC, the id whose track reported it.
/// </summary>
public record SharedIdOwnerProposal(string Id, SharedIdOwnerStatus Status, string Reason, Guid? Song = null,
    string Via = null);

/// <summary>
/// The proposal for a cluster's shared ids (one per id, Spotify and iTunes first), the removals
/// that carry it out (an album, when they'd take every song-level id off it), and the tracks
/// looked up, by extended id (null when the service didn't return one).
/// </summary>
public record SharedIdOwnership(IReadOnlyList<SharedIdOwnerProposal> Ids, IReadOnlyList<SharedIdRemoval> Removals,
    IReadOnlyDictionary<string, ServiceTrack> Tracks);

/// <summary>
/// For songs that share ids but are different songs, which song each shared id belongs to. A
/// Spotify or iTunes id is looked up, and the song whose title, artist and length match the
/// track owns it, a song whose title is the track's winning over ones that only nearly match.
/// When no song is the track, the id is bad. An ISRC goes with a Spotify or iTunes id that has an
/// owner and whose track reports it, and comes off an album that loses its Spotify or iTunes id.
/// See
/// architecture/plans/shared-id-ownership.md.
/// </summary>
public static class SharedIdOwners
{
    public static async Task<SharedIdOwnership> Propose(IReadOnlyList<Song> songs,
        Func<string, Task<ServiceTrack>> lookup)
    {
        var holders = songs
            .SelectMany(s => s.GetExtendedPurchaseIds().Distinct().Select(id => (Id: id, Song: s)))
            .GroupBy(h => h.Id)
            .Select(g => (Id: g.Key, Songs: g.Select(h => h.Song).DistinctBy(s => s.SongId).ToList()))
            .Where(h => h.Songs.Count > 1)
            .OrderBy(h => Order(h.Id)).ThenBy(h => h.Id)
            .ToList();

        var tracks = new Dictionary<string, ServiceTrack>();
        var proposals = new List<SharedIdOwnerProposal>();
        foreach (var (id, holding) in holders)
        {
            if (IsLookable(id))
            {
                var track = await Find(id, lookup);
                tracks[id] = track;
                proposals.Add(ForTrack(id, track, holding));
            }
            else if (id.StartsWith("R:"))
            {
                proposals.Add(ForIsrc(id, holding, proposals, tracks));
            }
            else
            {
                proposals.Add(new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure, "Can't be looked up"));
            }
        }

        return new SharedIdOwnership(proposals,
            Removals(proposals, holders.ToDictionary(h => h.Id, h => h.Songs), tracks), tracks);
    }

    // Spotify, then iTunes, then the ISRCs that follow them, then the rest
    private static int Order(string id) => id[0] switch { 'S' => 0, 'I' => 1, 'R' => 2, _ => 3 };

    private static bool IsLookable(string id) => id.StartsWith("S:") || id.StartsWith("I:");

    private static async Task<ServiceTrack> Find(string id, Func<string, Task<ServiceTrack>> lookup)
    {
        try
        {
            return await lookup(id);
        }
        catch (Exception e)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceError, $"SharedIdOwners: couldn't look up {id}: {e.Message}");
            return null;
        }
    }

    private static SharedIdOwnerProposal ForTrack(string id, ServiceTrack track, IReadOnlyList<Song> holding)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.Name))
        {
            return new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure, "The service doesn't have the track");
        }

        var credit = Credit(track);
        var named = holding.Where(s => NamesMatch(s, track.Name, credit)).ToList();
        if (named.Count == 0)
        {
            return new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Bad, $"No song is {Describe(track)}");
        }

        // The fuzzy match passes "Song" for "Song - Salsa Version"; a song whose title is the
        // track's, without feat. clauses and version notes, wins over the ones that only match
        var title = SharedIdMatcher.TitleKey(track.Name);
        var exact = named.Where(s => SharedIdMatcher.TitleKey(s.Title) == title).ToList();
        if (exact.Count > 0)
        {
            named = exact;
        }

        var timed = named.Where(s => LengthMatches(s.Length, track.Duration)).ToList();
        return timed.Count switch
        {
            1 => new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Owned, $"The song is {Describe(track)}",
                timed[0].SongId),
            0 => new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure,
                $"The title and artist match {Describe(track)}, but not the length"),
            _ => new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure,
                $"More than one song is {Describe(track)}")
        };
    }

    // An ISRC moves only with a looked-up id that has an owner and whose track reports it
    private static SharedIdOwnerProposal ForIsrc(string id, IReadOnlyList<Song> holding,
        IEnumerable<SharedIdOwnerProposal> proposals, IReadOnlyDictionary<string, ServiceTrack> tracks)
    {
        var isrc = id[2..];
        var via = proposals.FirstOrDefault(p =>
            p.Status == SharedIdOwnerStatus.Owned && holding.Any(s => s.SongId == p.Song) &&
            tracks.TryGetValue(p.Id, out var t) && string.Equals(t?.ISRC, isrc, StringComparison.OrdinalIgnoreCase));
        return via == null
            ? new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure,
                "No Spotify or iTunes id with an owner reports this ISRC")
            : new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Owned, $"The ISRC of {via.Id}", via.Song, via.Id);
    }

    // Spotify lists the artists; iTunes gives a credit
    private static string Credit(ServiceTrack track) =>
        track.Artists is { Length: > 1 } ? string.Join(" & ", track.Artists) : track.Artist ?? "";

    private static string Describe(ServiceTrack track)
    {
        var length = track.Duration is > 0 ? $" ({track.Duration / 60}:{track.Duration % 60:00})" : "";
        return $"\"{track.Name}\" by {Credit(track)}{length}";
    }

    // The fuzzy match that filters service tracks (Song.TitleArtistMatch), as given or without
    // the feat. clauses
    private static bool NamesMatch(Song song, string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(song.Title))
        {
            return false;
        }

        var stripped = new Song
        {
            Title = ArtistSplitter.RemoveTitleFeaturing(song.Title),
            Artist = ArtistSplitter.RemoveArtistFeaturing(song.Artist ?? "")
        };
        return song.TitleArtistMatch(title, artist ?? "") ||
            stripped.TitleArtistMatch(ArtistSplitter.RemoveTitleFeaturing(title),
                ArtistSplitter.RemoveArtistFeaturing(artist ?? ""));
    }

    // Within the tolerance; a song or track without a length passes
    private static bool LengthMatches(int? song, int? track) =>
        song is not > 0 || track is not > 0 || Math.Abs(song.Value - track.Value) <= SharedIdMatcher.LengthTolerance;

    // Each owned id comes off the songs that don't own it, a bad one off every song. An album
    // that loses a Spotify or iTunes id loses its ISRCs too, which came from that track, unless
    // a Spotify or iTunes id it keeps was looked up and reports the ISRC. An album goes when that
    // takes every song-level id off it.
    private static List<SharedIdRemoval> Removals(IEnumerable<SharedIdOwnerProposal> proposals,
        IReadOnlyDictionary<string, List<Song>> holders, IReadOnlyDictionary<string, ServiceTrack> tracks)
    {
        var direct = proposals
            .Where(p => p.Status != SharedIdOwnerStatus.Unsure)
            .SelectMany(p => holders[p.Id]
                .Where(s => p.Status == SharedIdOwnerStatus.Bad || s.SongId != p.Song)
                .SelectMany(s => s.Albums
                    .Where(a => a.GetExtendedPurchaseIds(PurchaseType.Song).Contains(p.Id))
                    .Select(a => (Song: s, Album: a, p.Id))))
            .ToList();

        var isrcs = direct
            .GroupBy(r => (r.Song.SongId, r.Album.Index))
            .Where(g => g.Any(r => IsLookable(r.Id)))
            .SelectMany(g =>
            {
                var gone = g.Select(r => r.Id).ToHashSet();
                var (song, album, _) = g.First();
                var ids = album.GetExtendedPurchaseIds(PurchaseType.Song);
                var kept = ids.Where(id => IsLookable(id) && !gone.Contains(id)).ToList();
                return ids
                    .Where(id => id.StartsWith("R:") && !gone.Contains(id) && !kept.Any(k =>
                        tracks.TryGetValue(k, out var t) &&
                        string.Equals(t?.ISRC, id[2..], StringComparison.OrdinalIgnoreCase)))
                    .Select(id => (Song: song, Album: album, Id: id));
            });
        var removed = direct.Concat(isrcs).ToList();

        return [.. removed
            .GroupBy(r => (r.Song.SongId, r.Album.Index))
            .SelectMany(g =>
            {
                var ids = g.Select(r => r.Id).ToHashSet();
                var all = g.First().Album.GetExtendedPurchaseIds(PurchaseType.Song);
                return all.All(ids.Contains)
                    ? [new SharedIdRemoval(g.Key.SongId, g.Key.Index)]
                    : ids.Select(id => new SharedIdRemoval(g.Key.SongId, g.Key.Index, id));
            })];
    }
}
