using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using m4d.Models;

namespace m4d.ViewModels
{
    // This is a transitory object (really a ViewModel object) that is used for 
    // viewing and editing a song, it shouldn't ever end up in a database,
    // it's meant to aggregate the information about a song in an easily digestible way
    public class SongDetails
    {
        public SongDetails()
        {
            
        }

        public SongDetails(Song song)
        {
            Song = song;

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

        public SongDetails(DanceMusicContext dmc, int songId, ICollection<SongProperty> properties)
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
                        case DanceMusicContext.UserField:
                            AddUser(dmc, prop.Value);
                            break;
                        case DanceMusicContext.DanceRatingField:
                            UpdateDanceRating(dmc, prop.Value);
                            // TODO: Need to rebuild the dance table here
                            break;
                        case DanceMusicContext.AlbumField:
                        case DanceMusicContext.PublisherField:
                        case DanceMusicContext.TrackField:
                        case DanceMusicContext.PurchaseField:
                            // All of these are taken care of with build album
                            break;
                        case DanceMusicContext.TimeField:
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

        public int SongId { get; set; }

        [Range(5.0,500.0)]
        public decimal? Tempo { get; set; }
        [Required]
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }
        [Range(1,999)]
        public int? Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        public List<AlbumDetails> Albums { get; set; }
        public List<DanceRating> DanceRatings { get; set; }
        public List<SongProperty> Properties { get; set; }
        public List<ModifiedRecord> ModifiedBy { get; set; }

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
            int hash = DanceMusicContext.CreateTitleHash(album);

            foreach (AlbumDetails ad in Albums)
            {
                if (DanceMusicContext.CreateTitleHash(ad.Name) == hash)
                {
                    candidates.Add(ad);
                    if (string.Equals(ad.Name,album,StringComparison.CurrentCultureIgnoreCase))
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

        public Song Song { get; private set; }

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
                //                where prop.BaseName.Equals(DanceMusicContext.AlbumField)
                select prop;
            return BuildAlbumInfo(properties);
        }
        public static List<AlbumDetails> BuildAlbumInfo(IEnumerable<SongProperty> properties)        
        {
            List<string> names = new List<string>(new string[] {"Album","Publisher","Track","Purchase"});

            // First build a hashtable of index->albuminfo, maintaining the total number and the
            // high water mark of indexed albums

            int count = 0;
            int max = 0;

            Dictionary<int,AlbumDetails> map = new Dictionary<int,AlbumDetails>();

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
                        case "Album":
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
                        case "Publisher":
                            if (remove)
                            {
                                d.Publisher = null;
                            }
                            else
                            {
                                d.Publisher = prop.Value;
                            }
                            break;
                        case "Track":
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
                        case "Purchase":
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
                    }
                }
            }

            List<AlbumDetails> albums = new List<AlbumDetails>(count);

            for (int i = 0; i <= max; i++ )
            {
                AlbumDetails d;
                if (map.TryGetValue(i, out d) && d.Name != null)
                {
                    albums.Add(d);
                }
            }

            return albums;
        }

        public int GetNextAlbumIndex()
        {
            int ret = 0;
            foreach (AlbumDetails ad in Albums)
            {
                if (ad.Index >= ret)
                {
                    ret = ad.Index + 1;
                }
            }
            return ret;
        }
        private void BuildAlbumInfo()
        {
            IEnumerable<SongProperty> properties =
                from prop in Properties
//                where prop.BaseName.Equals(DanceMusicContext.AlbumField)
                select prop;

            Albums = BuildAlbumInfo(properties);
        }

        private void AddUser(DanceMusicContext dmc, string userName)
        {
            ApplicationUser user = dmc.FindUser(userName);
            if (ModifiedBy == null)
            {
                ModifiedBy = new List<ModifiedRecord>();
            }


            if (!ModifiedBy.Any(u => u.ApplicationUserId == u.ApplicationUserId))
            {
                ModifiedRecord us = dmc.Modified.Create();
                us.ApplicationUser = user;
                ModifiedBy.Add(us);
            }
        }
        private void UpdateDanceRating(DanceMusicContext dmc, string value)
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
