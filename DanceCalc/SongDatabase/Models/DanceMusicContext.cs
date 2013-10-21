using DanceLibrary;
//using EFTracingProvider;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Data.Objects.DataClasses;

// Let's see if we can mock up a recoverable log file by spitting out
// something resembling a tab-separated flat list of songs items with a
// command associated with each line.  Might add a checkpoint command
// into the songproperties table as well...

// COMMAND  User    Title   Artist  Album   Publisher   Tempo   Length  Track   Genre   Purchase    DanceRating Custom

namespace SongDatabase.Models
{
    public class DanceMusicContext : DbContext
    {
        // You can add custom code to this file. Changes will not be overwritten.
        // 
        // If you want Entity Framework to drop and regenerate your database
        // automatically whenever you change your model schema, add the following
        // code to the Application_Start method in your Global.asax file.
        // Note: this will destroy and re-create your database with every model change.
        // 
        // System.Data.Entity.Database.SetInitializer(new System.Data.Entity.DropCreateDatabaseIfModelChanges<SongDatabase.Models.DanceMusicContext>());

        #region Trace Mechanics

        public static bool TraceEnabled = false;

        public static void SetTrace()
        {
#if DEBUG
            TraceEnabled = true;
            //EFTracingProviderConfiguration.LogToFile = @"d:\temp\log.txt";
#endif
        }

        public DanceMusicContext()
            : this("DefaultConnection")
        {
        }

        public DanceMusicContext(string nameOrConnectionString)
              : base(CreateConnection(nameOrConnectionString), true)
          {
              //if (TraceEnabled)
              //{ 
                  
              //    ((IObjectContextAdapter)this).ObjectContext.EnableTracing();
              //}
            }

        private static DbConnection CreateConnection(string nameOrConnectionString)
        {
            // does not support entity connection strings
            //EFTracingProviderFactory.Register();
                
            ConnectionStringSettings connectionStringSetting =
                ConfigurationManager.ConnectionStrings[nameOrConnectionString];
            string connectionString;
            string providerName;

            if (connectionStringSetting != null)
            {
                connectionString = connectionStringSetting.ConnectionString;
                providerName = connectionStringSetting.ProviderName;
            }
            else
            {
                providerName = "System.Data.SqlClient";
                connectionString = nameOrConnectionString;
            }

            return CreateConnection(connectionString, providerName);
        }

        private static DbConnection CreateConnection(string connectionString, string providerInvariantName)
        {
            DbConnection connection = null;
            //if (TraceEnabled)
            //{
            //    connection = CreateTracingConnection(connectionString, providerInvariantName);
            //}
            //else
            //{
            DbProviderFactory factory = DbProviderFactories.GetFactory(providerInvariantName);
            connection = factory.CreateConnection();
            connection.ConnectionString = connectionString;
            //}
            return connection;
        }


        //private static EFTracingConnection CreateTracingConnection(string connectionString, string providerInvariantName)
        //{

        //    string wrapperConnectionString =
        //        string.Format(@"wrappedProvider={0};{1}", providerInvariantName, connectionString);

        //    EFTracingConnection connection =
        //        new EFTracingConnection
        //        {
        //            ConnectionString = wrapperConnectionString
        //        };

        //    return connection;
        //}



        #endregion

        // Field names - note that these must be kept in sync with the actual property names
        public static readonly string UserField = "User";
        public static readonly string TimeField = "Time";
        public static readonly string TitleField = "Title";
        public static readonly string ArtistField = "Artist";
        public static readonly string AlbumField = "Album";
        public static readonly string PublisherField = "Publisher";
        public static readonly string TempoField = "Tempo";
        public static readonly string LengthField = "Length";
        public static readonly string TrackField = "Track";
        public static readonly string GenreField = "Genre";
        public static readonly string PurchaseField = "Purchase";

        // Complex fields
        public static readonly string Custom = "Custom";
        public static readonly string DanceRatingField = "DanceRating";

        // Commands
        public static readonly string CreateCommand = ".Create";
        public static readonly string EditCommand = ".Edit";
        public static readonly string DeleteCommand = ".Delete";
        public static readonly string DeletePropertyCommand = ".DeleteProperty";
        public static readonly string MergeCommand = ".MergeFrom";

