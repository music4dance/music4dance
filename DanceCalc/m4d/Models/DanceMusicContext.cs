using DanceLibrary;
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
using Microsoft.AspNet.Identity.EntityFramework;
using System.Reflection;

using m4d.ViewModels;

// Let's see if we can mock up a recoverable log file by spitting out
// something resembling a tab-separated flat list of songs items with a
// command associated with each line.  Might add a checkpoint command
// into the songproperties table as well...

// COMMAND  User    Title   Artist  Album   Publisher   Tempo   Length  Track   Genre   Purchase    DanceRating Custom

// Kill Publisher Track Purchase -> do these move to custom


namespace m4d.Models
{
    public enum UndoAction { Undo, Redo };

    public class DanceMusicContext : IdentityDbContext<ApplicationUser>
    {
        public DanceMusicContext()
            : base("DefaultConnection")
        {
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
            DbProviderFactory factory = DbProviderFactories.GetFactory(providerInvariantName);
            connection = factory.CreateConnection();
            connection.ConnectionString = connectionString;
            return connection;
        }

        // Field names - note that these must be kept in sync with the actual property names
        public const string UserField = "User";
        public const string TimeField = "Time";
        public const string TitleField = "Title";
        public const string ArtistField = "Artist";
        public const string TempoField = "Tempo";
        public const string LengthField = "Length";
        public const string GenreField = "Genre";

        public const string AlbumField = "Album";
        public const string PublisherField = "Publisher";
        public const string TrackField = "Track";
        public const string PurchaseField = "Purchase";

        // Complex fields
        public const string Custom = "Custom";
        public const string DanceRatingField = "DanceRating";
        public const string AlbumList = "AlbumList";

        // Commands
        public const string CreateCommand = ".Create";
        public const string EditCommand = ".Edit";
        public const string DeleteCommand = ".Delete";
        public const string MergeCommand = ".Merge";
        public const string UndoCommand = ".Undo";
        public const string RedoCommand = ".Redo";

        public const string SuccessResult = ".Success";
        public const string FailResult = ".Fail";
        public const string MessageData = ".Message";


        // Consider a parallel table for commands or commands not associates
        //  with a particular song?
        //public static readonly string StartBatchLoadCommand = ".StartBatchLoad";
        //public static readonly string EndBatchLoadCommand = ".EndBatchLoad";

        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        public DbSet<SongLog> Log { get; set; }

        protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
            modelBuilder.Entity<Song>().Ignore(song => song.CreateEntry);
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

        public SongDetails FindSongDetails(int id)
        {
            SongDetails sd = null;            
            
            Song song = Songs.Find(id);

            if (song != null)
                sd = new SongDetails(song);

            return sd;
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, string genre, decimal? tempo, int? length, List<AlbumDetails> albums)
        {
            string songIds = string.Join(";",songs.Select(s => s.SongId.ToString()));

            Song song = CreateSong(user, title, artist, genre, tempo, length, albums, MergeCommand, songIds, true);
            SaveChanges();
            song.CreateEntry.SongReference = song.SongId;
            song.CreateEntry.SongSignature = song.Signature;

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

                foreach (ApplicationUser u in from.ModifiedBy)
                {
                    if (!song.ModifiedBy.Contains(u))
                    {
                        song.ModifiedBy.Add(u);
                        CreateSongProperty(song, UserField, u.UserName, song.CreateEntry);
                    }
                }
            }

            // Dump the weight table
            foreach (KeyValuePair<string, int> dance in weights)
            {
                song.DanceRatings.Add(new DanceRating() {DanceId = dance.Key, SongId = song.SongId, Weight = dance.Value});

                string value = new DanceRatingDelta { DanceId = dance.Key, Delta = dance.Value }.ToString();
                    
                CreateSongProperty(song, DanceRatingField, value, song.CreateEntry);
            }

            // Delete all of the old songs (With merge-with Id from above)
            foreach (Song from in songs)
            {
                RemoveSong(from);
            }

            SaveChanges();

            return song;
        }

        public Song CreateSong(ApplicationUser user, SongDetails sd, List<string> dances)
        {
            Song song = CreateSong(user, sd.Title, sd.Artist, sd.Genre, sd.Tempo, sd.Length, sd.Albums, true);

            AddDanceRatings(song, dances, DanceRatingCreate);

            SaveChanges();

            return song;
        }

