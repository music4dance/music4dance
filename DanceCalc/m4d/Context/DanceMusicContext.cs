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
using m4d.Utilities;
using m4dModels;

// Let's see if we can mock up a recoverable log file by spitting out
// something resembling a tab-separated flat list of songs items with a
// command associated with each line.  Might add a checkpoint command
// into the songproperties table as well...

// COMMAND  User    Title   Artist  Album   Publisher   Tempo   Length  Track   Genre   Purchase    DanceRating Custom

// Kill Publisher Track Purchase -> do these move to custom


namespace m4d.Context
{
    public enum UndoAction { Undo, Redo };

    public class DanceMusicContext : IdentityDbContext<ApplicationUser>, IUserMap, ISongPropertyFactory
    {
        public DanceMusicContext() : base("DefaultConnection")
        {
        }

        private static DbConnection CreateConnection(string nameOrConnectionString)
        {
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


        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        public DbSet<SongLog> Log { get; set; }

        public DbSet<ModifiedRecord> Modified { get; set; }

        protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
            modelBuilder.Entity<Song>().Ignore(song => song.CreateEntry);
            modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);            
            modelBuilder.Entity<DanceRating>().HasKey(t => new { t.SongId, t.DanceId });
            modelBuilder.Entity<ModifiedRecord>().HasKey(t => new { t.ApplicationUserId, t.SongId });

            base.OnModelCreating(modelBuilder);
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

            Song song = CreateSong(user, title, artist, genre, tempo, length, albums, Song.MergeCommand, songIds, true);
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

                // TODO: Get this to work in a detached state
                foreach (ModifiedRecord us in from.ModifiedBy)
                {
                    if (AddUserToSong(us.ApplicationUser,song))
                    {
                        CreateSongProperty(song, Song.UserField, us.ApplicationUser.UserName, song.CreateEntry);
                    }
                }
            }

            // Dump the weight table
            foreach (KeyValuePair<string, int> dance in weights)
            {
                DanceRating dr = DanceRatings.Create();
                dr.DanceId = dance.Key;
                dr.SongId = song.SongId;
                dr.Weight = dance.Value;
                
                song.DanceRatings.Add(dr);
                var dre = Entry(dr);
                if (dre != null && dre.State != EntityState.Added)
                {
                    dre.State = EntityState.Added;
                }

                string value = new DanceRatingDelta { DanceId = dance.Key, Delta = dance.Value }.ToString();

                CreateSongProperty(song, Song.DanceRatingField, value, song.CreateEntry);
            }

            // Delete all of the old songs (With merge-with Id from above)
            foreach (Song from in songs)
            {
                RemoveSong(from);
            }

            var se = Entry(song);
            if (se != null)
            {
                se.State = EntityState.Modified;
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
            return CreateSong(user, title, artist, genre, tempo, length, albums, Song.CreateCommand, string.Empty, log);
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
                ModifiedRecord us = Modified.Create();
                us.Song = song;
                us.ApplicationUser = user;
                Modified.Add(us);
                CreateSongProperty(song, Song.UserField, user.UserName, log);
            }

            // Handle Timestamps
            song.Created = time;
            song.Modified = time;
            CreateSongProperty(song, Song.TimeField, time.ToString(), log);
            
            // Title
            Debug.Assert(!string.IsNullOrWhiteSpace(title));
            song.Title = title;
            CreateSongProperty(song, Song.TitleField, title, log);

            // Artist
            if (!string.IsNullOrWhiteSpace(artist))
            {
                song.Artist = artist;
                CreateSongProperty(song, Song.ArtistField, artist, log);
            }

            // Genre
            if (!string.IsNullOrWhiteSpace(genre))
            {
                song.Genre = genre;
                CreateSongProperty(song, Song.PublisherField, genre, log);
            }

            // Tempo
            if (tempo != null)
            {
                song.Tempo = tempo;
                CreateSongProperty(song, Song.TempoField, tempo.ToString(), log);
            }

            // Length
            if (length != null && length != 0)
            {
                song.Length = length;
                CreateSongProperty(song, Song.LengthField, length.ToString(), log);
            }

            // Album
            CreateAlbums(song,albums,log);

            song.Purchase = SongDetails.GetPurchaseTags(albums);

            song.TitleHash = Song.CreateTitleHash(title);

            song = Songs.Add(song);
            if (createLog)
            {
                Log.Add(log);
            }

