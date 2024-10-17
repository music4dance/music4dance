using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace m4dModels;

public abstract class SongIndex
{
    public const string SongIdField = "SongId";
    public const string AltIdField = "AlternateIds";
    public const string MoodField = "Mood";
    public const string BeatField = "Beat";
    public const string AlbumsField = "Albums";
    public const string CreatedField = "Created";
    public const string ModifiedField = "Modified";
    public const string EditedField = "Edited";
    public const string DancesField = "Dances";
    public const string UsersField = "Users";
    public const string CommentsField = "Comments";
    public const string DanceTagsInferred = "DanceTagsInferred";
    public const string GenreTags = "GenreTags";
    public const string TempoTags = "TempoTags";
    public const string StyleTags = "StyleTags";
    public const string OtherTags = "OtherTags";
    public const string PropertiesField = "Properties";
    public const string ServiceIds = "ServiceIds";
    public const string LookupStatus = "LookupStatus";

    // Public for testing purposes
    public virtual DanceMusicCoreService DanceMusicService { get; }
    private string SearchId { get; }
    private ISearchServiceManager Manager => DanceMusicService.SearchService;

    private SearchClient _client;
    protected SearchClient Client => _client ??= CreateSearchClient();

    private SearchIndexClient _indexClient;
    protected SearchIndexClient IndexClient => _indexClient ??= CreateSearchIndexClient();

    protected SearchServiceInfo Info => DanceMusicService.SearchService.GetInfo(SearchId);

    public static SongIndex Create(DanceMusicCoreService dms, string id = null)
    {
        var info = dms.SearchService.GetInfo(id);
        return info.IsStructured ? new StructuredSongIndex(dms, id): new FlatSongIndex(dms, id);
    }

    // For Moq
    protected SongIndex()
    {
    }

    protected SongIndex(DanceMusicCoreService dms, string id = null)
    {
        DanceMusicService = dms;
        SearchId = id;
    }

    #region Lookup

