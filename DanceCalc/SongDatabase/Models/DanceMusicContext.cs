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
using System.Reflection;

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
        const string CreateCommand = ".Create";
        const string EditCommand = ".Edit";
        const string DeleteCommand = ".Delete";
        const string DeletePropertyCommand = ".DeleteProperty";
        const string MergeFromCommand = ".MergeFrom";
        const string MergeToCommand = ".MergeTo";

        // Consider a parallel table for commands or commands not associates
        //  with a particular song?
        //public static readonly string StartBatchLoadCommand = ".StartBatchLoad";
        //public static readonly string EndBatchLoadCommand = ".EndBatchLoad";

        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<UserProfile> UserProfiles { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        public DbSet<SongLog> Log { get; set; }

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

            // Create log entries for all of the merge froms
            //  this lets us get them in the log before the merge-to which makes
            //  restorings much easier...
            foreach (Song from in songs)
            {
                LogSongCommand(MergeFromCommand, from, user);
            }

            Song song = CreateSong(user, title, artist, album, label, genre, tempo, length, track, purchase, MergeFromCommand, songIds);
            SaveChanges();

            // Add in the to/from properties and create new weight table as well as creating the user associations
            Dictionary<string, int> weights = new Dictionary<string, int>();
            foreach (Song from in songs)
            {
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

                foreach (UserProfile u in from.ModifiedBy)
                {
                    if (!song.ModifiedBy.Contains(u))
                    {
                        song.ModifiedBy.Add(u);
                        CreateSongProperty(song, UserField, u.UserName);
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

            LogSongCommand(MergeToCommand, song, user, false);

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

            SongLog log = CreateEditHeader(song, user, properties);
            log.SongSignature = Song.SignatureFromProperties(properties);

            modified |= UpdateSongProperty(song, TitleField, song.Title, properties,log);
            modified |= UpdateSongProperty(song, ArtistField, song.Artist, properties, log);
            modified |= UpdateSongProperty(song, AlbumField, song.Album, properties, log);
            modified |= UpdateSongProperty(song, PublisherField, song.Publisher, properties, log);
            modified |= UpdateSongProperty(song, TempoField, song.Tempo, properties, log);
            modified |= UpdateSongProperty(song, LengthField, song.Length, properties, log);
            modified |= UpdateSongProperty(song, TrackField, song.Track, properties, log);
            modified |= UpdateSongProperty(song, PurchaseField, song.Purchase, properties, log);
            modified |= UpdateSongProperty(song, GenreField, song.Genre, properties, log);

            if (modified)
            {
                FixupEdited(song);
                
                Log.Add(log);

                SaveChanges();
            }
            else
            {
                // TODO: figure out how to undo the top couple changes if no substantive changes were made... (may just be do nothing here)
            }

            return modified;
        }

        private SongLog CreateEditHeader(Song song, UserProfile user, IOrderedQueryable<SongProperty> properties = null)
        {
            SongLog log = CreateSongLog(user, song, EditCommand);

            // Add the command into the property log
            CreateSongProperty(song, EditCommand, string.Empty);

            // Handle User association
            if (user != null)
            {
                if (user.Songs.Contains(song))
                {
                    user.Songs.Add(song);
                }
                CreateSongProperty(song, UserField, user.UserName);
            }

            // Handle Timestamps
            DateTime time = DateTime.Now;
            song.Modified = time;
            CreateSongProperty(song, TimeField, time.ToString());

            return log;
        }


        private void FixupEdited(Song song)
        {
            if (song == null)
                return;

            // This seems totally non-optimal, but because of the relationship between users
            //  and songs the old song record is getting loaded underneath the new one
            var songs = Songs.Local.Where(s => s.SongId == song.SongId).ToArray();
            bool fixedup = false;

            foreach (Song s in songs)
            {
                if (s != song)
                {
                    ((IObjectContextAdapter)this).ObjectContext.Detach(s);
                    fixedup = true;
                }
            }

            Entry(song).State = System.Data.Entity.EntityState.Modified;
            song.TitleHash = CreateTitleHash(song.Title);

            Debug.WriteLine("Song:{0} Fixedup:{1}", song.SongId, fixedup);
        }

        public void DeleteSong(UserProfile user, Song song, string command = DeleteCommand)
        {
            LogSongCommand(command, song, user);
            Songs.Remove(song);
            SaveChanges();
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

        public bool UpdateSongProperty(Song song, string name, decimal? value, IOrderedQueryable<SongProperty> properties, SongLog log)
        {
            bool modified = true;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = GetCurrentProperty(properties, name);

            string oldString = null;
            if (prop != null) {
                oldString = prop.Value;
                decimal oldValue;
                if (decimal.TryParse(oldString, out oldValue) && oldValue == value)
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
                LogPropertyUpdate(np, log, oldString);
            }

            return modified;
        }

        public bool UpdateSongProperty(Song song, string name, int? value, IOrderedQueryable<SongProperty> properties, SongLog log)
        {
            bool modified = true;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = GetCurrentProperty(properties, name);

            string oldString = null;
            if (prop != null)
            {
                oldString = prop.Value;
                int oldValue;
                if (int.TryParse(oldString, out oldValue) && oldValue == value)
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
                LogPropertyUpdate(np, log, oldString);
            }

            return modified;
        }

        private SongProperty GetCurrentProperty(IOrderedQueryable<SongProperty> properties, string name)
        {
            // Note that this depends on properties list being ordered with id descending...
            return properties.FirstOrDefault(p => p.Name == name);
        }

        public bool UpdateSongProperty(Song song, string name, string value, IOrderedQueryable<SongProperty> properties, SongLog log)
        {
            bool modified = false;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = GetCurrentProperty(properties,name);

            // We are going to create a new property if there wasn't a property before and this property is non-empty OR
            //  if there was a property before and the value is different.
            modified = (prop == null && !string.IsNullOrWhiteSpace(value)) || (prop != null && !string.Equals(prop.Value,value,StringComparison.CurrentCulture));

            if (modified)
            {
                string oldString = null;
                if (prop != null)
                    oldString = prop.Value;

                SongProperty np = SongProperties.Create();
                np.Song = song;
                np.Name = name;
                np.Value = value;

                SongProperties.Add(np);
                LogPropertyUpdate(np, log, oldString);
            }

            return modified;
        }

        private void LogPropertyUpdate(SongProperty sp, SongLog log, string oldValue = null)
        {
            log.UpdateData(sp.Name, sp.Value, oldValue);
        }

        private void LogSongCommand(string command, Song song, UserProfile user, bool includeSignature = true)
        {
            SongLog log = Log.Create();
            log.Time = DateTime.Now;
            log.User = user;
            log.SongReference = song.SongId;
            log.Action = command;

            if (includeSignature)
            {
                log.SongSignature = song.Signature;
            }

            foreach (SongProperty p in song.SongProperties)
            {
                LogPropertyUpdate(p, log);
            }

            Log.Add(log);
        }

        public void RestoreFromLog(IEnumerable<string> lines)
        {
            List<int> mergeFroms = new List<int>();

            foreach (string line in lines)
            {
                RestoreFromLog(line,mergeFroms);
            }
        }

        public void RestoreFromLog(string line, List<int> mergeFroms)
        {
            string[] cells = line.Split(new char[] { '|' });

            // user|time|command|id|data...

            if (cells.Length < 4)
            {
                Debug.WriteLine(string.Format("Bad Line: {0}", line));
                return;
            }

            string userName = cells[0];
            string timeString = cells[1];
            string command = cells[2];
            string songRef = cells[3];
            string songSig = cells[4];

            List<string> data = new List<string>(cells);
            data.RemoveRange(0,5);

            UserProfile user = UserProfiles.FirstOrDefault(u => u.UserName == userName);
            if (user == null)
            {
                Debug.WriteLine(string.Format("Bad User Name: {0}", userName));
                return;
            }

            DateTime time;
            if (!DateTime.TryParse(timeString, out time))
            {
                Debug.WriteLine(string.Format("Bad Timestamp: {0}", timeString));
                return;
            }

            int songId = 0;
            if (!int.TryParse(songRef,out songId))
            {
                Debug.WriteLine(string.Format("Bad SongId: {0}", songRef));
                return;                
            }

            Song song = null;

            switch (command)
            {
                case MergeFromCommand:
                case DeleteCommand:
                case EditCommand:
                    song = FindSong(songId, songSig);
                    break;
                case MergeToCommand:
                case CreateCommand:
                    break;
                default:
                    Debug.WriteLine(string.Format("Bad Command: {0}", command));
                    break;
            }

            bool deleted = false;

            switch (command)
            {
                case MergeFromCommand:
                    mergeFroms.Add(song.SongId);
                    DeleteSong(user, song, command);
                    deleted = true;
                    break;
                case DeleteCommand:
                    DeleteSong(user, song, command);
                    deleted = true;
                    break;
                case EditCommand:
                    EditSongFromLog(user, song, data);
                    break;
                case MergeToCommand:
                case CreateCommand:
                    CreateSongFromLog(user,data,mergeFroms);
                    mergeFroms.Clear();
                    break;
                default:
                    Debug.WriteLine(string.Format("Bad Command: {0}", command));
                    break;
            }

            // This is a little sloppy, but delete cleans up after itself...
            if (!deleted)
            {
                FixupEdited(song);

                SaveChanges();
            }
        }

        private Song FindSong(int id, string signature)
        {
            // First find a match id
            Song song = Songs.FirstOrDefault(s => s.SongId == id);

            // If the id doesn't exist or if the signatures don't match, find by signature
            if (song == null || !MatchSigatures(signature,song.Signature))
            {
                song = FindSongBySignature(signature);
            }

            if (song == null)
            {
                Debug.WriteLine(string.Format("Couldn't find song by Id: {0} or signature {1}", song.SongId, song.Signature));
            }

            return song;
        }

        private Song FindSongBySignature(string signature)
        {
            Song song = Songs.FirstOrDefault(s => string.Equals(signature,s.Signature,StringComparison.Ordinal));

            return song;
        }

        private bool MatchSigatures(string sig1, string sig2)
        {
            return string.Equals(sig1, sig2, StringComparison.Ordinal);
        }

        private void EditSongFromLog(UserProfile user, Song song, List<string> data)
        {
            SongLog log = CreateEditHeader(song, user);

            foreach (string s in data)
            {
                RestorePropertyFromLog(song, log, s);
            }

            Log.Add(log);
        }

        private SongLog CreateSongLog(UserProfile user, Song song, string action)
        {
            SongLog log = Log.Create();

            log.Init(user, song, action);

            return log;
        }

        private void CreateSongFromLog(UserProfile user, List<string> data, List<int> mergeFroms)
        {
            string[] commandLine = data[0].Split(new char[] {'\t'});
            string command = CreateCommand;
            string initC = CreateCommand;
            string initV = string.Empty;

            if (string.Equals(commandLine[0],MergeFromCommand,StringComparison.InvariantCultureIgnoreCase))
            {
                StringBuilder sb = new StringBuilder();
                string separator = string.Empty;
                foreach (int id in mergeFroms)
                {
                    sb.AppendFormat("{0}{1}", separator, id);
                    separator = ";";
                }
                command = MergeToCommand;
                initC = MergeFromCommand;
                initV = sb.ToString();
            }
            else if (!string.Equals(commandLine[0],CreateCommand,StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.WriteLine("Bad Create Command");
            }
            data.RemoveAt(0);

            Song song = Songs.Create();
            song.Created = DateTime.Now;
            song.Modified = DateTime.Now;
            Songs.Add(song);
            
            // Is there a better way to get an id assigned to the song?
            SaveChanges();

            SongLog log = CreateSongLog(user, song, command);
            CreateSongProperty(song, initC, initV);
            log.UpdateData(initC, initV);

            if (user != null)
            {
                //if (user.Songs.FirstOrDefault(s => s.SongId == song.SongId) == null)
                if (!user.Songs.Contains(song))
                {
                    user.Songs.Add(song);
                }
                CreateSongProperty(song, UserField, user.UserName);
            }

            foreach (string s in data)
            {
                RestorePropertyFromLog(song, log, s);
            }

            song.TitleHash = CreateTitleHash(song.Title);

            Log.Add(log);
        }

        private void RestorePropertyFromLog(Song song, SongLog log, string data)
        {
            bool createProperty = true;

            string[] cells = data.Split(new char[] { '\t' });

            bool edit = false;
            if (cells.Length == 3)
            {
                edit = true;
            }
            else if (cells.Length != 2)
            {
                Debug.WriteLine(string.Format("Bad Property: {0}", data));
                return;
            }

            string name = cells[0];
            string value = cells[1];
            string oldValue = null;
            if (edit)
                oldValue = cells[2];

            PropertyInfo pi = song.GetType().GetProperty(name);
            if (pi != null)
            {
                Type type = pi.PropertyType;

                bool empty = string.IsNullOrWhiteSpace(value);
                object o = null;

                if (!empty)
                {
                    if (type == typeof(string))
                    {
                        o = value;
                    }
                    else if (type == typeof(DateTime))
                    {
                        DateTime dt;
                        if (DateTime.TryParse(value, out dt))
                        {
                            pi.SetValue(song, dt);
                        }
                        else
                        {
                            Debug.WriteLine(string.Format("Bad DateTime: {0}", dt));
                        }
                    }
                    else if (type == typeof(decimal?))
                    {
                        decimal d;
                        if (decimal.TryParse(value, out d))
                        {
                            o = d;
                        }
                        else
                        {
                            Debug.WriteLine(string.Format("Bad DateTime: {0}", d));
                        }
                    }
                    else if (type == typeof(int?))
                    {
                        int i;
                        if (int.TryParse(value, out i))
                        {
                            o = i;
                        }
                        else
                        {
                            Debug.WriteLine(string.Format("Bad DateTime: {0}", i));
                        }
                    }
                }

                pi.SetValue(song, o);
            }
            else if (string.Equals(name,DanceRatingField))
            {
                string danceId = value;
                int weight = 1;
                if (value.Contains(':'))
                {
                    string[] pair = value.Split(new char[]{':'});
                    if (pair.Length == 2)
                    {
                        danceId = pair[0];
                        if (!int.TryParse(pair[1],out weight))
                        {
                            Debug.WriteLine(string.Format("Bad Dance Weight☺: {0}", value));
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Bad DanceRating: {0}", value));
                    }
                }

                DanceRating dr = song.DanceRatings.FirstOrDefault(d => string.Equals(d.DanceId, danceId));
                if (dr == null)
                {
                    song.DanceRatings.Add(new DanceRating() { DanceId = danceId, SongId = song.SongId, Weight = weight });
                }
                else
                {
                    dr.Weight = weight;
                }
            }
            else if (string.Equals(name,UserField))
            {
                UserProfile user = UserProfiles.FirstOrDefault(u => u.UserName == value);

                //if (user != null && user.Songs.FirstOrDefault(s => s.SongId == song.SongId) == null)
                if (user != null && !user.Songs.Contains(song))
                {
                    user.Songs.Add(song);
                }
                else
                {
                    createProperty = false;
                }
            }

            if (createProperty)
            {
                CreateSongProperty(song, name, value);
            }

            // Update the log data
            log.UpdateData(name, value, oldValue);
        }

        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return SongDatabase.Models.MergeCluster.GetMergeCandidates(this, n, level);
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
