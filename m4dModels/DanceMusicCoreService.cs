using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace m4dModels;

public partial class DanceMusicCoreService(DanceMusicContext context, ISearchServiceManager searchService,
    IDanceStatsManager danceStatsManager, SongIndex songIndex = null) : IDisposable
{
    #region Logging

    public async Task UndoUserChanges(ApplicationUser user, Guid songId)
    {
        var song = await SongIndex.FindSong(songId);

        if (await song.UndoUserChanges(user, this))
        {
            await SongIndex.SaveSong(song);
        }
    }

    #endregion

    #region Lifetime Management

    //public static DanceMusicCoreService GetService()
    //{
    //    var optionsBuilder = new DbContextOptionsBuilder<TransientDanceMusicContext>();
    //    optionsBuilder.UseSqlServer(
    //        context.Configuration.GetConnectionString("DanceMusicContextConnection"));
    //    var context = new DanceMusicContext(options =>
    //        options
    //}

    public DanceMusicCoreService GetTransientService()
    {
        return new DanceMusicCoreService(
            Context.CreateTransientContext(), SearchService,
            DanceStatsManager);
    }

    public void Dispose()
    {
        var temp = Context;
        Context = null;
        temp?.Dispose();
    }

    #endregion

    #region Properties

    public DanceMusicContext Context { get; private set; } = context;

    public ISearchServiceManager SearchService { get; } = searchService;

    internal IDanceStatsManager DanceStatsManager { get; } = danceStatsManager;

    private SongIndex _songSearch = songIndex;

    public SongIndex SongIndex =>
        _songSearch ??= GetSongIndex("default");

    public SongIndex GetSongIndex(string id, bool? isNext = null) {
        if (_songSearch != null && isNext == null
            && (id == "default" || id == null))
        {
            return _songSearch;
        }

        return SongIndex.Create(this, id, isNext ?? SearchService.NextVersion);
    }

    public DbSet<Dance> Dances => Context.Dances;
    public DbSet<TagGroup> TagGroups => Context.TagGroups;
    public DbSet<Search> Searches => Context.Searches;
    public DbSet<PlayList> PlayLists => Context.PlayLists;

    public DbSet<ActivityLog> ActivityLog => Context.ActivityLog;

    public DbSet<UsageLog> UsageLog => Context.UsageLog;

    public async Task<int> SaveChanges()
    {
        return await Context.SaveChangesAsync();
    }

    public static readonly string EditRole = "canEdit";
    public static readonly string TagRole = "canTag";
    public static readonly string DiagRole = "showDiagnostics";
    public static readonly string DbaRole = "dbAdmin";
    public static readonly string PseudoRole = "pseudoUser";
    public static readonly string PremiumRole = "premium";
    public static readonly string BetaRole = "beta";
    public static readonly string TrialRole = "trial";

    public static readonly string[] Roles =
        [DiagRole, EditRole, DbaRole, TagRole, PremiumRole, TrialRole, BetaRole];

    public static IEnumerable<string> UserRoles(ClaimsPrincipal user)
    {
        return Roles.Where(user.IsInRole);
    }

    #endregion

    #region Edit


    public async Task<Dance> EditDance(DanceCore core)
    {
        var context = Context;
        var dance = await context.Dances.FindAsync(core.Id);
        if (dance == null)
        {
            return null;
        }

        if (!dance.Description.Equals(core.Description))
        {
            dance.Description = core.Description;
        }

        var newIds = new List<Guid>();

        if (core.DanceLinks != null)
        {
            foreach (var link in core.DanceLinks)
            {
                link.DanceId = core.Id;
                if (link.Id == Guid.Empty)
                {
                    var guid = Guid.NewGuid();
                    link.Id = guid;
                    newIds.Add(guid);
                }
            }
        }

        var danceLinks = core.DanceLinks ?? [];

        // Change new state to added
        foreach (var link in danceLinks.Where(d => newIds.Contains(d.Id)))
        {
            context.Entry(link).State = EntityState.Added;
        }

        // Find the deleted links (if any)
        var oldLinks = context.DanceLinks.Where(dl => dl.DanceId == core.Id).ToList();
        foreach (var link in oldLinks)
        {
            var newLink = danceLinks.Find(l => l.Id == link.Id);
            if (newLink == null)
            {
                context.Entry(link).State = EntityState.Deleted;
            }
            else if (link.Description != newLink.Description || link.Link != newLink.Link)
            {
                link.Description = newLink.Description;
                link.Link = newLink.Link;
                context.Entry(link).State = EntityState.Modified;
            }
            else
            {
                context.Entry(link).State = EntityState.Unchanged;
            }
        }

        dance.Modified = DateTime.Now;
        context.Update(dance);
        await SaveChanges();

        await DanceStatsManager.ReloadDances(this);
        return dance;
    }

    #endregion

    #region Searching



    public static ICollection<ICollection<PurchaseLink>> GetPurchaseLinks(
        ServiceType serviceType, IEnumerable<Song> songs, string region = null)
    {
        if (songs == null)
        {
            return null;
        }

        var links = new List<ICollection<PurchaseLink>>();
        var cid = MusicService.GetService(serviceType).CID;
        var sid = cid.ToString();

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var song in songs)
        {
            if (song.Purchase == null || !song.Purchase.Contains(cid))
            {
                continue;
            }

            var l = song.GetPurchaseLinks(sid, region);
            if (l != null)
            {
                links.Add(l);
            }
        }

        return links;
    }

    #endregion

    #region Tags

    public IReadOnlyDictionary<string, TagGroup> TagMap =>
        DanceStatsManager.Instance.TagManager.TagMap;

    public DanceStatsInstance DanceStats => _stats ?? DanceStatsManager.Instance;

    public TagManager TagManager => DanceStats.TagManager;

    public void SetStatsInstance(DanceStatsInstance stats)
    {
        _stats = stats;
    }

    private DanceStatsInstance _stats;

    // Add in category for tags that don't already have one + create
    //  tagtype if necessary
    public string NormalizeTags(string tags, string category)
    {
        var old = new TagList(tags);
        var result = new List<string>();
        foreach (var tag in old.Tags)
        {
            var tempTag = tag;
            var tempCat = category;
            var rg = tag.Split(':');
            if (rg.Length == 2)
            {
                tempTag = rg[0];
                tempCat = rg[1];
            }

            var group = TagManager.FindOrCreateTagGroup($"{tempTag}:{tempCat}");

            result.Add(group.Key);
        }

        return new TagList(result).ToString();
    }

    // Change the name of a tag: This finds or creates a primary tag with the new name
    //  points this tag to it, then renames all the actual tags.  The user can then
    //  delete this tag if we don't want all future use of this tag to point to the primary
    public async Task<bool> RenameTag(TagGroup tagGroup, string newKey)
    {
        return await UpdateTag(tagGroup, newKey, allowPrimaryCreation: true);
    }

    // Set Primary Tag: Set's the tag's primary and renames all atual references
    //  to the original tag.
    public async Task<bool> SetPrimaryTag(TagGroup tagGroup, string newPrimary)
    {
        return await UpdateTag(tagGroup, newPrimary, allowPrimaryCreation: false);
    }

    private async Task<bool> UpdateTag(TagGroup tagGroup, string newKey, bool allowPrimaryCreation)
    {
        var filter = FilterFromTag(tagGroup.Key);

        TagGroup primary;
        if (allowPrimaryCreation)
        {
            primary = TagManager.FindOrCreateTagGroup(newKey);
        }
        else if (!TagManager.TagMap.TryGetValue(newKey, out primary))
        {
            return false;
        }

        if (!await AddTagsToDatabase(tagGroup, primary))
        {
            return false;
        }

        await SongIndex.UpdateFromFilter(filter);

        return true;

    }

    private async Task<bool> AddTagsToDatabase(TagGroup tagGroup, TagGroup primary)
    {
        tagGroup = TagManager.SetPrimary(tagGroup.Key, primary.Key);
        if (tagGroup == null)
        {
            return false;
        }

        var dbGroup = await TagGroups.FindAsync(tagGroup.Key);
        if (dbGroup == null)
        {
            TagGroups.Add(tagGroup.GetDisconnected());
        }
        else
        {
            dbGroup.Primary = primary;
            dbGroup.PrimaryId = primary.Key;
        }
        var dbPrimary = await TagGroups.FindAsync(primary.Key);
        if (dbPrimary == null)
        {
            TagGroups.Add(primary.GetDisconnected());
        }
        return true;

    }


    private string FilterFromTag(string key)
    {
        var filter = SearchService.GetSongFilter();
        filter.Tags = key;
        var parameters = SongIndex.AzureParmsFromFilter(filter);
        var ret = parameters.Filter;

        if (string.IsNullOrWhiteSpace(ret))
        {
            throw new ArgumentOutOfRangeException(
                nameof(key),
                $"Attempted to UpdateTag {key}");
        }
        return ret;
    }



    public IEnumerable<TagGroup> OrderedTagGroups => DanceStatsManager.Instance.TagGroups;

    public virtual ICollection<TagGroup> GetTagRings(TagList tags)
    {
        var map = new Dictionary<string, TagGroup>();

        // ReSharper disable once LoopCanBePartlyConvertedToQuery
        foreach (var tag in tags.Tags)
        {
            var tt = GetTagRing(tag);
            map.TryAdd(tt.Key, tt);
        }

        return map.Values;
    }

    public TagGroup GetTagRing(string tag)
    {
        return TagMap.TryGetValue(tag, out var tt)
            ? tt.GetPrimary()
            : new TagGroup(tag);
    }

    protected TagGroup CreateTagGroup(string value, string category,
        bool updateTagManager = true)
    {
        var type = new TagGroup
        {
            Key = TagGroup.BuildKey(value, category)
        };

        var other = TagGroups.Find(type.Key);
        if (other != null)
        {
            // This will update case
            type = other;
        }
        else
        {
            type.Modified = DateTime.Now;
            // CORETODO: In EF6 we were taking the tracking entity from TagGroups.Add and adding that
            //  to TagManager - should we be doing something similar now???
            TagGroups.Add(type);
            if (updateTagManager)
            {
                TagManager.AddTagGroup(type);
            }
        }

        return type;
    }

    #endregion

    #region Index Management
    public async Task CloneIndex(string to)
    {
        AdminMonitor.UpdateTask("StartBackup");
        var lines = (await SongIndex.BackupIndex()).ToList();
        var toIndex = GetSongIndex(to);
        AdminMonitor.UpdateTask("StartReset");
        await toIndex.ResetIndex();
        AdminMonitor.UpdateTask("StartUpload");
        await toIndex.UploadIndex(lines, false);
    }

    public async Task UpdateIndex()
    {
        AdminMonitor.UpdateTask("StartCreate");
        var toIndex = GetSongIndex(null, isNext: true);
        await toIndex.ResetIndex();
        AdminMonitor.UpdateTask("StartBackup");
        var lines = (await SongIndex.BackupIndex()).ToList();
        AdminMonitor.UpdateTask("StartUpload");
        await toIndex.UploadIndex(lines, false);
        AdminMonitor.UpdateTask("RedirectToUpdate");
        _songSearch = null;
        SearchService.RedirectToUpdate();
    }

    #endregion

    #region Merging

    public async Task<IReadOnlyCollection<Song>> FindMergeCandidates(int n, int level)
    {
        return await MergeCluster.GetMergeCandidates(this, n, level);
    }

    public void RemoveMergeCandidates(IEnumerable<Song> songs)
    {
        foreach (var song in songs)
        {
            MergeCluster.RemoveMergeCandidate(song);
        }
    }

    public void ClearMergeCandidates()
    {
        MergeCluster.ClearMergeCandidateCache();
    }

    public void ClearCache()
    {
        DanceStatsManager.ClearCache(this, true);
        MergeCluster.ClearMergeCandidateCache();
        _songSearch = null;
    }

    public async Task UpdatePlayList(string id, IEnumerable<Song> songs)
    {
        var playlist = await PlayLists.FindAsync(id);
        if (playlist == null || playlist.Type != PlayListType.SongsFromSpotify)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }


        var service = MusicService.GetService(ServiceType.Spotify) ?? throw new ArgumentOutOfRangeException(nameof(id));
        playlist.AddSongs(songs.Select(s => s.GetPurchaseId(service.Id)));
        playlist.Updated = DateTime.Now;

        await SaveChanges();
    }

    public async Task AddPlaylist(string id, PlayListType type, string user, string tags)
    {
        var playlist = new PlayList
        {
            Id = id,
            Type = type,
            User = user,
            Data1 = tags,
            Created = DateTime.Now,
        };
        PlayLists.Add(playlist);
        await SaveChanges();
    }

    #endregion

    #region User

    protected virtual async Task<ApplicationUser> CoreFindUser(string name)
    {
        return await Context.Users.FirstOrDefaultAsync(u => u.UserName == name);
    }

    public async Task<ApplicationUser> FindUser(string name)
    {
        var idx = name.IndexOf('|');
        if (idx != -1)
        {
            name = name[..idx];
        }

        if (UserCache.TryGetValue(name, out var user))
        {
            return user;
        }

        user = await CoreFindUser(name);
        if (user != null)
        {
            UserCache[name] = user;
        }

        return user;
    }

    protected readonly Dictionary<string, ApplicationUser> UserCache =
        [];

    #endregion
}
