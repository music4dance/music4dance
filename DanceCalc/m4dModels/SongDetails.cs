using DanceLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

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

        // TODO: I want to be able to create SongDetails as a completely disconnected object
        //  but mapping all of the information from songs.  I believe I screwed up
        //  setting putting IUserMap here - Instead, I should not pass that in here but
        //  into the function that transforms SongDetails (back) into a Song
        //  For now, I'm going to accept a null in that field in which case I'll create
        //  the disconnected object but should revisit and cleanup soon
        public SongDetails(int songId, ICollection<SongProperty> properties, IUserMap users = null)
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
                                ModifiedRecord us = null;
                                // TODO:  See note above
                                if (users != null)
                                {
                                    us = users.CreateMapping(songId, prop.Value);
                                }
                                else
                                {
                                    us = new ModifiedRecord { SongId = songId, ApplicationUserId = prop.Value };
                                }
                                ModifiedBy.Add(us);
                                //Debug.WriteLine(string.Format("UserMap:\t{0}\t{1}",songId,prop.Value));
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
        
        public static SongDetails CreateFromRow(IList<string> fields, string row)
        {
            string[] cells = row.Split(new char[] { '\t' });

            List<SongProperty> properties = new List<SongProperty>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (fields[i] != null)
                {
                    string cell = cells[i];
                    if (string.Equals(fields[i],Song.DanceRatingField))
                    {
                        // Special case dance rating
                        throw new NotImplementedException("Dance Ratings need to be moved from admin to here.");
                    }
                    else if (string.Equals(fields[i],Song.LengthField))
                    {
                        // Special case length
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            try
                            {
                                SongDuration d = new SongDuration(cell);
                                decimal l = d.Length;
                                cell = l.ToString("F0");
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                cell = null;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        
                        int idx = Song.IsAlbumField(fields[i]) ? 0 : -1;
                        SongProperty prop = new SongProperty(0, fields[i], cell, idx);
                        properties.Add(prop);
                    }
                }
            }

            SongDetails song = new SongDetails(0, properties);
            return song;
        }

        public static List<string> BuildHeaderMap(string line)
        {
            List<string> map = new List<string>();
            string[] headers = line.ToUpper().Split(new char[] { '\t' });

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i];
                string field = null;
                // If this fails, we want to add a null to our list because
                // that indicates a column we don't care about
                s_propertyMap.TryGetValue(header, out field);
                map.Add(field);
            }

            return map;
        }

        private static Dictionary<string, string> s_propertyMap = new Dictionary<string, string>()
        {
            {"DANCE", Song.DanceRatingField},
            {"TITLE", Song.TitleField},
            {"ARTIST", Song.ArtistField},
            {"CONTRIBUTING ARTISTS", Song.ArtistField},
            {"LABEL", Song.PublisherField},
            {"BPM", Song.TempoField},
            {"BEATS-PER-MINUTE", Song.TempoField},
            {"LENGTH", Song.LengthField},
            {"ALBUM", Song.AlbumField},
            {"#", Song.TrackField},
            {"PUBLISHER", Song.PublisherField}
        };

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

        public int TitleHash 
        { 
            get 
            {
                return Song.CreateTitleHash(Title); 
            } 
        }
        
        #endregion

        #region Album
        public string AlbumList
        {
            get
            {
                if (HasAlbums)
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

        public bool HasAlbums
        {
            get 
            {
                return Albums != null && Albums.Count > 0;
            }
        }
        // "Real" albums in this case being non-ballroom compilation-type albums
        public bool HasRealAblums
        {
            get
            {
                bool ret = false;
                if (HasAlbums)
                {
                    ret = Albums.Any(a => a.IsRealAlbum);
                }
                return ret;
            }
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

            foreach (MusicService ms in MusicService.GetServices())
            {
                if (service.Contains(ms.CID))
                {
                    foreach (AlbumDetails album in Albums)
                    {
                        PurchaseLink l = album.GetPurchaseLink(ms.ID);
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

        public bool TitleArtistMatch(string title, string artist)
        {
            return
                string.Equals(Song.CreateNormalForm(title), Song.CreateNormalForm(Title)) &&
                string.Equals(Song.CreateNormalForm(artist), Song.CreateNormalForm(Artist));
        }

        public string CleanTitle
        {
            get
            {
                return Song.CleanString(Title);
            }
        }
        public string CleanArtist
        {
            get
            {
                return Song.CleanString(Artist);
            }
        }
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

        public bool TempoConflict(SongDetails s, decimal delta)
        {
            return Tempo.HasValue && s.Tempo.HasValue && Math.Abs(Tempo.Value - s.Tempo.Value) > delta;
        }

        /// <summary>
        /// Finds a representitive of the largest cluster of tracks 
        ///  (clustered by approximate duration) that is an very
        ///  close title/artist match
        /// </summary>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public ServiceTrack FindDominantTrack(IList<ServiceTrack> tracks)
        {
            ServiceTrack ret = null;

            Dictionary<int, List<ServiceTrack>> cluster = ClusterTracks(tracks);

            List<ServiceTrack> winner = null;

            // If we only have one cluster, we're set
            if (cluster.Count == 1)
            {
                winner = cluster.Values.First();
            }
            else if (cluster.Count != 0) // Try clustering off phase if we had any clustering at all
            {
                Dictionary<int, List<ServiceTrack>> clusterT = ClusterTracks(tracks,10,5);
                if (clusterT.Count == 1)
                {
                    winner = clusterT.Values.First();
                }
                else
                {
                    // Neither clustering results in a clear winner, so try for the one with the
                    // smallest number
                    if (clusterT.Count < cluster.Count)
                    {
                        cluster = clusterT;
                    }

                    winner = cluster.Values.Aggregate( ( seed, f ) => f.Count > seed.Count ? f : seed );
                }
            }

            if (winner != null)
            {
                ret = winner.First();
            }

            return ret;
        }

        private Dictionary<int, List<ServiceTrack>> ClusterTracks(IList<ServiceTrack> tracks, int size = 10, int offset = 0)
        {
            Dictionary<int, List<ServiceTrack>> ret = new Dictionary<int, List<ServiceTrack>>();

            foreach (ServiceTrack track in tracks)
            {
                if (track.Duration.HasValue && TitleArtistMatch(track.Name, track.Artist))
                {
                    int cluster = (track.Duration.Value + offset) / size;
                    List<ServiceTrack> list = null;
                    if (!ret.TryGetValue(cluster,out list))
                    {
                        list = new List<ServiceTrack>();
                        ret.Add(cluster, list);
                    }
                    list.Add(track);
                }
            }

            return ret;
        }
    }
}
