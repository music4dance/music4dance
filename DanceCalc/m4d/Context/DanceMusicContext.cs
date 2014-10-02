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
using Microsoft.AspNet.Identity;
using System.Data.Entity.Validation;
using System.Net;
using System.IO;

// Let's see if we can mock up a recoverable log file by spitting out
// something resembling a tab-separated flat list of songs items with a
// command associated with each line.  Might add a checkpoint command
// into the songproperties table as well...

// COMMAND  User    Title   Artist  Album   Publisher   Tempo   Length  Track   Genre   Purchase    DanceRating Custom

// Kill Publisher Track Purchase -> do these move to custom


namespace m4d.Context
{
    public enum UndoAction { Undo, Redo };

    public class DanceMusicContext : IdentityDbContext<ApplicationUser>, IUserMap, IFactories
    {
        #region Construction

        public static DanceMusicContext Create()
        {
            return new DanceMusicContext();
        }
        
        public DanceMusicContext()
            : base("DefaultConnection", throwIfV1Schema:false)
        {
            ;
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
        
        #endregion

        #region Properties
        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        public DbSet<Tag> Tags { get; set; }

        public DbSet<TagType> TagTypes { get; set; }

        public DbSet<SongLog> Log { get; set; }

        public DbSet<ModifiedRecord> Modified { get; set; }
        
        #endregion

        #region Events
        protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
            modelBuilder.Entity<Song>().Ignore(song => song.CurrentLog);
            modelBuilder.Entity<Song>().Ignore(song => song.AlbumName);
            modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);
            modelBuilder.Entity<DanceRating>().HasKey(dr => new { dr.SongId, dr.DanceId });
            modelBuilder.Entity<Tag>().HasKey(tag => new { tag.SongId, tag.Value });
            modelBuilder.Entity<TagType>().HasKey(tt => tt.Value);
            modelBuilder.Entity<TagType>().Ignore(tt => tt.CategoryList);
            modelBuilder.Entity<ModifiedRecord>().HasKey(t => new { t.ApplicationUserId, t.SongId });
            modelBuilder.Entity<ModifiedRecord>().Ignore(mr => mr.UserName);

            base.OnModelCreating(modelBuilder);
        }
        
        #endregion

        #region Edit
        private Song CreateSong(Guid? guid = null, bool doLog = false)
        {
            Guid g = guid ?? Guid.NewGuid();
            Song song = Songs.Create();
            song.SongId = g;

            if (doLog)
            {
                song.CurrentLog = Log.Create();
            }

            return song;
        }
        public Song CreateSong(ApplicationUser user, SongDetails sd, string command = SongBase.CreateCommand, string value = null, bool createLog=true)
        {
            if (string.Equals(sd.Title, sd.Artist))
            {
                Trace.WriteLine(string.Format("Title and Artist are the same ({0})", sd.Title));
            }

            Song song = CreateSong(null, createLog);
            song.Create(sd, user, command, value, this, this);

            song = Songs.Add(song);
            if (createLog)
            {
                Log.Add(song.CurrentLog);
            }

            return song;
        }

        public SongDetails EditSong(ApplicationUser user, SongDetails edit, List<string> addDances, List<string> remDances, string editTags, bool createLog=true)
        {
            Song song = Songs.Find(edit.SongId);
            if (createLog)
            {
                song.CurrentLog = CreateSongLog(user, song, Song.EditCommand);
            }

            if (song.Edit(user, edit, addDances, remDances, ParseTags(editTags), this, this))
            {
                if (createLog)
                {
                    Log.Add(song.CurrentLog);
                    return FindSongDetails(edit.SongId);
                }
                else
                {
                    return new SongDetails(song);
                }
            }
            else
            {
                return null;
            }
        }