        // Consider a parallel table for commands or commands not associates
        //  with a particular song?
        //public static readonly string StartBatchLoadCommand = ".StartBatchLoad";
        //public static readonly string EndBatchLoadCommand = ".EndBatchLoad";

        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<UserProfile> UserProfiles { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
            modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);
            modelBuilder.Entity<DanceRating>().HasKey(t => new { t.SongId, t.DanceId });

            base.OnModelCreating(modelBuilder);
        }

        public static Dances DanceLibrary
        {
            get
            {
                return _dances;
            }
        }

        public Song MergeSongs(UserProfile user, List<Song> songs, string title, string artist, string album, string label, string genre, decimal? tempo, int? length, int? track, string purchase)
        {
            string songIds = string.Join(";",songs.Select(s => s.SongId.ToString()));

            Song song = CreateSong(user, title, artist, album, label, genre, tempo, length, track, purchase, MergeCommand, songIds);

            // Add in the new song ratings

            SaveChanges();

            // Add in the to/from properties and create new weight table
            Dictionary<string, int> weights = new Dictionary<string, int>();
            foreach (Song from in songs)
            {
                CreateSongProperty(song, MergeCommand, from.SongId.ToString());

                foreach (DanceRating dr in from.DanceRatings)
                {
                    int weight = 0;
                    if (weights.TryGetValue(dr.DanceId, out weight))
                    {
                        weights[dr.DanceId] = weight + dr.Weight;
                    }
                    else
                    {
                        weights[dr.DanceId] = dr.Weight;
                    }
                }
            }

            // Dump the weight table
            foreach (KeyValuePair<string, int> dance in weights)
            {
                song.DanceRatings.Add(new DanceRating() {DanceId = dance.Key, SongId = song.SongId, Weight = dance.Value});

                string value = string.Format("{0}:{1}", dance.Key, dance.Value);
                CreateSongProperty(song, DanceRatingField, value);
            }
            
            // Delete all of the old songs (With merge-with Id from above)
            foreach (Song from in songs)
            {
                this.Songs.Remove(from);
            }

            SaveChanges();

            return song;
        }

        public Song CreateSong(UserProfile user, string title, string artist, string album, string label, string genre, decimal? tempo, int? length, int? track, string purchase)
        {
            return CreateSong(user, title, artist, album, label, genre, tempo, length, track, purchase, CreateCommand, string.Empty);
        }

        public Song CreateSong(UserProfile user, string title, string artist, string album, string label, string genre, decimal? tempo, int? length, int? track, string purchase, string command, string value)
        {
            DateTime time = DateTime.Now;

            Song song = Songs.Create();

            // Add the command into the property log
            CreateSongProperty(song, command, value);

            // Handle User association
            if (user != null)
            {
                user.Songs.Add(song);
                CreateSongProperty(song, UserField, user.UserName);
            }

            // Handle Timestamps
            song.Created = time;
            song.Modified = time;
            CreateSongProperty(song, TimeField, time.ToString());
            
            // Title
            Debug.Assert(!string.IsNullOrWhiteSpace(title));
            song.Title = title;
            CreateSongProperty(song, TitleField, title);

            // Artist
            if (!string.IsNullOrWhiteSpace(artist))
            {
                song.Artist = artist;
                CreateSongProperty(song, ArtistField, artist);
            }

            // Album
            if (!string.IsNullOrWhiteSpace(album))
            {
                song.Album = album;
                CreateSongProperty(song, AlbumField, album);
            }

            // Label
            if (!string.IsNullOrWhiteSpace(label))
            {
                song.Publisher = label;
                CreateSongProperty(song, PublisherField, label);
            }

            // Genre
            if (!string.IsNullOrWhiteSpace(label))
            {
                song.Publisher = label;
                CreateSongProperty(song, PublisherField, label);
            }

            // Tempo
            if (tempo != null)
            {
                song.Tempo = tempo;
                CreateSongProperty(song, TempoField, tempo.ToString());
            }

            // Length
            if (length != null && length != 0)
            {
                song.Length = length;
                CreateSongProperty(song, LengthField, length.ToString());
            }

            // Track
            if (track != null && track != 0)
            {
                song.Track = track;
                CreateSongProperty(song, TrackField, track.ToString());
            }

            // Purchase Info
            if (!string.IsNullOrWhiteSpace(purchase))
            {
                song.Purchase = purchase;
                CreateSongProperty(song, PurchaseField, purchase);
            }

            song.TitleHash = CreateTitleHash(title);

            song = Songs.Add(song);

            return song;
        }

