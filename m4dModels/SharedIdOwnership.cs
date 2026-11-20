using System.Diagnostics;

namespace m4dModels;

public enum SharedIdOwnerStatus
{
    // The id is one song's: it comes off the others, and goes to that song if it doesn't have it
    Owned,
    // No song is the track, or the service doesn't have it: it comes off every song
    Bad,
    // Nothing is proposed
    Unsure
}

/// <summary>
/// What SharedIdOwnership proposes for one of the songs' ids: Song is the owner (Owned only),
/// Reason says why in a few words, Via is, for an ISRC, the id whose track reported it, and
/// Shared says whether more than one of the songs holds it.
/// </summary>
public record SharedIdOwnerProposal(string Id, SharedIdOwnerStatus Status, string Reason, Guid? Song = null,
    string Via = null, bool Shared = true);

/// <summary>
/// An id to give one of a cluster's songs (SongId), its owner: the Spotify or iTunes id (Id,
/// extended, "S:…") from the album at FromAlbum (an AlbumDetails.Index) of the song that has it
/// (FromSong), with the ISRC its track reports (Isrc, or null). Song.AppendIdAddition puts it on
/// the owner's album of the same name, or a copy of that album.
/// </summary>
public record SharedIdAddition(Guid SongId, string Id, Guid FromSong, int FromAlbum, string Isrc = null);

/// <summary>
/// The proposal for a cluster's ids (one per id that needs one: the shared ids, Spotify and
/// iTunes first, then the songs' own ids that belong elsewhere or nowhere), the removals that
/// carry it out (an album, when they'd take every song-level id off it), the additions that give
/// an id to an owner that doesn't have it, and the tracks looked up, by extended id (null when
/// the service didn't return one).
/// </summary>
public record SharedIdOwnership(IReadOnlyList<SharedIdOwnerProposal> Ids, IReadOnlyList<SharedIdRemoval> Removals,
    IReadOnlyDictionary<string, ServiceTrack> Tracks, IReadOnlyList<SharedIdAddition> Additions = null);

/// <summary>
/// For songs that share ids but are different songs (different versions, say, whose albums and
/// ids are mixed up), which song each of their ids belongs to. Every Spotify and iTunes id on
/// the songs is looked up (the shared ones first, at most MaxLookups), and the song whose title,
/// artist and length match the track owns it, a song whose title is the track's, then one that
/// names exactly the track's artists, winning over ones that only nearly match; it needn't be a
/// song that has it. When no song is the track, or
/// the service doesn't have it, the id is bad. An ISRC goes with a Spotify or iTunes id that has
/// an owner and whose track reports it, and comes off an album that loses its Spotify or iTunes
/// id. See architecture/plans/shared-id-ownership.md.
/// </summary>
public static class SharedIdOwners
{
    /// <summary>A bound on the lookups one cluster makes.</summary>
    public const int MaxLookups = 50;

    public static async Task<SharedIdOwnership> Propose(IReadOnlyList<Song> songs,
        Func<string, Task<ServiceTrack>> lookup)
    {
        // Every song-level id and the songs that have it, the shared ones first
        var holders = songs
            .SelectMany(s => s.GetExtendedPurchaseIds().Distinct().Select(id => (Id: id, Song: s)))
            .GroupBy(h => h.Id)
            .Select(g => (Id: g.Key, Songs: g.Select(h => h.Song).DistinctBy(s => s.SongId).ToList()))
            .OrderBy(h => h.Songs.Count > 1 ? 0 : 1).ThenBy(h => Order(h.Id)).ThenBy(h => h.Id)
            .ToList();

        var tracks = new Dictionary<string, ServiceTrack>();
        var failed = new HashSet<string>();
        foreach (var (id, _) in holders.Where(h => IsLookable(h.Id)).Take(MaxLookups))
        {
            try
            {
                tracks[id] = await lookup(id);
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"SharedIdOwners: couldn't look up {id}: {e.Message}");
                _ = failed.Add(id);
            }
        }

        // A service that returned none of the tracks probably didn't answer, rather than not
        // having any of them
        var answered = tracks.Where(t => t.Value != null).Select(t => t.Key[0]).ToHashSet();

