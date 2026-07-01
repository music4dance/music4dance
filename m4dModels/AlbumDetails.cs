using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace m4dModels;

// ReSharper disable once InconsistentNaming

[DataContract]
public partial class AlbumDetails
{
    #region Construction

    public AlbumDetails()
    {
    }

    public AlbumDetails(AlbumDetails a)
    {
        Name = a.Name;
        Publisher = a.Publisher;
        Track = a.Track;

        Purchase = a.Purchase != null
            ? new Dictionary<string, string>(a.Purchase)
            : [];
    }

    #endregion

    #region Properties

    [DataMember]
    public string Name { get; set; }

    [DataMember]
    public string Publisher { get; set; }

    [DataMember]
    [Range(1, 999999999)]
    public int? Track { get; set; }

    // This is the serialization and default ordering index
    //  Actual order will be affected by repeated application
    //  of the MakePrimary attribute
    [DataMember]
    public int Index { get; set; }

    // Semi-colon separated purchase info of the form XX=YYYYYY (XX is service/type and YYYYY is id)
    // IA = Itunes Album
    // IS = Itunes Song
    // AA = Amazon Album
    // AS = Amazon Song
    // SA = Spotify Album
    // SS = Spotify Song
    // XA = Groove Album
    // XS = Groove Song
    // MS = AMG Song
    [DataMember]
    public Dictionary<string, string> Purchase { get; set; }

    [DataMember]
    public string PurchaseInfo
    {
        get => SerializePurchaseInfo();
        set => SetPurchaseInfo(value);
    }

    [DataMember]
    public IEnumerable<PurchaseLink> PurchaseLinks
    {
        // TODO: Think this through some more - we're basically
        //  faking a r/w property that is redundant with PurchaseInfo
        //  in order to get more easily consumable information
        //  into json
        get => GetPurchaseLinks();
        // ReSharper disable once ValueParameterNotUsed
        set { }
    }

    public bool HasPurchaseInfo => Purchase != null && Purchase.Count > 0;

