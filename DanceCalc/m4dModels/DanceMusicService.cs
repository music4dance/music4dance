using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
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

        public IDanceMusicContext Context => _context;
        public DbSet<Song> Songs => _context.Songs;
        public DbSet<SongProperty> SongProperties => _context.SongProperties;
        public DbSet<Dance> Dances => _context.Dances;
        public DbSet<DanceRating> DanceRatings => _context.DanceRatings;
        public DbSet<Tag> Tags => _context.Tags;
        public DbSet<TagType> TagTypes => _context.TagTypes;
        public DbSet<SongLog> Log => _context.Log;
        public DbSet<ModifiedRecord> Modified => _context.Modified;

        public UserManager<ApplicationUser> UserManager => _userManager;

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public static readonly string EditRole = "canEdit";
        public static readonly string TagRole = "canTag";
        public static readonly string DiagRole = "showDiagnostics";
        public static readonly string DbaRole = "dbAdmin";
        public static readonly string PseudoRole = "pseudoUser";

        #endregion

        #region Edit
        private Song CreateSong(Guid? guid = null, bool doLog = false)
        {
            if (guid == null || guid == Guid.Empty)
                guid = Guid.NewGuid();
            var song = _context.Songs.Create();
            song.SongId = guid.Value;

            if (doLog)
                song.CurrentLog = _context.Log.Create();

            return song;
        }

        public Song CreateSong(ApplicationUser user, SongDetails sd=null,  IEnumerable<UserTag> tags = null, string command = SongBase.CreateCommand, string value = null, bool createLog = true)
        {
            if (sd != null)
            {
                Trace.WriteLineIf(string.Equals(sd.Title, sd.Artist), $"Title and Artist are the same ({sd.Title})");                
            }

            var song = CreateSong(sd?.SongId, createLog);
            if (sd == null)
            {
                song.Create(user, command, value, true, this);
            }
            else
            {
                song.Create(sd, tags, user, command, value, this);
            }

            song = _context.Songs.Add(song);
            if (createLog)
            {
                _context.Log.Add(song.CurrentLog);
            }

            return song;
        }

        public SongDetails CreateSongDetails(ApplicationUser user, SongDetails sd, IEnumerable<UserTag> tags = null, bool createLog = true)
        {
            return new SongDetails(CreateSong(user, sd, tags, SongBase.CreateCommand, null, createLog),user.UserName,this);
        }

        public SongDetails EditSong(ApplicationUser user, SongDetails edit, IEnumerable<UserTag> tags = null, bool createLog = true)
        {
            var song = _context.Songs.Find(edit.SongId);
            if (createLog)
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);

            if (!song.Edit(user, edit, tags, this)) return null;

            if (createLog)
                _context.Log.Add(song.CurrentLog);

            return new SongDetails(song,user.UserName,this);
        }

        public SongDetails UpdateSong(ApplicationUser user, Song song, SongDetails edit, bool createLog = true)
        {
            if (createLog)
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);

            if (!song.Update(user, edit, this)) return null;

            if (createLog)
                _context.Log.Add(song.CurrentLog);

            return new SongDetails(song,user.UserName,this,false);
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        //  TODO: I'm pretty sure I can clean up this and all the other editing stuff by pushing
        //  the diffing part down into SongDetails (which will also let me unit test it more easily)
        public bool AdditiveMerge(ApplicationUser user, Guid songId, SongDetails edit, List<string> addDances, bool createLog = true)
        {
            var song = _context.Songs.Find(songId);
            if (createLog)
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);

            if (!song.AdditiveMerge(user, edit, addDances, this)) 
                return false;

            if (song.CurrentLog != null)
                _context.Log.Add(song.CurrentLog);

            return true;
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
        private IList<AlbumDetails> MergeAlbums(IList<Song> songs, string def, HashSet<string> keys, string artist)
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

                var name = SongBase.AlbumListField + "_" + i;

                if (defIdx != -1 && !keys.Contains(name)) continue;

                var t = albumsIn[i];
                t.Index = idx;
                albumsOut.Add(t);
                idx += 1;
            }

            return AlbumDetails.MergeAlbums(albumsOut,artist,false);
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, IList<AlbumDetails> albums)
        {
            var songIds = string.Join(";", songs.Select(s => s.SongId.ToString()));

            var song = CreateSong(user, null,null, SongBase.MergeCommand, songIds);
            song.CurrentLog.SongReference = song.SongId;
            song.CurrentLog.SongSignature = song.Signature;

            song = _context.Songs.Add(song);

            // Add in the properties for all of the songs and then delete them
            foreach (var from in songs)
            {
                song.UpdateProperties(from.SongProperties, new[] { SongBase.FailedLookup, SongBase.AlbumField, SongBase.TrackField, SongBase.PublisherField, SongBase.PurchaseField });
                RemoveSong(from, user);
            }
            song.UpdateFromService(this);

            var sd = new SongDetails(title,artist,tempo,length,albums);
            song.Edit(user, sd, null, this);

            return song;
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, string defAlbums, HashSet<string> keys)
        {
            return MergeSongs(user,songs,title,artist,tempo,length,MergeAlbums(songs,defAlbums,keys,artist));
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
            var dance = _context.Dances.Find(danceId);

            if (dance == null)
            {
                return null;
            }

            var dr = _context.DanceRatings.Create();

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
            var csp = _context.SongProperties;

            var ret = csp == null ? new SongProperty() : csp.Create();

            ret.Song = song;
            ret.SongId = song.SongId;
            ret.Name = name;
            ret.Value = SongProperty.SerializeValue(value);

            if (csp == null)
                return ret;

            if (song.SongProperties == null)
            {
                song.SongProperties = new List<SongProperty>();
            }
            song.SongProperties.Add(ret);

            if (log != null)
            {
                LogPropertyUpdate(ret, log,old?.ToString());
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

            var drDelete = new List<DanceRating>();
            var currentUser = entry.User;
            foreach (var lv in entry.GetValues())
            {
                if (lv.IsAction) continue;

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
                    var drd = new DanceRatingDelta(lv.Value);
                    if (action == UndoAction.Undo)
                    {
                        drd.Delta *= -1;
                    }

                    np.Value = drd.ToString();

                    // TODO: Consider implementing a MergeDanceRating at the song level
                    var dr = song.DanceRatings.FirstOrDefault(d => string.Equals(d.DanceId, drd.DanceId));
                    if (dr == null)
                    {
                        song.AddDanceRating(new DanceRating() { DanceId = drd.DanceId, Weight = drd.Delta });
                    }
                    else
                    {
                        dr.Weight += drd.Delta;
                        if (dr.Weight <= 0)
                        {
                            drDelete.Add(dr);
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

            foreach (var dr in drDelete)
            {
                song.DanceRatings.Remove(dr);
            }
        }
        #endregion

        #region Logging

        public void RestoreFromLog(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                RestoreFromLog(line);
            }
        }

        public IEnumerable<UndoResult> UndoLog(ApplicationUser user, IEnumerable<SongLog> entries)
        {
            return entries.Select(entry => UndoEntry(user, entry, true)).ToList();
        }

        // TODO: This depends on all of the user's changes being in the SongLog, which will hopefully be true of end users but isn't true
        //  of pseudo users/admin users, so eventually need to at minimum catch this and at best deal with it smoothly...
        public void UndoUserChanges(ApplicationUser user, Guid songId)
        {
            var song = Songs.Find(songId);

            // First update the song
            var logs = Context.Log.Where(l => l.User.Id == user.Id && l.SongReference == songId).OrderByDescending(l => l.Id).ToList();

            foreach (var log in logs)
            {
                UndoEntry(user, log);
            }

            // Then delete the songprops since they should have a net null effect at this point
            SongProperty lastCommand = null;
            var props = new List<SongProperty>();
            var collect = false;
            foreach (var prop in song.OrderedProperties)
            {
                if (prop.Name == SongBase.UserField)
                {
                    collect = string.Equals(prop.Value, user.UserName, StringComparison.OrdinalIgnoreCase);
                }
                else if (!collect && prop.IsAction)
                {
                    lastCommand = prop;
                }

                if (!collect) continue;

                if (lastCommand != null)
                {
                    props.Add(lastCommand);
                }
                props.Add(prop);
                lastCommand = null;
            }
            foreach (var prop in props)
            {
                song.SongProperties.Remove(prop);
                Context.SongProperties.Remove(prop);
                prop.Song = null;
                prop.SongId = Guid.Empty;
            }

            // Then remove the modified reference
            var mr = song.ModifiedBy.FirstOrDefault(m => m.ApplicationUser == user);
            if (mr != null) song.ModifiedBy.Remove(mr);

            // And finally, get rid of the undo entires and save the changes
            Context.Log.RemoveRange(logs);

            SaveChanges();
        }
        private UndoResult UndoEntry(ApplicationUser user, SongLog entry, bool doLog = false, string maskCommand = null)
        {
            var action = entry.Action;
            string error = null;

            // Quick recurse on Redo
            if (action.StartsWith(SongBase.RedoCommand))
            {
                var idx = entry.GetIntData(SongBase.SuccessResult);

                action = SongBase.RedoCommand;

                if (idx.HasValue)
                {
                    var uentry = _context.Log.Find(idx.Value);
                    var idx2 = uentry.GetIntData(SongBase.SuccessResult);

                    if (idx2.HasValue)
                    {
                        var rentry = _context.Log.Find(idx2.Value);

                        return UndoEntry(user, rentry, doLog, maskCommand);
                    }
                }

                error =
                    $"Unable to redo a failed undo song id='{entry.SongReference}' signature='{entry.SongSignature}'";
            }

            var result = new UndoResult { Original = entry };

            var song = FindSong(entry.SongReference, entry.SongSignature);

            if (song == null)
            {
                error = $"Unable to find song id='{entry.SongReference}' signature='{entry.SongSignature}'";
            }

            var command = SongBase.UndoCommand + entry.Action;
            if (error == null)
            {
                if (action.StartsWith(SongBase.UndoCommand))
                {
                    var idx = entry.GetIntData(SongBase.SuccessResult);
                    action = SongBase.UndoCommand;

                    if (idx.HasValue)
                    {
                        var rentry = _context.Log.Find(idx.Value);

                        error = RedoEntry(rentry, song);
                        command = SongBase.RedoCommand + entry.Action.Substring(SongBase.UndoCommand.Length);
                    }
                    else
                    {
                        error =
                            $"Unable to redo a failed undo song id='{entry.SongReference}' signature='{entry.SongSignature}'";
                    }
                }

                switch (action)
                {
                    case SongBase.DeleteCommand:
                        error = Undelete(song,user);
                        break;
                    case SongBase.MergeCommand:
                        error = Unmerge(entry, song);
                        break;
                    case SongBase.EditCommand:
                        error = RestoreValuesFromLog(entry, song, UndoAction.Undo, maskCommand);
                        break;
                    case SongBase.CreateCommand:
                        RemoveSong(song,user);
                        break;
                    case SongBase.UndoCommand:
                    case SongBase.RedoCommand:
                        break;
                    default:
                        error = $"'{entry.Action}' action not yet supported for Undo.";
                        break;
                }

            }

            if (doLog)
            {
                var newEntry = CreateSongLog(user, song, command);

                newEntry.UpdateData(error == null ? SongBase.SuccessResult : SongBase.FailResult, entry.Id.ToString());
                if (error != null)
                {
                    newEntry.UpdateData(SongBase.MessageData, error);
                }
                _context.Log.Add(newEntry);
                result.Result = newEntry;
            }
            else
            {
                result.Result = null;
            }

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
            // First restore the merged songs
            var t = entry.GetData(SongBase.MergeCommand);

            var songs = SongsFromList(t);
            foreach (var s in songs)
            {
                RestoreSong(s, entry.User);
            }

            // Now delete the merged song
            RemoveSong(song,entry.User);

            return null;
        }

        public void RestoreFromLog(string line)
        {
            var log = _context.Log.Create();

            if (!log.Initialize(line, this))
            {
                Trace.WriteLine($"Unable to restore line: {line}");
            }

            RestoreFromLog(log);
        }

        public void RestoreFromLog(SongLog log, string maskCommand=null)
        {
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
                    Trace.WriteLine($"Bad Command: {log.Action}");
                    return;
            }

            switch (log.Action)
            {
                case SongBase.DeleteCommand:
                    RemoveSong(song,log.User);
                    break;
                case SongBase.EditCommand:
                    RestoreValuesFromLog(log, song, UndoAction.Redo, maskCommand);
                    break;
                case SongBase.MergeCommand:
                case SongBase.CreateCommand:
                    CreateSongFromLog(log);
                    break;
                default:
                    Trace.WriteLine($"Bad Command: {log.Action}");
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
                    error = $"'{entry.Action}' action not yet supported for Redo.";
                    break;
            }

            return error;
        }

        private void LogSongCommand(string command, Song song, ApplicationUser user, bool includeSignature = true)
        {
            var log = _context.Log.Create();
            log.Time = DateTime.Now;
            log.User = user;
            log.SongReference = song.SongId;
            log.Action = command;

            if (includeSignature)
            {
                log.SongSignature = song.Signature;
            }

            foreach (var p in song.SongProperties)
            {
                LogPropertyUpdate(p, log);
            }

            _context.Log.Add(log);
        }

        private string RestoreValuesFromLog(SongLog entry, Song song, UndoAction action, string maskCommand = null)
        {
            var command = maskCommand ?? ((action == UndoAction.Undo) ? SongBase.UndoCommand : SongBase.RedoCommand);
            song.CreateEditProperties(entry.User,command,this,entry.Time);
            DoRestoreValues(song, entry, action);

            var sd = new SongDetails(song.SongId, song.SongProperties);
            song.RestoreScalar(sd);
            song.UpdateUsers(this);

            return null;
        }

        private string Remerge(SongLog entry, Song song, ApplicationUser user)
        {
            // First, restore the merged to song
            RestoreSong(song,user);

            // Then remove the merged from songs
            var t = entry.GetData(SongBase.MergeCommand);
            var songs = SongsFromList(t);
            foreach (var s in songs)
            {
                RemoveSong(s,user);
            }

            return null;
        }

        private void RestoreSong(Song song, ApplicationUser user)
        {
            if (!string.IsNullOrWhiteSpace(song.Title))
            {
                throw new ArgumentOutOfRangeException(nameof(song), @"Attempting to restore a song that hasn't been deleted");
            }
            song.CreateEditProperties(user, SongBase.DeleteCommand + "=false", this);
            var sd = new SongDetails(song.SongId, song.SongProperties);
            song.Restore(sd, this);
            song.UpdateUsers(this);
        }

        private SongLog CreateSongLog(ApplicationUser user, Song song, string action)
        {
            var log = _context.Log.Create();

            log.Initialize(user, song, action);

            return log;
        }

        private void CreateSongFromLog(SongLog log)
        {
            var initV = log.GetData(SongBase.MergeCommand);

            // For merge case, first we delete the old songs
            if (initV != null)
            {
                try
                {
                    foreach (var d in SongsFromList(initV))
                    {
                        RemoveSong(d,log.User);
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                }
            }

            var song = CreateSong(log.SongReference);
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
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose,
                    $"Couldn't find song by Id: {id} or signature {signature}");
            }

            return song;
        }

        public SongDetails FindSongDetails(Guid id, string userName = null)
        {
            var song = FindSong(id);

            return song == null ? null : new SongDetails(song,userName,this);
        }

        public SongDetails FindMergedSong(Guid id, string userName = null)
        {
            while (true)
            {
                var idS = id.ToString();
                var property = SongProperties.FirstOrDefault(p => p.Name == SongBase.MergeCommand && p.Value.Contains(idS));

                if (property == null) return null;

                var sd = FindSongDetails(property.SongId, userName);
                if (sd != null) return sd;

                id = property.SongId;
            }
        }


        private Song FindSongBySignature(string signature)
        {
            var song = _context.Songs.FirstOrDefault(s => s.Signature == signature);

            return song;
        }

        private IEnumerable<Song> SongsFromList(string list)
        {
            var dels = list.Split(';');
            var songs = new List<Song>(list.Length);

            foreach (var t in dels)
            {
                Guid idx;
                if (Guid.TryParse(t, out idx))
                {
                    var s = _context.Songs.Find(idx);
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
            var traceVerbose = TraceLevels.General.TraceVerbose;
            int count;
            var lastCount = 0;
            if (traceVerbose)
            {
                count = lastCount = songs.Count();
                Trace.WriteLine($"Total Songs = {count}");
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
            var userFilter = false;
            if (!string.IsNullOrWhiteSpace(filter.User))
            {
                var user = FindUser(filter.User);
                if (user != null)
                {
                    songs = from m in Modified where m.ApplicationUserId == user.Id && m.Song.TitleHash != 0 select m.Song;
                    userFilter = true;
                }
            }

#if TRACETag
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs per user = {0}", songs.Count()));
                lastCount = count;
            }
#endif
            // Now limit it down to the ones that are marked as a particular dance or dances
            List<string> danceList = null;
            var danceQuery = filter.DanceQuery;
            if (!danceQuery.All)
            {
                danceList = danceQuery.ExpandedIds.ToList();

                songs = danceQuery.IsExclusive ? 
                    songs.Where(s => danceList.All(did => s.DanceRatings.Any(dr => dr.DanceId == did))) : 
                    songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            }
            else if ((cruft & CruftFilter.NoDances) != CruftFilter.NoDances)
            {
                songs = songs.Where(s => s.DanceRatings.Any());
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, $"Songs by dance = {songs.Count()}");
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
                Trace.WriteLineIf(count != lastCount, $"Songs by tempo = {songs.Count()}");
                lastCount = count;
            }
#endif

            // Now limit it by anything that has the search string in the title, album or artist
            if (!string.IsNullOrEmpty(filter.SearchString))
            {
                if (userFilter)
                {
                    var str = filter.SearchString.ToUpper();
                    songs = songs.Where(
                        s => (s.Title != null && s.Title.ToUpper().Contains(str)) ||
                        (s.Album != null && s.Album.Contains(str)) ||
                        (s.Artist != null && s.Artist.Contains(str)) ||
                        (s.TagSummary.Summary != null && s.TagSummary.Summary.Contains(str)));
                }
                else
                {
                    songs = songs.Where(
                        s => s.Title.Contains(filter.SearchString) ||
                        s.Album.Contains(filter.SearchString) ||
                        s.Artist.Contains(filter.SearchString) ||
                        s.TagSummary.Summary.Contains(filter.SearchString));
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, $"Songs by search = {songs.Count()}");
                lastCount = count;
            }
#endif

            // Now Handle Tag Filtering
            if (!string.IsNullOrWhiteSpace(filter.Tags))
            {
                var tlInclude = new TagList(filter.Tags);
                var tlExclude = new TagList();

                if (tlInclude.IsQualified)
                {
                    var temp = tlInclude;
                    tlInclude = temp.ExtractAdd();
                    tlExclude = temp.ExtractRemove();
                }

                // We're accepting either a straight include list of tags or a qualified list (+/- for include/exlude)
                // TODO: For now this is going to be explicit (i&i&!e*!e) - do we need a stronger expression syntax at this level
                //  or can we do some kind of top level OR of queries?

                var typeInclude = GetTagRings(tlInclude).Select(tt => tt.Key).ToList();
                var typeExclude = GetTagRings(tlExclude).Select(tt => tt.Key).ToList();

                songs = from s in songs where s.TitleHash != 0 && typeInclude.All(val => s.TagSummary.Summary.Contains(val) || s.DanceRatings.Any(dr => dr.TagSummary.Summary.Contains(val))) select s;
                if (typeExclude.Count > 0)
                {
                    songs = from s in songs where !typeExclude.Any(val => s.TagSummary.Summary.Contains(val) || s.DanceRatings.Any(dr => dr.TagSummary.Summary.Contains(val))) select s;
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, $"Songs by tags = {songs.Count()}");
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
                var not = false;
                var purch = filter.Purchase;
                if (purch.StartsWith("!"))
                {
                    not = true;
                    purch = purch.Substring(1);
                }

                var services = purch.ToCharArray();
                if (services.Length == 1)
                {
                    var c = services[0].ToString();
                    songs = not ? songs.Where(s => s.Purchase == null || !s.Purchase.Contains(c)) : songs.Where(s => s.Purchase != null && s.Purchase.Contains(c));
                }
                else if (services.Length == 2)
                {
                    var c0 = services[0].ToString();
                    var c1 = services[1].ToString();

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
                Trace.WriteLineIf(count != lastCount, $"Songs by purchase = {songs.Count()}");
            }
#endif

            var songSort = new SongSort(filter.SortOrder);

            switch (songSort.Id)
            {
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
                    var did = TrySingleId(danceList) ?? (filter.Dances == null ? null : TrySingleId(new List<string>(new[] {filter.Dances})));
                    songs = did != null ? songs.OrderByDescending(s => s.DanceRatings.FirstOrDefault(dr => dr.DanceId.StartsWith(did)).Weight) : songs.OrderByDescending(s => s.DanceRatings.Max(dr => dr.Weight));
                }
                    break;
                case "Modified":
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Modified) : songs.OrderBy(s => s.Modified);
                    break;
                case "Created":
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Created) : songs.OrderBy(s => s.Created);
                    break;
            }

            // Then take the top n songs if
            if (songSort.Count != -1)
            {
                songs = songs.Take(10);
            }

            return songs.Include("DanceRatings").Include("ModifiedBy").Include("SongProperties");
        }

        public enum MatchMethod { None, Tempo, Merge };

        public IList<LocalMerger> MatchSongs(IList<SongDetails> newSongs, MatchMethod method)
        {
            var merge = new List<LocalMerger>();

            foreach (var song in newSongs)
            {
                var songT = song;
                var songs = from s in Songs where (s.TitleHash == songT.TitleHash) select s;

                var candidates = new List<SongDetails>();
                foreach (var s in songs)
                {
                    // Title-Artist match at minimum
                    if (string.Equals(SongBase.CreateNormalForm(s.Artist), SongBase.CreateNormalForm(song.Artist)))
                    {
                        candidates.Add(new SongDetails(s));
                    }
                }

                SongDetails match = null;
                var type = MatchType.None;

                if (candidates.Count > 0)
                {
                    // Now we have a list of existing songs that are a title-artist match to our new song - so see
                    //  if we have a title-artist-album match

                    if (song.HasAlbums)
                    {
                        var songD = song;
                        foreach (var s in candidates.Where(s => s.FindAlbum(songD.Albums[0].Name) != null))
                        {
                            match = s;
                            type = MatchType.Exact;
                            break;
                        }
                    }

                    // If not, try for a length match
                    if (match == null && song.Length.HasValue)
                    {
                        var songD = song;
                        foreach (var s in candidates.Where(s => songD.Length != null && (s.Length.HasValue && Math.Abs(s.Length.Value - songD.Length.Value) < 5)))
                        {
                            match = s;
                            type = MatchType.Length;
                            break;
                        }
                    }

                    // TODO: We may want to make this even weaker (especially for merge): If merge doesn't have album remove candidate.HasRealAlbums?

                    // Otherwise, if there is only one candidate and it doesn't have any 'real'
                    //  albums, we will choose it
                    if (match == null && candidates.Count == 1 && (!song.HasAlbums || !candidates[0].HasRealAblums))
                    {
                        type = MatchType.Weak;
                        match = candidates[0];
                    }
                }

                var m = new LocalMerger { Left = song, Right = match, MatchType = type, Conflict = false };
                switch (method)
                {
                    case MatchMethod.Tempo:
                        if (match != null)
                            m.Conflict = song.TempoConflict(match, 3);
                        break;
                    case MatchMethod.Merge:
                        // Do we need to do anything special here???
                        break;
                }

                merge.Add(m);
            }

            return merge;
        }

        public bool MergeCatalog(ApplicationUser user, IList<LocalMerger> merges, IEnumerable<string> dances = null)
        {
            var modified = false;

            var dancesL = dances?.ToList() ?? new List<string>();

            foreach (var m in merges)
            {

                // Matchtype of none indicates a new (to us) song, so just add it
                if (m.MatchType == MatchType.None)
                {
                    if (dancesL.Any())
                    {
                        m.Left.UpdateDanceRatingsAndTags(user, dancesL, SongBase.DanceRatingInitial);
                        m.Left.InferDances(user);
                    }
                    var temp = CreateSong(user, m.Left);
                    if (temp != null)
                    {
                        modified = true;
                        m.Left.SongId = temp.SongId;
                    }
                }
                // Any other matchtype should result in a merge, which for now is just adding the dance(s) from
                //  the new list to the existing song (or adding weight).
                // Now we're going to potentially add tempo - need a more general solution for this going forward
                else
                {
                    modified = AdditiveMerge(user, m.Right.SongId, m.Left, dancesL);
                }
            }

            return modified;
        }
        public ICollection<ICollection<PurchaseLink>> GetPurchaseLinks(ServiceType serviceType, IEnumerable<Song> songs, string region = null)
        {
            var links = new List<ICollection<PurchaseLink>>();
            var cid = MusicService.GetService(serviceType).CID;
            var sid = cid.ToString();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var song in songs)
            {
                if (song.Purchase == null || !song.Purchase.Contains(cid)) continue;

                var sd = new SongDetails(song);
                var l = sd.GetPurchaseLinks(sid,region);
                if (l != null)
                    links.Add(l);
            }

            return links;
        }

        public ICollection<ICollection<PurchaseLink>> GetPurchaseLinks(ServiceType serviceType, IEnumerable<Guid> songIds, string region = null)
        {
            var songs = Context.Songs.Where(s => songIds.Contains(s.SongId)).Include("DanceRatings").Include("ModifiedBy.ApplicationUser").Include("SongProperties");
            return GetPurchaseLinks(serviceType, songs, region);
            
        }

        public string GetPurchaseInfo(ServiceType serviceType, IEnumerable<Song> songs, string region)
        {
            var songLinks = GetPurchaseLinks(serviceType, songs, region);
            return PurchaseLinksToInfo(songLinks, region);
        }

        public string GetPurchaseInfo(ServiceType serviceType, IEnumerable<Guid> songIds, string region)
        {
            var songLinks = GetPurchaseLinks(serviceType, songIds, region);
            return PurchaseLinksToInfo(songLinks, region);
        }

        public string GetPurchaseInfo(ServiceType serviceType, string ids, string region)
        {
            return GetPurchaseInfo(serviceType, Array.ConvertAll(ids.Split(','), s => new Guid(s)), region);
        }

        public static string PurchaseLinksToInfo(ICollection<ICollection<PurchaseLink>> songLinks, string region)
        {
            var results = (from links in songLinks select links.SingleOrDefault(l => l.AvailableMarkets == null || l.AvailableMarkets.Contains(region)) into link where link != null select link.SongId).ToList();
            return string.Join(",",results);
        }

        public static ICollection<PurchaseLink> ReducePurchaseLinks(ICollection<ICollection<PurchaseLink>> songLinks, string region)
        {
            return (from links in songLinks select links.SingleOrDefault(l => l.AvailableMarkets == null || l.AvailableMarkets.Contains(region)) into link where link != null select link).ToList();
        }

        // TODO: This is extremely dependent on the form of the danceIds, just
        //  a temporary kludge until we get multi-select working
        private static string TrySingleId(List<string> danceList)
        {
            string ret = null;
            if (danceList != null && danceList.Count > 0 && !string.IsNullOrWhiteSpace(danceList[0]))
            {
                ret = danceList[0].Substring(0, 3).ToUpper();
                for (var i = 1; i < danceList.Count; i++)
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
            var temp = new TagType(tag);

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
                var types = TagTypes.Where(tt => tt.Value == tag).ToList();
                var type = types.FirstOrDefault(tt => tt.Category == category);
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
            var old = new TagList(tags);
            var result = new List<string>();
            foreach (var tag in old.Tags)
            {
                var fullTag = tag;
                var tempTag = tag;
                var tempCat = category;
                var rg = tag.Split(':');
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

        public IEnumerable<TagCount> GetTagSuggestions(Guid? user = null, char? targetType = null, string tagType = null, int count = int.MaxValue, bool normalized=false)
        {
            // from m in Modified where m.ApplicationUserId == user.Id && m.Song.TitleHash != 0 select m.Song;

            var userString = user?.ToString();
            var trg = targetType.HasValue ? new string(targetType.Value, 1) : null;
            var tagLabel = tagType == null ? null : ":" + tagType;

            IOrderedEnumerable<TagCount> ret;

            if (userString == null)
            {
                var tts = (tagType == null) ? TagTypes : TagTypes.ToList().Where(tt => tt.Category == tagType);
                ret = TagType.ToTagCounts(tts).OrderByDescending(tc => tc.Count);
            }
            else
            {
                var tags = from t in Tags
                            where
                                (userString == t.UserId) && 
                                (trg == null || t.Id.StartsWith(trg)) &&
                                (tagType == null || t.Tags.Summary.Contains(tagLabel))
                            select t;

                var dictionary = new Dictionary<string, int>();
                TagTypes.Load();
                foreach (var t in tags)
                {
                    foreach (var ti in t.Tags.Tags.Where(ti => tagLabel == null || ti.EndsWith(tagLabel)))
                    {
                        var tag = ti;
                        if (normalized)
                        {
                            var tt = TagTypes.Find(tag);
                            if (tt != null)
                            {
                                tag = tt.GetPrimary().ToString();
                            }
                        }
                        int c;
                        if (!dictionary.TryGetValue(tag, out c))
                            c = 0;

                        dictionary[tag] = c + 1;
                    }
                }

                ret = dictionary.Select(pair => new TagCount(pair.Key, pair.Value))
                    .OrderByDescending(tc => tc.Count);
            }

            if (count < int.MaxValue)
            {
                ret = ret.Take(count).OrderByDescending(tc => tc.Count);
            }

            return ret;
        }

        public ICollection<TagType> GetTagRings(TagList tags)
        {
            var map = new Dictionary<string, TagType>();

            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var tag in tags.Tags)
            {
                var tt = TagTypes.Find(tag);
                if (tt == null)
                    continue;

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
            var type = _context.TagTypes.Create();
            type.Key = TagType.BuildKey(value, category);
            type.PrimaryId = primary;
            type = _context.TagTypes.Add(type);

            return type; 
        }

        #endregion

        #region Load

        private const string SongBreak = "+++++SONGS+++++";
        private const string TagBreak = "+++++TAGSS+++++";
        private const string DanceBreak = "+++++DANCES+++++";
        private const string UserHeader = "UserId\tUserName\tRoles\tPWHash\tSecStamp\tLockout\tProviders\tEmail\tEmailConfirmed\tStartDate\tRegion\tPrivacy\tCanContact\tServicePreference";

        static public bool IsCompleteBackup(IList<string> lines)
        {
            string[] breaks = {DanceBreak, TagBreak, SongBreak};

            if (lines == null || lines.Count == 0 || !IsUserBreak(lines[0])) return false;

            var ibreak = 0;
            foreach (var l in lines)
            {
                if (!IsBreak(l, breaks[ibreak])) continue;

                ibreak += 1;
                if (ibreak == breaks.Length)
                {
                    return true;
                }
            }

            return false;
        }

        static public bool IsSongBreak(string line) {
            return IsBreak(line, SongBreak);
        }
        static public bool IsTagBreak(string line)
        {
            return IsBreak(line, TagBreak);
        }
        static public bool IsUserBreak(string line)
        {
            return UserHeader.StartsWith(line.Trim());
        }
        static public bool IsDanceBreak(string line)
        {
            return IsBreak(line, DanceBreak);
        }

        static private bool IsBreak(string line, string brk)
        {
            return string.Equals(line.Trim(), brk, StringComparison.InvariantCultureIgnoreCase);
        }

        public void LoadUsers(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Entering LoadUsers");

            if (lines == null || lines.Count < 1 || !IsUserBreak(lines[0]))
            {
                throw new ArgumentOutOfRangeException();
            }

            var fieldCount = lines[0].Split('\t').Length;
            var i = 1;
            while (i < lines.Count)
            {
                AdminMonitor.UpdateTask("LoadUsers",i-1);
                var s = lines[i];
                i += 1;

                if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                var cells = s.Split('\t');
                if (cells.Length != fieldCount) continue;

                var userId = cells[0];
                var userName = cells[1];
                var roles = cells[2];
                var hash = string.IsNullOrWhiteSpace(cells[3]) ? null : cells[3];
                var stamp = cells[4];
                var lockout = cells[5];
                var providers = cells[6];
                string email = null;
                var emailConfirmed = false;
                var date = new DateTime();
                string region = null;
                byte privacy = 0;
                var canContact = ContactStatus.None;
                string servicePreference = null;

                var extended = cells.Length >= 13;
                if (extended)
                {
                    email = cells[7];
                    bool.TryParse(cells[8], out emailConfirmed);
                    DateTime.TryParse(cells[9], out date);
                    region = cells[10];
                    Byte.TryParse(cells[11], out privacy);
                    byte canContactT;
                    Byte.TryParse(cells[12], out canContactT);
                    canContact = (ContactStatus)canContactT;
                    servicePreference = cells[13];
                }

                var user = FindUser(userName);
                var create = user == null;                    

                if (create)
                {
                    user = _context.Users.Create();
                    user.Id = userId;
                    user.UserName = userName;
                    user.PasswordHash = hash;
                    user.SecurityStamp = stamp;
                    user.LockoutEnabled = string.Equals(lockout, "TRUE", StringComparison.InvariantCultureIgnoreCase);

                    if (!string.IsNullOrWhiteSpace(providers))
                    {
                        var entries = providers.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        for (var j = 0; j < entries.Length; j += 2)
                        {
                            var login = new IdentityUserLogin() { LoginProvider = entries[j], ProviderKey = entries[j + 1], UserId = userId };
                            user.Logins.Add(login);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(roles))
                    {
                        var roleNames = roles.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var roleName in roleNames)
                        {
                            var role = Context.Roles.FirstOrDefault(r => r.Name == roleName.Trim());
                            if (role == null) continue;

                            var iur = new IdentityUserRole() { UserId = user.Id, RoleId = role.Id };
                            user.Roles.Add(iur);
                        }
                    }

                    if (extended)
                    {
                        user.StartDate = date;
                        user.Email = email;
                        user.EmailConfirmed = emailConfirmed;
                        user.Region = region;
                        user.Privacy = privacy;
                        user.CanContact = canContact;
                        user.ServicePreference = servicePreference;
                    }

                    Context.Users.Add(user);
                }
                else if (extended)
                {
                    if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(email))
                    {
                        user.Email = email;
                        user.EmailConfirmed = emailConfirmed;
                    }
                    if (string.IsNullOrWhiteSpace(user.Region) && !string.IsNullOrWhiteSpace(region))
                    {
                        user.Region = region;
                        user.Privacy = privacy;
                        user.CanContact = canContact;
                        user.ServicePreference = servicePreference;                        
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

            if (lines.Count > 0 && IsDanceBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            AdminMonitor.UpdateTask("LoadDances");
            LoadDances();
            var modified = false;
            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index];
                AdminMonitor.UpdateTask("LoadDances", index+1);
                if (string.IsNullOrWhiteSpace(s))
                    continue;
                var cells = s.Split('\t').ToList();
                var d = Dances.Find(cells[0]);
                if (d == null)
                {
                    d = Dances.Create();
                    d.Id = cells[0];
                    d.SongTags = new TagSummary();
                    Dances.Add(d);
                    modified = true;
                }

                cells.RemoveAt(0);
                modified |= d.Update(cells);
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

            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index];
                AdminMonitor.UpdateTask("LoadTags", index + 1);

                var cells = s.Split('\t');
                TagType tt = null;
                if (cells.Length >= 2)
                {
                    var category = cells[0];
                    var value = cells[1];

                    tt = FindOrCreateTagType(value, category);
                }

                if (tt != null && cells.Length >= 3 && !string.IsNullOrWhiteSpace(cells[2]))
                {
                    tt.PrimaryId = cells[2];
                }
            }

            foreach (var tt in TagTypes)
            {
                if (tt.PrimaryId != null && tt.Primary == null)
                {
                    tt.Primary = TagTypes.Find(tt.PrimaryId);
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

            var c = 0;
            foreach (var line in lines)
            {
                AdminMonitor.UpdateTask("LoadSongs",c);
                var time = DateTime.Now;
                var song = new Song {Created = time, Modified = time};

                song.Load(line, this);
                _context.Songs.Add(song);
                
                c += 1;
                //if (c % 50 == 0)
                //{
                //    Trace.WriteLineIf(TraceLevels.General.TraceInfo, string.Format("{0} songs loaded", c));
                //}

                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving next 100 songs");
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

            if (lines.Count > 0 && IsSongBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            var c = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("//"))
                    continue;

                AdminMonitor.UpdateTask("UpdateSongs", c);

                var sd = new SongDetails(line);
                var song = FindSong(sd.SongId);

                if (song == null)
                {
                    var up = sd.FirstProperty(SongBase.UserField);
                    var user = FindOrAddUser(up != null ? up.Value : "batch", EditRole);

                    song = CreateSong(sd.SongId);
                    UpdateSong(user, song, sd, false);
                    Songs.Add(song);

                    // This was a merge so delete the input songs
                    if (sd.Properties.Count > 0 && sd.Properties[0].Name == SongBase.MergeCommand)
                    {
                        var list = SongsFromList(sd.Properties[0].Value);
                        foreach (var s in list)
                        {
                            DeleteSong(user, s, false);
                        }
                    }
                }
                else
                {
                    var up = sd.LastProperty(SongBase.UserField);
                    var user = FindOrAddUser(up != null ? up.Value : "batch", EditRole);
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
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{c} songs updated");
                }
            }

            _context.TrackChanges(true);
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
            SongCounts.ClearCache();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting UpdateSongs");
        }

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static Guid _guidError = new Guid("25053e8c-5f1e-441e-bd54-afdab5b1b638");

        public void RebuildUserTags(string userName, bool update, string songIds=null)
        {
            if (!AdminMonitor.StartTask("RebuildUserTags")) return;

            var addedCount = 0;
            var removedCount = 0;
            var modifiedCount = 0;

            try
            {
                _context.TrackChanges(false);
                var tracker = TagContext.CreateService(this);

                var user = FindUser(userName);
                var c = 0;

                songIds = songIds?.Replace("-", string.Empty);

                var songs = (songIds == null) ? Songs : SongsFromList(songIds);

                foreach (var song in songs)
                {
                    AdminMonitor.UpdateTask("Running Songs", c);

                    if (song.SongId == _guidError)
                    {
                        Trace.WriteLine("This One: " + song);
                    }

                    if (song.IsNull) continue;

                    song.RebuildUserTags(user, tracker);
                    c += 1;
                    if (c%100 == 0)
                    {
                        Trace.WriteLineIf(
                            TraceLevels.General.TraceInfo,
                            $"{c} songs loaded");

                        _context.ClearEntities(new[] { "SongProperty", "DanceRating", "Song", "ModifiedRecord" });
                    }                    
                }
                _context.CheckpointSongs();
                
                AdminMonitor.UpdateTask("Computing Tags");

                var newTags = new Dictionary<string, Tag>();

                foreach (var ut in tracker.Tags)
                {
                    var key = ut.Id + ut.UserId;
                    newTags.Add(key, ut);
                }

                // First go through the old tags & remove or modify
                var remove = new List<Tag>();

                var tagIdList = songIds?.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries).ToList();

                var tags = (songIds == null) ? Tags : Tags.Where(t => tagIdList.Any(id => t.Id.EndsWith(id)));

                if (TraceLevels.General.TraceVerbose)
                {
                    var l = tags.ToList();
                    foreach (var t in l)
                    {
                        Trace.WriteLine(t);
                    }
                }

                foreach (var ot in tags)
                {
                    var key = ot.Id + ot.UserId;
                    Tag nt;

                    if (newTags.TryGetValue(key, out nt))
                    {
                        newTags.Remove(key);

                        if (string.Equals(ot.Tags.ToString(), nt.Tags.ToString(), StringComparison.OrdinalIgnoreCase))
                            continue;
                        Trace.WriteLine($"C\t{ot}\t{nt}");
                        modifiedCount += 1;
                        if (update)
                        {
                            ot.Tags = nt.Tags;
                        }
                    }
                    else
                    {
                        Trace.WriteLine($"D\t{ot}\t");
                        removedCount += 1;
                        if (update)
                        {
                            remove.Add(ot);
                        }
                    }
                }

                if (update)
                {
                    AdminMonitor.UpdateTask($"Removing {removedCount} Tags");
                    // Do the actual removes
                    Trace.WriteLine("Remove old tags");
                    foreach (var rt in remove)
                    {
                        Tags.Remove(rt);
                    }

                    addedCount = newTags.Values.Count;
                    AdminMonitor.UpdateTask($"Adding {addedCount} Tags");
                    // Then do the adds
                    Trace.WriteLine("Add new user tags");
                    foreach (var nt in newTags.Values)
                    {
                        Trace.WriteLine($"A\t\t{nt}\t");
                        Tags.Add(nt);
                    }

                    AdminMonitor.UpdateTask("Updating Database");
                    _context.TrackChanges(true);
                }

                var message = $"Success: added = {addedCount}, removed = {removedCount}, modified ={modifiedCount}";
                Trace.WriteLine(message);
                AdminMonitor.CompleteTask(true,message);
            }
            catch (Exception e)
            {
                AdminMonitor.CompleteTask(false,e.Message,e);
            }
        }

        public void SeedDances()
        {
            var dances = DanceLibrary.Dances.Instance;
            foreach (var d in dances.AllDances)
            {
                var dance = _context.Dances.Find(d.Id);
                if (dance == null)
                {
                    dance = new Dance { Id = d.Id };
                    _context.Dances.Add(dance);
                }
            }

        }
        private void LoadDances()
        {
            Context.LoadDances();
        }
        #endregion

        #region Save
        public IList<string> SerializeUsers(bool withHeader=true)
        {
            var users = new List<string>();

            if (withHeader)
            {
                users.Add(UserHeader);
            }
            
            foreach (var user in _userManager.Users)
            {
                var userId = user.Id;
                var username = user.UserName;
                var roles = string.Join("|", _userManager.GetRoles(user.Id));
                var hash = user.PasswordHash;
                var stamp = user.SecurityStamp;
                var lockout = user.LockoutEnabled.ToString();
                var providers = string.Join("|", _userManager.GetLogins(user.Id).Select(l => l.LoginProvider + "|" + l.ProviderKey));
                var email = user.Email;
                var emailConfirmed = user.EmailConfirmed;
                var time = user.StartDate.ToString("g");
                var region = user.Region;
                var privacy = user.Privacy.ToString();
                var canContact = ((byte) user.CanContact).ToString();
                var servicePreference = user.ServicePreference;

                users.Add(
                    $"{userId}\t{username}\t{roles}\t{hash}\t{stamp}\t{lockout}\t{providers}\t{email}\t{emailConfirmed}\t{time}\t{region}\t{privacy}\t{canContact}\t{servicePreference}");
            }

            return users;
        }

        public IList<string> SerializeTags(bool withHeader = true)
        {
            var tags = new List<string>();

            if (withHeader)
            {
                tags.Add(TagBreak);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var tt in TagTypes)
            {
                tags.Add($"{tt.Category}\t{tt.Value}\t{tt.PrimaryId}");
            }

            return tags;
        }

        public IList<string> SerializeSongs(bool withHeader = true, bool withHistory = true, int max = -1, DateTime? from = null, SongFilter filter = null, HashSet<Guid> exclusions = null)
        {
            var songs = new List<string>();

            if (withHeader)
            {
                songs.Add(SongBreak);
            }

            var songlist = (filter == null ? Songs : BuildSongList(filter)).OrderByDescending(t => t.Modified).ThenByDescending(t => t.SongId);

            if (max != -1)
            {
                songlist = songlist.Take(max) as IOrderedQueryable<Song>;
            }

            if (from.HasValue)
            {
                songlist = songlist?.Where(s => s.Modified > from.Value) as IOrderedQueryable<Song>;
            }

            string[] actions = null;
            var alist = new List<string>();
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

            _context.TrackChanges(false);
            if (max > 0)
            {
                SerializeChunk(songlist.Include("SongProperties").ToList(), actions, songs,exclusions);
            }
            else
            {
                var timer = new QuickTimer();
                const int chunkSize = 100;
                var chunkIndex = 0;
                List<Song> chunk;
                do
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Chunk: {chunkIndex}");
                    var chunkQ = songlist?.Skip(chunkIndex*chunkSize).Take(chunkSize).Include(s => s.SongProperties);
                    chunk = chunkQ?.ToList();
                    timer.ReportTime("ReadDb");
                    SerializeChunk(chunk,actions,songs,exclusions);
                    timer.ReportTime("Serialize");
                    AdminMonitor.TryUpdateTask("songs", chunkIndex * chunkSize);
                    _context.ClearEntities(new[] { "SongProperty", "DanceRating", "Song", "ModifiedRecord" });
                    timer.ReportTime("Clear");
                    chunkIndex += 1;
                } while (chunk?.Count > 0);

                timer.ReportTotals();
            }
            _context.TrackChanges(true);

            return songs;
        }

        private void SerializeChunk(IEnumerable<Song> songs, string[] actions, List<string> lines, HashSet<Guid> exclusions)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var song in songs)
            {
                if (exclusions != null && exclusions.Contains(song.SongId))
                    continue;

                var line = song.Serialize(actions);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                lines.Add(line);
            }
        }

        public IList<string> SerializeDances(bool withHeader = true)
        {
            var songs = new List<string>();

            if (withHeader)
            {
                songs.Add(DanceBreak);
            }

            var dancelist = Dances.OrderBy(d => d.Id);
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var dance in dancelist)
            {
                var line = dance.Serialize();
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
            ApplicationUser user;
            if (_userCache.TryGetValue(name,out user))
                return user;

            user = _userManager.FindByName(name);
            if (user != null)
                _userCache[name] = user;

            return user;
        }
        public ApplicationUser FindOrAddUser(string name, string role)
        {
            
            var user = FindUser(name);

            if (user == null)
            {
                user = new ApplicationUser { UserName = name, Email = name + "@music4dance.net", EmailConfirmed = true, StartDate = DateTime.Now };
                var res = _userManager.Create(user, "_This_Is_@_placeh0lder_");
                if (res.Succeeded)
                {
                    var user2 = FindUser(name);
                    Trace.WriteLine($"{user2.UserName}:{user2.Id}");
                }

            }

            if (string.Equals(role, PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else
            {
                AddRole(user.Id, role);
            }
            return user;
        }

        public ModifiedRecord CreateModified(Guid songId, string applicationId)
        {
            var us = _context.Modified.Create();
            us.ApplicationUserId = applicationId;
            us.SongId = songId;
            return us;
        }

        private void AddRole(string id, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return;

            var key = id + ":" + role;
            if (_roleCache.Contains(key))
                return;

            if (!_userManager.IsInRole(id, role))
            {
                _userManager.AddToRole(id, role);
            }

            _roleCache.Add(key);
        }

        private readonly Dictionary<string, ApplicationUser> _userCache = new Dictionary<string, ApplicationUser>();
        private readonly HashSet<string> _roleCache = new HashSet<string>();

        #endregion
        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(_context, n, level);
        }
    }
}
