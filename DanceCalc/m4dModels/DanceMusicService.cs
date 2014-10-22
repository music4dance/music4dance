using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public enum UndoAction { Undo, Redo };
    public class DanceMusicService : IDisposable
    {
        #region Lifetime Management
        private IDanceMusicContext _context;

        public DanceMusicService(IDanceMusicContext context)
        {
            _context = context;
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
            Guid g = guid ?? Guid.NewGuid();
            Song song = _context.Songs.Create();
            song.SongId = g;

            if (doLog)
            {
                song.CurrentLog = _context.Log.Create();
            }

            return song;
        }
        public Song CreateSong(ApplicationUser user, SongDetails sd, string command = SongBase.CreateCommand, string value = null, bool createLog = true)
        {
            if (string.Equals(sd.Title, sd.Artist))
            {
                Trace.WriteLine(string.Format("Title and Artist are the same ({0})", sd.Title));
            }

            Song song = CreateSong(null, createLog);
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
                song.CurrentLog = CreateSongLog(user, song, Song.EditCommand);
            }

            if (song.Edit(user, edit, addDances, remDances, ParseTags(editTags), this))
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
                song.CurrentLog = CreateSongLog(user, song, Song.EditCommand);
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
        public SongDetails AdditiveMerge(ApplicationUser user, Guid songId, SongDetails edit, List<string> addDances)
        {
            Song song = _context.Songs.Find(songId);
            song.CurrentLog = CreateSongLog(user, song, Song.EditCommand);

            if (song.AdditiveMerge(user, edit, addDances, this))
            {
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
                song.CurrentLog = CreateSongLog(user, song, Song.EditCommand);
            }

            song.CreateEditProperties(user, Song.EditCommand, this);
            song.EditDanceRatings(deltas, this);
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, string tags, List<AlbumDetails> albums)
        {
            string songIds = string.Join(";", songs.Select(s => s.SongId.ToString()));

            SongDetails sd = new SongDetails(title, artist, tempo, length, albums);
            sd.AddTags(tags);

            Song song = CreateSong(user, sd, Song.MergeCommand, songIds, true);
            song.CurrentLog.SongReference = song.SongId;
            song.CurrentLog.SongSignature = song.Signature;

            song = _context.Songs.Add(song);

            song.MergeDetails(songs, this);

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
                LogPropertyUpdate(ret, log);
            }

            _context.SongProperties.Add(ret);

            return ret;
        }


        private void RestoreSongProperty(Song song, LogValue lv, UndoAction action)
        {
            // For scalar properties and albums just updating the property will
            //  provide the information for rebulding the song
            // For users, this is additive, so no need to do anything except with a new song
            // For DanceRatings, we're going to update the song here
            //  since it is cummulative

            SongProperty np = _context.SongProperties.Create();

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
                    SongLog uentry = _context.Log.Find(idx.Value);
                    int? idx2 = uentry.GetIntData(Song.SuccessResult);

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
            string command = Song.UndoCommand + entry.Action;

            if (error == null)
            {
                if (action.StartsWith(Song.UndoCommand))
                {
                    int? idx = entry.GetIntData(Song.SuccessResult);
                    action = Song.UndoCommand;

                    if (idx.HasValue)
                    {
                        SongLog rentry = _context.Log.Find(idx.Value);

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

            _context.Log.Add(log);
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
            SongLog log = _context.Log.Create();

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
            string ret = null;

            song.CreateEditProperties(entry.User, Song.EditCommand, this);

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
            _context.Songs.Add(song);
        }
        #endregion

        #region Song Lookup
        public Song FindSong(Guid id, string signature = null)
        {
            // First find a match id
            Song song = _context.Songs.Find(id);

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

        public SongDetails FindSongDetails(Guid id)
        {
            SongDetails sd = null;

            Song song = _context.Songs.Find(id);

            if (song != null)
                sd = new SongDetails(song);

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
            string[] dels = list.Split(new char[] { ';' });
            List<Song> songs = new List<Song>(list.Length);

            for (int i = 0; i < dels.Length; i++)
            {
                Guid idx = Guid.Empty;
                if (Guid.TryParse(dels[i], out idx))
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
                        Trace.WriteLine(string.Format("Bad Value for Tag: {0}", v));
                    }
                }

                if (!remove)
                {
                    FindOrCreateTagType(v, c);
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

            Tag tag = _context.Tags.Create();

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
            TagType type = _context.TagTypes.Find(value);

            if (type == null)
            {
                type = _context.TagTypes.Create();
                type.Value = value;
                TagType added = _context.TagTypes.Add(type);
                Trace.WriteLine(added.ToString());
            }
            type.AddCategory(categories);
            return type;
        }

        public IEnumerable<TagType> GetTypes(string category)
        {
            return _context.TagTypes.Where(t => t.Value == category);
        }

        #endregion

        #region Load

        private const string _songBreak = "+++++SONGS+++++";
        private const string _tagBreak = "+++++TAGSS+++++";
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

            int fieldCount = _userHeader.Split(new char[] { '\t' }).Length;
            int i = 1;
            while (i < lines.Count)
            {
                string s = lines[i];
                i += 1;

                if (string.Equals(s, _tagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                string[] cells = s.Split(new char[] { '\t' });
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
                            string[] entries = providers.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 0; j < entries.Length; j += 2)
                            {
                                IdentityUserLogin login = new IdentityUserLogin() { LoginProvider = entries[j], ProviderKey = entries[j + 1], UserId = userId };
                                user.Logins.Add(login);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(roles))
                        {
                            string[] roleNames = roles.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
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
        public void LoadTags(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadTags");

            int i = 0;
            while (i < lines.Count)
            {
                string s = lines[i];
                i += 1;

                if (string.Equals(s, _songBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                string[] cells = s.Split(new char[] { '\t' });
                if (cells.Length == 2)
                {
                    string category = cells[0];
                    string value = cells[1];

                    FindOrCreateTagType(value, category);
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            SaveChanges();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadTags");
        }
        public void LoadSongs(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering Load Songs");

            _context.TrackChanges(false);

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Dances");
            Dances.Load();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            int c = 0;
            foreach (string line in lines)
            {
                DateTime time = DateTime.Now;
                Song song = new Song();
                song.Created = time;
                song.Modified = time;

                song.Load(line, this);
                _context.Songs.Add(song);
                
                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, string.Format("{0} songs loaded", c));
                }

                Trace.WriteLineIf(TraceLevels.General.TraceInfo && song.Length.HasValue && song.Length.Value > 1000, string.Format("Long Song: {0} '{1}'", song.Length, song.Title));

                if (c % 1000 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving next 1000 songs");
                    _context.TrackChanges(true);
                    _context.TrackChanges(false);
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Songs");
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving tail");
            _context.TrackChanges(true);

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
            SongCounts.ClearCache();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadSongs");

        }

        public void UpdateSongs(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering UpdateSongs");

            _context.TrackChanges(false);

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Dances");
            Dances.Load();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            foreach (string line in lines)
            {
                SongDetails sd = new SongDetails(line);
                Song song = FindSong(sd.SongId);

                string name = sd.ModifiedList.Last().UserName;
                ApplicationUser user = FindOrAddUser(name, DanceMusicService.EditRole);

                if (song == null)
                {
                    song = CreateSong(user, sd);
                }
                else if (sd.IsNull)
                {
                    DeleteSong(user, song);
                }
                else
                {
                    UpdateSong(user, song, sd, false);
                }
                //song.Modified = DateTime.Now;
            }

            _context.TrackChanges(true);
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
            SongCounts.ClearCache();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting UpdateSongs");

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

            foreach (ApplicationUser user in _context.Users)
            {
                string userId = user.Id;
                string username = user.UserName;
                string roles = user.GetRoles(Context.Roles, "|");
                string hash = user.PasswordHash;
                string stamp = user.SecurityStamp;
                string lockout = user.LockoutEnabled.ToString();
                string providers = user.GetProviders();

                users.Add(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", userId, username, roles, hash, stamp, lockout, providers));
            }

            return users;
        }

        public IList<string> SerializeTags(bool withHeader = true)
        {
            List<string> tags = new List<string>();

            if (withHeader)
            {
                tags.Add(_tagBreak);
            }
            foreach (TagType tt in TagTypes)
            {
                tags.Add(string.Format("{0}\t{1}", tt.Categories, tt.Value));
            }

            return tags;
        }
        public IList<string> SerializeSongs(bool withHeader = true, bool withHistory = true)
        {
            List<string> songs = new List<string>();

            if (withHeader)
            {
                songs.Add(_songBreak);
            }

            var songlist = Songs.OrderBy(t => t.Modified).ThenBy(t => t.SongId);
            foreach (Song song in songlist)
            {
                string[] actions = null;
                if (withHistory)
                {
                    actions = new string[] { Song.FailedLookup };
                }
                string line = song.Serialize(actions);
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
            return _context.Users.FirstOrDefault(u => u.UserName.ToLower() == name.ToLower());
        }

        public ApplicationUser FindOrAddUser(string name, string role)
        {
            return _context.FindOrAddUser(name, role);
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
        public void Dump()
        {
            // TODO: Create a dump routine to help dump the object graph - definitely need object id of some kind (address)

            Trace.WriteLine("------------------- songs ------------------");
            foreach (Song song in _context.Songs.Local)
            {
                song.Dump();
            }

            Trace.WriteLine("------------------- properties ------------------");
            foreach (SongProperty prop in _context.SongProperties.Local)
            {
                prop.Dump();
            }

            //Trace.WriteLine("------------------- users ------------------");
            //foreach (ApplicationUser user in Users.Local)
            //{
            //    user.Dump();
            //}
        }

    }
}