            return song;
        }

        bool AddUserToSong(ApplicationUser user, Song song)
        {
            if (!song.ModifiedBy.Any(u => u.ApplicationUserId == user.Id))
            {
                ModifiedRecord us = Modified.Create();
                us.Song = song;
                us.ApplicationUser = user;
                Modified.Add(us);
                song.ModifiedBy.Add(us);
                return true;
            }
            else
            {
                return false;
            }
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

            modified |= UpdateSongProperty(edit, song, Song.TitleField, log);
            modified |= UpdateSongProperty(edit, song, Song.ArtistField, log);
            modified |= UpdateSongProperty(edit, song, Song.TempoField, log);
            modified |= UpdateSongProperty(edit, song, Song.LengthField, log);
            modified |= UpdateSongProperty(edit, song, Song.GenreField, log);

            List<AlbumDetails> oldAlbums = SongDetails.BuildAlbumInfo(song);

            bool foundFirst = false;
            bool foundOld = false;

            List<int> promotions = new List<int>();

            song.Album = null;

            // TODO: Think about how we might want to 
            //  put the xbox selection at the top of the album list...
            for (int aidx = 0; aidx < edit.Albums.Count; aidx++ )
            {
                AlbumDetails album =  edit.Albums[aidx];
                AlbumDetails old = oldAlbums.FirstOrDefault(a => a.Index == album.Index);

                if (!foundFirst && !string.IsNullOrEmpty(album.Name))
                {
                    foundFirst = true;
                    song.Album = album.Name;
                }

                if (old != null)
                {
                    // We're in existing album territory
                    foundOld = true;
                    modified |= album.ModifyInfo(this, song, old, log);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (!string.IsNullOrWhiteSpace(album.Name))
                    {
                        album.CreateProperties(this,song,log);
                        modified = true;

                        // Push this to the front if we haven't run into an old album yet
                        if (!foundOld)
                        {
                            promotions.Insert(0,album.Index);
                        }
                    }
                }
            }

            // Now push the promotions
            foreach (int p in promotions)
            {
                AlbumDetails.AddProperty(this, song, p, Song.AlbumPromote, null, string.Empty, log);
            }

            modified |= EditDanceRatings(edit, song, addDances, remDances, log);

            modified |= UpdatePurchaseInfo(song, edit);

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

        private bool UpdatePurchaseInfo(Song song, SongDetails edit)
        {
            bool ret = false;
            string pi = edit.GetPurchaseTags();
            if (!string.Equals(song.Purchase, pi))
            {
                song.Purchase = pi;
                ret = true;
            }
            return ret;
        }

        private SongLog CreateEditHeader(Song song, ApplicationUser user)
        {
            SongLog log = CreateSongLog(user, song, Song.EditCommand);

            CreateEditProperties(song, user, Song.EditCommand);

            return log;
        }

        private void CreateEditProperties(Song song, ApplicationUser user, string command)
        {
            // Add the command into the property log
            CreateSongProperty(song, Song.EditCommand, string.Empty);

            // Handle User association
            if (user != null)
            {
                AddUserToSong(user, song);
                CreateSongProperty(song, Song.UserField, user.UserName);
            }

            // Handle Timestamps
            DateTime time = DateTime.Now;
            song.Modified = time;
            CreateSongProperty(song, Song.TimeField, time.ToString());
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
            song.TitleHash = Song.CreateTitleHash(song.Title);

            Trace.WriteLine(string.Format("Song:{0} Fixedup:{1}", song.SongId, fixedup));
        }

        public void DeleteSong(ApplicationUser user, Song song, string command = Song.DeleteCommand)
        {
            LogSongCommand(command, song, user);
            RemoveSong(song);
            SaveChanges();
        }

        private void RemoveSong(Song song)
        {
            song.Delete();
            var entry = Entry(song);
            if (entry != null)
            {
                entry.State = EntityState.Modified;
            }
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

                CreateSongProperty(song, Song.DanceRatingField, string.Format("{0}+{1}", dance.Id, weight), song.CreateEntry);
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
                    np.Name = Song.DanceRatingField;
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
                    np.Name = Song.DanceRatingField;
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
                np.Value = LogBase.SerializeValue(eP);

                SongProperties.Add(np);
                LogPropertyUpdate(np, log, LogBase.SerializeValue(oP));
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


            if (lv.Name.Equals(Song.UserField))
            {
                // Only new song has a null ModifiedBy?
                ApplicationUser user = FindUser(lv.Value);
                AddUserToSong(user, song);
            }
            else if (lv.Name.Equals(Song.DanceRatingField))
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

                        ad.CreateProperties(this, song, log);
                    }
                }
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
            foreach (string line in lines)
            {
                RestoreFromLog(line);
            }
        }

        public void RestoreFromLog(string line)
        {
            SongLog log = Log.Create();

            if (!log.Initialize(line,this))
            {
                Trace.WriteLine(string.Format("Unable to restore line: {0}", line));
            }


            Song song = null;

            switch (log.Action)
            {
                case Song.DeleteCommand:
                case Song.EditCommand:
                    song = FindSong(log.SongReference, log.SongSignature);
                    break;
                case Song.MergeCommand:
                case Song.CreateCommand:
                    break;
                default:
                    Trace.WriteLine(string.Format("Bad Command: {0}", log.Action));
                    return;
            }

            switch (log.Action)
            {
                case Song.DeleteCommand:
                    RemoveSong(song);
                    break;
                case Song.EditCommand:
                    RestoreValuesFromLog(log,song,UndoAction.Redo);
                    break;
                case Song.MergeCommand:
                case Song.CreateCommand:
                    CreateSongFromLog(log);
                    break;
                default:
                    Trace.WriteLine(string.Format("Bad Command: {0}", log.Action));
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
            if (action.StartsWith(Song.RedoCommand))
            {
                int? idx = entry.GetIntData(Song.SuccessResult);

                action = Song.RedoCommand;

                if (idx.HasValue)
                {
                    SongLog uentry = Log.Find(idx.Value);
                    int? idx2 = uentry.GetIntData(Song.SuccessResult);

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
            string command = Song.UndoCommand + entry.Action;

            if (error == null)
            {
                if (action.StartsWith(Song.UndoCommand))
                {
                    int? idx = entry.GetIntData(Song.SuccessResult);
                    action = Song.UndoCommand;

                    if (idx.HasValue)
                    {
                        SongLog rentry = Log.Find(idx.Value);

                        error = RedoEntry(rentry,song);
                        command = Song.RedoCommand + entry.Action.Substring(Song.UndoCommand.Length);
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
                    case Song.DeleteCommand:
                        error = Undelete(song);
                        break;
                    case Song.MergeCommand:
                        error = Unmerge(entry, song);
                        break;
                    case Song.EditCommand:
                        error = RestoreValuesFromLog(entry, song, UndoAction.Undo);
                        break;
                    case Song.CreateCommand:
                        RemoveSong(song);
                        break;
                    case Song.UndoCommand:
                    case Song.RedoCommand:
                        break;
                    default:
                        error = string.Format("'{0}' action not yet supported for Undo.", entry.Action);
                        break;
                }

            }

            log.UpdateData(error == null ? Song.SuccessResult : Song.FailResult, entry.Id.ToString());

            if (error != null)
            {
                log.UpdateData(Song.MessageData, error);
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
                case Song.DeleteCommand:
                    RemoveSong(song);
                    break;
                case Song.MergeCommand:
                    error = Remerge(entry, song);
                    break;
                case Song.EditCommand:
                    error = RestoreValuesFromLog(entry, song, UndoAction.Redo);
                    break;
                case Song.CreateCommand:
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
            string t = entry.GetData(Song.MergeCommand);

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

            CreateEditProperties(song, entry.User, Song.EditCommand);

            IList<LogValue> values = entry.GetValues();
            foreach (LogValue lv in values)
            {
                if (!lv.IsAction)
                {
                    RestoreSongProperty(song, lv, action);
                }            
            }

            RestoreSong(song);

            return ret;
        }

        private string Remerge(SongLog entry, Song song)
        {
            string ret = null;

            // First, restore the merged to song
            RestoreSong(song);

            // Then remove the merged from songs
            string t = entry.GetData(Song.MergeCommand);
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
            SongDetails sd = new SongDetails(song.SongId, song.SongProperties, this);
            song.Restore(sd);
            UpdateUsers(song);
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
                Trace.WriteLine(string.Format("Couldn't find song by Id: {0} or signature {1}", song.SongId, song.Signature));
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
            string initV = log.GetData(Song.MergeCommand);

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
                    Trace.WriteLine(e.Message);
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

            RestoreSong(song);
        }


        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(this, n, level);
        }

        /// <summary>
        /// Update the ModifiedBy refeerences based on the song properties
        /// </summary>
        /// <param name="song"></param>
        public void UpdateUsers(Song song)
        {
            HashSet<string> users = new HashSet<string>();

            foreach (ModifiedRecord us in song.ModifiedBy)
            {
                us.Song = song;
                us.ApplicationUser = FindUser(us.ApplicationUserId);

                if (users.Contains(us.ApplicationUserId))
                {
                    Trace.WriteLine(string.Format("Duplicate Mapping: Song = {0} User = {1}", song.SongId, us.ApplicationUserId));
                }
                else
                {
                    users.Add(us.ApplicationUserId);
                }
            }
        }

        
        public void Dump()
        {
            // TODO: Create a dump routine to help dump the object graph - definitely need object id of some kind (address)

            Trace.WriteLine("------------------- songs ------------------");
            foreach (Song song in Songs.Local)
            {
                song.Dump();
            }

            Trace.WriteLine("------------------- properties ------------------");
            foreach (SongProperty prop in SongProperties.Local)
            {
                prop.Dump();
            }

            //Trace.WriteLine("------------------- users ------------------");
            //foreach (ApplicationUser user in Users.Local)
            //{
            //    user.Dump();
            //}
        }


#region IUserMap
        public ApplicationUser FindUser(string name)
        {
            return Users.FirstOrDefault(u => u.UserName.ToLower() == name.ToLower());
        }
        public ModifiedRecord CreateMapping(int songId, string name)
        {
            ModifiedRecord us = Modified.Create();
            us.ApplicationUserId = name;
            us.SongId = songId;
            return us;
        }

#endregion

        public SongProperty CreateSongProperty(Song song, string name, object value)
        {
            SongProperty np = SongProperties.Create();
            np.Song = song;
            np.Name = name;
            np.Value = LogBase.SerializeValue(value);

            SongProperties.Add(np);

            return np;
        }
    }
}