        public Song CreateSong(ApplicationUser user, string title, string artist, string genre, decimal? tempo, int? length, List<AlbumDetails> albums, bool log = false)
        {
            return CreateSong(user, title, artist, genre, tempo, length, albums, CreateCommand, string.Empty, log);
        }

        public Song CreateSong(ApplicationUser user, string title, string artist, string genre, decimal? tempo, int? length, List<AlbumDetails> albums, string command, string value, bool createLog = false)
        {
            DateTime time = DateTime.Now;

            Song song = Songs.Create();

            SongLog log = null;
            if (createLog)
            {
                log = CreateSongLog(user, song, command);
                song.CreateEntry = log;
            }

            // Add the command into the property log
            CreateSongProperty(song, command, value, log);

            // Handle User association
            if (user != null)
            {
                user.Songs.Add(song);
                CreateSongProperty(song, UserField, user.UserName, log);
            }

            // Handle Timestamps
            song.Created = time;
            song.Modified = time;
            CreateSongProperty(song, TimeField, time.ToString(), log);
            
            // Title
            Debug.Assert(!string.IsNullOrWhiteSpace(title));
            song.Title = title;
            CreateSongProperty(song, TitleField, title, log);

            // Artist
            if (!string.IsNullOrWhiteSpace(artist))
            {
                song.Artist = artist;
                CreateSongProperty(song, ArtistField, artist, log);
            }

            // Genre
            if (!string.IsNullOrWhiteSpace(genre))
            {
                song.Genre = genre;
                CreateSongProperty(song, PublisherField, genre, log);
            }

            // Tempo
            if (tempo != null)
            {
                song.Tempo = tempo;
                CreateSongProperty(song, TempoField, tempo.ToString(), log);
            }

            // Length
            if (length != null && length != 0)
            {
                song.Length = length;
                CreateSongProperty(song, LengthField, length.ToString(), log);
            }

            // Album
            CreateAlbums(song,albums,log);

            song.TitleHash = CreateTitleHash(title);

            song = Songs.Add(song);
            if (createLog)
            {
                Log.Add(log);
            }

            return song;
        }



        public SongDetails EditSong(ApplicationUser user, SongDetails edit, List<string> addDances, List<string> remDances)
        {
            bool modified = false;

            Song song = Songs.Find(edit.SongId);

            //var properties = from p in SongProperties
            //                 where p.SongId == song.SongId
            //                 orderby p.Id ascending
            //                 select p;

            SongLog log = CreateEditHeader(song, user);
            log.SongSignature = song.Signature;

            // TODO: AlbumInfo and DanceRating still need to be handled
            modified |= UpdateSongProperty(edit, song, TitleField, log);
            modified |= UpdateSongProperty(edit, song, ArtistField, log);
            modified |= UpdateSongProperty(edit, song, TempoField, log);
            modified |= UpdateSongProperty(edit, song, LengthField, log);
            modified |= UpdateSongProperty(edit, song, GenreField, log);

            List<AlbumDetails> oldAlbums = SongDetails.BuildAlbumInfo(song);

            bool foundFirst = false;

            // TODO: Think about how we might want to 
            //  put the xbox selection at the top of the album list...
            for (int aidx = 0, cidx = oldAlbums.Count; aidx < edit.Albums.Count; aidx++ )
            {
                if (!foundFirst && !string.IsNullOrEmpty(edit.Albums[aidx].Name))
                {
                    foundFirst = true;
                    song.Album = edit.Albums[aidx].Name;
                }

                if (aidx < cidx)
                {
                    // We're in existing album territory
                    modified |= edit.Albums[aidx].ModifyInfo(this,song,aidx,oldAlbums[aidx], log);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (!string.IsNullOrWhiteSpace(edit.Albums[aidx].Name))
                    {
                        edit.Albums[aidx].CreateProperties(this,song,aidx,log);
                        modified = true;
                    }
                }
            }

            if (!foundFirst)
            {
                song.Album = null;
            }

            modified |= EditDanceRatings(edit, song, addDances, remDances, log);

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

            if (modified)
            { 
                return FindSongDetails(edit.SongId); 
            }
            else
            {
                return null;
            }
            
        }


