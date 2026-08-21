using DanceLibrary;

namespace m4dModels.Sandbox;

/// <summary>
/// In-memory implementation of SongIndex for tests and the no-external-service sandbox host.
/// Uses late-binding pattern to avoid circular dependency with DanceMusicService.
/// Create this first, pass to SandboxServiceFactory.CreateService, then call AttachToService.
/// </summary>
public class SongIndexLocal : SongIndex
{
    private DanceMusicCoreService _actualService;
    private readonly Dictionary<Guid, Song> _songStore = [];

    public List<EditSongCall> EditCalls { get; } = new();

    /// <summary>
    /// Creates a SongIndexLocal with late-bound service.
    /// Must call AttachToService after the DanceMusicService is created.
    /// </summary>
    public SongIndexLocal() : base()
    {
    }

    /// <summary>
    /// Attaches this SongIndexLocal to the actual service after service creation.
    /// This resolves the circular dependency where SongIndexLocal needs the service
    /// but the service needs SongIndexLocal during construction.
    /// </summary>
    /// <param name="service">The DanceMusicService to attach to</param>
    public void AttachToService(DanceMusicCoreService service)
    {
        _actualService = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Override to use the actual service attached via AttachToService.
    /// This ensures all operations use the correct service context.
    /// </summary>
    public override DanceMusicCoreService DanceMusicService =>
        _actualService ?? throw new InvalidOperationException(
            "SongIndexLocal.AttachToService must be called before using this index. " +
            "Pattern: var localIndex = new SongIndexLocal(); var service = await CreateService(dbName, customSongIndex: localIndex); localIndex.AttachToService(service);");

    // Override the virtual EditSong method to capture calls
    public override async Task<bool> EditSong(ApplicationUser user, Song song, Song edit,
        IEnumerable<UserTag> tags = null)
    {
        // Capture the call
        EditCalls.Add(new EditSongCall(user, song, edit, tags?.ToList()));

        // Call the base implementation (which updates the song in memory)
        return await base.EditSong(user, song, edit, tags);
    }

    /// <summary>
    /// Override SaveSong to store the full song in the local store (for FindSong retrieval)
    /// and update DanceStats, without calling Azure Search.
    /// </summary>
    public override async Task SaveSong(Song song, string id = "default")
    {
        _songStore[song.SongId] = song;

        // Update stats but skip Azure Search index update
        var stats = DanceMusicService.DanceStats;
        stats.UpdateSong(song);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Override SaveSongs to skip Azure Search updates in tests.
    /// Only updates DanceStats, does not call UpdateAzureIndex.
    /// </summary>
    public override async Task SaveSongs(IEnumerable<Song> songs, string id = "default")
    {
        if (songs == null || !songs.Any())
        {
            return;
        }

        // Update stats but skip Azure Search index update
        var stats = DanceMusicService.DanceStats;
        foreach (var song in songs)
        {
            _songStore[song.SongId] = song;
            stats.UpdateSong(song);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Override LoadLightSongsStreamingAsync to provide in-memory light songs for merge testing.
    /// Loads songs from DanceStats cache which is populated during testing.
    /// </summary>
    public override async IAsyncEnumerable<Song> LoadLightSongsStreamingAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Get all songs from DanceStats cache (populated by SaveSong/SaveSongs)
        var allSongs = DanceMusicService.DanceStats.GetAllSongs();

        foreach (var song in allSongs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Song properties might be null if song is a light song already
            var title = song.Title;
            var artist = song.Artist;

            // Skip songs without title
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }

            // Create light song (same pattern as Song.CreateLightSong but using public properties)
            var lightSong = new Song
            {
                SongId = song.SongId,
                Title = title,
                Artist = artist ?? string.Empty,
                Tempo = song.Tempo,
                Length = song.Length
            };

            // Add minimal SongProperties for proper Title/Artist hashing
            lightSong.SongProperties.Add(new SongProperty(Song.TitleField, title));
            lightSong.SongProperties.Add(new SongProperty(Song.ArtistField, artist ?? string.Empty));

            if (song.Length.HasValue)
            {
                lightSong.SongProperties.Add(new SongProperty(Song.LengthField, song.Length.ToString()));
            }

            if (song.Tempo.HasValue)
            {
                lightSong.SongProperties.Add(new SongProperty(Song.TempoField, song.Tempo.ToString()));
            }

            yield return lightSong;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Override FindSong to return from the local store populated by SaveSong.
    /// Avoids Azure Search client creation in tests.
    /// </summary>
    public override Task<Song> FindSong(Guid id)
    {
        _songStore.TryGetValue(id, out var song);
        return Task.FromResult(song);
    }

    /// <summary>
    /// Exposes the protected DocumentFromSong for direct assertion in index-shape tests.
    /// </summary>
    public object CallDocumentFromSong(Song song) => DocumentFromSong(song);

    /// <summary>
    /// Evaluates a SongFilter's already-parsed sub-query objects (DanceQuery, TagQuery,
    /// UserQuery, KeywordQuery, SongSort) directly against the in-memory song store, instead of
    /// generating and re-parsing an OData filter string against a real Azure index - see
    /// architecture/contributor-test-environments.md L1f, option A. Covers the common cases
    /// (dance selection/threshold, tags, tempo/length range, keyword substring match, sort,
    /// paging, cruft). Known gaps, left for a later pass since a partially-correct filter
    /// already beats today's always-empty result: raw/customsearch filters (SongFilter.IsRaw -
    /// falls back to keyword+sort+paging only, no filtering), per-dance-scoped tag queries
    /// (DanceQueryItem.TagQuery), Purchase/service-availability filtering, and vote-based
    /// queries (UserQuery.IsVoted - SongSearch routes those through VoteSearch/StreamAll
    /// instead, which this class doesn't override).
    /// </summary>
    public override Task<SearchResults> Search(
        SongFilter filter, int? pageSize = null, CruftFilter cruft = CruftFilter.NoCruft)
    {
        if (filter.CruftFilter != CruftFilter.NoCruft)
        {
            cruft = filter.CruftFilter;
        }

        var size = pageSize ?? 25;
        var page = filter.Page ?? 1;
        var skip = (page - 1) * size;
        var singleDanceId = GetSingleDanceId(filter);

        IEnumerable<Song> matches = _songStore.Values;

        if (!filter.IsRaw)
        {
            matches = matches
                .Where(s => PassesCruft(s, cruft))
                .Where(s => MatchesDanceQuery(s, filter.DanceQuery, cruft))
                .Where(s => MatchesTagQuery(s, filter.TagQuery))
                .Where(s => MatchesUserQuery(s, filter.UserQuery))
                .Where(s => MatchesRange(s, filter, singleDanceId));

            if (filter.SongSort.Id == SongSort.Comments)
            {
                matches = matches.Where(HasAnyComments);
            }
        }

        matches = matches.Where(s => MatchesKeyword(s, filter.KeywordQuery));

        var matchList = matches.ToList();
        matchList.Sort((a, b) => CompareSongs(a, b, filter, singleDanceId));

        var total = matchList.Count;
        var paged = matchList.Skip(skip).Take(size).ToList();

        // Match SongIndex.Search(string, ...): report the Lucene/backtick-stripped keyword
        // text, not the raw search string, so the UI sees the same query across index
        // implementations.
        return Task.FromResult(
            new SearchResults(filter.KeywordQuery.Keywords, paged.Count, total, page, size, paged, null));
    }

    private static string GetSingleDanceId(SongFilter filter)
    {
        if (filter.IsRaw)
        {
            return null;
        }

        return filter.IsSingleDance
            ? filter.DanceQuery.DanceIds.FirstOrDefault()
            : filter.DanceQuery.PrimaryDanceId;
    }

    private static bool PassesCruft(Song song, CruftFilter cruft)
    {
        if (!cruft.HasFlag(CruftFilter.NoPublishers) && !song.PurchaseInfo.Any())
        {
            return false;
        }

        var hasDanceTags = song.TagSummary.GetTagSet("Dance").Count > 0;
        if (!cruft.HasFlag(CruftFilter.NoDances) && !hasDanceTags)
        {
            return false;
        }

        if (!cruft.HasFlag(CruftFilter.UnconfirmedDances) && hasDanceTags)
        {
            var confirmedTotal = song.DanceRatings.Where(dr => !dr.IsUnconfirmedOnly).Sum(dr => dr.Weight);
            if (confirmedTotal == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesDanceQuery(Song song, DanceQuery danceQuery, CruftFilter cruft)
    {
        var expandedDances = Dances.Instance.ExpandGroups(danceQuery.Dances).ToList();
        if (expandedDances.Count == 0)
        {
            return true;
        }

        var includeUnconfirmed = cruft.HasFlag(CruftFilter.UnconfirmedDances);
        var itemResults = new List<bool>();

        foreach (var item in danceQuery.Items)
        {
            var matchingDances = expandedDances.Where(d =>
                d.Id == item.Id ||
                ((d as DanceType)?.Groups?.Any(g => g.Id == item.Id) ?? false)).ToList();

            if (matchingDances.Count == 0)
            {
                continue;
            }

            itemResults.Add(matchingDances.Any(d =>
                VoteMatches(song.FindRating(d.Id), item.Threshold, includeUnconfirmed)));
        }

        if (itemResults.Count == 0)
        {
            return true;
        }

        return danceQuery.IsExclusive ? itemResults.All(r => r) : itemResults.Any(r => r);
    }

    private static bool VoteMatches(DanceRating dr, int threshold, bool includeUnconfirmed)
    {
        if (dr == null)
        {
            return false;
        }

        var weight = dr.IsUnconfirmedOnly ? -1 : dr.Weight;
        var baseMatch = threshold > 0 ? weight >= threshold : weight <= Math.Abs(threshold);
        return baseMatch || (includeUnconfirmed && weight == -1);
    }

    private static readonly string[] s_tagClasses = ["Music", "Style", "Tempo", "Other"];

    private static bool MatchesTagQuery(Song song, TagQuery tagQuery)
    {
        if (tagQuery == null || tagQuery.IsEmpty)
        {
            return true;
        }

        var tagList = tagQuery.TagList;
        var include = tagList.IsQualified ? tagList.ExtractAdd() : tagList;
        var exclude = tagList.IsQualified ? tagList.ExtractRemove() : new TagList();

        var (songTags, danceAllTags) = BuildTagSets(song);

        foreach (var tagClass in s_tagClasses)
        {
            var incNames = include.Filter(tagClass).Strip();
            if (incNames.Count > 0 &&
                !incNames.All(n => TagPresent(tagClass, n, songTags, danceAllTags, tagQuery.ExcludeDanceTags)))
            {
                return false;
            }

            var excNames = exclude.Filter(tagClass).Strip();
            if (excNames.Any(n => TagPresent(tagClass, n, songTags, danceAllTags, tagQuery.ExcludeDanceTags)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TagPresent(
        string tagClass, string name, HashSet<string> songTags, HashSet<string> danceAllTags,
        bool excludeDanceTags) =>
        tagClass switch
        {
            "Music" => songTags.Contains(name),
            "Style" => danceAllTags.Contains(name),
            _ => songTags.Contains(name) || (!excludeDanceTags && danceAllTags.Contains(name)),
        };

    private static (HashSet<string> songTags, HashSet<string> danceAllTags) BuildTagSets(Song song)
    {
        var songTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        songTags.UnionWith(song.TagSummary.GetTagSet("Music"));
        songTags.UnionWith(song.TagSummary.GetTagSet("Tempo"));
        songTags.UnionWith(song.TagSummary.GetTagSet("Other"));

        var danceAllTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dr in song.DanceRatings)
        {
            danceAllTags.UnionWith(dr.TagSummary.GetTagSet("Style"));
            danceAllTags.UnionWith(dr.TagSummary.GetTagSet("Tempo"));
            danceAllTags.UnionWith(dr.TagSummary.GetTagSet("Other"));
        }

        return (songTags, danceAllTags);
    }

    private static bool MatchesUserQuery(Song song, UserQuery userQuery)
    {
        if (userQuery.IsEmpty || userQuery.IsIdentity)
        {
            return true;
        }

        var targetUser = userQuery.UserName;
        if (string.IsNullOrEmpty(targetUser))
        {
            return true;
        }

        var mr = song.ModifiedBy.FirstOrDefault(
            m => string.Equals(m.UserName, targetUser, StringComparison.OrdinalIgnoreCase));

        bool matches;
        if (userQuery.IsLike)
        {
            matches = mr?.Like == true;
        }
        else if (userQuery.IsHate)
        {
            matches = mr?.Like == false;
        }
        else
        {
            // IsAny / edited-with-no-opinion. Vote-based queries (IsVoted) never reach here -
            // SongSearch.Search short-circuits those to VoteSearch/StreamAll before calling
            // SongIndex.Search(SongFilter, ...).
            matches = mr != null;
        }

        return userQuery.IsInclude ? matches : !matches;
    }

    private static bool MatchesKeyword(Song song, KeywordQuery kq)
    {
        if (string.IsNullOrWhiteSpace(kq.Keywords))
        {
            return true;
        }

        if (!kq.IsLucene)
        {
            var text = kq.Keywords;
            return ContainsText(song.Title, text) || ContainsText(song.Artist, text);
        }

        var everywhere = kq.GetField("Everywhere");
        if (!string.IsNullOrEmpty(everywhere) &&
            !(ContainsText(song.Title, everywhere) || ContainsText(song.Artist, everywhere)))
        {
            return false;
        }

        var title = kq.GetField("Title");
        if (!string.IsNullOrEmpty(title) && !ContainsText(song.Title, title))
        {
            return false;
        }

        var artist = kq.GetField("Artist");
        if (!string.IsNullOrEmpty(artist) && !ContainsText(song.Artist, artist))
        {
            return false;
        }

        var albums = kq.GetField("Albums");
        if (!string.IsNullOrEmpty(albums) &&
            !(song.Albums?.Any(al => ContainsText(al.Name, albums)) ?? false))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsText(string haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesRange(Song song, SongFilter filter, string singleDanceId)
    {
        var tempo = GetEffectiveTempo(song, singleDanceId);

        if (filter.TempoMin.HasValue)
        {
            var min = filter.TempoMin.Value % 1M < 0.0001M ? filter.TempoMin.Value - 0.5M : filter.TempoMin.Value;
            if (!tempo.HasValue || tempo.Value < min)
            {
                return false;
            }
        }

        if (filter.TempoMax.HasValue)
        {
            var max = filter.TempoMax.Value % 1M < 0.0001M ? filter.TempoMax.Value + 0.5M : filter.TempoMax.Value;
            if (!tempo.HasValue || tempo.Value > max)
            {
                return false;
            }
        }

        if (filter.LengthMin.HasValue && (!song.Length.HasValue || song.Length.Value < filter.LengthMin.Value))
        {
            return false;
        }

        if (filter.LengthMax.HasValue && (!song.Length.HasValue || song.Length.Value > filter.LengthMax.Value))
        {
            return false;
        }

        return true;
    }

    private static decimal? GetEffectiveTempo(Song song, string singleDanceId)
    {
        if (singleDanceId == null)
        {
            return song.Tempo;
        }

        var dr = song.FindRating(singleDanceId);
        return dr?.Tempo ?? song.Tempo;
    }

    private static bool HasAnyComments(Song song) =>
        (song.Comments?.Count ?? 0) > 0 || song.DanceRatings.Any(dr => (dr.Comments?.Count ?? 0) > 0);

    private static int DanceVoteTotal(Song song, DanceQuery danceQuery)
    {
        var primary = danceQuery.PrimaryDanceId;
        if (primary != null)
        {
            var dr = song.FindRating(primary);
            return dr == null ? 0 : dr.IsUnconfirmedOnly ? -1 : dr.Weight;
        }

        var expanded = Dances.Instance.ExpandGroups(danceQuery.Dances).ToList();
        if (expanded.Count == 1)
        {
            var dr = song.FindRating(expanded[0].Id);
            return dr == null ? 0 : dr.IsUnconfirmedOnly ? -1 : dr.Weight;
        }

        return song.DanceRatings.Where(dr => !dr.IsUnconfirmedOnly).Sum(dr => dr.Weight);
    }

    private static bool EffectiveDescending(SongSort sort)
    {
        // Mirrors SongFilter.ODataSort/SongSort.OData: these fields default to "most
        // recent"/"most popular" first, so Descending=false is the inverted actual order.
        var inverted = sort.Id is SongSort.Modified or SongSort.Created or SongSort.Edited
            or SongSort.Comments or SongSort.Dances;
        return inverted ? !sort.Descending : sort.Descending;
    }

    private static int CompareSongs(Song a, Song b, SongFilter filter, string singleDanceId)
    {
        var sort = filter.SongSort;
        var cmp = sort.Id switch
        {
            SongSort.Dances => DanceVoteTotal(a, filter.DanceQuery).CompareTo(DanceVoteTotal(b, filter.DanceQuery)),
            SongSort.Comments => a.Modified.CompareTo(b.Modified),
            SongIndex.ModifiedField => a.Modified.CompareTo(b.Modified),
            SongIndex.CreatedField => a.Created.CompareTo(b.Created),
            SongIndex.EditedField => a.Edited.CompareTo(b.Edited),
            Song.TempoField => Nullable.Compare(
                GetEffectiveTempo(a, singleDanceId), GetEffectiveTempo(b, singleDanceId)),
            Song.LengthField => Nullable.Compare(a.Length, b.Length),
            Song.ArtistField => string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase),
            SongIndex.BeatField => Nullable.Compare(a.Danceability, b.Danceability),
            Song.EnergyField => Nullable.Compare(a.Energy, b.Energy),
            SongIndex.MoodField => Nullable.Compare(a.Valence, b.Valence),
            _ => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase),
        };

        return EffectiveDescending(sort) ? -cmp : cmp;
    }

    /// <summary>
    /// In-memory stand-in for the Azure-backed exact ServiceIds lookup: scans songs already
    /// saved via SaveSong for one whose extended purchase ids contain "{CID}:{id}". Lets tests
    /// exercise service-id/ISRC lookup paths (e.g. MusicServiceManager.CreateSong's ISRC
    /// fallback) without a real search backend.
    /// </summary>
    public override Task<Song> GetSongFromService(MusicService service, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<Song>(null);
        }

        var needle = $"{service.CID}:{id}";
        var match = _songStore.Values.FirstOrDefault(
            s => s.GetExtendedPurchaseIds().Contains(needle));
        return Task.FromResult(match);
    }

    // Record for capturing EditSong calls
    public record EditSongCall(
        ApplicationUser User,
        Song Original,
        Song Edit,
        List<UserTag> Tags);
}