        public bool EditSong(UserProfile user, Song song)
        {
            var properties = from p in SongProperties 
                             where p.SongId == song.SongId
                             orderby p.Id descending
                             select p;

            bool modified = false;

            // Add the command into the property log
            CreateSongProperty(song, EditCommand, string.Empty);

            // Handle User association
            if (user != null)
            {
                //Dump();
                if (user.Songs.FirstOrDefault(s => s.SongId == song.SongId) == null)
                {
                    user.Songs.Add(song);
                }
                //Dump();
                CreateSongProperty(song, UserField, user.UserName);
                //Dump();
            }

            // Handle Timestamps
            DateTime time = DateTime.Now;
            song.Modified = time;
            CreateSongProperty(song, TimeField, time.ToString());

            modified |= UpdateSongProperty(song, TitleField, song.Title, properties);
            modified |= UpdateSongProperty(song, ArtistField, song.Artist, properties);
            modified |= UpdateSongProperty(song, AlbumField, song.Album, properties);
            modified |= UpdateSongProperty(song, PublisherField, song.Publisher, properties);
            modified |= UpdateSongProperty(song, TempoField, song.Tempo, properties);
            modified |= UpdateSongProperty(song, LengthField, song.Length, properties);
            modified |= UpdateSongProperty(song, TrackField, song.Track, properties);
            modified |= UpdateSongProperty(song, PurchaseField, song.Purchase, properties);
            modified |= UpdateSongProperty(song, GenreField, song.Genre, properties);

            if (modified)
            {
                song.TitleHash = CreateTitleHash(song.Title);

                // This seems totally non-optimal, but because of the relationship between users
                //  and songs the old song record is getting loaded underneath the new one
                var songs = Songs.Local.Where(s => s.SongId == song.SongId).ToArray();
                foreach (Song s in songs)
                {
                    if (s != song)
                    {
                        ((IObjectContextAdapter)this).ObjectContext.Detach(s);
                    }
                }

                Entry(song).State = System.Data.Entity.EntityState.Modified;

                SaveChanges();
            }
            else
            {
                // TODO: figure out how to undo the top couple changes if no substantive changes were made... (may just be do nothing here)
            }

            return modified;
        }

        public void AddDanceRatings(Song song, IEnumerable<string> danceIds)
        {
            foreach (string danceId in danceIds)
            {
                Dance dance = Dances.Local.First(d => d.Id == danceId);
                Debug.Assert(dance != null);

                DanceRating dr = DanceRatings.Create();
                dr.Song = song;
                dr.Dance = dance;
                dr.Weight = 1;

                DanceRatings.Add(dr);

                string value = string.Format("{0}:{1}", dance.Id, 1);
                CreateSongProperty(song, DanceRatingField, dance.Id);
            }
        }

        //static private void DuplicationTestGraph(IEntityWithChangeTracker rootEntity)
        //{
        //    const string primaryKey = "Id";
        //    var graphObjects = new Dictionary<string, IList<Object>>();

        //    using (ChangeTrackerIterator iterator = ChangeTrackerIterator.Create(rootEntity))
        //    {
        //        iterator.ToList().ForEach(u =>
        //        {
        //            if (!graphObjects.Keys.Contains(u.GetType().Name))
        //                graphObjects.Add(u.GetType().Name, new List<object>());
        //            IList<object> tmpList;
        //            graphObjects.TryGetValue(u.GetType().Name, out tmpList);
        //            tmpList.Add(u);
        //        });

        //        foreach (IList<object> objectsList in graphObjects.Values)
        //        {
        //            foreach (object obj in objectsList)
        //            {
        //                foreach (object obj1 in objectsList)
        //                {
        //                    if (!obj1.Equals(obj))
        //                    {
        //                        int primaryKey1 = (int)obj.GetType().GetProperty(primaryKey).GetValue(obj, null);
        //                        int primaryKey2 = (int)obj1.GetType().GetProperty(primaryKey).GetValue(obj1, null);