        private SongLog CreateEditHeader(Song song, ApplicationUser user)
        {
            SongLog log = CreateSongLog(user, song, EditCommand);

            CreateEditProperties(song, user, EditCommand);

            return log;
        }

        private void CreateEditProperties(Song song, ApplicationUser user, string command)
        {
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
        }

        private void FixupEdited(Song song)
        {
            if (song == null)
                return;

            // This seems totally non-optimal, but because of the relationship between users
            //  and songs the old song record is getting loaded underneath the new one
            // Note: this may be fixed with the use of SongDetails viewmodel, but not sure
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

        public void DeleteSong(ApplicationUser user, Song song, string command = DeleteCommand)
        {
            LogSongCommand(command, song, user);
            RemoveSong(song);
            SaveChanges();
        }

        private void RemoveSong(Song song)
        {
            song.Delete();
        }

        public void AddDanceRatings(Song song, IEnumerable<string> danceIds, int weight = 0)
        {
            if (Dances.Local.Count == 0)
            {
                Dances.Load();
            }

            if (weight == 0 )
                weight = DanceRatingAutoCreate;

            foreach (string danceId in danceIds)
            {
                Dance dance = Dances.Local.First(d => d.Id == danceId);
                Debug.Assert(dance != null);

                DanceRating dr = DanceRatings.Create();
                dr.Song = song;
                dr.Dance = dance;
                dr.Weight = weight;

                DanceRatings.Add(dr);

                CreateSongProperty(song, DanceRatingField, string.Format("{0}+{1}",dance.Id,weight),song.CreateEntry);
            }
        }

        public bool EditDanceRatings(SongDetails sd, Song song, List<string> add, List<string> remove, SongLog log)
        {
            bool changed = false;

            // TODO: Get some different weightings into the system so we can make add heavier than delete

            List<DanceRating> del = new List<DanceRating>();

            // Cleaner way to get old dance ratings?
            foreach (DanceRating dr in song.DanceRatings)
            {
                bool added = false;
                int delta = 0;

                // This handles the incremental weights
                if (add != null && add.Contains(dr.DanceId))
                {
                    delta = DanceRatingIncrement;
                    add.Remove(dr.DanceId);
                    added = true;
                }

                // This handles the decremented weights
                if (remove != null && !remove.Contains(dr.DanceId))
                {
                    if (!added)
                    {
                        delta += DanceRatingDecrement;
                    }

                    if (dr.Weight + delta <= 0)
                    {
                        del.Add(dr);
                    }
                }

                if (delta != 0)
                {
                    dr.Weight += delta;

                    SongProperty np = SongProperties.Create();
                    np.Song = song;
                    np.Name = DanceRatingField;
                    np.Value = new DanceRatingDelta {DanceId = dr.DanceId, Delta = delta}.ToString();

                    SongProperties.Add(np);
                    LogPropertyUpdate(np, log);

                    changed = true;
                }
            }

            // This handles the deleted weights
            foreach (DanceRating dr in del)
            {
                song.DanceRatings.Remove(dr);
            }
           
            // This handles the new ratings
            if (add != null)
            {
                foreach (string ndr in add)
                {
                    Dance dance = Dances.First(d => d.Id == ndr);
                    Debug.Assert(dance != null);

                    DanceRating dr = DanceRatings.Create();
                    dr.Song = song;
                    dr.Dance = dance;
                    dr.Weight = DanceRatingInitial;

                    song.DanceRatings.Add(dr);

                    SongProperty np = SongProperties.Create();
                    np.Song = song;
                    np.Name = DanceRatingField;
                    np.Value = new DanceRatingDelta { DanceId = ndr, Delta = DanceRatingInitial }.ToString();

                    SongProperties.Add(np);
                    LogPropertyUpdate(np, log);

                    changed = true;
                }
            }

            return changed;
        }

        // TODO: Change Inc to 10 and dec to -2 when next rebuild happens
        public readonly int DanceRatingCreate = 10;  // TODO: when we allow a user to manually add a song, give lots of credit
        public readonly int DanceRatingInitial = 6;
        public readonly int DanceRatingIncrement = 3;
        public readonly int DanceRatingAutoCreate = 5;
        public readonly int DanceRatingDecrement = -2;


        public static int CreateTitleHash(string title)
        {
            return CreateNormalForm(title).GetHashCode();
        }

        public static string CreateNormalForm(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);

            string norm = s.Normalize(NormalizationForm.FormD);

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

            return sb.ToString();
        }

