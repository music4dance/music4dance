using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace m4dModels
{
    // This is a transitory object (really a ViewModel object) that is used for 
    // viewing and editing a song, it shouldn't ever end up in a database,
    // it's meant to aggregate the information about a song in an easily digestible way
    public class SongDetails
    {
        #region Construction
        public SongDetails()
        {
        }

        public SongDetails(Song song)
        {
            SongId = song.SongId;
            Tempo = song.Tempo;
            Title = song.Title;
            Artist = song.Artist;
            Genre = song.Genre;
            Length = song.Length;
            Created = song.Created;
            Modified = song.Modified;

            DanceRatings = song.DanceRatings.ToList();
            Properties = song.SongProperties.ToList();
            ModifiedBy = song.ModifiedBy.ToList();

            BuildAlbumInfo();
        }

        public SongDetails(int songId, ICollection<SongProperty> properties, IUserMap users)
        {
            SongId = songId;
            bool created = false;

            foreach (SongProperty prop in properties)
            {
                string bn = prop.BaseName;

                if (!prop.IsAction)
                {
                    switch (bn)
                    {
                        case Song.UserField:
                            if (ModifiedBy == null)
                            {
                                ModifiedBy = new List<ModifiedRecord>();
                            }
                            if (!ModifiedBy.Any(u => u.ApplicationUserId == prop.Value ))
                            {
                                ModifiedRecord us = users.CreateMapping(songId, prop.Value);
                                ModifiedBy.Add(us);
                                Debug.WriteLine(string.Format("UserMap:\t{0}\t{1}",songId,prop.Value));
                            }
                            break;
                        case Song.DanceRatingField:
                            UpdateDanceRating(prop.Value);
                            break;
                        case Song.AlbumField:
                        case Song.PublisherField:
                        case Song.TrackField:
                        case Song.PurchaseField:
                            // All of these are taken care of with build album
                            break;
                        case Song.TimeField:
                            {
                                DateTime time = (DateTime)prop.ObjectValue;
                                if (!created)
                                {
                                    Created = time;
                                    created = true;
                                }
                                Modified = time;
                            }
                            break;
                        default:
                            // All of the simple properties we can just set
                            {
                                PropertyInfo pi = this.GetType().GetProperty(bn);
                                if (pi != null)
                                {
                                    pi.SetValue(this, prop.ObjectValue);
                                }
                            }
                            break;
                    }
                }
            }

            Albums = BuildAlbumInfo(properties);
        }
        
        #endregion

        #region Properties
        public int SongId { get; set; }

        [Range(5.0, 500.0)]
        public decimal? Tempo { get; set; }
        [Required]
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }
        [Range(1, 999)]
        public int? Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        public List<AlbumDetails> Albums { get; set; }
        public List<DanceRating> DanceRatings { get; set; }
        public List<SongProperty> Properties { get; set; }
        public List<ModifiedRecord> ModifiedBy { get; set; }
        
        #endregion

        #region Album
        public string AlbumList
        {
            get
            {
                if (Albums != null && Albums.Count > 0)
                {
                    StringBuilder ret = new StringBuilder();
                    string sep = string.Empty;

                    foreach (AlbumDetails album in Albums)
                    {
                        ret.Append(sep);
                        ret.Append(album.Name);
                        sep = "|";
                    }

                    return ret.ToString();
                }
                else
                {
                    return null;
                }
            }
        }
        public AlbumDetails FindAlbum(string album)
        {
            AlbumDetails ret = null;
            List<AlbumDetails> candidates = new List<AlbumDetails>();
            int hash = Song.CreateTitleHash(album);

            foreach (AlbumDetails ad in Albums)
            {
                if (Song.CreateTitleHash(ad.Name) == hash)
                {
                    candidates.Add(ad);
                    if (string.Equals(ad.Name, album, StringComparison.CurrentCultureIgnoreCase))
                    {
                        ret = ad;
                    }
                }
            }

            if (ret == null && candidates.Count > 0)
            {
                ret = candidates[0];
            }

            return ret;
        }

        public List<AlbumDetails> CloneAlbums()
        {
            List<AlbumDetails> albums = new List<AlbumDetails>(Albums.Count);
            foreach (var album in Albums)
            {
                albums.Add(new AlbumDetails(album));
            }

            return albums;
        }

        public int GetNextAlbumIndex()
        {
            return GetNextAlbumIndex(Albums);
        }

        public string GetPurchaseTags()
        {
            return GetPurchaseTags(Albums);
        }

        public ICollection<PurchaseLink> GetPurchaseLinks(string service = "AIX")
        {
            List<PurchaseLink> links = new List<PurchaseLink>();

            for (int i = 1; i < (int)MusicService.Max; i++)
            {
                if (service.Contains(AlbumDetails.ServiceId((MusicService)i)))
                {
                    foreach (AlbumDetails album in Albums)
                    {
                        PurchaseLink l = album.GetPurchaseLink((MusicService)i);
                        if (l != null)
                        {
                            links.Add(l);
                            break;
                        }
                    }
                }
            }

            return links;
        }

        public static string GetPurchaseTags(ICollection<AlbumDetails> albums)
        {
            StringBuilder sb = new StringBuilder();
            HashSet<char> added = new HashSet<char>();

            foreach (AlbumDetails d in albums)
            {
                string tags = d.GetPurchaseTags();
                if (tags != null)
                {
                    foreach (char c in tags)
                    {
                        if (!added.Contains(c))
                        {
                            added.Add(c);
                            sb.Append(c);
                        }
                    }
                }
            }

            if (sb.Length == 0)
                return null;
            else
                return sb.ToString();
        }
        public static int GetNextAlbumIndex(ICollection<AlbumDetails> albums)
        {
            int ret = 0;
            foreach (AlbumDetails ad in albums)
            {
                if (ad.Index >= ret)
                {
                    ret = ad.Index + 1;
                }
            }
            return ret;
        }

        public static List<AlbumDetails> BuildAlbumInfo(IList<Song> songs)
        {
            List<AlbumDetails> results = BuildAlbumInfo(songs[0]);

            for (int i = 1; i < songs.Count; i++)
            {
                List<AlbumDetails> next = BuildAlbumInfo(songs[i]);

                foreach (AlbumDetails ad in next)
                {
                    if (!results.Any(d => d.Name == ad.Name))
                    {
                        results.Add(ad);
                    }
                }
            }

            return results;
        }

        public static List<AlbumDetails> BuildAlbumInfo(Song song)
        {
            IEnumerable<SongProperty> properties =
                from prop in song.SongProperties
                //                where prop.BaseName.Equals(Song.AlbumField)
                select prop;
            return BuildAlbumInfo(properties);
        }
        public static List<AlbumDetails> BuildAlbumInfo(IEnumerable<SongProperty> properties)
        {
            List<string> names = new List<string>(new string[] {
                Song.AlbumField,Song.PublisherField,Song.TrackField,Song.PurchaseField,Song.AlbumPromote
            });

            // First build a hashtable of index->albuminfo, maintaining the total number and the
            // high water mark of indexed albums

            int count = 0;
            int max = 0;

            Dictionary<int, AlbumDetails> map = new Dictionary<int, AlbumDetails>();

            // Also keep a list of 'promotions' - current semantics are that if an album
            //  has a promotion it is removed and re-inserted at the head of the list
            List<int> promotions = new List<int>();

            foreach (SongProperty prop in properties)
            {
                string name = prop.BaseName;
                int idx = prop.Index;
                string qual = prop.Qualifier;

                if (names.Contains(name))
                {
                    AlbumDetails d;
                    if (map.ContainsKey(idx))
                    {
                        d = map[idx];
                    }
                    else
                    {
                        count += 1;
                        if (idx > max)
                        {
                            max = idx;
                        }
                        d = new AlbumDetails { Index = idx };
                        map.Add(idx, d);
                    }

                    bool remove = string.IsNullOrWhiteSpace(prop.Value);

                    switch (name)
                    {
                        case Song.AlbumField:
                            if (remove)
                            {
                                d.Name = null;
                                count -= 1; // This is an album that has been removed
                            }
                            else
                            {
                                d.Name = prop.Value;
                            }
                            break;
                        case Song.PublisherField:
                            if (remove)
                            {
                                d.Publisher = null;
                            }
                            else
                            {
                                d.Publisher = prop.Value;
                            }
                            break;
                        case Song.TrackField:
                            if (remove)
                            {
                                d.Track = null;
                            }
                            else
                            {
                                int t = 0;
                                int.TryParse(prop.Value, out t);
                                d.Track = t;
                            }
                            break;
                        case Song.PurchaseField:
                            if (d.Purchase == null)
                            {
                                d.Purchase = new Dictionary<string, string>();
                            }

                            if (remove)
                            {
                                d.Purchase.Remove(qual);
                            }
                            else
                            {
                                d.Purchase[qual] = prop.Value;
                            }
                            break;
                        case Song.AlbumPromote:
                            promotions.Add(idx);
                            break;
                    }
                }
            }

            List<AlbumDetails> albums = new List<AlbumDetails>(count);

            for (int i = 0; i <= max; i++)
            {
                AlbumDetails d;
                if (map.TryGetValue(i, out d) && d.Name != null)
                {
                    albums.Add(d);
                }
            }

            for (int i = 0; i < promotions.Count; i++)
            {
                AlbumDetails d;
                if (map.TryGetValue(promotions[i], out d) && d.Name != null)
                {
                    albums.Remove(d);
                    albums.Insert(0, d);
                }

            }

            return albums;
        }

        private void BuildAlbumInfo()
        {
            IEnumerable<SongProperty> properties =
                from prop in Properties
                //                where prop.BaseName.Equals(Song.AlbumField)
                select prop;

            Albums = BuildAlbumInfo(properties);
        }
        
        #endregion
        private void UpdateDanceRating(string value)
        {
            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
            }

            DanceRatingDelta drd = new DanceRatingDelta(value);

            DanceRating dr = DanceRatings.Find(r => r.DanceId.Equals(drd.DanceId));
            if (dr == null)
            {
                dr = new DanceRating { SongId = this.SongId, DanceId = drd.DanceId, Weight = 0 };
                DanceRatings.Add(dr);
            }

            dr.Weight += drd.Delta;
        }
    }
}