    public async Task<Song> FindSong(Guid id)
    {
        try
        {
            var response = await Client.GetDocumentAsync<SearchDocument>(
                id.ToString(),
                new GetDocumentOptions { SelectedFields = {
                    SongIdField,
                    PropertiesField
                } });
            var doc = response.Value;
            if (doc == null)
            {
                return null;
            }

            return await CreateSong(doc);
        }
        catch (RequestFailedException e)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, e.Message);
            return null;
        }
    }


    public async Task<IEnumerable<Song>> FindSongs(IEnumerable<Guid> ids)
    {
        // Apparently EF won't let me do these in parallel
        var songs = new List<Song>();
        foreach (var id in ids)
        {
            songs.Add(await InternalFindSong(id));
        }
        return songs;
    }

    private async Task<Song> InternalFindSong(Guid id)
    {
        return DanceMusicService.DanceStats.FindSongDetails(id) ?? await FindSong(id);
    }

    internal async Task<IEnumerable<Song>> SongsFromList(string list)
    {
        var dels = list.Split(';');
        var songs = new List<Song>(list.Length);

        foreach (var t in dels)
        {
            if (!Guid.TryParse(t, out var idx))
            {
                continue;
            }

            var s = await FindSong(idx);
            if (s != null)
            {
                songs.Add(s);
            }
        }

        return songs;
    }

    public async Task<IList<Song>> SongsFromTracks(ApplicationUser user,
        IEnumerable<ServiceTrack> tracks,
        string multiDance, string songTags, string playlist)
    {
        return await CreateSongs(
            tracks.Where(
                track => !string.IsNullOrEmpty(track.Artist)), user, multiDance,
            songTags, playlist);
    }

    public async Task<Song> GetSongFromService(MusicService service, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var sid = $"\"{service.CID}:{id}\"";
        var parameters = new SearchOptions();
        parameters.SearchFields.Add(ServiceIds);
        var results = await DoSearch(sid, parameters, CruftFilter.AllCruft);
        var r = results.GetResults().FirstOrDefault();
        return r == null ? null : await CreateSong(r.Document);
    }

    #endregion

    #region Edit

    public static Song CreateSong(Guid? guid = null)
    {
        return new Song
            { SongId = guid == null || guid == Guid.Empty ? Guid.NewGuid() : guid.Value };
    }

    public async Task<Song> CreateSong(ApplicationUser user, Song sd = null,
        IEnumerable<UserTag> tags = null, string command = Song.CreateCommand,
        string value = null)
    {
        if (sd != null)
        {
            Trace.WriteLineIf(
                string.Equals(sd.Title, sd.Artist),
                $"Title and Artist are the same ({sd.Title})");
        }

        var song = CreateSong(sd?.SongId);
        if (sd == null)
        {
            song.Create(user, command, value, true);
        }
        else
        {
            song.Create(sd, tags, user, command, value, DanceMusicService.DanceStats);
        }

        return await Song.Create(song, DanceMusicService);
    }

    public async Task<bool> CreateOrMergeSong(ICollection<SongProperty> props, ApplicationUser user)
    {
        var properties = props.ToList();
        await SaveSong(await Song.Create(Guid.NewGuid(), properties, DanceMusicService));
        var mergeIdx = properties.FindLastIndex(p => p.BaseName == Song.MergeCommand);
        var actionIdx = properties.FindLastIndex(p => p.IsAction);
        if (mergeIdx != -1 && mergeIdx == actionIdx)
        {
            var merge = properties[mergeIdx];
            var ids = merge.Value.Split(';');
            foreach (var id in ids)
            {
                await DeleteSong(user, await FindSong(new Guid(id)));
            }
        }
        return true;
    }

    private async Task<List<Song>> CreateSongs<T>(IEnumerable<T> items,
        Func<T, Task<Song>> create)
    {
        if (items == null)
        {
            return null;
        }

        var songs = new List<Song>();
        foreach (var item in items)
        {
            songs.Add(await create(item));
        }

        return songs;
    }

    public async Task<List<Song>> CreateSongs(IEnumerable<string> strings)
    {
        return await CreateSongs(strings, s => Song.Create(s, DanceMusicService));
    }

    public async Task<List<Song>> CreateSongs(
        IEnumerable<SearchResult<SearchDocument>> documents)
    {
        return await CreateSongs(documents, d => CreateSong(d.Document));
    }

    public async Task<List<Song>> CreateSongs(
        IEnumerable<ServiceTrack> tracks,
        ApplicationUser user, string multiDance, string songTags, string playlist)
    {
        return await CreateSongs(
            tracks, t => Song.CreateFromTrack(user, t, multiDance, songTags, playlist, DanceMusicService));
    }

    public async Task<Song> EditSong(ApplicationUser user, Song edit,
        IEnumerable<UserTag> tags = null)
    {
        var song = await FindSong(edit.SongId);

        // TODO: Figure out if we need to rebuild the song after edit in all cases or if there is a cleaner way to do this
        return !song.Edit(user, edit, tags, DanceMusicService.DanceStats)
            ? null
            : await Song.Create(song.SongId, song.SongProperties, DanceMusicService);
    }

    // Returns true if changed
    public async Task<bool> EditSong(ApplicationUser user, Song song, Song edit,
        IEnumerable<UserTag> tags = null)
    {
        var changed = song.Edit(user, edit, tags, DanceMusicService.DanceStats);
        if (!changed)
        {
            return false;
        }

        await song.Load(song.SongId, song.SongProperties, DanceMusicService);
        return true;
    }

    public async Task<bool> ReloadSong(Song song)
    {
        await song.Reload(DanceMusicService);
        return true;
    }

    public async Task<bool> CheckProperties(Song song)
    {
        return await song.CheckProperties(DanceMusicService);
    }

    public async Task DeleteSong(ApplicationUser user, Song song)
    {
        song.Delete(user);
        await SaveSong(song);
    }

    public async Task<bool> AdminEditSong(Song edit, string properties)
    {
        return await edit.AdminEdit(properties, DanceMusicService);
    }

    public async Task<bool> AdminEditSong(SongHistory history, IMapper mapper)
    {
        var edit = await FindSong(history.Id);
        if (await edit.AdminEdit(
                history.Properties.Select(mapper.Map<SongProperty>).ToList(),
                DanceMusicService))
        {
            await SaveSong(edit);
            return true;
        }

        return false;
    }

    public async Task<bool> AdminAppendSong(Song edit, ApplicationUser user, string properties)
    {
        return await edit.AdminAppend(user, properties, DanceMusicService);
    }

    public async Task<bool> CorrectTempoSong(Song edit, ApplicationUser user,
        decimal multiplier)
    {
        var properties = new SongProperty(
            Song.TempoField, (edit.Tempo * multiplier)
            .ToString()).ToString();
        return await edit.AdminAppend(user, properties, DanceMusicService);
    }

    public async Task<bool> AdminEditSong(string properties)
    {
        if (Song.TryParseId(properties, out var id) == 0)
        {
            return false;
        }

        var song = await FindSong(id);
        return song != null && await AdminEditSong(song, properties);
    }

    public async Task<bool> AdminModifySong(Song edit, string songModifier)
    {
        return await edit.AdminModify(songModifier, DanceMusicService);
    }

    public async Task<bool> AppendHistory(SongHistory history, IMapper mapper)
    {
        var song = await FindSong(history.Id);
        if (song == null)
        {
            return false;
        }

        await song.AppendHistory(history, mapper, DanceMusicService);
        await SaveSong(song);
        return true;
    }

    public async Task<Song> UpdateSong(ApplicationUser user, Song song, Song edit)
    {
        if (!await song.Update(user.UserName, edit, DanceMusicService))
        {
            return null;
        }

        await SaveSong(song);
        return song;
    }
    #endregion

    #region Merge
    public async Task<Song> FindMergedSong(Guid id)
    {
        var response = await DoSearch(
            null,
            new SearchOptions { Filter = $"(AlternateIds/any(t: t eq '{id}'))" },
            CruftFilter.AllCruft);
        var songs = await CreateSongs(response.GetResults());
        return songs.FirstOrDefault(s => !s.IsNull);
    }

    public async Task<Song> FindMatchingSong(Song song)
    {
        var merger = await MergeFromTitle(song);

        return merger.MatchType == MatchType.Exact || merger.MatchType == MatchType.Length
            ? merger.Right
            : null;
    }

    // This is an additive merge - only add new things if they don't conflict with the old
    //  TODO: I'm pretty sure I can clean up this and all the other editing stuff by pushing
    //  the diffing part down into Song (which will also let me unit test it more easily)
    public bool AdditiveMerge(ApplicationUser user, Song initial, Song edit,
        List<string> addDances)
    {
        return initial.AdditiveMerge(user, edit, addDances, DanceMusicService.DanceStats);
    }

    public async Task<int> CleanupAlbums(ApplicationUser user, Song song)
    {
        var albums = AlbumDetails.MergeAlbums(song.Albums, song.Artist, true);
        if (albums.Count == song.Albums.Count)
        {
            return 0;
        }

        var delta = song.Albums.Count - albums.Count;
        Trace.WriteLineIf(
            TraceLevels.General.TraceVerbose,
            $"{delta}: {song.Title} {song.Artist}");
        song.Albums = albums.ToList();
        await EditSong(user, song);
        return delta;
    }

    private static IList<AlbumDetails> MergeAlbums(IEnumerable<Song> songs, string def,
        ICollection<string> keys, string artist)
    {
        var details = songs as IList<Song> ?? songs.ToList();
        var albumsIn = new List<AlbumDetails>();
        var albumsOut = new List<AlbumDetails>();

        foreach (var sd in details)
        {
            albumsIn.AddRange(sd.Albums);
        }

        var defIdx = -1;
        if (!string.IsNullOrWhiteSpace(def))
        {
            int.TryParse(def, out defIdx);
        }

        var idx = 0;
        if (defIdx >= 0 && albumsIn.Count > defIdx)
        {
            var t = albumsIn[defIdx];
            t.Index = 0;
            albumsOut.Add(t);
            idx = 1;
        }

        for (var i = 0; i < albumsIn.Count; i++)
        {
            if (i == defIdx)
            {
                continue;
            }

            var name = Song.AlbumListField + "_" + i;

            if (defIdx != -1 && !keys.Contains(name))
            {
                continue;
            }

            var t = albumsIn[i];
            t.Index = idx;
            albumsOut.Add(t);
            idx += 1;
        }

        return AlbumDetails.MergeAlbums(albumsOut, artist, false);
    }

    public async Task<Song> MergeSongs(ApplicationUser user, List<Song> songs, string title,
        string artist,
        decimal? tempo, int? length, IList<AlbumDetails> albums)
    {
        var songIds = songs.Select(s => s.SongId).ToList();
        var stringIds = string.Join(";", songIds.Select(id => id.ToString()));

        if (songs.Any(s => s.SongProperties == null))
        {
            songs = (await DanceMusicService.SongIndex.FindSongs(songIds)).ToList();
        }

        var song = await CreateSong(user, null, null, Song.MergeCommand, stringIds);

        // Add in the properties for all of the songs and then delete them
        foreach (var from in songs)
        {
            await song.UpdateProperties(
                from.SongProperties, DanceMusicService,
                [
                    Song.FailedLookup, Song.AlbumField, Song.TrackField, Song.PublisherField,
                    Song.PurchaseField, Song.AlbumListField, Song.AlbumOrder, Song.AlbumPromote
                ]);
            await DeleteSong(user, from);
        }

        var sd = new Song(title, artist, tempo, length, [])
        {
            Danceability = song.Danceability,
            Energy = song.Energy,
            Valence = song.Valence,
            Sample = song.Sample
        };

        song.Edit(user, sd, null, DanceMusicService.DanceStats);

        song.CreateAlbums(albums);

        song = await Song.Create(song.SongId, song.SongProperties, DanceMusicService);
        await song.CleanupProperties(DanceMusicService, "RE");

        await SaveSong(song);

        return song;
    }

    public async Task<Song> MergeSongs(ApplicationUser user, List<Song> songs, string title,
        string artist,
        decimal? tempo, int? length, string defAlbums, HashSet<string> keys)
    {
        return await MergeSongs(
            user, songs, title, artist, tempo, length,
            MergeAlbums(songs, defAlbums, keys, artist));
    }


    public IEnumerable<Song> MergeCatalog(ApplicationUser user, IList<LocalMerger> merges,
        IEnumerable<string> dances = null)
    {
        var songs = new List<Song>();

        var dancesL = dances?.ToList() ?? [];

        foreach (var m in merges)
            // Matchtype of none indicates a new (to us) song, so just add it
        {
            if (m.MatchType == MatchType.None)
            {
                if (dancesL.Any())
                {
                    m.Left.UpdateDanceRatingsAndTags(
                        user.UserName, dancesL,
                        Song.DanceRatingInitial, DanceMusicService.DanceStats);
                }

                songs.Add(m.Left);
            }
            // Any other matchtype should result in a merge, which for now is just adding the dance(s) from
            //  the new list to the existing song (or adding weight).
            // Now we're going to potentially add tempo - need a more general solution for this going forward
            else
            {
                if (AdditiveMerge(user, m.Right, m.Left, dancesL))
                {
                    songs.Add(m.Right);
                }
            }
        }

        return songs;
    }

    public async Task<IList<LocalMerger>> MatchSongs(IList<Song> newSongs, DanceMusicCoreService.MatchMethod method)
    {
        newSongs = RemoveDuplicateSongs(newSongs);
        var merge = new List<LocalMerger>();

        foreach (var song in newSongs)
        {
            var m = await MergeFromPurchaseInfo(song)
                ?? await MergeFromTitle(song);

            switch (method)
            {
                case DanceMusicCoreService.MatchMethod.Tempo:
                    if (m.Right != null)
                    {
                        m.Conflict = song.TempoConflict(m.Right, 3);
                    }

                    break;
                case DanceMusicCoreService.MatchMethod.Merge:
                    // Do we need to do anything special here???
                    break;
            }

            merge.Add(m);
            Trace.WriteLineIf(merge.Count % 10 == 0, $"{merge.Count} songs merged");
            AdminMonitor.UpdateTask("Merge", merge.Count);
        }

        return merge;
    }

    public IList<Song> RemoveDuplicateSongs(IList<Song> songs)
    {
        var hash = new HashSet<string>();
        var ret = new List<Song>();
        foreach (var song in songs)
        {
            var key = song.TitleArtistString;
            if (hash.Contains(key))
            {
                continue;
            }

            hash.Add(key);
            ret.Add(song);
        }

        return ret;
    }

    internal async Task<LocalMerger> MergeFromTitle(Song song)
    {
        var songs = await SongsFromTitle(song.Title);

        var candidates =
            (from s in songs where song.TitleArtistEquivalent(s) select s).ToList();

        if (candidates.Count <= 0)
        {
            return new LocalMerger
            { Left = song, Right = null, MatchType = MatchType.None, Conflict = false };
        }

        Song match = null;
        var type = MatchType.None;

        // Now we have a list of existing songs that are a title-artist match to our new song - so see
        //  if we have a title-artist-album match
        if (song.HasAlbums)
        {
            var songD = song;
            foreach (var s in candidates.Where(s => s.FindAlbum(songD.Albums[0].Name) != null))
            {
                match = s;
                type = MatchType.Exact;
                break;
            }
        }

        // If not, try for a length match
        if (match == null && song.Length.HasValue)
        {
            var songD = song;
            foreach (var s in candidates
                         .Where(
                             s =>
                                 songD.Length != null && s.Length.HasValue &&
                                 Math.Abs(s.Length.Value - songD.Length.Value) < 5))
            {
                match = s;
                type = MatchType.Length;
                break;
            }
        }

        // TODO: We may want to make this even weaker (especially for merge): If merge doesn't have album remove candidate.HasRealAlbums?

        // Otherwise, if there is only one candidate we will choose it
        // TODO: and it doesn't have any 'real' albums [&& (!song.HasAlbums || !candidates[0].HasRealAblums)] (I obviously wanted this extra filter at some point...)
        if (match == null && candidates.Count == 1)
        {
            type = MatchType.Weak;
            match = candidates[0];
        }

        return new LocalMerger
        { Left = song, Right = match, MatchType = type, Conflict = false };
    }

    private async Task<LocalMerger> MergeFromPurchaseInfo(Song song)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var service in MusicService.GetSearchableServices())
        {
            var ids = song.GetPurchaseIds(service);

            foreach (var id in ids)
            {
                var match = await GetSongFromService(service, id);
                if (match != null)
                {
                    return new LocalMerger
                    {
                        Left = song, Right = match, MatchType = MatchType.Exact,
                        Conflict = false
                    };
                }
            }
        }

        return null;
    }

    #endregion

    #region User
    internal async Task<IEnumerable<Song>> FindUserSongs(string user, bool includeHate = false)
    {
        const int max = 10000;

        var filter = new SongFilter { User = user };
        if (includeHate)
        {
            filter.User += "|a";
        }

        var afilter = AzureParmsFromFilter(filter);
        afilter.Size = max;
        afilter.IncludeTotalCount = false;

        var response = await DoSearch(
            null, afilter, CruftFilter.AllCruft);

        return await SongsFromAzureResult(response);
    }

    public async Task<int> UserSongCount(string user, bool? like)
    {
        const int max = 10000;

        var filter = new SongFilter { User = new UserQuery(user, true, like).Query };

        var afilter = AzureParmsFromFilter(filter);
        afilter.Size = max;
        afilter.IncludeTotalCount = true;

        var response = await DoSearch(
            null, afilter, CruftFilter.AllCruft);

        return (int)(response.TotalCount ?? 0);
    }

    public async Task<IReadOnlyList<VotingRecord>> GetVotingRecords()
    {
        var results = await GetTagFacets("Users", 10000);

        var facets = results["Users"];

        var users = new Dictionary<string, VotingRecord>();

        foreach (var facet in facets)
        {
            var value = (string)facet.Value;
            if (value == null || !facet.Count.HasValue)
            {
                continue;
            }

            var fields = value.Split('|');

            var userId = fields[0];

            if (userId.StartsWith("batch-") || userId == "batch")
            {
                continue;
            }

            var user = users.GetValueOrDefault(userId);
            var count = (int)facet.Count.Value;

            if (user == null)
            {
                user = new VotingRecord { UserId = userId };
                users[userId] = user;
            }

            if (fields.Length == 1)
            {
                user.Votes = count;
            }
            else if (fields.Length == 2)
            {
                if (fields[1] == "l")
                {
                    user.Likes = count;
                }
                else if (fields[1] == "h")
                {
                    user.Hates = count;
                }
                else
                {
                    Trace.WriteLine($"User {userId} has invalid modifier '{fields[1]}'");
                }
            }
            else
            {
                Trace.WriteLine($"User {userId} has invalid number of fields 'value'");
            }
        }

        return users.Values.ToList();
    }

    #endregion

    #region Index Update
    public async Task SaveSong(Song song, string id = "default")
    {
        await SaveSongs([song], id);
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public async Task SaveSongs(IEnumerable<Song> songs, string id = "default")
    {
        if (songs == null || !songs.Any())
        {
            return;
        }

        var stats = DanceMusicService.DanceStats;
        foreach (var song in songs)
        {
            stats.UpdateSong(song);
        }

        await DanceMusicService.GetSongIndex(id).UpdateAzureIndex(songs, DanceMusicService);
    }

    public async Task<int> UpdateAzureIndex(IEnumerable<Song> songs, DanceMusicCoreService dms)
    {
        if (songs == null)
        {
            return 0;
        }

        try
        {
            var processed = 0;
            var list = songs as List<Song> ?? songs.ToList();

            while (list.Count > 0)
            {
                var deleted = new List<object>();
                var added = new List<object>();

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var song in list)
                {
                    if (!song.IsNull)
                    {
                        added.Add(DocumentFromSong(song));
                    }
                    else
                    {
                        deleted.Add(DocumentFromSong(song));
                    }

                    if (added.Count > 990 || deleted.Count > 990)
                    {
                        break;
                    }
                }

                if (added.Count > 0)
                {
                    try
                    {
                        var batch = IndexDocumentsBatch.Upload(added);
                        var results = await Client.IndexDocumentsAsync(batch);
                        Trace.WriteLine($"Added = {results.Value.Results.Count}");
                    }
                    catch (RequestFailedException ex)
                    {
                        Trace.WriteLine($"RequestFailedException: {ex.Message}");
                    }
                }

                if (deleted.Count > 0)
                {
                    try
                    {
                        var batch = IndexDocumentsBatch.Delete(
                            deleted.Select(d => new SearchDocument { [SongIdField] = GetSongId(d) }));
                        var results = await Client.IndexDocumentsAsync(batch);
                        Trace.WriteLine($"Deleted = {results.Value.Results.Count}");
                    }
                    catch (RequestFailedException ex)
                    {
                        Trace.WriteLine($"RequestFailedException: {ex.Message}");
                    }
                }

                list.RemoveRange(0, added.Count + deleted.Count);
                processed += added.Count + deleted.Count;
            }

            return processed;
        }
        catch (Exception e)
        {
            Trace.WriteLine($"UpdateAzureIndex Failed: {e.Message}");
            return 0;
        }
    }

    protected abstract string GetSongId(object doc);

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public void SaveSongsImmediate(IEnumerable<Song> songs)
    {
        if (songs == null || !songs.Any())
        {
            return;
        }

        var stats = DanceMusicService.DanceStats;
        foreach (var song in songs)
        {
            stats.UpdateSong(song);
        }

        IndexUpdater.Enqueue(DanceMusicService.GetTransientService(), Info);
    }

    internal async Task UpdateFromFilter(string filter)
    {
        var parameters = new SearchOptions { Filter = filter };

        await UpdateAzureIndex(
            await SongsFromAzureResult(await DoSearch(null, parameters)), DanceMusicService);

        await DanceMusicService.SaveChanges();
    }
    #endregion

    #region Search
    public async Task<SearchResults> Search(
    SongFilter filter, int? pageSize = null, CruftFilter cruft = CruftFilter.NoCruft)
    {
        if (filter.CruftFilter != CruftFilter.NoCruft)
        {
            cruft = filter.CruftFilter;
        }

        return await Search(
            filter.SearchString, AzureParmsFromFilter(filter, pageSize), cruft);
    }

    public async Task<SearchResults> Search(
        string search, SearchOptions parameters, CruftFilter cruft = CruftFilter.NoCruft)
    {
        // Strip off the Lucene syntax indicator
        search = new KeywordQuery(search).Keywords;

        try
        {
            var response = await DoSearch(search, parameters, cruft);
            var songs = await CreateSongs(response.GetResults());
            var pageSize = parameters.Size ?? 25;
            var page = (parameters.Skip ?? 0) / pageSize + 1;
            var facets = response.Facets;
            return new SearchResults(
                search, songs.Count, response.TotalCount ?? -1, page, pageSize,
                songs, facets);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Failed Search: ${e.Message}");
            return new SearchResults(
                search, 0, -1, 0, 0, [], null);
        }
    }

    public async Task<SearchResults> List(
        IEnumerable<string> ids)
    {
        // Strip off the Lucene syntax indicator
        try
        {
            var options = new SearchOptions { Filter = ListToOdata(ids), Size = 1000 };
            var response = await DoSearch("*", options);
            var songs = await CreateSongs(response.GetResults());
            var facets = response.Facets;
            return new SearchResults(
                "*", songs.Count, response.TotalCount ?? -1, 1, 1000,
                songs, facets);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Failed Search: ${e.Message}");
            return new SearchResults(
                "", 0, -1, 0, 0, [], null);
        }
    }

    private string ListToOdata(IEnumerable<string> ids)
    {
        return $"search.in(SongId,'{string.Join(",", ids)}')";
    }


    public async Task<IEnumerable<Song>> FindAlbum(string name, CruftFilter cruft = CruftFilter.NoCruft)
    {
        return await FindByField(AlbumsField, name, cruft);
    }

    public async Task<IEnumerable<Song>> FindArtist(string name,
        CruftFilter cruft = CruftFilter.NoCruft)
    {
        return await FindByField(Song.ArtistField, name, cruft);
    }

    public async Task<IEnumerable<Song>> FindByField(string field, string name,
        CruftFilter cruft = CruftFilter.NoCruft)
    {
        var options = new SearchOptions();
        options.SearchFields.Add(field);
        return await SongsFromAzureResult(await DoSearch($"\"{name}\"", options, cruft));
    }

    internal async Task<IEnumerable<Song>> SongsFromTitle(string title)
    {
        var options = new SearchOptions
        {
            SearchFields = { "Title" },
            Size = 50
        };
        var response = await DoSearch(
            title, options, CruftFilter.AllCruft);
        return await CreateSongs(response.GetResults());
    }

    public async Task<IEnumerable<Song>> SongsFromTitleArtist(string title, string artist,
        SearchClient client = null)
    {
        var options = new SearchOptions
        {
            SearchFields = { "Title", "Artist" },
            Size = 50
        };
        var response = await DoSearch(
            $"{title} {artist}", options, CruftFilter.AllCruft);
        return await CreateSongs(response.GetResults());
    }

    public async Task<IEnumerable<Song>> SimpleSearch(string search, SearchClient client = null)
    {
        var options = new SearchOptions
        {
            Size = 6
        };

        var response = await DoSearch(search, options);

        return await CreateSongs(response.GetResults());
    }

    public async Task<IEnumerable<Song>> LoadLightSongs()
    {
        var parameters = new SearchOptions
        {
            QueryType = SearchQueryType.Simple,
            Size = int.MaxValue,
        };
        parameters.Select.AddRange(
            new[]
            {
                SongIdField, Song.TitleField, Song.ArtistField, Song.LengthField,
                Song.TempoField
            });

        var results = new List<Song>();
        var response = await Client.SearchAsync<SearchDocument>("", parameters);

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var res in response.Value.GetResults())
        {
            var doc = res.Document;
            var title = doc[Song.TitleField] as string;
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }

            results.Add(Song.CreateLightSong(doc));
        }

        return results;
    }


    #endregion

    #region Facets
    public async Task<IDictionary<string, IList<FacetResult>>> GetTagFacets(string categories, int count)
    {
        var parameters = AzureParmsFromFilter(new SongFilter(), 1);
        AddAzureCategories(parameters, categories, count);

        return (await DoSearch(null, parameters)).Facets;
    }

    public static void AddAzureCategories(SearchOptions parameters, string categories,
        int count)
    {
        parameters.Facets.AddRange(
            categories.Split(',').Select(c => $"{c},count:{count}").ToList());
    }

    public async Task<SuggestionList> AzureSuggestions(string query)
    {
        try
        {
            var sp = new SuggestOptions { Size = 50 };

            if (query.Length > 100)
            {
                query = query[..100];
            }

            var response = await Client.SuggestAsync<SearchDocument>(
                query, "songs", sp);

            var comp = new SuggestionComparer();
            var ret = response.Value.Results.Select(
                result => new Suggestion
                {
                    Value = result.Text,
                    Data = result.Document.GetString(SongIdField)
                }).Distinct(comp).Take(10).ToList();

            return new SuggestionList
            {
                Query = query,
                Suggestions = ret
            };
        }
        catch (Exception e)
        {
            Trace.WriteLineIf(
                TraceLevels.General.TraceWarning,
                $"Azure Search Suggestion Failed on '{query}' with '{e.Message}'");
            return null;
        }
    }
    #endregion

    #region Filters
    public SearchOptions AzureParmsFromFilter(SongFilter filter, int? pageSize = null)
    {
        pageSize ??= 25;

        if (filter.IsRaw)
        {
            return new RawSearch(filter).GetAzureSearchParams(pageSize);
        }

        var order = filter.ODataSort;
        var odataFilter = filter.GetOdataFilter(DanceMusicService);

        var useLucene = filter.KeywordQuery.IsLucene;
        var ret = new SearchOptions
        {
            QueryType = useLucene ? SearchQueryType.Full : SearchQueryType.Simple,
            SearchMode = useLucene ? SearchMode.All : SearchMode.Any,
            Filter = odataFilter,
            IncludeTotalCount = true,
            Size = pageSize,
            Skip = ((filter.Page ?? 1) - 1) * pageSize
        };
        ret.OrderBy.AddRange(order);
        return ret;
    }

    public static SearchOptions AddCruftInfo(SearchOptions parameters, CruftFilter cruft)
    {
        if (cruft == CruftFilter.AllCruft)
        {
            return parameters;
        }

        var extra = new StringBuilder();
        if ((cruft & CruftFilter.NoPublishers) != CruftFilter.NoPublishers)
        {
            extra.Append("Purchase/any()");
        }

        if ((cruft & CruftFilter.NoDances) != CruftFilter.NoDances)
        {
            if (extra.Length > 0)
            {
                extra.Append(" and ");
            }

            extra.Append("DanceTags/any()");
        }

        if (parameters.Filter == null)
        {
            parameters.Filter = extra.ToString();
        }
        else
        {
            extra.AppendFormat(" and {0}", parameters.Filter);
            parameters.Filter = extra.ToString();
        }

        return parameters;
    }

    #endregion

    #region Search Helpers

    private async Task<SearchResults<SearchDocument>> DoSearch(
        string search, SearchOptions parameters, CruftFilter cruft = CruftFilter.NoCruft)
    {
        parameters = AddCruftInfo(parameters, cruft);
        if (string.IsNullOrWhiteSpace(search))
        {
            search = "*";
        }

        return await Client.SearchAsync<SearchDocument>(search, parameters);
    }
    #endregion

    #region Index Management

    public async Task<SearchIndex> ResetIndex()
    {
        var info = Info;
        try
        {
            var response = await IndexClient.DeleteIndexAsync(info.Index);
            Trace.WriteLine(response.Status);
        }
        catch (RequestFailedException ex)
        {
            // SearchTODO: If the index doesn't exist, do we end up here?
            Trace.WriteLine(ex.Message);
        }

        var index = GetIndex();

        try
        {
            var response = await IndexClient.CreateIndexAsync(index);
            return response.Value;
        }
        catch (RequestFailedException ex)
        {
            Trace.WriteLine(ex.Message);
            return null;
        }
    }

    public abstract SearchIndex GetIndex();

    public abstract Task<bool> UpdateIndex(IEnumerable<string> dances);

    public async Task<int> UploadIndex(IList<string> lines, bool trackDeleted)
    {
        const int chunkSize = 500;
        var page = 0;
        var added = 0;
        var delete = new List<string>();

        for (var i = 0; i < lines.Count; page += 1)
        {
            AdminMonitor.UpdateTask("AddSongs", added);
            var chunk = new List<Song>();
            for (; i < lines.Count && i < (page + 1) * chunkSize; i++)
            {
                var song = await Song.Create(lines[i], DanceMusicService);
                chunk.Add(song);
                if (trackDeleted)
                {
                    delete.AddRange(song.GetAltids());
                }
            }

            if (chunk.Count == 0)
            {
                continue;
            }

            //var songs = chunk.Where(s => !s.IsNull).Select(s => 
            //    new IndexDocumentsAction<SearchDocument>(
            //        IndexActionType.MergeOrUpload, s.GetIndexDocument()));

            try
            {
                var songs = chunk.Where(s => !s.IsNull).Select(song => DocumentFromSong(song));
                var batch = IndexDocumentsBatch.Upload(songs);
                var results = await Client.IndexDocumentsAsync(batch);
                added += results.Value.Results.Count;
            }
            catch (RequestFailedException ex)
            {
                Trace.WriteLine($"RequestFailedException: {ex.Message}");
            }

            Trace.WriteLine($"Upload Index: {added} songs added.");
        }

        if (delete.Count <= 0)
        {
            return added;
        }

        try
        {
            var batch = IndexDocumentsBatch.Delete(
                delete.Select(d => new SearchDocument { [SongIdField] = d }));
            var results = await Client.IndexDocumentsAsync(batch);
            Trace.WriteLine($"Deleted = {results.Value.Results.Count}");
        }
        catch (RequestFailedException ex)
        {
            Trace.WriteLine($"RequestFailedException: {ex.Message}");
        }

        return added;
    }

    protected abstract object DocumentFromSong(Song song);

    public async Task<IEnumerable<string>> BackupIndex(int count = -1, SongFilter filter = null)
    {
        filter ??= new SongFilter();

        var parameters = AzureParmsFromFilter(filter);
        parameters.IncludeTotalCount = false;
        parameters.Skip = null;
        parameters.Size = count == -1 ? null : count;
        parameters.OrderBy.Add("Modified desc");
        parameters.Select.AddRange(
            [SongIdField, ModifiedField, PropertiesField]);

        var searchString = string.IsNullOrWhiteSpace(filter.SearchString)
            ? null
            : filter.SearchString;
        var response = await Client.SearchAsync<SearchDocument>(searchString, parameters);
        return response.Value.GetResults().Select(
            r =>
                Song.Serialize(
                    r.Document.GetString(SongIdField),
                    r.Document.GetString(PropertiesField)));

    }
    #endregion

    #region Helpers
    protected abstract Task<Song> CreateSong(SearchDocument document);

    protected async Task<IEnumerable<Song>> SongsFromAzureResult(
        SearchResults<SearchDocument> result)
    {
        return await CreateSongs(result.GetResults());
    }

    private SearchClient CreateSearchClient()
    {
        var info = Manager.GetInfo(SearchId);
        var endpoint = new Uri("https://msc4dnc.search.windows.net");
        var credentials = new AzureKeyCredential(info.AdminKey);
        return new SearchClient(endpoint, info.Index, credentials);
    }

    private SearchIndexClient CreateSearchIndexClient()
    {
        var info = Manager.GetInfo(SearchId);
        var endpoint = new Uri("https://msc4dnc.search.windows.net");
        var credentials = new AzureKeyCredential(info.AdminKey);
        return new SearchIndexClient(endpoint, credentials);
    }

    protected static void AccumulateComments(List<UserComment> comments, List<string> accumulator)
    {
        if (comments is { Count: > 0 })
        {
            accumulator.AddRange(comments.Select(c => c.Comment));
        }
    }

    protected static float? CleanNumber(float? f)
    {
        return f.HasValue && !float.IsFinite(f.Value) ? null : f;
    }


    #endregion
}

