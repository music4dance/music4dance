using AutoMapper;

namespace m4dModels;

public class SongPropertyProfile : Profile
{
    public SongPropertyProfile()
    {
        _ = CreateMap<SongProperty, SongPropertySparse>();
        _ = CreateMap<SongPropertySparse, SongProperty>();
    }
}

public class SongPropertySparse
{
    public string Name { get; set; }
    public string Value { get; set; }
}

public class SongHistory
{
    public Guid Id { get; set; }
    public List<SongPropertySparse> Properties { get; set; }

    private static readonly HashSet<string> AlbumGroupFields =
    [
        Song.AlbumField, Song.PublisherField, Song.TrackField, Song.PurchaseField,
        Song.RemovedPurchaseField,
    ];

    /// <summary>
    /// A copy of this history without the album properties a song-list row can never show.
    /// Album, Track, Publisher and Purchase properties are about half of a list page's model:
    /// compilations give popular songs dozens of albums, each with its own purchase ids.
    ///
    /// The client builds albums from these properties alone (Song.buildAlbumInfo in
    /// ClientApp/src/models/Song.ts), and a list row reads only albums[0] (track number) and
    /// the first album with a Spotify or iTunes song id (play links; Amazon is always a search
    /// link). Keeping every property of those album indexes, in order, and dropping the rest
    /// gives the client the same values. Edit headers are left alone, so modified, edited and
    /// modifiedBy don't change.
    ///
    /// Meant for anonymous list pages only. The song details page shows every album, and
    /// signed-in users' pages are left complete. If a property name doesn't parse the way the
    /// client expects, the history is returned unchanged.
    /// </summary>
    public SongHistory TrimAlbumsForList()
    {
        var names = new Dictionary<int, string>();
        var ids = new Dictionary<(int Index, string Slot), List<string>>();

        foreach (var property in Properties)
        {
            var baseName = SongProperty.ParseBaseName(property.Name);
            if (!AlbumGroupFields.Contains(baseName) ||
                SongProperty.ParsePart(property.Name, 1) == null)
            {
                continue;
            }

            var index = SongProperty.ParseIndex(property.Name);
            if (index is null or < 0)
            {
                return this;
            }

            var value = property.Value;
            switch (baseName)
            {
                case Song.AlbumField:
                    names[index.Value] = string.IsNullOrEmpty(value) ? null : value;
                    break;
                case Song.PurchaseField:
                case Song.RemovedPurchaseField:
                {
                    var qualifier = SongProperty.ParseQualifier(property.Name);
                    if (qualifier is not { Length: 2 })
                    {
                        return this;
                    }

                    var slot = (index.Value, qualifier.ToLowerInvariant());
                    if (!ids.TryGetValue(slot, out var list))
                    {
                        list = [];
                        ids[slot] = list;
                    }

                    if (baseName == Song.RemovedPurchaseField)
                    {
                        _ = list.RemoveAll(id => id == value);
                    }
                    else if (!string.IsNullOrEmpty(value) && !list.Contains(value))
                    {
                        list.Add(value);
                    }

                    break;
                }
            }
        }

        var named = names.Where(n => n.Value != null).Select(n => n.Key).Order().ToList();
        if (named.Count == 0)
        {
            return this;
        }

        bool HasSongId(int index, string service) =>
            ids.TryGetValue((index, service + "s"), out var list) && list.Count > 0;

        var keep = new HashSet<int> { named[0] };
        foreach (var service in new[] { "s", "i" })
        {
            var first = named.FirstOrDefault(i => HasSongId(i, service), -1);
            if (first >= 0)
            {
                _ = keep.Add(first);
            }
        }

        return new SongHistory
        {
            Id = Id,
            Properties = [.. Properties.Where(p => KeepForList(p, keep))],
        };
    }

    private static bool KeepForList(SongPropertySparse property, HashSet<int> keep)
    {
        if (!AlbumGroupFields.Contains(SongProperty.ParseBaseName(property.Name)))
        {
            return true;
        }

        var index = SongProperty.ParseIndex(property.Name);
        return index == null || keep.Contains(index.Value);
    }
}
