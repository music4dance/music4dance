using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongDatabase.Models
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

        public int SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }
        public int? Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        public List<AlbumDetails> Albums { get; set; }
        public List<DanceRating> DanceRatings { get; set; }
        public List<SongProperty> Properties { get; set; }
        public List<UserProfile> ModifiedBy { get; set; }

        public Song Song { get; private set; }

        /// <summary>
        /// This should create a new song based on the songdetails info
        /// If it was created with a song, do we do a merge/edit?
        /// </summary>
        /// <returns></returns>
        public Song CreateSong()
        {
            return null;
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
                        d = new AlbumDetails();
                        map.Add(idx, d);
                    }

                    bool remove = string.IsNullOrWhiteSpace(prop.Value);

                    // Is there a difference between add and replace?  Maybe for a non-indexed/ordered property
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
        private void BuildAlbumInfo()
        {
            IEnumerable<SongProperty> properties =
                from prop in Properties
//                where prop.BaseName.Equals(DanceMusicContext.AlbumField)
                select prop;

            Albums = BuildAlbumInfo(properties);
        }

    }

    public class AlbumDetails
    {
        public string Name { get; set; }
        public string Publisher { get; set; }
        public int? Track { get; set; }
        // Semi-colon separated purchase info of the form XX=YYYYYY (XX is service/type and YYYYY is id)
        // IA = Itunes Album
        // IS = Itunes Song
        // AA = Amazon Album
        // AS = Amazon Song
        // XA = Xbox Album
        // XS = Xbox Song
        // MS = AMG Song
        public Dictionary<string,string> Purchase { get; set; }

        /// <summary>
        /// Formatted purchase info
        /// </summary>
        /// <returns></returns>
        public IList<string> GetPurcahseInfo()
        {
            if (Purchase == null || Purchase.Count == 0)
            {
                return null;
            }

            List<string> info = new List<string>(Purchase.Count);
            foreach (KeyValuePair<string,string> p in Purchase)
            {
                info.Add(ExpandPurcahseType(p.Key) + "=" + p.Value);
            }

            return info;
        }

        public bool HasPurchaseInfo
        {
            get
            {
                return Purchase != null && Purchase.Count > 0;
            }
        }

        static public string ExpandPurcahseType(string abbrv)
        {
            if (abbrv == null)
            {
                throw new ArgumentNullException("abbrv"); 
            }
            else if (abbrv.Length != 2)
            {
                throw new ArgumentOutOfRangeException("abbrv");
            }

            string service = null;
            string type = null;
            switch (abbrv[0])
            {
                case 'I':
                    service = "ITunes";
                    break;
                case 'A':
                    service = "Amazon";
                    break;
                case 'X':
                    service = "XBox";
                    break;
                case 'M':
                    service = "American Music Group";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("abbrv");
            }

            switch (abbrv[1])
            {
                case 'S':
                    type = "Song";
                    break;
                case 'A':
                    type = "Album";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("abbrv");
            }

            return service + " " + type;
        }
    }
}