        // Every Spotify and iTunes id's proposal, for the ISRCs, and the ones that need something
        var all = new List<SharedIdOwnerProposal>();
        var proposals = new List<SharedIdOwnerProposal>();
        foreach (var (id, holding) in holders.Where(h => IsLookable(h.Id)))
        {
            var shared = holding.Count > 1;
            var proposal = failed.Contains(id)
                ? new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure, "The lookup failed")
                : !tracks.TryGetValue(id, out var track)
                    ? new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure, "Not looked up: too many ids")
                    : ForTrack(id, track, songs, answered.Contains(id[0]));
            proposal = proposal with { Shared = shared };
            all.Add(proposal);
            // A song's own id that's already where it belongs needs nothing
            if (shared || proposal.Status != SharedIdOwnerStatus.Owned || proposal.Song != holding[0].SongId)
            {
                proposals.Add(proposal);
            }
        }

        foreach (var (id, holding) in holders.Where(h => !IsLookable(h.Id) && h.Songs.Count > 1))
        {
            proposals.Add(id.StartsWith("R:")
                ? ForIsrc(id, all, tracks)
                : new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure, "Can't be looked up"));
        }

        var held = holders.ToDictionary(h => h.Id, h => h.Songs);
        proposals = [.. proposals.OrderBy(p => p.Shared ? 0 : 1).ThenBy(p => Order(p.Id)).ThenBy(p => p.Id)];
        return new SharedIdOwnership(proposals, Removals(proposals, held, tracks), tracks,
            Additions(proposals, held, songs, tracks));
    }

    // Spotify, then iTunes, then the ISRCs that follow them, then the rest
    private static int Order(string id) => id[0] switch { 'S' => 0, 'I' => 1, 'R' => 2, _ => 3 };

    private static bool IsLookable(string id) => id.StartsWith("S:") || id.StartsWith("I:");

    private static SharedIdOwnerProposal ForTrack(string id, ServiceTrack track, IReadOnlyList<Song> songs,
        bool serviceAnswered)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.Name))
        {
            return serviceAnswered
                ? new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Bad, "The service doesn't have the track")
                : new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure,
                    "The service didn't return any of the songs' tracks");
        }

        var credit = Credit(track);
        var named = songs.Where(s => NamesMatch(s, track.Name, credit)).ToList();
        if (named.Count == 0)
        {
            return new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Bad, $"No song is {Describe(track)}");
        }

        // The fuzzy match passes "Song" for "Song - Salsa Version"; a song whose title is the
        // track's, without feat. clauses and version notes, wins over the ones that only match
        var exact = named.Where(s => SharedIdMatcher.SameVersionWithoutFeaturing(s.Title, track.Name)).ToList();
        if (exact.Count > 0)
        {
            named = exact;
        }

        // The fuzzy artist match passes "Tony Bennett" for a track by Tony Bennett and Lady Gaga
        // too; a song that names the track's artists, no more and no fewer, wins
        var knowledge = FeaturedCleanup.WithGroupLeaders(
            FeaturedCleanup.Knowing(songs.SelectMany(s => FeaturedCleanup.IndividualArtists(s))), songs);
        var artists = TrackArtistKeys(track, knowledge);
        var credited = named.Where(s => ArtistKeys(FeaturedCleanup.IndividualArtists(s, knowledge), knowledge)
            .SetEquals(artists)).ToList();
        if (credited.Count > 0)
        {
            named = credited;
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
    private static SharedIdOwnerProposal ForIsrc(string id, IEnumerable<SharedIdOwnerProposal> proposals,
        IReadOnlyDictionary<string, ServiceTrack> tracks)
    {
        var isrc = id[2..];
        var via = proposals.FirstOrDefault(p =>
            p.Status == SharedIdOwnerStatus.Owned && IsLookable(p.Id) &&
            tracks.TryGetValue(p.Id, out var t) && string.Equals(t?.ISRC, isrc, StringComparison.OrdinalIgnoreCase));
        return via == null
            ? new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Unsure,
                "No Spotify or iTunes id with an owner reports this ISRC")
            : new SharedIdOwnerProposal(id, SharedIdOwnerStatus.Owned, $"The ISRC of {via.Id}", via.Song, via.Id);
    }

    // Each owned Spotify or iTunes id whose owner doesn't have it goes to the owner, from the
    // first song and album that has it, with the ISRC its track reports
    private static List<SharedIdAddition> Additions(IEnumerable<SharedIdOwnerProposal> proposals,
        IReadOnlyDictionary<string, List<Song>> holders, IReadOnlyList<Song> songs,
        IReadOnlyDictionary<string, ServiceTrack> tracks) =>
    [
        .. proposals
            .Where(p => p.Status == SharedIdOwnerStatus.Owned && IsLookable(p.Id) &&
                holders[p.Id].All(s => s.SongId != p.Song))
            .Select(p =>
            {
                var from = holders[p.Id]
                    .SelectMany(s => s.Albums
                        .Where(a => !string.IsNullOrWhiteSpace(a.Name) &&
                            a.GetExtendedPurchaseIds(PurchaseType.Song).Contains(p.Id))
                        .Select(a => (Song: s, Album: a)))
                    .FirstOrDefault();
                if (from.Song == null)
                {
                    return null;
                }

                var owner = songs.First(s => s.SongId == p.Song);
                var isrc = tracks.GetValueOrDefault(p.Id)?.ISRC;
                var hasIsrc = string.IsNullOrWhiteSpace(isrc) ||
                    owner.GetExtendedPurchaseIds().Any(i => string.Equals(i, $"R:{isrc}", StringComparison.OrdinalIgnoreCase));
                return new SharedIdAddition(p.Song.Value, p.Id, from.Song.SongId, from.Album.Index,
                    hasIsrc ? null : isrc);
            })
            .Where(a => a != null)
    ];

    // The track's individual artists: Spotify lists them, and an iTunes credit (and its title's
    // feat. clause) is split with the songs' artists as evidence
    internal static HashSet<string> TrackArtistKeys(ServiceTrack track, IArtistKnowledge knowledge) =>
        ArtistKeys(track.Artists is { Length: > 0 } ? track.Artists
            : FeaturedCleanup.IndividualArtists(new Song { Title = track.Name, Artist = track.Artist ?? "" }, knowledge),
            knowledge);

    // Each artist's identity, without a leader's backing group and under the name to use
    private static HashSet<string> ArtistKeys(IEnumerable<string> artists, IArtistKnowledge knowledge) =>
    [
        .. artists.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a =>
            ArtistSplitter.ArtistKey(ArtistAliases.PreferredName(ArtistSplitter.RemoveBackingGroup(a, knowledge))))
    ];

    // Spotify lists the artists; iTunes gives a credit
    private static string Credit(ServiceTrack track) =>
        track.Artists is { Length: > 1 } ? string.Join(" & ", track.Artists) : track.Artist ?? "";

    private static string Describe(ServiceTrack track)
    {
        var length = track.Duration is > 0 ? $" ({track.Duration / 60}:{track.Duration % 60:00})" : "";
        return $"\"{track.Name}\" by {Credit(track)}{length}";
    }

    // The fuzzy match that filters service tracks (Song.TitleArtistMatch), as given or without
    // the feat. clauses, with the credits under the names to use (ArtistAliases)
    private static bool NamesMatch(Song song, string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(song.Title))
        {
            return false;
        }

        var credit = ArtistAliases.PreferredCredit(artist ?? "");
        var named = new Song { Title = song.Title, Artist = ArtistAliases.PreferredCredit(song.Artist ?? "") };
        var stripped = new Song
        {
            Title = ArtistSplitter.RemoveTitleFeaturing(song.Title),
            Artist = ArtistSplitter.RemoveArtistFeaturing(named.Artist)
        };
        return named.TitleArtistMatch(title, credit) ||
            stripped.TitleArtistMatch(ArtistSplitter.RemoveTitleFeaturing(title),
                ArtistSplitter.RemoveArtistFeaturing(credit));
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