        public SongProperty CreateSongProperty(Song song, string name, string value, SongLog log = null)
        {
            SongProperty ret = SongProperties.Create();
            ret.Song = song;
            ret.Name = name;
            ret.Value = value;

            if (log != null)
            {
                LogPropertyUpdate(ret, log);
            }

            SongProperties.Add(ret);

            return ret;
        }


        public bool UpdateSongProperty(SongDetails edit, Song old, string name, SongLog log)
        {
            bool modified = false;

            object eP = edit.GetType().GetProperty(name).GetValue(edit);
            object oP = old.GetType().GetProperty(name).GetValue(old);
            
            if (!object.Equals(eP,oP))
            {
                modified = true;

                old.GetType().GetProperty(name).SetValue(old, eP);

                SongProperty np = SongProperties.Create();
                np.Song = old;
                np.Name = name;
                np.Value = SerializeValue(eP);

                SongProperties.Add(np);
                LogPropertyUpdate(np, log, SerializeValue(oP));
            }

            return modified;
        }

        public void RestoreSongProperty(Song song, LogValue lv, UndoAction action)
        {
            // For scalar properties and albums just updating the property will
            //  provide the information for rebulding the song
            // For users, this is additive, so no need to do anything except with a new song
            // For DanceRatings, we're going to update the song here
            //  since it is cummulative

            SongProperty np = SongProperties.Create();

            np.Song = song;
            np.Name = lv.Name;

            // This works for everything but Dancerating, which will be overwritten below
            if (action == UndoAction.Undo)
                np.Value = lv.Old;
            else
                np.Value = lv.Value;


            if (lv.Name.Equals(UserField))
            {
                // Only new song has a null ModifiedBy?
                ApplicationUser user = FindUser(lv.Value);
                if (!user.Songs.Contains(song))
                {
                    user.Songs.Add(song);
                }
            }
            else if (lv.Name.Equals(DanceRatingField))
            {
                DanceRatingDelta drd = new DanceRatingDelta(lv.Value);
                if (action == UndoAction.Undo)
                { 
                    drd.Delta *= -1; 
                }
                
                np.Value = drd.ToString();

                DanceRating dr = song.DanceRatings.FirstOrDefault(d => string.Equals(d.DanceId, drd.DanceId));
                if (dr == null)
                {
                    song.DanceRatings.Add(new DanceRating() { DanceId = drd.DanceId, SongId = song.SongId, Weight = drd.Delta });
                }
                else
                {
                    dr.Weight += drd.Delta;
                    if (dr.Weight <= 0)
                    {
                        song.DanceRatings.Remove(dr);
                    }
                }
            }

            song.SongProperties.Add(np);
        }

        private void CreateAlbums(Song song, List<AlbumDetails> albums, SongLog log = null)
        {
            if (albums != null)
            {
                for (int ia = 0; ia < albums.Count; ia++)
                {
                    AlbumDetails ad = albums[ia];
                    if (!string.IsNullOrWhiteSpace(ad.Name))
                    {
                        if (ia == 0)
                        {
                            song.Album = albums[0].Name;
                        }

                        ad.CreateProperties(this, song, ia, log);
                    }
                }
            }
        }

        public static string SerializeValue(object o)
        {
            if (o == null)
            {
                return null;
            }
            else
            {
                return o.ToString();
            }
        }

        public bool UpdateSongProperty(Song song, string name, decimal? value, IOrderedQueryable<SongProperty> properties, SongLog log)
        {
            bool modified = true;

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

        public void LogPropertyUpdate(SongProperty sp, SongLog log, string oldValue = null)
        {
            log.UpdateData(sp.Name, sp.Value, oldValue);
        }

        private void LogSongCommand(string command, Song song, ApplicationUser user, bool includeSignature = true)
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
            // TODONEXT: Merge and Edit both seem to be buggy - also value of user field is null
            foreach (string line in lines)
            {
                RestoreFromLog(line);
            }
        }