        public SongDetails UpdateSong(ApplicationUser user, Song song, SongDetails edit, bool createLog = true)
        {
            if (createLog)
            {
                song.CurrentLog = CreateSongLog(user, song, Song.EditCommand);
            }

            if (song.Update(user, edit, this, this))
            {
                if (createLog)
                {
                    Log.Add(song.CurrentLog);
                    return FindSongDetails(edit.SongId);
                }
                else
                {
                    return new SongDetails(song);
                }
            }
            else
            {
                return null;
            }
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        //  TODO: I'm pretty sure I can clean up this and all the other editing stuff by pushing
        //  the diffing part down into SongDetails (which will also let me unit test it more easily)
        public SongDetails AdditiveMerge(ApplicationUser user, Guid songId, SongDetails edit, List<string> addDances)
        {
            Song song = Songs.Find(songId);
            song.CurrentLog = CreateSongLog(user, song, Song.EditCommand);

            if (song.AdditiveMerge(user, edit, addDances, this, this))
            {
                Log.Add(song.CurrentLog);
                SaveChanges();
                return FindSongDetails(songId);
            }
            else
            {
                return null;
            }
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, string tags, List<AlbumDetails> albums)
        {
            string songIds = string.Join(";", songs.Select(s => s.SongId.ToString()));

            SongDetails sd = new SongDetails(title, artist, tempo, length, albums);
            sd.AddTags(tags);

            Song song = CreateSong(user, sd, Song.MergeCommand, songIds, true);
            song.CurrentLog.SongReference = song.SongId;
            song.CurrentLog.SongSignature = song.Signature;

            song = Songs.Add(song);

            song.MergeDetails(songs,this,this);

            // Delete all of the old songs (With merge-with Id from above)
            foreach (Song from in songs)
            {
                RemoveSong(from);
            }

            SaveChanges();

            SongCounts.ClearCache();

            return song;
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
            //var entry = Entry(song);
            //if (entry != null)
            //{
            //    entry.State = EntityState.Modified;
            //}
        }
        
        #endregion

        #region Dance Ratings

        public DanceRating CreateDanceRating(Song song, string danceId, int weight)
        {
            Dance dance = Dances.Find(danceId);

            if (dance == null)
            {
                return null;
            }

            DanceRating dr = DanceRatings.Create();

            dr.Dance = dance;
            dr.DanceId = dance.Id;

            dr.Weight = weight;

            song.AddDanceRating(dr);

            return dr;
        }

        #endregion

        #region Properties

        public SongProperty CreateSongProperty(Song song, string name, object value, SongLog log)
        {
            SongProperty ret = SongProperties.Create();
            ret.Song = song;
            ret.Name = name;
            ret.Value = SongProperty.SerializeValue(value);

            if (song.SongProperties == null)
            {
                song.SongProperties = new List<SongProperty>();
            }
            song.SongProperties.Add(ret);

            if (log != null)
            {
                LogPropertyUpdate(ret, log);
            }

            SongProperties.Add(ret);

            return ret;
        }


        private void RestoreSongProperty(Song song, LogValue lv, UndoAction action)
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
                ApplicationUser user = FindUser(lv.Value);
                song.AddUser(user, this);
            }
            else if (lv.Name.Equals(Song.DanceRatingField))
            {
                DanceRatingDelta drd = new DanceRatingDelta(lv.Value);
                if (action == UndoAction.Undo)
                {
                    drd.Delta *= -1;
                }

                np.Value = drd.ToString();

                // TODO: Consider implementing a MergeDanceRating at the song level
                DanceRating dr = song.DanceRatings.FirstOrDefault(d => string.Equals(d.DanceId, drd.DanceId));
                if (dr == null)
                {
                    song.AddDanceRating(new DanceRating() { DanceId = drd.DanceId, Weight = drd.Delta });
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
            else if (lv.Name.Equals(Song.TagField))
            {
                string value = lv.Value;
                int delta = 1;
                if (lv.Value.Length > 0 && lv.Value[0] == '-')
                {
                    delta = -1;
                    value = lv.Value.Substring(1);
                }
                if (action == UndoAction.Undo)
                {
                    delta *= -1;
                }
                Tag tag = song.FindTag(value);
                if (tag == null)
                {
                    song.AddTag(new Tag() { Value = value, Count = 1 });
                }
                else
                {
                    tag.Count += delta;
                    if (tag.Count <= 0)
                    {
                        song.Tags.Remove(tag);
                    }
                }
            }

            song.SongProperties.Add(np);
        }


        #endregion

        #region Logging

        public void RestoreFromLog(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                RestoreFromLog(line);
            }
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

        private UndoResult UndoEntry(ApplicationUser user, SongLog entry)
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

            Song song = FindSong(entry.SongReference, entry.SongSignature);

            if (song == null)
            {
                error = string.Format("Unable to find song id='{0}' signature='{1}'", entry.SongReference, entry.SongSignature);
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

                        error = RedoEntry(rentry, song);
                        command = Song.RedoCommand + entry.Action.Substring(Song.UndoCommand.Length);
                    }
                    else
                    {
                        error = string.Format("Unable to redo a failed undo song id='{0}' signature='{1}'", entry.SongReference, entry.SongSignature);
                    }
                }

