using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DanceLibrary;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace m4dModels
{
    public class DanceMusicService : IDisposable
    {
        #region Lifetime Management
        private readonly IDanceMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DanceMusicService(IDanceMusicContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public void Dispose()
        {
            _context.Dispose();
        }

        #endregion

        #region Properties

        public IDanceMusicContext Context { get { return _context; } }
        public DbSet<Song> Songs { get { return _context.Songs; } }
        public DbSet<SongProperty> SongProperties { get { return _context.SongProperties; } }
        public DbSet<Dance> Dances { get { return _context.Dances; } }
        public DbSet<DanceRating> DanceRatings { get { return _context.DanceRatings; } }
        public DbSet<Tag> Tags { get { return _context.Tags; } }
        public DbSet<TagType> TagTypes { get { return _context.TagTypes; } }
        public DbSet<SongLog> Log { get { return _context.Log; } }
        public DbSet<ModifiedRecord> Modified { get { return _context.Modified; } }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public static readonly string EditRole = "canEdit";
        public static readonly string DiagRole = "showDiagnostics";
        public static readonly string DbaRole = "dbAdmin";
        public static readonly string PseudoRole = "pseudoUser";

        #endregion

        #region Edit
        private Song CreateSong(Guid? guid = null, bool doLog = false)
        {
            if (guid == null || guid == Guid.Empty)
                guid = Guid.NewGuid();
            Song song = _context.Songs.Create();
            song.SongId = guid.Value;

            if (doLog)
                song.CurrentLog = _context.Log.Create();

            return song;
        }

        public Song CreateSong(ApplicationUser user, SongDetails sd, string command = SongBase.CreateCommand, string value = null, bool createLog = true)
        {
            Trace.WriteLineIf(string.Equals(sd.Title, sd.Artist),string.Format("Title and Artist are the same ({0})", sd.Title));

            var song = CreateSong(sd.SongId, createLog);
            song.Create(sd, user, command, value, this);

            song = _context.Songs.Add(song);
            if (createLog)
            {
                _context.Log.Add(song.CurrentLog);
            }

            return song;
        }

        public SongDetails EditSong(ApplicationUser user, SongDetails edit, List<string> addDances, List<string> remDances, string editTags, bool createLog = true)
        {
            Song song = _context.Songs.Find(edit.SongId);
            if (createLog)
            {
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);
            }

            // Null edit tags is semantically == don't change
            editTags = editTags == null ? song.UserTags(user,this).Summary : NormalizeTags(editTags, "Other");
            if (song.Edit(user, edit, addDances, remDances, editTags, this))
            {
                if (createLog)
                {
                    _context.Log.Add(song.CurrentLog);
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
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);
            }

            if (song.Update(user, edit, this))
            {
                if (createLog)
                {
                    _context.Log.Add(song.CurrentLog);
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
        public SongDetails AdditiveMerge(ApplicationUser user, Guid songId, SongDetails edit, List<string> addDances, bool createLog = true)
        {
            Song song = _context.Songs.Find(songId);
            if (createLog)
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);

            if (song.AdditiveMerge(user, edit, addDances, this))
            {
                if (song.CurrentLog != null)
                    _context.Log.Add(song.CurrentLog);
                SaveChanges();
                return FindSongDetails(songId);
            }
            else
            {
                return null;
            }
        }

        public void UpdateDances(ApplicationUser user, Song song, IEnumerable<DanceRatingDelta> deltas, bool doLog = true)
        {
            if (doLog)
            {
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);
            }

            song.CreateEditProperties(user, SongBase.EditCommand, this);
            song.EditDanceRatings(deltas, this);
        }

        public bool EditTags(ApplicationUser user, Guid songId, IEnumerable<UserTag> tags, bool doLog = true)
        {
            var song = _context.Songs.Find(songId);
            if (doLog)
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);

            if (!song.EditTags(user, tags, this)) return false;

            if (song.CurrentLog != null)
                _context.Log.Add(song.CurrentLog);
            SaveChanges();
            return true;
        }
        private List<AlbumDetails> MergeAlbums(IList<Song> songs, string def, HashSet<string> keys)
        {
            var details = songs.Select(s => new SongDetails(s)).ToList();
            var albumsIn = new List<AlbumDetails>();
            var albumsOut = new List<AlbumDetails>();

            foreach (var sd in details)
            {
                albumsIn.AddRange(sd.Albums);
            }

            var defIdx = -1;
            if (!string.IsNullOrWhiteSpace(def))
            {
                int.TryParse(def, out defIdx);
            }

            var idx = 0;
            if (defIdx >= 0 && albumsIn.Count > defIdx)
            {
                var t = albumsIn[defIdx];
                t.Index = 0;
                albumsOut.Add(t);
                idx = 1;
            }

            for (var i = 0; i < albumsIn.Count; i++)
            {
                if (i == defIdx) continue;

                string name = SongBase.AlbumListField + "_" + i.ToString();

                if (defIdx == -1 || keys.Contains(name))
                {
                    var t = albumsIn[i];
                    t.Index = idx;
                    albumsOut.Add(t);
                    idx += 1;
                }
            }

            return albumsOut;
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, List<AlbumDetails> albums)
        {
            var songIds = string.Join(";", songs.Select(s => s.SongId.ToString()));

            var sd = new SongDetails(title, artist, tempo, length, albums);

            var song = CreateSong(user, sd, SongBase.MergeCommand, songIds, true);
            song.CurrentLog.SongReference = song.SongId;
            song.CurrentLog.SongSignature = song.Signature;

            song = _context.Songs.Add(song);

            song.MergeDetails(songs, this);

            // Delete all of the old songs (With merge-with Id from above)
            foreach (var from in songs)
            {
                RemoveSong(from, user);
            }

            SaveChanges();

            SongCounts.ClearCache();

            return song;
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, string defAlbums, HashSet<string> keys)
        {
            return MergeSongs(user,songs,title,artist,tempo,length,MergeAlbums(songs, defAlbums, keys));
        }

        public void DeleteSong(ApplicationUser user, Song song, bool createLog=true)
        {
            if (createLog)
                LogSongCommand(SongBase.DeleteCommand, song, user);
            RemoveSong(song,user);
            SaveChanges();
        }

        private void RemoveSong(Song song, ApplicationUser user)
        {
            song.Delete(user, this);
        }
        #endregion

        #region Dance Ratings

        public DanceRating CreateDanceRating(Song song, string danceId, int weight)
        {
            Dance dance = _context.Dances.Find(danceId);

            if (dance == null)
            {
                return null;
            }

            DanceRating dr = _context.DanceRatings.Create();

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
            return CreateSongProperty(song, name, value, null, log);
        }
        public SongProperty CreateSongProperty(Song song, string name, object value, object old, SongLog log)
        {
            SongProperty ret = _context.SongProperties.Create();
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
                LogPropertyUpdate(ret, log,old==null?null:old.ToString());
            }

            _context.SongProperties.Add(ret);

            return ret;
        }

        private void DoRestoreValues(Song song, SongLog entry, UndoAction action)
        {
            // For scalar properties and albums just updating the property will
            //  provide the information for rebulding the song
            // For users, this is additive, so no need to do anything except with a new song
            // For DanceRatings and tags, we're going to update the song here since it is cummulative
            ApplicationUser currentUser = entry.User;
            foreach (LogValue lv in entry.GetValues())
            {
                if (!lv.IsAction)
                {
                    var np = _context.SongProperties.Create();
                    var baseName = lv.BaseName;

                    np.Song = song;
                    np.Name = lv.Name;

                    // This works for everything but Dancerating and Tags, which will be overwritten below
                    np.Value = action == UndoAction.Undo ? lv.Old : lv.Value;

                    if (lv.Name.Equals(SongBase.UserField))
                    {
                        currentUser = FindUser(lv.Value);
                        song.AddUser(currentUser, this);
                    }
                    else if (lv.Name.Equals(SongBase.DanceRatingField))
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
                    // For tags, we leave the list of tags in place and toggle the add/remove
                    else if (baseName.Equals(SongBase.AddedTags) || baseName.Equals(SongBase.RemovedTags))
                    {
                        np = null;
                        var add = (baseName.Equals(SongBase.AddedTags) && action == UndoAction.Redo) ||
                                   (baseName.Equals(SongBase.RemovedTags) && action == UndoAction.Undo);

                        if (add)
                            song.AddObjectTags(lv.DanceQualifier, lv.Value, currentUser, this);
                        else
                            song.RemoveObjectTags(lv.DanceQualifier, lv.Value, currentUser, this);
                    }

                    if (np != null)
                        song.SongProperties.Add(np);
                }
            }
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
            if (action.StartsWith(SongBase.RedoCommand))
            {
                int? idx = entry.GetIntData(SongBase.SuccessResult);

                action = SongBase.RedoCommand;

                if (idx.HasValue)
                {
                    SongLog uentry = _context.Log.Find(idx.Value);
                    int? idx2 = uentry.GetIntData(SongBase.SuccessResult);

                    if (idx2.HasValue)
                    {
                        SongLog rentry = _context.Log.Find(idx2.Value);

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
            string command = SongBase.UndoCommand + entry.Action;

            if (error == null)
            {
                if (action.StartsWith(SongBase.UndoCommand))
                {
                    int? idx = entry.GetIntData(SongBase.SuccessResult);
                    action = SongBase.UndoCommand;

                    if (idx.HasValue)
                    {
                        SongLog rentry = _context.Log.Find(idx.Value);

                        error = RedoEntry(rentry, song);
                        command = SongBase.RedoCommand + entry.Action.Substring(SongBase.UndoCommand.Length);
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
                    case SongBase.DeleteCommand:
                        error = Undelete(song,user);
                        break;
                    case SongBase.MergeCommand:
                        error = Unmerge(entry, song);
                        break;
                    case SongBase.EditCommand:
                        error = RestoreValuesFromLog(entry, song, UndoAction.Undo);
                        break;
                    case SongBase.CreateCommand:
                        RemoveSong(song,user);
                        break;
                    case SongBase.UndoCommand:
                    case SongBase.RedoCommand:
                        break;
                    default:
                        error = string.Format("'{0}' action not yet supported for Undo.", entry.Action);
                        break;
                }

            }

            log.UpdateData(error == null ? SongBase.SuccessResult : SongBase.FailResult, entry.Id.ToString());

            if (error != null)
            {
                log.UpdateData(SongBase.MessageData, error);
            }

            _context.Log.Add(log);
            // Have to save changes each time because
            // the may be cumulative (can we optimize by
            // doing a savechanges when a songId comes
            // up a second time?
            SaveChanges();

            return result;
        }

        private string Undelete(Song song, ApplicationUser user)
        {
            RestoreSong(song, user);
            return null;
        }

        private string Unmerge(SongLog entry, Song song)
        {
            string ret = null;

            // First restore the merged songs
            string t = entry.GetData(SongBase.MergeCommand);

            ICollection<Song> songs = SongsFromList(t);
            foreach (Song s in songs)
            {
                RestoreSong(s, entry.User);
            }

            // Now delete the merged song
            RemoveSong(song,entry.User);

            return ret;
        }

        private void RestoreFromLog(string line)
        {
            SongLog log = _context.Log.Create();

            if (!log.Initialize(line, this))
            {
                Trace.WriteLine(string.Format("Unable to restore line: {0}", line));
            }


            Song song = null;

            switch (log.Action)
            {
                case SongBase.DeleteCommand:
                case SongBase.EditCommand:
                    song = FindSong(log.SongReference, log.SongSignature);
                    break;
                case SongBase.MergeCommand:
                case SongBase.CreateCommand:
                    break;
                default:
                    Trace.WriteLine(string.Format("Bad Command: {0}", log.Action));
                    return;
            }

            switch (log.Action)
            {
                case SongBase.DeleteCommand:
                    RemoveSong(song,log.User);
                    break;
                case SongBase.EditCommand:
                    RestoreValuesFromLog(log, song, UndoAction.Redo);
                    break;
                case SongBase.MergeCommand:
                case SongBase.CreateCommand:
                    CreateSongFromLog(log);
                    break;
                default:
                    Trace.WriteLine(string.Format("Bad Command: {0}", log.Action));
                    break;
            }

            _context.Log.Add(log);
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
                case SongBase.DeleteCommand:
                    RemoveSong(song,entry.User);
                    break;
                case SongBase.MergeCommand:
                    error = Remerge(entry, song, entry.User);
                    break;
                case SongBase.EditCommand:
                    error = RestoreValuesFromLog(entry, song, UndoAction.Redo);
                    break;
                case SongBase.CreateCommand:
                    RestoreSong(song,entry.User);
                    break;
                default:
                    error = string.Format("'{0}' action not yet supported for Redo.", entry.Action);
                    break;
            }

            return error;
        }

        private void LogSongCommand(string command, Song song, ApplicationUser user, bool includeSignature = true)
        {
            SongLog log = _context.Log.Create();
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

            _context.Log.Add(log);
        }

        private string RestoreValuesFromLog(SongLog entry, Song song, UndoAction action)
        {
            song.CreateEditProperties(entry.User,(action == UndoAction.Undo) ? SongBase.UndoCommand : SongBase.RedoCommand,this);
            DoRestoreValues(song, entry, action);

            var sd = new SongDetails(song.SongId, song.SongProperties);
            song.RestoreScalar(sd);
            song.UpdateUsers(this);

            return null;
        }

        private string Remerge(SongLog entry, Song song, ApplicationUser user)
        {
            string ret = null;

            // First, restore the merged to song
            RestoreSong(song,user);

            // Then remove the merged from songs
            string t = entry.GetData(SongBase.MergeCommand);
            ICollection<Song> songs = SongsFromList(t);
            foreach (Song s in songs)
            {
                RemoveSong(s,user);
            }

            return ret;
        }

        private void RestoreSong(Song song, ApplicationUser user)
        {
            if (!string.IsNullOrWhiteSpace(song.Title))
            {
                throw new ArgumentOutOfRangeException("song", "Attempting to restore a song that hasn't been deleted");
            }
            song.CreateEditProperties(user, SongBase.DeleteCommand + "=false", this);
            SongDetails sd = new SongDetails(song.SongId, song.SongProperties);
            song.Restore(sd, this);
            song.UpdateUsers(this);
        }

        private SongLog CreateSongLog(ApplicationUser user, Song song, string action)
        {
            SongLog log = _context.Log.Create();

            log.Initialize(user, song, action);

            return log;
        }

        private void CreateSongFromLog(SongLog log)
        {
            string initV = log.GetData(SongBase.MergeCommand);

            // For merge case, first we delete the old songs
            if (initV != null)
            {
                try
                {
                    foreach (Song d in SongsFromList(initV))
                    {
                        RemoveSong(d,log.User);
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

            DoRestoreValues(song, log, UndoAction.Redo);

            RestoreSong(song,log.User);
            _context.Songs.Add(song);
        }
        #endregion

        #region Song Lookup
        public Song FindSong(Guid id, string signature = null)
        {
            // First find a match id
            //Song song = _context.Songs.Find(id);
            var song = _context.Songs.Where(s => s.SongId == id).Include("DanceRatings").Include("ModifiedBy").Include("SongProperties").FirstOrDefault();

            // TODO: Think about signature mis-matches, we can't do the straighforward fail on mis-match because
            //  we're using this for edit and it's perfectly reasonable to edit parts of the sig...
            // || !(string.IsNullOrWhiteSpace(signature) || song.IsNull || MatchSigatures(signature,song.Signature))
            if (song == null && signature != null)
            {
                song = FindSongBySignature(signature);
            }

            if (song == null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("Couldn't find song by Id: {0} or signature {1}", id, signature));
            }

            return song;
        }

        public SongDetails FindSongDetails(Guid id, bool forSerialization = false, string userName = null)
        {
            var song = FindSong(id);

            if (song == null)
                return null;

            var sd = new SongDetails(song);

            if (userName != null)
            {
                var user = FindUser(userName);
                if (user != null)
                {
                    sd.SetCurrentUserTags(user, this);
                }

                if (forSerialization)
                {
                    sd.SetupSerialization(user,this);
                }
            }

            return sd;
        }

        private Song FindSongBySignature(string signature)
        {
            Song song = _context.Songs.FirstOrDefault(s => s.Signature == signature);

            return song;
        }

        private bool MatchSigatures(string sig1, string sig2)
        {
            return string.Equals(sig1, sig2, StringComparison.Ordinal);
        }

        private ICollection<Song> SongsFromList(string list)
        {
            string[] dels = list.Split(';');
            List<Song> songs = new List<Song>(list.Length);

            foreach (string t in dels)
            {
                Guid idx;
                if (Guid.TryParse(t, out idx))
                {
                    Song s = _context.Songs.Find(idx);
                    if (s != null)
                    {
                        songs.Add(s);
                    }
                }
            }

            return songs;
        }
        #endregion

        #region Searching

        [Flags]
        public enum CruftFilter
        {
            NoCruft = 0x00,
            NoPublishers = 0x01,
            NoDances =0x02,
            AllCruft = 0x03
        };

        public  IQueryable<Song> BuildSongList(SongFilter filter, CruftFilter cruft=CruftFilter.NoCruft)
        {
            // Now setup the view
            // Start with all of the songs in the database
            var songs = from s in Songs where s.TitleHash != 0 select s;

#if TRACE
            bool traceVerbose = TraceLevels.General.TraceVerbose;
            int count;
            int lastCount = 0;
            if (traceVerbose)
            {
                count = lastCount = songs.Count();
                Trace.WriteLine(string.Format("Total Songs = {0}", count));
            }
#endif

            // Now if the current user is anonymous, filter out anything that we
            //  don't have purchase info for
            if ((cruft & CruftFilter.NoPublishers) != CruftFilter.NoPublishers)
            {
                songs = songs.Where(s => s.Purchase != null);
            }

            // Filter by user first since we have a nice key to pull from
            // TODO: This ends up going down a completely different LINQ path
            //  that is requiring some special casing further along, need
            //  to dig into how to manage that better...
            bool userFilter = false;
            if (!string.IsNullOrWhiteSpace(filter.User))
            {
                ApplicationUser user = FindUser(filter.User);
                if (user != null)
                {
                    songs = from m in Modified where m.ApplicationUserId == user.Id && m.Song.TitleHash != 0 select m.Song;
                    userFilter = true;
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs per user = {0}", songs.Count()));
                lastCount = count;
            }
#endif
            // Now limit it down to the ones that are marked as a particular dance or dances
            string[] danceList = null;
            if (!string.IsNullOrWhiteSpace(filter.Dances) && !string.Equals(filter.Dances, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: We don't need to expand anything except MSC - should we just special case MSC
                // and kill this code path...
                danceList = Dance.DanceLibrary.ExpandDanceList(filter.Dances);

                songs = songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            }
            else if ((cruft & CruftFilter.NoDances) != CruftFilter.NoDances)
            {
                songs = songs.Where(s => s.DanceRatings.Any());
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by dance = {0}", songs.Count()));
                lastCount = count;
            }
#endif

            // Now limit it by tempo
            if (filter.TempoMin.HasValue)
            {
                songs = songs.Where(s => (s.Tempo >= filter.TempoMin));
            }
            if (filter.TempoMax.HasValue)
            {
                songs = songs.Where(s => (s.Tempo <= filter.TempoMax));
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by tempo = {0}", songs.Count()));
                lastCount = count;
            }
#endif

            // Now limit it by anything that has the serach string in the title, album or artist
            if (!String.IsNullOrEmpty(filter.SearchString))
            {
                if (userFilter)
                {
                    string str = filter.SearchString.ToUpper();
                    songs = songs.Where(
                        s => (s.Title != null && s.Title.ToUpper().Contains(str)) ||
                        (s.Album != null && s.Album.Contains(str)) ||
                        (s.Artist != null && s.Artist.Contains(str)));
                        //(s.TagSummary != null && s.TagSummary.Contains(str)));
                }
                else
                {
                    songs = songs.Where(
                        s => s.Title.Contains(filter.SearchString) ||
                        s.Album.Contains(filter.SearchString) ||
                        s.Artist.Contains(filter.SearchString));
                        //s.TagSummary.Contains(filter.SearchString));
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by search = {0}", songs.Count()));
                lastCount = count;
            }
#endif

            // Filter on purcahse info
            // TODO: Figure out how to get LINQ to do the permutation on contains
            //  any of "AIX" in a database safe way - right now I'm doing this
            //  last because I'm pulling things into memory to do the union.
            //if (!string.IsNullOrWhiteSpace(filter.Purchase))
            //{
            //    char[] services = filter.Purchase.ToCharArray();

            //    string c = services[0].ToString();
            //    var acc = songs.Where(a => a.Purchase.Contains(c));
            //    string accTag = c;

            //    DumpSongs(acc, c);
            //    for (int i = 1; i < services.Length; i++)
            //    {
            //        c = services[i].ToString();
            //        IEnumerable<Song> first = acc.AsEnumerable();
            //        var acc2 = songs.Where(a => a.Purchase.Contains(c));
            //        DumpSongs(acc2, c);
            //        IEnumerable<Song> second = acc2.AsEnumerable();
            //        acc = first.Union(second).AsQueryable();
            //        //acc = acc.Union(acc2);
            //        accTag = accTag + "+" + c;
            //        DumpSongs(acc, accTag);
            //    }
            //    songs = acc;
            //}

            // TODO: There has to be a better way to filter based on available
            //  service - what I want to do is ask if a particular string contains
            //  any character from a different string within a the context
            //  of a Linq EF statement, but I can't figure that out.
            if (!string.IsNullOrWhiteSpace(filter.Purchase))
            {
                bool not = false;
                string purch = filter.Purchase;
                if (purch.StartsWith("!"))
                {
                    not = true;
                    purch = purch.Substring(1);
                }

                char[] services = purch.ToCharArray();
                if (services.Length == 1)
                {
                    string c = services[0].ToString();
                    songs = not ? songs.Where(s => s.Purchase == null || !s.Purchase.Contains(c)) : songs.Where(s => s.Purchase != null && s.Purchase.Contains(c));
                }
                else if (services.Length == 2)
                {
                    string c0 = services[0].ToString();
                    string c1 = services[1].ToString();

                    songs = not ? songs.Where(s => s.Purchase == null || (!s.Purchase.Contains(c0) && !s.Purchase.Contains(c1))) : songs.Where(s => s.Purchase != null && (s.Purchase.Contains(c0) || s.Purchase.Contains(c1)));

                }
                else // Better == 3
                {
                    songs = not ? songs.Where(s => s.Purchase == null) : songs.Where(s => s.Purchase != null);
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by purchase = {0}", songs.Count()));
            }
#endif

            SongSort songSort = new SongSort(filter.SortOrder);

            switch (songSort.Id)
            {
                case "Title":
                default:
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Title) : songs.OrderBy(s => s.Title);
                    break;
                case "Artist":
                    songs = songs.Where(s => s.Title != null);
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Artist) : songs.OrderBy(s => s.Artist);
                    break;
                case "Album":
                    songs = songs.Where(s => s.Album != null);
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Album) : songs.OrderBy(s => s.Album);
                    break;
                case "Tempo":
                    songs = songs.Where(s => s.Tempo != null);
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Tempo) : songs.OrderBy(s => s.Tempo);
                    break;
                case "Dances":
                    // TODO: Better icon for dance order
                    // TODO: Get this working for multi-dance selection
                    {
                        string did = TrySingleId(danceList) ?? TrySingleId(new[] {filter.Dances});
                        if (did != null)
                        {
                            //DanceRating drE = new DanceRating() { Weight = 0 };
                            songs = songs.OrderByDescending(s => s.DanceRatings.FirstOrDefault(dr => dr.DanceId.StartsWith(did)).Weight);
                        }
                    }
                    break;
                case "Modified":
                    songs = songs.OrderBy(s => s.Modified);
                    break;
                case "Created":
                    songs = songs.OrderBy(s => s.Created);
                    break;
            }

            // Then take the top n songs if
            if (songSort.Count != -1)
            {
                songs = songs.Take(10);
            }

            return songs.Include("DanceRatings").Include("ModifiedBy").Include("SongProperties");
        }

        public string GetPurchaseInfo(ServiceType serviceType, IEnumerable<Song> songs)
        {
            var sb = new StringBuilder();
            var sep = "";
            foreach (var song in songs)
            {
                if (song.Purchase == null || !song.Purchase.Contains('S')) continue;

                var sd = new SongDetails(song);
                var id = sd.GetPurchaseId(ServiceType.Spotify);
                sb.Append(sep);
                sb.Append(id);
                sep = ",";
            }

            return sb.ToString();
        }

        // TODO: This is extremely dependent on the form of the danceIds, just
        //  a temporary kludge until we get multi-select working
        private static string TrySingleId(string[] danceList)
        {
            string ret = null;
            if (danceList != null && danceList.Length > 0)
            {
                ret = danceList[0].Substring(0, 3).ToUpper();
                for (int i = 1; i < danceList.Length; i++)
                {
                    if (!string.Equals(ret, danceList[i].Substring(0, 3),StringComparison.OrdinalIgnoreCase))
                    {
                        ret = null;
                        break;
                    }
                }
            }

            return ret;
        }

        #endregion

        #region Tags

        public TagType FindOrCreateTagType(string tag)
        {
            // Create a transitory TagType just for the parsing
            TagType temp = new TagType(tag);

            return FindOrCreateTagType(temp.Value, temp.Category);
        }

        public TagType FindOrCreateTagType(string value, string category, string primary = null)
        {            
            return _context.TagTypes.Find(TagType.BuildKey(value, category)) ?? CreateTagType(value, category);
        }

        // TAGDELETE: I think we can get rid of this because is seems easier just to keep
        //  verbose normalized tags around (looks like a case of over eager optimization)
        public string CompressTags(string tags, string category)
        {
            var old = new TagList(tags);
            var result = new List<string>();
            foreach (var tag in old.Tags)
            {
                List<TagType> types = null;
                TagType type = null;

                types = TagTypes.Where(tt => tt.Value == tag).ToList();
                type = types.FirstOrDefault(tt => tt.Category == category);
                var count = types.Count;

                if (type == null)
                {
                    type = CreateTagType(tag, category);
                    count += 1;
                }

                result.Add(count > 1 ? type.ToString() : tag);
            }

            return new TagList(result).ToString();
        }

        // Add in category for tags that don't already have one + create
        //  tagtype if necessary
        public string NormalizeTags(string tags, string category)
        {
            TagList old = new TagList(tags);
            List<string> result = new List<string>();
            foreach (string tag in old.Tags)
            {
                string fullTag = tag;
                string tempTag = tag;
                string tempCat = category;
                string[] rg = tag.Split(':');
                if (rg.Length == 1)
                {
                    fullTag = TagType.BuildKey(tag, category);
                }
                else if (rg.Length == 2)
                {
                    tempTag = rg[0];
                    tempCat = rg[1];
                }

                FindOrCreateTagType(tempTag, tempCat);

                result.Add(fullTag);
            }

            return new TagList(result).ToString();
        }

        public IEnumerable<TagType> GetTagTypes(string value)
        {
            return _context.TagTypes.Where(t => t.Key.StartsWith(value + ":"));
        }

        public ICollection<TagType> GetTagRings(TagList tags)
        {
            Dictionary<string, TagType> map = new Dictionary<string, TagType>();

            foreach (var tag in tags.Tags)
            {
                TagType tt = TagTypes.Find(tag);
                while (tt.Primary != null)
                {
                    tt = tt.Primary;
                }
                if (!map.ContainsKey(tt.Key))
                {
                    map.Add(tt.Key, tt);
                }
            }

            return map.Values;
        }

        private TagType CreateTagType(string value, string category, string primary = null) 
        {
            TagType type = null;

            type = _context.TagTypes.Create();
            type.Key = TagType.BuildKey(value, category);
            type.PrimaryId = primary;
            type = _context.TagTypes.Add(type);

            return type;
        }

        #endregion

        #region Load

        private const string _songBreak = "+++++SONGS+++++";
        private const string _tagBreak = "+++++TAGSS+++++";
        private const string _danceBreak = "+++++DANCES+++++";
        private const string _userHeader = "UserId\tUserName\tRoles\tPWHash\tSecStamp\tLockout\tProviders";

        static public bool IsSongBreak(string line) {
            return IsBreak(line, _songBreak);
        }
        static public bool IsTagBreak(string line)
        {
            return IsBreak(line, _tagBreak);
        }
        static public bool IsUserBreak(string line)
        {
            return IsBreak(line, _userHeader);
        }
        static public bool IsDanceBreak(string line)
        {
            return IsBreak(line, _danceBreak);
        }

        static private bool IsBreak(string line, string brk)
        {
            return string.Equals(line.Trim(), brk, StringComparison.InvariantCultureIgnoreCase);
        }

        public void LoadUsers(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Entering LoadUsers");

            if (lines == null || lines.Count < 1 || !string.Equals(lines[0], _userHeader, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentOutOfRangeException();
            }

            int fieldCount = _userHeader.Split('\t').Length;
            int i = 1;
            while (i < lines.Count)
            {
                string s = lines[i];
                i += 1;

                if (string.Equals(s, _tagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                string[] cells = s.Split('\t');
                if (cells.Length == fieldCount)
                {
                    string userId = cells[0];
                    string userName = cells[1];
                    string roles = cells[2];
                    string hash = string.IsNullOrWhiteSpace(cells[3]) ? null : cells[3];
                    string stamp = cells[4];
                    string lockout = cells[5];
                    string providers = cells[6];

                    // Don't trounce existing users
                    ApplicationUser user = FindUser(userName);
                    if (user == null)
                    {
                        user = _context.Users.Create();
                        user.Id = userId;
                        user.UserName = userName;
                        user.PasswordHash = hash;
                        user.SecurityStamp = stamp;
                        user.LockoutEnabled = string.Equals(lockout, "TRUE", StringComparison.InvariantCultureIgnoreCase);

                        if (!string.IsNullOrWhiteSpace(providers))
                        {
                            string[] entries = providers.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 0; j < entries.Length; j += 2)
                            {
                                IdentityUserLogin login = new IdentityUserLogin() { LoginProvider = entries[j], ProviderKey = entries[j + 1], UserId = userId };
                                user.Logins.Add(login);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(roles))
                        {
                            string[] roleNames = roles.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string roleName in roleNames)
                            {
                                IdentityRole role = Context.Roles.FirstOrDefault(r => r.Name == roleName.Trim());
                                if (role != null)
                                {
                                    IdentityUserRole iur = new IdentityUserRole() { UserId = user.Id, RoleId = role.Id };
                                    user.Roles.Add(iur);
                                }
                            }
                        }

                        Context.Users.Add(user);
                    }
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Saving Changes");
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadUsers");
        }
        public void LoadDances(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering Dances");

            LoadDances();
            bool modified = false;
            foreach (string s in lines)
            {
                if (string.IsNullOrWhiteSpace(s))
                    continue;
                List<string> cells = s.Split('\t').ToList();
                Dance d = Dances.Find(cells[0]);
                if (d != null)
                {
                    cells.RemoveAt(0);
                    modified |= d.Update(cells);
                }
                else
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceError, string.Format("Bad Dance: {0}",s));
                }
            }

            if (modified)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
                SaveChanges();
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting Dances");
        }

        public void LoadTags(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadTags");

            foreach (string s in lines)
            {
                string[] cells = s.Split('\t');
                TagType tt = null;
                if (cells.Length >= 2)
                {
                    string category = cells[0];
                    string value = cells[1];

                    tt = FindOrCreateTagType(value, category);
                }

                if (tt != null && cells.Length >= 3 && !string.IsNullOrWhiteSpace(cells[2]))
                {
                    tt.PrimaryId = cells[2];
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            SaveChanges();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadTags");
        }
        public void LoadSongs(IList<string> lines)
        {
            _context.TrackChanges(false);

            // Load the dance List
            LoadDances();

            //Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            int c = 0;
            foreach (string line in lines)
            {
                DateTime time = DateTime.Now;
                Song song = new Song {Created = time, Modified = time};

                song.Load(line, this);
                _context.Songs.Add(song);
                
                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, string.Format("{0} songs loaded", c));
                }

                if (c % 1000 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving next 1000 songs");
                    _context.CheckpointSongs();
                }
            }

            _context.TrackChanges(true);
        }

        public void UpdateSongs(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering UpdateSongs");

            _context.TrackChanges(false);

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Dances");
            LoadDances();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            int c = 0;
            foreach (string line in lines)
            {
                SongDetails sd = new SongDetails(line);
                Song song = FindSong(sd.SongId);

                if (song == null)
                {
                    SongProperty up = sd.FirstProperty(SongBase.UserField);
                    ApplicationUser user = FindOrAddUser(up != null ? up.Value as string : "batch", EditRole);

                    song = CreateSong(sd.SongId);
                    UpdateSong(user, song, sd, false);
                    Songs.Add(song);
                }
                else
                {
                    SongProperty up = sd.LastProperty(SongBase.UserField);
                    ApplicationUser user = FindOrAddUser(up != null ? up.Value as string : "batch", EditRole);
                    if (sd.IsNull)
                    {
                        DeleteSong(user, song, false);
                    }
                    else
                    {
                        UpdateSong(user, song, sd, false);
                    }
                }

                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, string.Format("{0} songs updated", c));
                }
            }

            _context.TrackChanges(true);
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
            SongCounts.ClearCache();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting UpdateSongs");

        }

        public void SeedDances()
        {
            Dances dances = DanceLibrary.Dances.Instance;
            foreach (DanceObject d in dances.AllDances)
            {
                Dance dance = _context.Dances.Find(d.Id);
                if (dance == null)
                {
                    dance = new Dance { Id = d.Id };
                    _context.Dances.Add(dance);
                }
            }

        }
        private void LoadDances()
        {
            Dances.Include("DanceLinks").Load();
        }
        #endregion

        #region Save
        public IList<string> SerializeUsers(bool withHeader=true)
        {
            List<string> users = new List<string>();

            if (withHeader)
            {
                users.Add(_userHeader);
            }
            
            foreach (var user in _userManager.Users)
            {
                string userId = user.Id;
                string username = user.UserName;
                string roles = string.Join("|", _userManager.GetRoles(user.Id));
                string hash = user.PasswordHash;
                string stamp = user.SecurityStamp;
                string lockout = user.LockoutEnabled.ToString();
                string providers = string.Join("|", _userManager.GetLogins(user.Id));

                users.Add(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", userId, username, roles, hash, stamp, lockout, providers));
            }

            return users;
        }

        public IList<string> SerializeTags(bool withHeader = true)
        {
            var tags = new List<string>();

            if (withHeader)
            {
                tags.Add(_tagBreak);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var tt in TagTypes)
            {
                tags.Add(string.Format("{0}\t{1}\t{2}", tt.Category, tt.Value, tt.PrimaryId));
            }

            return tags;
        }
        public IList<string> SerializeSongs(bool withHeader = true, bool withHistory = true, int max = -1)
        {
            List<string> songs = new List<string>();

            if (withHeader)
            {
                songs.Add(_songBreak);
            }

            var songlist = Songs.OrderByDescending(t => t.Modified).ThenByDescending(t => t.SongId);
            if (max != -1)
            {
                songlist = songlist.Take(max) as IOrderedQueryable<Song>;
            }

            string[] actions = null;
            List<string> alist = new List<string>();
            if (!withHistory)
            {
                alist.Add(SongBase.FailedLookup);
            }
            if (max != -1)
            {
                alist.Add(SongBase.SerializeDeleted);
            }
            if (alist.Count > 0)
            {
                actions = alist.ToArray();
            }

            foreach (Song song in songlist)
            {
                string line = song.Serialize(actions);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    songs.Add(line);
                }
            }
            //songs.AddRange(songlist.Select(song => song.Serialize(actions)).Where(line => !string.IsNullOrWhiteSpace(line)));

            return songs;
        }

        public IList<string> SerializeDances(bool withHeader = true)
        {
            List<string> songs = new List<string>();

            if (withHeader)
            {
                songs.Add(_danceBreak);
            }

            var dancelist = Dances.OrderBy(d => d.Id);
            foreach (Dance dance in dancelist)
            {
                string line = dance.Serialize();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    songs.Add(line);
                }
            }

            return songs;
        }

        #endregion

        #region User
        public ApplicationUser FindUser(string name)
        {
            return _userManager.FindByName(name);
        }
        public ApplicationUser FindOrAddUser(string name, string role)
        {
            var user = _userManager.FindByName(name);

            if (user == null)
            {
                user = new ApplicationUser { UserName = name, Email = name + "@music4dance.net", EmailConfirmed = true, StartDate = DateTime.Now };
                var res = _userManager.Create(user, "_This_Is_@_placeh0lder_");
                if (res.Succeeded)
                {
                    var user2 = _userManager.FindByName(name);
                    Trace.WriteLine(string.Format("{0}:{1}", user2.UserName, user2.Id));
                }

            }

            if (string.Equals(role, PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else if (!_userManager.IsInRole(user.Id, role))
            {
                _userManager.AddToRole(user.Id, role);
            }

            return user;
        }

        public ModifiedRecord CreateModified(Guid songId, string applicationId)
        {
            ModifiedRecord us = _context.Modified.Create();
            us.ApplicationUserId = applicationId;
            us.SongId = songId;
            return us;
        }
        #endregion
        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(_context, n, level);
        }
    }
}