        public void RestoreFromLog(string line)
        {
            SongLog log = Log.Create();

            if (!log.Initialize(this,line))
            {
                Debug.WriteLine(string.Format("Unable to restore line: {0}", line));
            }


            Song song = null;

            switch (log.Action)
            {
                case DeleteCommand:
                case EditCommand:
                    song = FindSong(log.SongReference, log.SongSignature);
                    break;
                case MergeCommand:
                case CreateCommand:
                    break;
                default:
                    Debug.WriteLine(string.Format("Bad Command: {0}", log.Action));
                    return;
            }

            switch (log.Action)
            {
                case DeleteCommand:
                    RemoveSong(song);
                    break;
                case EditCommand:
                    RestoreValuesFromLog(log,song,UndoAction.Redo);
                    break;
                case MergeCommand:
                case CreateCommand:
                    CreateSongFromLog(log);
                    break;
                default:
                    Debug.WriteLine(string.Format("Bad Command: {0}", log.Action));
                    break;
            }

            Log.Add(log);
            SaveChanges();
        }

        public IEnumerable<UndoResult> UndoLog(ApplicationUser user, IEnumerable<SongLog> entries)
        {
            List<UndoResult> results = new List<UndoResult>();

            foreach (SongLog entry in entries)
            {
                results.Add(UndoEntry(user, entry));
            }

            return results;
        }

        public UndoResult UndoEntry(ApplicationUser user, SongLog entry)
        {
            string action = entry.Action;
            string error = null;

            // Quick recurse on Redo
            if (action.StartsWith(RedoCommand))
            {
                int? idx = entry.GetIntData(SuccessResult);

                action = RedoCommand;

                if (idx.HasValue)
                {
                    SongLog uentry = Log.Find(idx.Value);
                    int? idx2 = uentry.GetIntData(SuccessResult);

                    if (idx2.HasValue)
                    {
                        SongLog rentry = Log.Find(idx2.Value);

                        return UndoEntry(user, rentry);
                    }
                }

                error = string.Format("Unable to redo a failed undo song id='{0}' signature='{1}'", entry.SongReference, entry.SongSignature);
            }

            UndoResult result = new UndoResult { Original = entry };

            Song song = FindSong(entry.SongReference,entry.SongSignature);

            if (song == null)
            {
                error = string.Format("Unable to find song id='{0}' signature='{1}'",entry.SongReference,entry.SongSignature);
            }

            SongLog log = null;
            string command = UndoCommand + entry.Action;

            if (error == null)
            {
                if (action.StartsWith(UndoCommand))
                {
                    int? idx = entry.GetIntData(SuccessResult);
                    action = UndoCommand;

                    if (idx.HasValue)
                    {
                        SongLog rentry = Log.Find(idx.Value);

                        error = RedoEntry(rentry,song);
                        command = RedoCommand + entry.Action.Substring(UndoCommand.Length);
                    }
                    else
                    {
                        error = string.Format("Unable to redo a failed undo song id='{0}' signature='{1}'", entry.SongReference, entry.SongSignature);
                    }
                }

                log = CreateSongLog(user, song,  command);
                result.Result = log;

                switch (action)
                {
                    case DeleteCommand:
                        error = Undelete(song);
                        break;
                    case MergeCommand:
                        error = Unmerge(entry, song);
                        break;
                    case EditCommand:
                        error = RestoreValuesFromLog(entry, song, UndoAction.Undo);
                        break;
                    case CreateCommand:
                        RemoveSong(song);
                        break;
                    case UndoCommand:
                    case RedoCommand:
                        break;
                    default:
                        error = string.Format("'{0}' action not yet supported for Undo.", entry.Action);
                        break;
                }

            }

            log.UpdateData(error == null ? SuccessResult : FailResult, entry.Id.ToString());

            if (error != null)
            {
                log.UpdateData(MessageData, error);
            }

            Log.Add(log);
            // Have to save changes each time because
            // the may be cumulative (can we optimize by
            // doing a savechanges when a songId comes
            // up a second time?
            SaveChanges();

            return result;
        }

        private string RedoEntry(SongLog entry, Song song)
        {
            string error = null;

            switch (entry.Action)
            {
                case DeleteCommand:
                    RemoveSong(song);
                    break;
                case MergeCommand:
                    error = Remerge(entry, song);
                    break;
                case EditCommand:
                    error = RestoreValuesFromLog(entry, song, UndoAction.Redo);
                    break;
                case CreateCommand:
                    RestoreSong(song);
                    break;
                default:
                    error = string.Format("'{0}' action not yet supported for Redo.", entry.Action);
                    break;
            }

            return error;
        }