    public bool IsRealAlbum
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Name) && Track.HasValue && !WordPattern()
                .Split(Name.ToLower()).Any(w => BallroomWords.Contains(w));
        }
    }

    public TrackNumber TrackNumber => new(Track ?? 0);

    #endregion

    #region Purchase Serialization

    /// <summary>
    ///     Formatted purchase info
    /// </summary>
    /// <returns></returns>
    public IList<string> GetPurchaseInfo(bool compact = false)
    {
        if (Purchase == null || Purchase.Count == 0)
        {
            return null;
        }

        var info = new List<string>(Purchase.Count);
        foreach (var p in Purchase)
        {
            if (compact)
            {
                info.Add(p.Key + "=" + p.Value);
            }
            else
            {
                info.Add(MusicService.ExpandPurchaseType(p.Key) + "=" + p.Value);
            }
        }

        return info;
    }

    public string SerializePurchaseInfo()
    {
        var pi = GetPurchaseInfo(true);
        return pi == null ? null : string.Join(";", pi);
    }

    #endregion

    #region Purchase Manipulation

    public string GetPurchaseTags()
    {
        var sb = new StringBuilder();
        var added = new HashSet<char>();

        if (Purchase == null)
        {
            return sb.Length == 0 ? null : sb.ToString();
        }

        foreach (var t in Purchase.Keys)
        {
            var c = t[0];
            if (added.Contains(c))
            {
                continue;
            }

            _ = added.Add(c);
            _ = sb.Append(c);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    public void SetPurchaseInfo(PurchaseType pt, ServiceType ms, string value)
    {
        Purchase ??= [];

        Purchase[BuildPurchaseKey(pt, ms)] = value;
    }

    // Services (Spotify in particular) periodically reissue a different id for what is
    // otherwise the same recording/album. Rather than overwrite (and lose) a prior id, multiple
    // ids for the same service/type are packed into one dictionary value, separated by ','.
    // Service ids themselves never contain a comma (alphanumeric Spotify/iTunes/Amazon ids).
    private const char IdSeparator = ',';

    private static IReadOnlyList<string> SplitIds(string value) =>
        [.. value.Split(IdSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];

    /// <summary>
    /// Adds <paramref name="value"/> to the (possibly already multi-valued) id slot for this
    /// service/type, preserving any id(s) already there. No-ops if it's already present.
    /// Returns true if the stored value changed.
    /// </summary>
    public bool AddPurchaseId(PurchaseType pt, ServiceType ms, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var ids = GetPurchaseIdentifiers(ms, pt);
        if (ids.Contains(value))
        {
            return false;
        }

        Purchase ??= [];
        Purchase[BuildPurchaseKey(pt, ms)] = string.Join(IdSeparator, [.. ids, value]);
        return true;
    }

    /// <summary>
    /// Adds a single <paramref name="id"/> to the slot identified by the raw
    /// <paramref name="purchaseKey"/> (e.g. "SS", "IA"), accumulating alongside any existing
    /// id(s). No-ops if <paramref name="id"/> is already present. Returns true if changed.
    /// </summary>
    public bool AddPurchaseId(string purchaseKey, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        Purchase ??= [];
        if (Purchase.TryGetValue(purchaseKey, out var existing) && !string.IsNullOrWhiteSpace(existing))
        {
            var ids = SplitIds(existing);
            if (ids.Contains(id))
            {
                return false;
            }

            Purchase[purchaseKey] = string.Join(IdSeparator, [.. ids, id]);
        }
        else
        {
            Purchase[purchaseKey] = id;
        }

        return true;
    }

    /// <summary>
    /// Removes a specific <paramref name="id"/> from the slot identified by
    /// <paramref name="purchaseKey"/>. Removes the entire slot when it was the last id.
    /// No-ops if the id is not present. Returns true if changed.
    /// </summary>
    public bool RemovePurchaseId(string purchaseKey, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || Purchase == null)
        {
            return false;
        }

        if (!Purchase.TryGetValue(purchaseKey, out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            return false;
        }

        var allIds = SplitIds(existing);
        var remaining = allIds.Where(x => x != id).ToArray();
        if (remaining.Length == allIds.Count)
        {
            return false;
        }

        if (remaining.Length == 0)
        {
            _ = Purchase.Remove(purchaseKey);
        }
        else
        {
            Purchase[purchaseKey] = string.Join(IdSeparator, remaining);
        }

        return true;
    }

    /// <summary>
    /// All ids on file for this service/type — usually one, but see <see cref="AddPurchaseId"/>.
    /// </summary>
    public IList<string> GetPurchaseIdentifiers(ServiceType ms, PurchaseType pt)
    {
        if (Purchase == null ||
            !Purchase.TryGetValue(MusicService.GetService(ms).BuildPurchaseKey(pt), out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return [.. value.Split(IdSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    public static string BuildPurchaseInfo(PurchaseType pt, ServiceType ms, string value)
    {
        return $"{BuildPurchaseKey(pt, ms)}={value}";
    }

    public static string BuildPurchaseInfo(ServiceType ms, string collection, string track)
    {
        var sb = new StringBuilder();

        if (collection != null)
        {
            _ = sb.Append(BuildPurchaseInfo(PurchaseType.Album, ms, collection));
        }

        if (track != null)
        {
            if (sb.Length != 0)
            {
                _ = sb.Append(';');
            }

            _ = sb.Append(BuildPurchaseInfo(PurchaseType.Song, ms, track));
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    public static string BuildPurchaseKey(PurchaseType purchaseType, ServiceType serviceType)
    {
        return purchaseType == PurchaseType.None
            ? throw new ArgumentOutOfRangeException(nameof(purchaseType))
            : serviceType == ServiceType.None
            ? throw new ArgumentOutOfRangeException(nameof(serviceType))
            : MusicService.GetService(serviceType).BuildPurchaseKey(purchaseType);
    }

    public void SetPurchaseInfo(string purchase)
    {
        if (string.IsNullOrWhiteSpace(purchase))
        {
            return;
        }

        var values = purchase.Split(';');

        foreach (var value in values)
        {
            if (MusicService.TryParsePurchaseInfo(value, out var pt, out var ms, out var pi))
            {
                SetPurchaseInfo(pt, ms, pi);
            }
        }
    }

    public PurchaseLink GetPurchaseLink(ServiceType ms, string region = null)
    {
        return GetPurchaseLink(ms, PurchaseType.Song, region) ??
            GetPurchaseLink(ms, PurchaseType.Album, region);
    }

    public IList<PurchaseLink> GetPurchaseLinks()
    {
        return [.. MusicService.GetServices().Select(service => GetPurchaseLink(service.Id)).Where(link => link != null)];
    }

    public IList<string> GetExtendedPurchaseIds(PurchaseType pt)
    {
        return Purchase != null
            ? [.. MusicService.GetIndexedServices()
                .SelectMany(
                    service => GetPurchaseIdentifiers(service.Id, pt)
                        .Select(id => $"{service.CID}:{id}"))]
            : [];
    }

    public PurchaseLink GetPurchaseLink(ServiceType ms, PurchaseType pt, string region = null)
    {
        // Short-circuit if there is no purchase info for this album
        if (Purchase == null)
        {
            return null;
        }

        var service = MusicService.GetService(ms);

        // Only the primary (first) id is used to build a clickable link — a multi-valued
        // slot (see AddPurchaseId) can only ever link to one canonical destination.
        var albumInfo = GetPurchaseIdentifier(ms, PurchaseType.Album);
        var songInfo = GetPurchaseIdentifier(ms, PurchaseType.Song);

        var link = service.GetPurchaseLink(pt, albumInfo, songInfo);
        return link != null && !string.IsNullOrWhiteSpace(region) &&
            link.AvailableMarkets != null && !link.AvailableMarkets.Contains(region)
                ? null
                : link;
    }

    /// <summary>
    /// The primary (first) id on file for this service/type. See <see cref="GetPurchaseIdentifiers"/>
    /// for the full, possibly multi-valued, list.
    /// </summary>
    public string GetPurchaseIdentifier(ServiceType ms, PurchaseType pt)
    {
        return GetPurchaseIdentifiers(ms, pt).FirstOrDefault();
    }

    public bool PurchaseDiff(Song song, AlbumDetails old)
    {
        var modified = false;

        // Emit Purchase- for each id that existed in old but is gone from new
        if (old.Purchase != null)
        {
            foreach (var key in old.Purchase.Keys)
            {
                var oldIds = SplitIds(old.Purchase[key]);
                var newIds = Purchase != null && Purchase.TryGetValue(key, out var nv) ? SplitIds(nv) : [];
                foreach (var removedId in oldIds.Except(newIds))
                {
                    _ = song.CreateProperty(
                        SongProperty.FormatName(Song.RemovedPurchaseField, Index, key), removedId);
                    modified = true;
                }
            }
        }

        if (Purchase == null)
        {
            return modified;
        }

        // Emit one Purchase property per id that is new (not in old)
        foreach (var key in Purchase.Keys)
        {
            var newIds = SplitIds(Purchase[key]);
            var oldIds = old.Purchase != null && old.Purchase.TryGetValue(key, out var ov) ? SplitIds(ov) : [];
            foreach (var addedId in newIds.Except(oldIds))
            {
                _ = song.CreateProperty(
                    SongProperty.FormatName(Song.PurchaseField, Index, key), addedId);
                modified = true;
            }
        }

        return modified;
    }

    public void PurchaseAdd(Song song, AlbumDetails old)
    {
        if (Purchase == null)
        {
            return;
        }

        // Emit one Purchase property per id that is new (not in old), for any key
        foreach (var key in Purchase.Keys)
        {
            var newIds = SplitIds(Purchase[key]);
            var oldIds = old.Purchase != null && old.Purchase.TryGetValue(key, out var ov) ? SplitIds(ov) : [];
            foreach (var addedId in newIds.Except(oldIds))
            {
                _ = song.CreateProperty(
                    SongProperty.FormatName(Song.PurchaseField, Index, key), addedId);
            }
        }
    }

    #endregion

    #region Merging

    public static IList<AlbumDetails> MergeAlbums(IList<AlbumDetails> albums, string artist,
        bool preserveIndices)
    {
        var dict = new Dictionary<string, List<AlbumDetails>>();

        var duplicate = false;

        foreach (var a in albums)
        {
            var name = Song.CleanAlbum(a.Name, artist);
            if (dict.TryGetValue(name, out var l))
            {
                duplicate = true;
            }
            else
            {
                l = [];
                dict.Add(name, l);
            }

            l.Add(a);
        }

        // Short circuit out of here if there was nothing to merge
        if (!duplicate)
        {
            return albums;
        }

        var merge = new List<AlbumDetails>();
        foreach (var a in albums)
        {
            var name = Song.CleanAlbum(a.Name, artist);
            if (!dict.TryGetValue(name, out var l))
            {
                continue;
            }

            _ = dict.Remove(name);
            merge.AddRange(MergeList(l));
        }

        if (preserveIndices)
        {
            return merge;
        }

        foreach (var album in merge.OrderBy(x => x.Name))
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"{album.Name}:{album.Track}");
        }

        for (var i = 0; i < merge.Count; i++)
        {
            merge[i].Index = i;
        }

        return merge;
    }

    private static AlbumDetails MergeTrackList(IList<AlbumDetails> l)
    {
        if (l.Count == 1)
        {
            return l[0];
        }

        var max = l.Max(t => t.Track);

        Trace.WriteLineIf(
            TraceLevels.General.TraceVerbose,
            string.Join(" / ", l.Select(t => t.Name)));
        var m = l[0];
        for (var i = 1; i < l.Count; i++)
        {
            m.Merge(l[i]);
        }

        m.Track = max;

        return m;
    }

    private static IEnumerable<AlbumDetails> MergeList(IList<AlbumDetails> albums)
    {
        var dict = new Dictionary<int, List<AlbumDetails>>();

        foreach (var a in albums)
        {
            var t = a.TrackNumber.Track ?? 0;
            if (!dict.TryGetValue(t, out var l))
            {
                l = [];
                dict.Add(t, l);
            }

            l.Add(a);
        }

        // If we have tracks that don't have a number + tracks that do,
        //  add the numberless tracks to the batch that already has the
        //  most members
        if (dict.TryGetValue(0, out var temp) && dict.Count > 1)
        {
            _ = dict.Remove(0);
            var max = dict.Values.Max(t => t.Count);
            var l = dict.Values.First(t => t.Count == max);
            l.AddRange(temp);
        }

        return [.. dict.Values.Select(MergeTrackList)];
    }

    private void Merge(AlbumDetails album)
    {
        if (!string.IsNullOrWhiteSpace(album.Name) && album.Name.Length < Name.Length)
        {
            Name = album.Name;
        }

        if (string.IsNullOrWhiteSpace(Publisher))
        {
            Publisher = album.Publisher;
        }

        if (!Track.HasValue)
        {
            Track = album.Track;
        }

        if (!HasPurchaseInfo)
        {
            Purchase = album.Purchase;
        }
        else if (album.HasPurchaseInfo)
        {
            // Case where both albums have purchase info: Merge the dictionaries keeping all unique entries
            foreach (var key in Purchase.Keys)
            {
                _ = album.Purchase.Remove(key);
            }

            foreach (var p in album.Purchase)
            {
                Purchase[p.Key] = p.Value;
            }
        }
    }

    #endregion

    #region Property Utilities

    public bool ModifyInfo(Song song, AlbumDetails old)
    {
        var modified = false;

        // This indicates a deleted album
        if (string.IsNullOrWhiteSpace(Name))
        {
            old.Remove(song);
            modified = true;
        }
        else
        {
            modified |= ChangeProperty(song, old.Index, Song.AlbumField, null, old.Name, Name);
            modified |= ChangeProperty(
                song, old.Index, Song.TrackField, null, old.Track,
                Track);
            modified |= ChangeProperty(
                song, old.Index, Song.PublisherField, null,
                old.Publisher, Publisher);

            modified |= PurchaseDiff(song, old);
        }

        return modified;
    }

    public void Remove(Song song)
    {
        _ = ChangeProperty(song, Index, Song.AlbumField, null, Name, null);
        if (Track.HasValue)
        {
            _ = ChangeProperty(song, Index, Song.TrackField, null, Track, null);
        }

        if (!string.IsNullOrWhiteSpace(Publisher))
        {
            _ = ChangeProperty(song, Index, Song.PublisherField, null, Publisher, null);
        }
    }

    // Additive update
    public bool UpdateInfo(Song song, AlbumDetails old)
    {
        var modified = false;

        modified |= UpdateProperty(song, old.Index, Song.AlbumField, null, old.Name, Name);
        modified |= UpdateProperty(song, old.Index, Song.TrackField, null, old.Track, Track);
        modified |= UpdateProperty(
            song, old.Index, Song.PublisherField, null, old.Publisher,
            Publisher);

        PurchaseAdd(song, old);

        return modified;
    }

    public void CreateProperties(Song song)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new FieldAccessException(@"Name");
        }

        AddProperty(song, Index, Song.AlbumField, null, Name);
        AddProperty(song, Index, Song.TrackField, null, Track);
        AddProperty(song, Index, Song.PublisherField, null, Publisher);
        if (Purchase == null)
        {
            return;
        }

        foreach (var purchase in Purchase)
        {
            foreach (var id in SplitIds(purchase.Value))
            {
                _ = song.CreateProperty(
                    SongProperty.FormatName(Song.PurchaseField, Index, purchase.Key), id);
            }
        }
    }

    public static void AddProperty(Song song, int idx, string name, string qual, object value)
    {
        if (value == null)
        {
            return;
        }

        var fullName = SongProperty.FormatName(name, idx, qual);

        var op = song.SongProperties.FirstOrDefault(p => p.Name == fullName);

        if (op == null || Equals(op.Value, value))
        {
            _ = song.CreateProperty(fullName, value);
        }
    }


    public static bool ChangeProperty(Song song, int idx, string name, string qual,
        object oldValue, object newValue)
    {
        if (Equals(oldValue, newValue))
        {
            return false;
        }

        _ = song.CreateProperty(SongProperty.FormatName(name, idx, qual), newValue);

        return true;
    }

    public static bool UpdateProperty(Song song, int idx, string name, string qual,
        object oldValue, object newValue)
    {
        if (oldValue != null || newValue == null)
        {
            return false;
        }

        _ = song.CreateProperty(SongProperty.FormatName(name, idx, qual), newValue);

        return true;
    }

    [GeneratedRegex(@"\W")]
    private static partial Regex WordPattern();

    private static readonly HashSet<string> BallroomWords =
    [
        "ballroom", "latin", "ultimate", "standard", "dancing", "competition", "classics",
        "dance"
    ];

    #endregion
}