                log = CreateSongLog(user, song, command);
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

        private string Undelete(Song song)
        {
            string ret = null;

            RestoreSong(song);

            return ret;
        }

        private string Unmerge(SongLog entry, Song song)
        {
            // TODONEXT: Unmerge is crashing...
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

        private void RestoreFromLog(string line)
        {
            SongLog log = Log.Create();

            if (!log.Initialize(line, this))
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
                    RestoreValuesFromLog(log, song, UndoAction.Redo);
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

        private void LogPropertyUpdate(SongProperty sp, SongLog log, string oldValue = null)
        {
            log.UpdateData(sp.Name, sp.Value, oldValue);
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

        private string RestoreValuesFromLog(SongLog entry, Song song, UndoAction action)
        {
            string ret = null;

            song.CreateEditProperties(entry.User, Song.EditCommand,this,this);

            IList<LogValue> values = entry.GetValues();
            foreach (LogValue lv in values)
            {
                if (!lv.IsAction)
                {
                    RestoreSongProperty(song, lv, action);
                }
            }

            SongDetails sd = new SongDetails(song.SongId, song.SongProperties);
            song.RestoreScalar(sd);
            song.UpdateUsers(this);

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
            SongDetails sd = new SongDetails(song.SongId, song.SongProperties);
            song.Restore(sd,this, this);
            song.UpdateUsers(this);
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

            Song song = CreateSong(log.SongReference);
            song.Created = log.Time;
            song.Modified = DateTime.Now;

            IList<LogValue> values = log.GetValues();
            foreach (LogValue lv in values)
            {
                if (!lv.IsAction)
                {
                    RestoreSongProperty(song, lv, UndoAction.Redo);
                }
            }

            RestoreSong(song);
            Songs.Add(song);
        }
        #endregion

        #region Song Lookup
        public Song FindSong(Guid id, string signature = null)
        {
            // First find a match id
            Song song = Songs.Find(id);

            // TODO: Think about signature mis-matches, we can't do the straighforward fail on mis-match because
            //  we're using this for edit and it's perfectly reasonable to edit parts of the sig...
            // || !(string.IsNullOrWhiteSpace(signature) || song.IsNull || MatchSigatures(signature,song.Signature))
            if (song == null && signature != null)
            {
                song = FindSongBySignature(signature);
            }

            if (song == null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose,string.Format("Couldn't find song by Id: {0} or signature {1}", id, signature));
            }
            

            return song;
        }

        public SongDetails FindSongDetails(Guid id)
        {
            SongDetails sd = null;

            Song song = Songs.Find(id);

            if (song != null)
                sd = new SongDetails(song);

            return sd;
        }

        private Song FindSongBySignature(string signature)
        {
            Song song = Songs.FirstOrDefault(s => s.Signature == signature);

            return song;
        }

        private bool MatchSigatures(string sig1, string sig2)
        {
            return string.Equals(sig1, sig2, StringComparison.Ordinal);
        }
        
        private ICollection<Song> SongsFromList(string list)
        {
            string[] dels = list.Split(new char[] { ';' });
            List<Song> songs = new List<Song>(list.Length);

            for (int i = 0; i < dels.Length; i++)
            {
                Guid idx = Guid.Empty;
                if (Guid.TryParse(dels[i], out idx))
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
        #endregion

        #region MusicService
        // Obviously not the clean abstraction, but Amazon is different enough that my abstraction
        //  between itunes and xbox doesn't work.   So I'm going to shoe-horn this in to get it working
        //  and refactor later.

        public IList<ServiceTrack> FindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null, string album = null)
        {
            IList<ServiceTrack> list = null;

            if (service != null)
            {
                list = DoFindMusicServiceSong(song, service, clean, title, artist);
            }
            else
            {
                List<ServiceTrack> acc = new List<ServiceTrack>();
                foreach (var servT in MusicService.GetServices())
                {
                    IList<ServiceTrack> t = DoFindMusicServiceSong(song, servT, clean, title, artist);
                    if (t != null)
                    {
                        acc.AddRange(t);
                    }
                }

                list = acc;
            }

            if (list != null)
            {
                list = FilterKaraoke(list);
                if (song != null)
                {
                    list = song.RankTracks(list);
                }
                else
                {
                    list = SongDetails.RankTracksByCluster(list,album);
                }
            }

            return list;
        }

        private static IList<ServiceTrack> FilterKaraoke(IList<ServiceTrack> list)
        {
            List<ServiceTrack> tracks = new List<ServiceTrack>();

            foreach (var track in list)
            {
                if (!ContainsKaraoke(track.Name) && !ContainsKaraoke(track.Album))
                {
                    tracks.Add(track);
                }
            }

            return tracks;
        }

        private static bool ContainsKaraoke(string name)
        {
            string[] exclude = new string[] {"karaoke","in the style of","a tribute to"};
            foreach (var s in exclude)
            {
                if (name.IndexOf(s,StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        private IList<ServiceTrack> DoFindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null)
        {
            IList<ServiceTrack> tracks = null;
            switch (service.ID)
            {
                case ServiceType.Amazon:
                    tracks = FindMSSongAmazon(song, clean, title, artist);
                    break;
                default:
                    tracks = FindMSSongGeneral(song, service, clean, title, artist);
                    break;
            }

            if (tracks != null)
            {
                foreach (var track in tracks)
                {
                    track.AlbumLink = service.GetPurchaseLink(PurchaseType.Album, track.CollectionId, track.TrackId);
                    track.SongLink = service.GetPurchaseLink(PurchaseType.Song, track.CollectionId, track.TrackId);
                    track.PurchaseInfo = AlbumDetails.BuildPurchaseInfo(service.ID, track.CollectionId, track.TrackId);
                }
            }
            return tracks;
        }
        private IList<ServiceTrack> FindMSSongAmazon(SongDetails song, bool clean = false, string title = null, string artist = null)
        {
            if (_awsFetcher == null)
            {
                _awsFetcher = new AWSFetcher();
            }

            bool custom = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(artist);

            if (custom)
            {
                return _awsFetcher.FetchTracks(title, artist);
            }
            else
            {
                return _awsFetcher.FetchTracks(song, clean);
            }
        }

        private IList<ServiceTrack> FindMSSongGeneral(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null)
        {
            HttpWebRequest request = null;
            HttpWebResponse response = null;

            string responseString = null;

            // Make Music database request

            string req = service.BuildSearchRequest(artist, title);

            if (req == null)
            {
                return null;
            }

            request = (HttpWebRequest)WebRequest.Create(req);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";
            if (service.RequiresKey)
            {
                request.Headers.Add("Authorization", XboxAuthorization);
            }

            using (response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        responseString = sr.ReadToEnd();
                    }
                }
                else
                {
                    throw new WebException(response.StatusDescription);
                }
            }

            if (responseString != null)
            {
                responseString = service.PreprocessSearchResponse(responseString);
                dynamic results = System.Web.Helpers.Json.Decode(responseString);
                return service.ParseSearchResults(results);
            }
            else
            {
                return new List<ServiceTrack>();
            }
        }

        private static string XboxAuthorization
        {
            get
            {
                if (s_admAuth == null)
                {
                    string clientId = "music4dance";
                    string clientSecret = "iGvYm97JA+qYV1K2lvh8sAnL8Pebp5cN2KjvGnOD4gI=";

                    s_admAuth = new AdmAuthentication(clientId, clientSecret);

                }

                return "Bearer " + s_admAuth.GetAccessToken().access_token;
            }
        }

        private static AdmAuthentication s_admAuth = null;
        
        #endregion

        #region IUserMap
        public ApplicationUser FindUser(string name)
        {
            return Users.FirstOrDefault(u => u.UserName.ToLower() == name.ToLower());
        }
        public ModifiedRecord CreateMapping(Guid songId, string applicationId)
        {
            ModifiedRecord us = Modified.Create();
            us.ApplicationUserId = applicationId;
            us.SongId = songId;
            return us;
        }
        #endregion

        #region User

        public ApplicationUser FindOrAddUser(string name, string role)
        {
            var ustore = new UserStore<ApplicationUser>(this);
            var umanager = new UserManager<ApplicationUser>(ustore);

            var user = FindUser(name);
            if (user == null)
            {
                user = new ApplicationUser { UserName = name };
                umanager.Create(user, "_this_is_a_placeholder_");
            }

            if (string.Equals(role,PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else if (!umanager.IsInRole(user.Id, role))
            {
                umanager.AddToRole(user.Id, role);
            }

            return user;
        }

        
        #endregion

        #region Tags

        // Take a an arbitrary tag list, pull out categories, create tag types and then re-assemble withough the categories
        public string ParseTags(string editTags)
        {
            if (string.IsNullOrWhiteSpace(editTags))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            string sep = string.Empty;
            string[] values = editTags.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string value in values)
            {
                string v = value.Trim();
                string c = null;
                bool remove = false;
                if (v.Length > 0 && v[0] == '-')
                {
                    remove = true;
                    v = v.Substring(1);
                }

                if (v.Contains('='))
                {
                    string[] cells = v.Split(new char[] { '=' });
                    if (cells.Length == 2)
                    {
                        c = cells[0];
                        v = cells[1];
                    }
                    else 
                    {
                        Trace.WriteLine(string.Format("Bad Value for Tag: {0}",v));
                    }
                }

                if (!remove)
                {
                    FindOrCreateTagType(v,c);
                }

                sb.Append(sep);
                sep = "|";
                if (remove)
                {
                    sb.Append("-");
                }
                sb.Append(v);
            }

            return sb.ToString();
        }

        public Tag CreateTag(Song song, string value, int count)
        {
            TagType type = FindOrCreateTagType(value, null);

            Tag tag = Tags.Create();

            tag.Song = song;
            tag.SongId = song.SongId;

            tag.Value = value;
            tag.Type = type;

            tag.Count = count;

            song.AddTag(tag);

            return tag;
        }

        public TagType FindOrCreateTagType(string value, string categories)
        {
            TagType type = TagTypes.Find(value);

            if (type == null)
            {
                type = TagTypes.Create();
                type.Value = value;
                TagType added = TagTypes.Add(type);
                Trace.WriteLine(added.ToString());
            }
            type.AddCategory(categories);
            return type;
        }

        public IEnumerable<TagType> GetTypes(string category)
        {
            return TagTypes.Where(t => t.Value == category);
        }
        
        #endregion
        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(this, n, level);
        }
        public override int SaveChanges()
        {
            int ret = 0;
            try
            {
                ret = base.SaveChanges();
            }
            catch (DbEntityValidationException e)
            {
                foreach (var err in e.EntityValidationErrors)
                {
                    foreach (var ve in err.ValidationErrors)
                    {
                        Trace.WriteLine(ve.ErrorMessage);
                    }
                }

                Debug.Assert(false);
                throw;
            }

            return ret;
        }

        public IDictionary<string, IdentityRole> RoleDictionary
        {
            get
            {
                if (_roles == null)
                {
                    _roles = new Dictionary<string, IdentityRole>();

                    foreach (var role in Roles)
                    {
                        _roles.Add(role.Id, role);
                    }
                }
                return _roles;
            }
        }
        private IDictionary<string, IdentityRole> _roles = null;

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

        public static readonly string EditRole = "canEdit";
        public static readonly string DiagRole = "showDiagnostics";
        public static readonly string DbaRole = "dbAdmin";
        public static readonly string PseudoRole = "pseudoUser";

        AWSFetcher _awsFetcher;

    }
}