        public string Undelete(Song song)
        {
            string ret = null;

            RestoreSong(song);

            return ret;
        }

        public string Unmerge(SongLog entry, Song song)
        {
            string ret = null;

            // First restore the merged songs
            string t = entry.GetData(MergeCommand);

            ICollection<Song> songs = SongsFromList(t);
            foreach (Song s in songs)
            {
                RestoreSong(s);
            }

            // Now delete the merged song
            RemoveSong(song);
            
            return ret;
        }

        private string RestoreValuesFromLog(SongLog entry, Song song, UndoAction action)
        {
            string ret = null;

            CreateEditProperties(song, entry.User, EditCommand);

            IList<LogValue> values = entry.GetValues();
            foreach (LogValue lv in values)
            {
                if (!lv.IsAction)
                {
                    RestoreSongProperty(song, lv, action);
                }            
            }

            SongDetails sd = new SongDetails(this, song.SongId, song.SongProperties);
            song.RestoreScalar(this, sd);

            return ret;
        }

        private string Remerge(SongLog entry, Song song)
        {
            string ret = null;

            // First, restore the merged to song
            RestoreSong(song);

            // Then remove the merged from songs
            string t = entry.GetData(MergeCommand);
            ICollection<Song> songs = SongsFromList(t);
            foreach (Song s in songs)
            {
                RemoveSong(s);
            }

            return ret;
        }

        private void RestoreSong(Song song)
        {
            if (!string.IsNullOrWhiteSpace(song.Title))
            {
                throw new ArgumentOutOfRangeException("song", "Attempting to restore a song that hasn't been deleted");
            }
            SongDetails sd = new SongDetails(this, song.SongId, song.SongProperties);
            song.Restore(this, sd);
        }

        private ICollection<Song> SongsFromList(string list)
        {
            string[] dels = list.Split(new char[] { ';' });
            List<Song> songs = new List<Song>(list.Length);

            for (int i = 0; i < dels.Length; i++)
            {
                int idx = 0;
                if (int.TryParse(dels[i], out idx))
                {
                    Song s = Songs.Find(idx);
                    if (s != null)
                    {
                        songs.Add(s);
                    }
                }
            }

            return songs;
        }

        private Song FindSong(int id, string signature)
        {
            // First find a match id
            Song song = Songs.FirstOrDefault(s => s.SongId == id);

            // TODO: Think about signature mis-matches, we can't do the straighforward fail on mis-match because
            //  we're using this for edit and it's perfectly reasonable to edit parts of the sig...
            // || !(string.IsNullOrWhiteSpace(signature) || song.IsNull || MatchSigatures(signature,song.Signature))
            if (song == null)
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

        private SongLog CreateSongLog(ApplicationUser user, Song song, string action)
        {
            SongLog log = Log.Create();

            log.Initialize(user, song, action);

            return log;
        }

        private void CreateSongFromLog(SongLog log)
        {
            string initV = log.GetData(MergeCommand);

            // For merge case, first we delete the old songs
            if (initV != null)
            {
                try
                {
                    foreach (Song d in SongsFromList(initV))
                    {
                        RemoveSong(d);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }

            Song song = Songs.Create();
            song.Created = log.Time;
            song.Modified = DateTime.Now;
            Songs.Add(song);

            // Is there a better way to get an id assigned to the song?
            SaveChanges();

            //CreateSongProperty(song, initC, initV);

            IList<LogValue> values = log.GetValues();
            foreach (LogValue lv in values)
            {
                if (!lv.IsAction)
                {
                    RestoreSongProperty(song, lv, UndoAction.Redo);
                }
            }

            SongDetails sd = new SongDetails(this, song.SongId, song.SongProperties);
            song.RestoreScalar(this, sd);
        }


        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(this, n, level);
        }

        public ApplicationUser FindUser(string name)
        {
            return Users.FirstOrDefault(u => u.UserName.ToLower() == name.ToLower());
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

            //Debug.WriteLine("------------------- users ------------------");
            //foreach (ApplicationUser user in Users.Local)
            //{
            //    user.Dump();
            //}
        }

        private static Dances _dances = new Dances();

    }
}