        //                        if (primaryKey1 == primaryKey2)
        //                            throw new Exception("There are at least two instances of the class " + obj.GetType().Name +
        //                                                " which have the same primary key = " + primaryKey1 + ". Make sure that the key values are unique before calling AcceptChanges.");
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        public static int CreateTitleHash(string title)
        {
            StringBuilder sb = new StringBuilder(title.Length);

            string norm = title.Normalize(NormalizationForm.FormD);

            bool paren = false;
            foreach (char c in norm)
            {
                if (paren)
                {
                    if (c == ')')
                    {
                        paren = false;
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        char cNew = char.ToUpper(c);
                        sb.Append(cNew);
                    }
                    else if (c == '(')
                    {
                        paren = true;
                    }
                }
            }

            string s = sb.ToString();
            int hash = s.GetHashCode();
            //Debug.WriteLine("{0}\t{1}",hash,s);

            return hash;
        }

        public SongProperty CreateSongProperty(Song song, string name, string value)
        {
            SongProperty ret = SongProperties.Create();
            ret.Song = song;
            ret.Name = name;
            ret.Value = value;

            SongProperties.Add(ret);

            return ret;
        }

        public SongProperty CreateSongProperty(string name, string value)
        {
            SongProperty ret = SongProperties.Create();
            ret.Song = null;
            ret.SongId = -1;
            ret.Name = name;
            ret.Value = value;

            SongProperties.Add(ret);

            return ret;
        }

        public bool UpdateSongProperty(Song song, string name, decimal? value, IOrderedQueryable<SongProperty> properties)
        {
            bool modified = true;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = properties.FirstOrDefault(p => p.Name == name);

            if (prop != null) {
                decimal oldValue;
                if (decimal.TryParse(prop.Value, out oldValue) && oldValue == value)
                {
                    modified = false;
                }
            }
            else if (value == null)
            {
                modified = false;
            }

            if (modified)
            {
                SongProperty np = SongProperties.Create();
                np.Song = song;
                np.Name = name;
                np.Value = value.ToString();

                SongProperties.Add(np);
            }

            return modified;
        }

        public bool UpdateSongProperty(Song song, string name, int? value, IOrderedQueryable<SongProperty> properties)
        {
            bool modified = true;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = properties.FirstOrDefault(p => p.Name == name);

            if (prop != null)
            {
                int oldValue;
                if (int.TryParse(prop.Value, out oldValue) && oldValue == value)
                {
                    modified = false;
                }
            }
            else if (value == null)
            {
                modified = false;
            }

            if (modified)
            {
                SongProperty np = SongProperties.Create();
                np.Song = song;
                np.Name = name;
                np.Value = value.ToString();

                SongProperties.Add(np);
            }

            return modified;
        }

        public bool UpdateSongProperty(Song song, string name, string value, IOrderedQueryable<SongProperty> properties)
        {
            bool modified = false;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = properties.FirstOrDefault(p => p.Name == name);

            // We are going to create a new property if there wasn't a property before and this property is non-empty OR
            //  if there was a property before and the value is different.
            modified = (prop == null && !string.IsNullOrWhiteSpace(value)) || (prop != null && !string.Equals(prop.Value,value,StringComparison.CurrentCulture));

            if (modified)
            {
                SongProperty np = SongProperties.Create();
                np.Song = song;
                np.Name = name;
                np.Value = value;

                SongProperties.Add(np);
            }

            return modified;
        }

        public IList<Song> FindMergeCandidates(int n)
        {
            return SongDatabase.Models.MergeCluster.GetMergeCandidates(this, n);
        }

        public void Dump()
        {
            // TODO: Create a dump routine to help dump the object graph - definitely need object id of some kind (address)

            Debug.WriteLine("------------------- songs ------------------");
            foreach (Song song in Songs.Local)
            {
                song.Dump();
            }

            Debug.WriteLine("------------------- properties ------------------");
            foreach (SongProperty prop in SongProperties.Local)
            {
                prop.Dump();
            }

            Debug.WriteLine("------------------- users ------------------");
            foreach (UserProfile user in UserProfiles.Local)
            {
                user.Dump();
            }
        }

        private static Dances _dances = new Dances();
    }
}
