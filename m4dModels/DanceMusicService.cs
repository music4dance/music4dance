using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DanceLibrary;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace m4dModels
{
    public class DanceMusicService : IDisposable
    {
        #region Lifetime Management

        public static IDanceMusicFactory Factory { get; set; }

        public static DanceMusicService GetService()
        {
            return Factory.CreateDisconnectedService();
        }

        private IDanceMusicContext _context;

        public DanceMusicService(IDanceMusicContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            UserManager = userManager;
        }
        public void Dispose()
        {
            var temp = _context;
            _context = null;
            temp.Dispose();
        }

        public bool IsDisposed => _context == null;

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
        public DbSet<Search> Searches => _context.Searches;

        public UserManager<ApplicationUser> UserManager { get; }

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

        public bool AdminEditSong(Song edit, string properties)
        {
            return edit.AdminEdit(properties,this);
        }

        public bool AdminEditSong(string properties)
        {
            Guid id;
            if (SongBase.TryParseId(properties, out id) == 0) return false;

            var song = FindSong(id);
            return song != null && AdminEditSong(song, properties);
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

        public bool EditLike(ApplicationUser user, Guid songId, bool? like, string danceId=null, bool doLog = true)
        {
            var song = _context.Songs.Find(songId);
            if (doLog)
                song.CurrentLog = CreateSongLog(user, song, SongBase.EditCommand);

            if (danceId == null)
            {
                if (!song.EditLike(user, like, this)) return false;
            }
            else
            {
                if (!song.EditDanceLike(user, like, danceId, this)) return false;
            }

            if (song.CurrentLog != null)
                _context.Log.Add(song.CurrentLog);
            SaveChanges();
            return true;
        }

        public int CleanupAlbums(ApplicationUser user, Song song)
        {
            var changed = 0;
            var sd = new SongDetails(song);

            var albums = AlbumDetails.MergeAlbums(sd.Albums, sd.Artist, true);
            if (albums.Count != sd.Albums.Count)
            {
                var delta = sd.Albums.Count - albums.Count;
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"{delta}: {song.Title} {song.Artist} {song.Album}");
                changed += delta;
                sd.Albums = albums.ToList();
                sd = EditSong(user, sd, null, false);
            }

            var album = sd.Album;
            if (song.Album == album) return changed;

            song.Album = album;
            return changed;
        }

        private static IList<AlbumDetails> MergeAlbums(IEnumerable<Song> songs, string def, ICollection<string> keys, string artist)
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
                song.UpdateProperties(from.SongProperties, TagMap, new[] { SongBase.FailedLookup, SongBase.AlbumField, SongBase.TrackField, SongBase.PublisherField, SongBase.PurchaseField });
                RemoveSong(from, user);
            }
            song.UpdateFromService(this);

            var sd = new SongDetails(title,artist,tempo,length,albums);

            // TODO: This is a bit of a kludge - for scalar parameters that we
            //  didn't ask the user about, we'll just copy over the one from the auto-merged song

            sd.Danceability = song.Danceability;
            sd.Energy = song.Energy;
            sd.Valence = song.Valence;
            sd.Sample = song.Sample;

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

        public BatchInfo CleanupProperties(int max, DateTime from, SongFilter filter)
        {
            var ret = new BatchInfo();

            if (filter == null) filter = new SongFilter();
            var songlist = TakeTail(BuildSongList(filter,CruftFilter.AllCruft), from, max);

            var lastTouched = DateTime.MinValue;
            var succeeded = new List<Song>();
            var failed = new List<Song>();
            var cummulative = 0;

            foreach (var song in songlist)
            {
                lastTouched = song.Modified;

                var init = song.SongProperties.Count;

                var changed = song.CleanupProperties(this);

                var final = song.SongProperties.Count;

                if (changed)
                {
                    Trace.WriteLine($"Succeeded ({init-final})): {song}");
                    succeeded.Add(song);
                    cummulative += init - final;
                }
                else
                {
                    if (init - final != 0)
                    {
                        Trace.WriteLine($"Failed ({init - final}): {song}");
                    }
                    Trace.WriteLine($"Skipped: {song}");
                    failed.Add(song);
                }

                AdminMonitor.UpdateTask("CleanProperty", succeeded.Count + failed.Count);

                if (succeeded.Count + failed.Count >= max)
                {
                    break;
                }
            }

            ret.LastTime = lastTouched;
            ret.Succeeded = succeeded.Count;
            ret.Failed = failed.Count;

            ret.Message = $"Cleaned up {cummulative} properties.";
            if (ret.Succeeded > 0)
            {
                SaveChanges();
            }

            if (ret.Succeeded + ret.Failed >= max) return ret;

            ret.Complete = true;
            ret.Message += "No more songs to clean up";

            return ret;
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

        public void UpdateDanceRatingsAndTags(SongDetails sd, ApplicationUser user, IEnumerable<string> dances, string songTags, string danceTags, int weight)
        {
            if (!string.IsNullOrEmpty(songTags))
            {
                sd.AddTags(songTags, user, this, sd, false);
            }
            var danceList = dances as IList<string> ?? dances.ToList();
            sd.UpdateDanceRatingsAndTags(user, danceList, SongBase.DanceRatingIncrement);
            if (!string.IsNullOrWhiteSpace(danceTags))
            {
                foreach (var id in danceList)
                {
                    sd.ChangeDanceTags(id, danceTags, user, this);
                }
            }
            sd.InferDances(user);
        }

        public void RebuildDances()
        {
            // Clear out the Top10s
            Context.Database.ExecuteSqlCommand("TRUNCATE TABLE dbo.TopNs");

            // TODO: Remove this when everyone is updated...
            Context.Database.ExecuteSqlCommand("delete from dances where LEN(dances.Id) > 3");

            Context.TrackChanges(false);

            // TODO: Add include/exclude as permanent fixtures in the header and link them to appropriate cloud
            //  Think about how we might fix the multi-categorization problem (LTN vs. International Latin)
            // Get the Max Weight and Count of songs for each dance

            var index = 0;
            foreach (var dance in Dances.Include("TopSongs.Song.DanceRatings"))
            {
                var message = "UpdateDance = " + dance.Name;
                Trace.WriteLine(message);
                AdminMonitor.UpdateTask(message, index++);

                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Computing info for " + dance.Name);
                dance.SongCount = dance.DanceRatings.Select(dr => dr.Song.Purchase).Count(p => p != null);
                dance.MaxWeight = (dance.SongCount == 0) ? 0 : dance.DanceRatings.Max(dr => dr.Weight);

                var filter = new SongFilter
                {
                    SortOrder = "Dances_10",
                    Dances = ((dance.Info is DanceGroup) ? "ADX," : string.Empty) + dance.Id,
                    TempoMax = 500,
                    TempoMin = 1
                };
                var songs = BuildSongList(filter).ToList();

                if (songs.Count < 10)
                {
                    filter.TempoMax = null;
                    filter.TempoMin = null;
                    songs = BuildSongList(filter).ToList();
                }

                if (songs.Count == 0) continue;

                var rank = 1;
                foreach (var s in songs)
                {
                    var tn = Context.TopNs.Create();
                    tn.Dance = dance;
                    tn.Song = s;
                    tn.Rank = rank;

                    Context.TopNs.Add(tn);
                    rank += 1;
                }
            }
            Context.TrackChanges(true);
        }

        public void RebuildDanceTags()
        {
            Context.TrackChanges(false);

            var index = 0;
            foreach (var dance in Dances)
            {
                AdminMonitor.UpdateTask("UpdateTags: Dance = " + dance.Name, index++);

                var dT = dance;
                var acc = new TagAccumulator();
                var tag = dance.Name + ":Dance";

                foreach (var rating in DanceRatings.Where(dr => dr.DanceId == dT.Id).Include("Song"))
                {
                    if (!rating.Song.TagSummary.HasTag(tag)) continue;
                    acc.AddTags(rating.TagSummary);
                    acc.AddTags(rating.Song.TagSummary);
                }
                dance.SongTags = acc.TagSummary();

                Context.CheckpointSongs();
            }

            Context.TrackChanges(true);
        }

        public void RebuildDanceInfo()
        {
            RebuildDances();
            RebuildDanceTags();
            DanceStatsManager.ClearCache();
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
            // For Like/Hate we'll update the modified record here

            var drDelete = new List<DanceRating>();
            var currentUser = entry.User;
            ModifiedRecord currentModified = null;

            foreach (var lv in entry.GetValues())
            {
                if (lv.IsAction) continue;

                var np = _context.SongProperties.Create();
                var baseName = lv.BaseName;

                np.Song = song;
                np.Name = lv.Name;

                // This works for everything but Dancerating and Tags, which will be overwritten below
                np.Value = action == UndoAction.Undo ? lv.Old : lv.Value;

                switch (baseName)
                {
                    case SongBase.UserField:
                    case SongBase.UserProxy:
                        currentUser = FindUser(lv.Value);
                        song.AddUser(currentUser, this);
                        currentModified = song.FindModified(currentUser.UserName);
                        break;
                    case SongBase.DanceRatingField:
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
                        break;
                    case SongBase.AddedTags:
                    case SongBase.RemovedTags:
                        np = null;
                        var add = (baseName.Equals(SongBase.AddedTags) && action == UndoAction.Redo) ||
                                  (baseName.Equals(SongBase.RemovedTags) && action == UndoAction.Undo);

                        if (add)
                            song.AddObjectTags(lv.DanceQualifier, lv.Value, currentUser, this);
                        else
                            song.RemoveObjectTags(lv.DanceQualifier, lv.Value, currentUser, this);
                        break;
                    case SongBase.LikeTag:
                        if (currentModified != null)
                        {
                            currentModified.LikeString = lv.Value;
                        }
                        break;
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
        //  and preferably handle that case - I think this is probably a case of figuring out how to rebuild the song
        //  locally (not hitting global tagtypes and other values) and cache the 'old' values for everything we would want to undo
        //  Actually, we may need to do this to get this feature to work in the general case - I think right now we may be
        //  tromping values that other users have changed between the current user's actions and the undo
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

        private static void LogPropertyUpdate(SongProperty sp, SongLog log, string oldValue = null)
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

            var sd = new SongDetails(song.SongId, song.SongProperties,TagMap);
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
            var sd = new SongDetails(song.SongId, song.SongProperties,TagMap);
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

        public SongDetails FindSongDetails(Guid id, string userName = null, bool showDiagnostics=false)
        {
            var song = FindSong(id);

            if (song == null) return null;

            if (showDiagnostics)
            {
                song.LoadTags(this);
            }

            return new SongDetails(song, userName, this);
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
                if (!Guid.TryParse(t, out idx)) continue;

                var s = _context.Songs.Find(idx);
                if (s != null)
                {
                    songs.Add(s);
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
            if ((cruft & CruftFilter.NoPublishers) != CruftFilter.NoPublishers )
            {
                songs = songs.Where(s => s.Purchase != null);
            }

            // Filter by user first since we have a nice key to pull from
            // TODO: This ends up going down a completely different LINQ path
            //  that is requiring some special casing further along, need
            //  to dig into how to manage that better...
            var userFilter = false;
            var userQuery = filter.UserQuery;

            if (!userQuery.IsEmpty && userQuery.IsInclude)
            {
                var user = FindUser(userQuery.UserName);
                if (user != null)
                {
                    var mod = from m in Modified
                        where m.ApplicationUserId == user.Id && m.Song.TitleHash != 0
                        select m;

                    if (userQuery.IsHate)
                    {
                        mod = mod.Where(m => m.Like == false);
                    }
                    else if (userQuery.IsLike)
                    {
                        mod = mod.Where(m => m.Like == true);
                    }
                    else
                    {
                        // For the all songs tagged condition, we're going to exclude explicitly disliked songs
                        mod = mod.Where(m => m.Like != false);
                    }

                    songs = from m in mod select m.Song;
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

                if (danceQuery.IncludeInferred)
                {
                    songs = danceQuery.IsExclusive
                        ? songs.Where(s => danceList.All(did => s.DanceRatings.Any(dr => dr.DanceId == did)))
                        : songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
                }
                else
                {
                    var dtList = danceQuery.Dances.Select(d => d.Name + ":Dance");
                    songs = danceQuery.IsExclusive
                        ? songs.Where(s => dtList.All(dt => s.TagSummary.Summary.Contains(dt)))
                        : songs.Where(s => dtList.Any(dt => s.TagSummary.Summary.Contains(dt)));
                    //This turns out to be overly strong - anyboday 'not liking' a song for a dance excludes it from the list
                    //songs = danceQuery.IsExclusive
                    //    ? songs.Where(s => dtList.All(dt => s.TagSummary.Summary.Contains(dt) && !s.TagSummary.Summary.Contains("!" + dt)))
                    //    : songs.Where(s => dtList.Any(dt => s.TagSummary.Summary.Contains(dt) && !s.TagSummary.Summary.Contains("!" + dt)));
                }
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

            if (!userQuery.IsEmpty && userQuery.IsExclude)
            {
                var user = FindUser(userQuery.UserName);
                if (user != null)
                {
                    var mod = from m in Modified
                              where m.ApplicationUserId == user.Id
                              select m;

                    if (userQuery.IsHate)
                    {
                        mod = mod.Where(m => m.Like == false);
                    }
                    else if (userQuery.IsLike)
                    {
                        mod = mod.Where(m => m.Like == true);
                    }

                    songs = songs.Where(s => !s.ModifiedBy.Contains(mod.FirstOrDefault(m => m.SongId == s.SongId)));
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, $"Songs by user like = {songs.Count()}");
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
                case "Mood":
                    songs = songs.Where(s => s.Valence != null);
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Valence) : songs.OrderBy(s => s.Valence);
                    break;
                case "Energy":
                    songs = songs.Where(s => s.Energy != null);
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Energy) : songs.OrderBy(s => s.Energy);
                    break;
                case "Beat":
                    songs = songs.Where(s => s.Danceability != null);
                    songs = songSort.Descending ? songs.OrderByDescending(s => s.Danceability) : songs.OrderBy(s => s.Danceability);
                    break;
                case "Dances":
                    // TODO: Better icon for dance order
                    // TODO: Get this working for multi-dance selection
                    {
                        var did = TrySingleId(danceList) ?? (filter.Dances == null ? null : TrySingleId(new List<string>(new[] {filter.Dances})));
                        songs = did != null ? songs.OrderByDescending(s => s.DanceRatings.FirstOrDefault(dr => dr.DanceId.StartsWith(did)).Weight) : songs.OrderByDescending(s => s.DanceRatings.Max(dr => dr.Weight));
                    }
                    break;
                // Note that Date sort is the counter-intuitive order since the UI shows amount of time since 
                case "Modified":
                    songs = songSort.Descending ? songs.OrderBy(s => s.Modified) : songs.OrderByDescending(s => s.Modified);
                    break;
                case "Created":
                    songs = songSort.Descending ? songs.OrderBy(s => s.Created) : songs.OrderByDescending(s => s.Created);
                    break;
            }

            // Then take the top n songs if
            if (songSort.Count != -1)
            {
                songs = songs.Take(songSort.Count);
            }

            return songs.Include("DanceRatings").Include("ModifiedBy").Include("SongProperties");
        }

        public LikeDictionary UserLikes(IEnumerable<SongBase> songs, string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return null;

            var likes = new LikeDictionary();
            foreach (var s in songs)
            {
                var mod = s.ModifiedBy.FirstOrDefault(m => m.UserName == userName);
                if (mod != null)
                {
                    likes.Add(s.SongId, mod.Like);
                }
            }
            return likes;
        }

        public LikeDictionary UserDanceLikes(IEnumerable<SongBase> songs, string danceId, string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return null;

            var likes = new LikeDictionary();
            foreach (var s in songs)
            {
                var level = s.UserDanceRating(userName, danceId);

                if (level > 0) likes.Add(s.SongId, true);
                else if (level < 0) likes.Add(s.SongId, false);
            }
            return likes;
        }

        // TODO: Think about aggregating annonymous & users to show most searched, most recent, etc.
        public void UpdateSearches(ApplicationUser user, SongFilter filter)
        {
            var f = new SongFilter(filter.ToString()) {Page = null};
            if (user != null)
                f.Anonymize(user.UserName);
            if (!f.IsAzure)
                f.Action = null;
            var userId = user?.Id;
            var q = f.ToString();
            var search = Searches.FirstOrDefault(s => s.ApplicationUserId == userId && s.Query == q);
            var now = DateTime.Now;
            if (search == null)
            {
                search = Searches.Create();
                search.ApplicationUserId = userId;
                search.Created = now;
                search.Query = q;
                search.Count = 0;
                search.Name = null;
                Searches.Add(search);
            }
            search.Modified = now;
            search.Count += 1;

            SaveChanges();
        }

        public enum MatchMethod { None, Tempo, Merge };

        private LocalMerger MergeFromTitle(SongDetails song)
        {
            var songT = song;
            var songs = from s in Songs where (s.TitleHash == songT.TitleHash) select s;

            var candidates = new List<SongDetails>();
            foreach (var s in songs)
            {
                // Title-Artist match at minimum
                if (SongBase.SoftArtistMatch(s.Artist,song.Artist))
                {
                    candidates.Add(new SongDetails(s));
                }
            }

            if (candidates.Count <= 0)
                return new LocalMerger {Left = song, Right = null, MatchType = MatchType.None, Conflict = false};

            SongDetails match = null;
            var type = MatchType.None;

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

            // Otherwise, if there is only one candidate we will choose it
            // TODO: and it doesn't have any 'real' albums [&& (!song.HasAlbums || !candidates[0].HasRealAblums)] (I obviously wanted this extra filter at some point...)
            if (match == null && candidates.Count == 1)
            {
                type = MatchType.Weak;
                match = candidates[0];
            }

            return new LocalMerger { Left = song, Right = match, MatchType = type, Conflict = false };
        }

        private LocalMerger MergeFromPurchaseInfo(SongDetails song)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var service in MusicService.GetSearchableServices())
            {
                var id = song.GetPurchaseId(service.Id);

                if (id == null) continue;

                var match = GetSongFromService(service, id);
                if (match != null)
                {
                    return new LocalMerger { Left = song, Right = match, MatchType = MatchType.Exact, Conflict = false };
                }
            }
            return null;
        }

        public IList<LocalMerger> MatchSongs(IList<SongDetails> newSongs, MatchMethod method)
        {
            newSongs = RemoveDuplicateSongs(newSongs);
            var merge = new List<LocalMerger>();
            foreach (var song in newSongs)
            {
                var m = MergeFromPurchaseInfo(song) ?? MergeFromTitle(song);

                switch (method)
                {
                    case MatchMethod.Tempo:
                        if (m.Right != null)
                            m.Conflict = song.TempoConflict(m.Right, 3);
                        break;
                    case MatchMethod.Merge:
                        // Do we need to do anything special here???
                        break;
                }

                merge.Add(m);
            }

            return merge;
        }

        public IList<SongDetails> RemoveDuplicateSongs(IList<SongDetails> songs)
        {
            var hash = new HashSet<string>();
            var ret = new List<SongDetails>();
            foreach (var song in songs)
            {
                var key = song.TitleArtistString;
                if (hash.Contains(key))
                    continue;
                hash.Add(key);
                ret.Add(song);
            }

            return ret;
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
        public static ICollection<ICollection<PurchaseLink>> GetPurchaseLinks(ServiceType serviceType, IEnumerable<SongBase> songs, string region = null)
        {
            if (songs == null) return null;

            var links = new List<ICollection<PurchaseLink>>();
            var cid = MusicService.GetService(serviceType).CID;
            var sid = cid.ToString();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var song in songs)
            {
                if (song.Purchase == null || !song.Purchase.Contains(cid)) continue;

                var sd = (song as SongDetails) ?? new SongDetails(song);
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
        public SongDetails GetSongFromService(MusicService service,string id,string userName=null)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var end = $":{service.CID}S";
            var purchase = SongProperties.FirstOrDefault(sp => sp.Value.StartsWith(id) && sp.Name.StartsWith("Purchase:") && sp.Name.EndsWith(end));

            return purchase == null ? null : FindSongDetails(purchase.SongId, userName);
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

        public IReadOnlyDictionary<string, TagType> TagMap => DanceStatsManager.GetInstance(this).TagMap;

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

        public IReadOnlyList<TagType> CachedTagTypes()
        {
            return DanceStatsManager.GetInstance(this).TagTypes;
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
                lock (s_sugMap)
                {
                    if (tagType == null) tagType = string.Empty;
                    if (s_sugMap.ContainsKey(tagType))
                    {
                        ret = s_sugMap[tagType];
                    }
                    else
                    {
                        var tts = (tagType == string.Empty) ? CachedTagTypes() : CachedTagTypes().Where(tt => tt.Category == tagType);
                        ret = TagType.ToTagCounts(tts).OrderByDescending(tc => tc.Count);
                        s_sugMap[tagType] = ret;
                    }
                }
            }
            else
            {
                // AZURETODO: Can't currently do this without user tags in sql database - do we run the user songs???
                var tags = from t in Tags
                            where
                                (userString == t.UserId) && 
                                (trg == null || t.Id.StartsWith(trg)) &&
                                (tagType == null || t.Tags.Summary.Contains(tagLabel))
                            select t;

                var tagMap = TagMap;
                var dictionary = new Dictionary<string, int>();
                foreach (var t in tags)
                {
                    foreach (var ti in t.Tags.Tags.Where(ti => tagLabel == null || ti.EndsWith(tagLabel)))
                    {
                        var tag = ti;
                        if (normalized)
                        {
                            TagType tt;
                            if (tagMap.TryGetValue(tag.ToLower(), out tt))
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

        public IEnumerable<TagType> OrderedTagTypes => DanceStatsManager.GetInstance(this).TagTypes;

        public ICollection<TagType> GetTagRings(TagList tags)
        {
            var tagCache = TagMap;
            var map = new Dictionary<string, TagType>();
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var tag in tags.Tags)
            {
                TagType tt;
                if (!tagCache.TryGetValue(tag.ToLower(), out tt))
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

            var other = TagTypes.Find(type.Key);
            if (other != null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Attempt to add duplicate tag: {other} {type}");
                type = other;
            }
            else
            {
                type = _context.TagTypes.Add(type);
                DanceStatsManager.GetInstance(this).AddTagType(type);
            }
            return type;
        }

        public static void BlowTagCache()
        {
            lock (s_sugMap)
            {
                s_sugMap.Clear();
            }
        }

        private static readonly Dictionary<string,IOrderedEnumerable<TagCount>> s_sugMap = new Dictionary<string, IOrderedEnumerable<TagCount>>();

        #endregion

        #region Load

        private const string SongBreak = "+++++SONGS+++++";
        private const string TagBreak = "+++++TAGSS+++++";
        private const string SearchBreak = "+++++SEARCHES+++++";
        private const string DanceBreak = "+++++DANCES+++++";
        private const string UserHeader = "UserId\tUserName\tRoles\tPWHash\tSecStamp\tLockout\tProviders\tEmail\tEmailConfirmed\tStartDate\tRegion\tPrivacy\tCanContact\tServicePreference\tLastActive\tRowCount\tColumns";

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
        static public bool IsSearchBreak(string line)
        {
            return IsBreak(line, SearchBreak);
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
                var active = new DateTime();
                int? rc = null;
                string col = null;

                var extended = cells.Length >= 13;
                if (extended)
                {
                    email = cells[7];
                    bool.TryParse(cells[8], out emailConfirmed);
                    DateTime.TryParse(cells[9], out date);
                    region = cells[10];
                    byte.TryParse(cells[11], out privacy);
                    byte canContactT;
                    byte.TryParse(cells[12], out canContactT);
                    canContact = (ContactStatus)canContactT;
                    servicePreference = cells[13];
                    DateTime.TryParse(cells[14], out active);
                    int rcT;
                    if (!string.IsNullOrWhiteSpace(cells[15]) && int.TryParse(cells[15], out rcT))
                    {
                        rc = rcT;
                    }
                    if (!string.IsNullOrWhiteSpace(cells[16]))
                    {
                        col = cells[16];
                    }
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
                        user.LastActive = active;
                        user.RowCountDefault = rc;
                        user.ColumnDefaults = col;
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

                    user.LastActive = active;
                    user.RowCountDefault = rc;
                    user.ColumnDefaults = col;
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Saving Changes");
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadUsers");
        }

        public void LoadSearches(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadSearches");

            if (lines == null || lines.Count < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (lines.Count > 1 && IsSearchBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            var fieldCount = lines[0].Split('\t').Length;
            for (var i = 0; i < lines.Count; i++)
            {
                AdminMonitor.UpdateTask("LoadSearches", i);
                var s = lines[i];

                if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                var cells = s.Split('\t');
                if (cells.Length != fieldCount) continue;

                var userName = cells[0];
                var name = cells[1];
                var query = cells[2];
                var favorite = string.Equals(cells[3], "true", StringComparison.OrdinalIgnoreCase);
                int count;
                int.TryParse(cells[4], out count);
                DateTime created;
                DateTime.TryParse(cells[5], out created);
                DateTime modified;
                DateTime.TryParse(cells[6], out modified);

                var user = string.IsNullOrWhiteSpace(userName) ? null : FindUser(userName);

                var search = user == null ? Searches.FirstOrDefault(x => x.ApplicationUser == null && x.Query == query) : 
                    Searches.FirstOrDefault(x => x.ApplicationUser != null && x.ApplicationUser.Id == user.Id && x.Query == query);

                if (search == null)
                {
                    search = Searches.Create();
                    search.ApplicationUser = user;
                    search.Query = query;
                    Searches.Add(search);
                }

                search.Name = name;
                search.Favorite = favorite;
                search.Count = count;
                search.Created = created;
                search.Modified = modified;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadSearches");
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

                DateTime modified;
                if (tt != null && cells.Length >= 4 && 
                    !string.IsNullOrWhiteSpace(cells[3]) && 
                    DateTime.TryParse(cells[3], out modified))
                {
                    tt.Modified = modified;
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

        public void UpdateSongs(IList<string> lines, bool clearCache=true)
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

            var tagMap = TagMap;
            var c = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("//"))
                    continue;

                AdminMonitor.UpdateTask("UpdateSongs", c);

                var sd = new SongDetails(line,tagMap);
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

            if (clearCache)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
                DanceStatsManager.ClearCache();
            }
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting UpdateSongs");
        }

        public void AdminUpdate(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering AdminUpdate");

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

                AdminEditSong(line);

                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{c} songs updated");
                }
            }

            _context.TrackChanges(true);
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
            DanceStatsManager.ClearCache();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting AdminUpdate");
        }

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static Guid s_guidError = new Guid("25053e8c-5f1e-441e-bd54-afdab5b1b638");

        public void RebuildUserTags(string userName, bool update, string songIds=null, SongFilter filter=null)
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

                if (songIds == null && filter != null)
                {
                    songIds = string.Join(";", BuildSongList(filter).Select(s => s.SongId));
                }
                songIds = songIds?.Replace("-", string.Empty);

                var songs = (songIds == null) ?Songs : SongsFromList(songIds);

                foreach (var song in songs)
                {
                    AdminMonitor.UpdateTask("Running Songs", c);

                    if (song.SongId == s_guidError)
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

        public void RebuildTags(string userName, string songIds = null, SongFilter filter = null)
        {
            if (!AdminMonitor.StartTask("RebuildTags")) return;

            try
            {
                _context.TrackChanges(false);
                var tracker = TagContext.CreateService(this);

                var user = FindUser(userName);
                var c = 0;

                if (songIds == null && filter != null)
                {
                    songIds = string.Join(";", BuildSongList(filter).Select(s => s.SongId));
                }
                songIds = songIds?.Replace("-", string.Empty);

                var songs = (songIds == null) ? Songs : SongsFromList(songIds);

                foreach (var song in songs)
                {
                    AdminMonitor.UpdateTask("Running Songs", c);

                    if (song.SongId == s_guidError)
                    {
                        Trace.WriteLine("This One: " + song);
                    }

                    if (song.IsNull) continue;

                    song.RebuildUserTags(user, tracker);
                    c += 1;
                    if (c % 100 == 0)
                    {
                        Trace.WriteLineIf(
                            TraceLevels.General.TraceInfo,
                            $"{c} songs");
                    }
                }
                _context.TrackChanges(true);

                var message = $"Success: songcount = {c}";
                Trace.WriteLine(message);
                AdminMonitor.CompleteTask(true, message);
            }
            catch (Exception e)
            {
                AdminMonitor.CompleteTask(false, e.Message, e);
            }
        }

        public int RebuildTagTypes(bool update = false)
        {
            var oldCounts = TagTypes.ToDictionary(tt => tt.Key.ToUpper(), tt => tt.Count);
            var newCounts = new Dictionary<string, Dictionary<string, int>>();

            // Compute the tag type count based on the user tags
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var ut in Tags)
            {
                foreach (var tag in ut.Tags.Tags)
                {
                    var norm = tag.ToUpper();
                    Dictionary<string, int> n;
                    if (!newCounts.TryGetValue(norm, out n))
                    {
                        n = new Dictionary<string, int>();
                        newCounts[norm] = n;
                    }

                    if (n.ContainsKey(tag))
                    {
                        n[tag] += 1;
                    }
                    else
                    {
                        n[tag] = 1;
                    }
                }
            }

            if (update)
            {
                Context.TrackChanges(false);
            }

            var changed = 0;
            foreach (var nc in newCounts)
            {
                var key = nc.Key;
                var val = nc.Value.Sum(v => v.Value);

                if (!oldCounts.ContainsKey(key))
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"A\t{key}\t\t{val}");
                    if (update)
                    {
                        var tt = TagTypes.Create();
                        tt.Key = nc.Value.Keys.First();
                        tt.Count = val;
                        TagTypes.Add(tt);
                    }
                    changed += 1;
                }
                else
                {
                    if (val != oldCounts[key])
                    {
                        Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"C\t{key}\t{oldCounts[key]}\t{val}");
                        if (update)
                        {
                            var tt = TagTypes.Find(key);
                            tt.Count = val;
                        }
                        changed += 1;
                    }
                    oldCounts.Remove(key);
                }
            }

            foreach (var oc in oldCounts.Where(oc => oc.Value > 0))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"R\t{oc.Key}\t{oc.Value}\t");
                if (update)
                {
                    var tt = TagTypes.Find(oc.Key);
                    tt.Count = 0;
                }
                changed += 1;
            }

            if (update)
            {
                Context.TrackChanges(true);
            }

            return changed;
        }

        public int CleanDeletedSongs()
        {
            var songs = Songs.Where(s => s.Title == null);

            Context.TrackChanges(false);

            var count = 0;
            foreach (var song in songs)
            {
                RemoveSong(song,null);
                count += 1;
            }

            Context.TrackChanges(true);

            return count;
        }

        public void SeedDances()
        {
            var dances = DanceLibrary.Dances.Instance;
            foreach (var dance in from d in dances.AllDanceGroups let dance = _context.Dances.Find(d.Id) where dance == null select new Dance { Id = d.Id })
            {
                _context.Dances.Add(dance);
            }
            foreach (var dance in from d in dances.AllDanceTypes let dance = _context.Dances.Find(d.Id) where dance == null select new Dance { Id = d.Id })
            {
                _context.Dances.Add(dance);
            }
        }

        private void LoadDances()
        {
            Context.LoadDances();
        }

        public void UpdatePurchaseInfo(string songIds)
        {
            var songs = (songIds == null) ? Songs.Where(s => s.TitleHash != 0) : SongsFromList(songIds);

            foreach (var song in songs)
            {
                var sd = new SongDetails(song);
                song.Purchase = sd.GetPurchaseTags();
            }
        }
        #endregion

        #region Save
        public IList<string> SerializeUsers(bool withHeader=true, DateTime? from = null)
        {
            var users = new List<string>();

            if (!from.HasValue) from = new DateTime(1,1,1);

            foreach (var user in UserManager.Users.Where(u => u.StartDate >= from.Value))
            {
                var userId = user.Id;
                var username = user.UserName;
                var roles = string.Join("|", UserManager.GetRoles(user.Id));
                var hash = user.PasswordHash;
                var stamp = user.SecurityStamp;
                var lockout = user.LockoutEnabled.ToString();
                var providers = string.Join("|", UserManager.GetLogins(user.Id).Select(l => l.LoginProvider + "|" + l.ProviderKey));
                var email = user.Email;
                var emailConfirmed = user.EmailConfirmed;
                var time = user.StartDate.ToString("g");
                var region = user.Region;
                var privacy = user.Privacy.ToString();
                var canContact = ((byte) user.CanContact).ToString();
                var servicePreference = user.ServicePreference;
                var lastActive = user.LastActive.ToString("g");
                var rc = user.RowCountDefault;
                var col = user.ColumnDefaults;

                users.Add(
                    $"{userId}\t{username}\t{roles}\t{hash}\t{stamp}\t{lockout}\t{providers}\t{email}\t{emailConfirmed}\t{time}\t{region}\t{privacy}\t{canContact}\t{servicePreference}\t{lastActive}\t{rc}\t{col}");
            }

            if (withHeader && users.Count > 0)
            {
                users.Insert(0,UserHeader);
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
                tags.Add($"{tt.Category}\t{tt.Value}\t{tt.PrimaryId}\t{tt.Modified.ToString("g")}");
            }

            return tags;
        }

        public IList<string> SerializeSearches(bool withHeader = true)
        {
            var searches = new List<string>();

            if (withHeader)
            {
                searches.Add(SearchBreak);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var search in Searches.Include(s => s.ApplicationUser))
            {
                var userName = (search.ApplicationUser != null) ? search.ApplicationUser.UserName : string.Empty;
                searches.Add($"{userName}\t{search.Name}\t{search.Query}\t{search.Favorite}\t{search.Count}\t{search.Created.ToString("g")}\t{search.Modified.ToString("g")}");
            }

            return searches;
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

        private static void SerializeChunk(IEnumerable<SongBase> songs, string[] actions, ICollection<string> lines, ICollection<Guid> exclusions)
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

        public IList<string> SerializeDances(bool withHeader = true, DateTime? from = null)
        {
            var dances = new List<string>();

            if (!from.HasValue) from = new DateTime(1, 1, 1);

            var dancelist = Dances.Where(d => d.Modified >= from.Value).OrderBy(d => d.Id);
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var dance in dancelist)
            {
                var line = dance.Serialize();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    dances.Add(line);
                }
            }

            if (withHeader && dances.Count > 0)
            {
                dances.Insert(0,DanceBreak);
            }

            return dances;
        }

        #endregion

        #region Search

        public bool ResetIndex(string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);
            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            {
                if (serviceClient.Indexes.Exists(info.Index))
                {
                    serviceClient.Indexes.Delete(info.Index);
                }

                var index = SongDetails.GetIndex(this);
                index.Name = info.Index;

                serviceClient.Indexes.Create(index);
            }

            return true;
        }

        private static IQueryable<Song> TakeTail(IQueryable<Song> songs, DateTime? from, int max)
        {
            if (from != null)
            {
                songs = songs.Where(s => s.Modified >= from);
            }

            songs = songs.OrderBy(t => t.Modified).ThenBy(t => t.SongId);

            return songs.Take(max + 100);
        }

        public BatchInfo IndexSongs(int max = -1, DateTime? from = null, bool rebuild = false, SongFilter filter = null, string id="default")
        {
            if (max == -1) max = 100;

            var ret = new BatchInfo();

            if (filter == null) filter = new SongFilter();
            var songlist = TakeTail(BuildSongList(filter,CruftFilter.AllCruft), from, max);

            var songs = new List<Document>();
            var deleted = new List<Document>();

            var lastTouched = DateTime.MinValue;

            var info = SearchServiceInfo.GetInfo(id);

            var idField = new List<string> {"SongId"};

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                var tried = 0;
                var exists = true;
                foreach (var song in songlist)
                {
                    Document doc = null;
                    lastTouched = song.Modified;

                    if (!rebuild || exists)
                    {
                        try
                        {
                            doc = indexClient.Documents.Get(song.SongId.ToString(),idField);
                        }
                        catch (Microsoft.Rest.Azure.CloudException e)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceVerbose,e.Message);
                            // Not found.
                        }
                    }

                    var si = new SongDetails(song).GetIndexDocument();
                    if (doc != null)
                    {
                        if (rebuild)
                        {
                            AdminMonitor.UpdateTask("ScanBatch", tried);
                            tried += 1;
                        }
                        else
                        {
                            deleted.Add(si);
                            songs.Add(si);
                        }
                    }

                    if (doc != null) continue;

                    AdminMonitor.UpdateTask("BuildBatch", songs.Count);
                    songs.Add(si);
                    exists = false;

                    if (songs.Count >= max) break;
                }

                var deletes = 0;
                if (!rebuild)
                {
                    songlist = TakeTail(Songs.Where(s => s.TitleHash == 0), from, max);

                    foreach (var song in songlist)
                    {
                        deleted.Add(new SongDetails(song).GetIndexDocument());
                        if (lastTouched < song.Modified)
                        {
                            lastTouched = song.Modified;
                        }
                        deletes += 1;
                    }
                }

                ret.LastTime = lastTouched;
                if (songs.Count == 0 && deleted.Count == 0)
                {
                    if (tried < max + 100)
                    {
                        ret.Complete = true;
                        ret.Message = "No more songs to index";
                    }
                    else
                    {
                        ret.Message = "Correct for semaphore burp.";
                    }
                    return ret;
                }

                try
                {
                    if (deleted.Count > 0)
                    {
                        AdminMonitor.UpdateTask("DeleteBatch");
                        var delete = IndexBatch.Delete(deleted);
                        indexClient.Documents.Index(delete);
                    }

                    if (songs.Count > 0)
                    {
                        AdminMonitor.UpdateTask("UpdateBatch");
                        var batch = IndexBatch.Upload(songs);
                        indexClient.Documents.Index(batch);
                    }
                    ret.Succeeded = songs.Count + deletes;
                }
                catch (IndexBatchException e)
                {
                    // Sometimes when your Search service is under load, indexing will fail for some of the documents in
                    // the batch. Depending on your application, you can take compensating actions like delaying and
                    // retrying. For this simple demo, we just log the failed document keys and continue.
                    var keys = e.IndexingResults.Where(r => !r.Succeeded).Select(r => r.Key).ToList();
                    var error = $"Failed to index {keys.Count} of the documents: {string.Join(",", keys)}";
                    Trace.WriteLine(error);

                    ret.Message = error;
                    ret.Failed = keys.Count;
                    ret.Succeeded = e.IndexingResults.Count(r => r.Succeeded);
                }
            }

            return ret;
        }

        static private readonly string[] s_updateFields =  { "Modified", "Properties" };
        static private readonly string[] s_updateEntities = { "Song", "SongProperties", "ModifiedBy", "DanceRatings" };
        static private readonly string[] s_updateNoId = { SongBase.NoSongId };
        private static readonly TimeSpan s_updateDelta = TimeSpan.FromMilliseconds(10);


        public int UpdateAzureIndex(string id = "default")
        {
            var skip = 0;
            var done = false;

            var info = SearchServiceInfo.GetInfo(id);

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                do
                {
                    var songs = new List<Document>();
                    var deleted = new List<Document>();

                    var chunk = 5;
                    switch (skip)
                    {
                        case 0:
                            break;
                        case 5:
                            chunk = 50;
                            break;
                        default:
                            chunk = 500;
                            Context.ClearEntities(s_updateEntities);
                            break;
                    }

                    var sqlRecent = TakeRecent(chunk, skip);
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var song in sqlRecent)
                    {
                        var exists = true;
                        try
                        {
                            var doc = indexClient.Documents.Get(song.SongId.ToString(), s_updateFields);
                            var diff = song.Modified - ((DateTimeOffset) doc["Modified"]).UtcDateTime;
                            var teq = diff < s_updateDelta;
                            if (teq && doc["Properties"] as string == song.Serialize(s_updateNoId))
                            {
                                done = true;
                                break;
                            }
                        }
                        catch (Microsoft.Rest.Azure.CloudException)
                        {
                            exists = false;
                            // Document isn't in the index at all, so add it
                        }

                        if (!song.IsNull)
                        {
                            songs.Add(new SongDetails(song).GetIndexDocument());
                        }
                        else if (exists)
                        {
                            deleted.Add(new SongDetails(song).GetIndexDocument());
                        }
                        skip += 1;
                    }

                    if (songs.Count > 0)
                    {
                        var batch = IndexBatch.MergeOrUpload(songs);
                        indexClient.Documents.Index(batch);
                    }
                    else if (deleted.Count > 0)
                    {
                        var delete = IndexBatch.Delete(deleted);
                        indexClient.Documents.Index(delete);
                    }

                } while (!done);
            }

            return skip;
        }

        private IEnumerable<Song> TakeRecent(int c, int skip = 0)
        {
            return Songs.OrderByDescending(s => s.Modified).Skip(skip).Take(c).Include("DanceRatings").Include("ModifiedBy").Include("SongProperties").ToList();
        }

        public SearchResults AzureSearch(SongFilter filter, int? pageSize = null, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            return AzureSearch(filter.SearchString, AzureParmsFromFilter(filter, pageSize), cruft, id);
        }

        public SearchResults AzureSearch(string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            parameters.IncludeTotalResultCount = true;
            var response = DoAzureSearch(search,parameters,cruft,id);
            var tagMap = TagMap;
            var songs = response.Results.Select(d => new SongDetails(d.Document,tagMap)).ToList();
            var pageSize = parameters.Top ?? 25;
            var page = ((parameters.Skip ?? 0)/pageSize) + 1;
            var facets = response.Facets;
            return new SearchResults(search, songs.Count,response.Count ?? -1,page,pageSize,songs,facets);
        }

        public FacetResults GetTagFacets(string categories, int count, string id = "default")
        {
            var parameters = AzureParmsFromFilter(new SongFilter(), 1);
            AddAzureCategories(parameters,categories,count);

            return DoAzureSearch(null, parameters, CruftFilter.NoCruft, id).Facets;
        }

        public static void AddAzureCategories(SearchParameters parameters, string categories, int count)
        {
            parameters.Facets = categories.Split(',').Select(c => $"{c},count:{count}").ToList();
        }

        private static DocumentSearchResult DoAzureSearch(string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);

            var extra = new StringBuilder();
            if ((cruft & CruftFilter.NoPublishers) != CruftFilter.NoPublishers)
            {
                extra.Append("Purchase/any()");
            }

            if ((cruft & CruftFilter.NoDances) != CruftFilter.NoDances)
            {
                if (extra.Length > 0) extra.Append(" and ");
                extra.Append("DanceTags/any()");
            }

            if (extra.Length > 0)
            {
                if (parameters.Filter == null)
                {
                    parameters.Filter = extra.ToString();
                }
                else
                {
                    extra.AppendFormat(" and {0}", parameters.Filter);
                    parameters.Filter = extra.ToString();
                }
            }

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                return indexClient.Documents.Search(search, parameters);
            }
        }

        public SearchParameters AzureParmsFromFilter(SongFilter filter, int? pageSize = null)
        {
            if (!pageSize.HasValue) pageSize = 25;

            if (filter.IsRaw)
            {
                return new RawSearch(filter).GetAzureSearchParams(pageSize);
            }
            var order = filter.ODataSort;
            var odataFilter = filter.GetOdataFilter(this);

            return new SearchParameters
            {
                QueryType = QueryType.Simple, // filter.IsSimple ? QueryType.Simple : QueryType.Full,
                Filter = odataFilter,
                IncludeTotalResultCount = true,
                Top = pageSize,
                Skip = ((filter.Page??1) - 1) * pageSize,
                OrderBy = order
            };
        }

        public SuggestionList AzureSuggestions(string query, string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                var sp = new SuggestParameters {Top=50};
          
                var response = indexClient.Documents.Suggest(query, "songs", sp);

                var comp = new SuggestionComparer();
                //var ret = new SuggestionList {Query = "query", Suggestions = new List<Suggestion>()};
                var ret = response.Results.Select(result => new Suggestion
                {
                    Value = result.Text,
                    Data = result.Document["SongId"] as string
                }).Distinct(comp).Take(10).ToList();

               return new SuggestionList
                {
                    Query = query,
                    Suggestions = ret
                };
            }
        }

        public IEnumerable<string> BackupIndex(string name = "default", int count = -1, DateTime? from = null, string filter = null)
        {
            var info = SearchServiceInfo.GetInfo(name);
            var songFilter = (filter == null) ? SongFilter.AzureSimple : new SongFilter(filter);

            var parameters = filter == null ? new SearchParameters {QueryType = QueryType.Simple} : AzureParmsFromFilter(songFilter);
            parameters.IncludeTotalResultCount = false;
            parameters.Skip = null;
            parameters.Top = (count == -1) ? (int?) null : count;
            parameters.OrderBy = new [] {"Modified desc"};
            parameters.Select = new[] {"SongId", "Modified", "Properties"};

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                SearchContinuationToken token = null;
                var searchString = string.IsNullOrWhiteSpace(songFilter.SearchString) ? null : songFilter.SearchString;
                var results = new List<string>();
                do
                {
                    var response = (token == null)
                        ? indexClient.Documents.Search(searchString, parameters)
                        : indexClient.Documents.ContinueSearch(token);

                    foreach (var doc in response.Results)
                    {
                        var m = doc.Document["Modified"];
                        var modified = (DateTimeOffset)m;
                        if (from != null && modified < from)
                        {
                            response.ContinuationToken = null;
                            break;
                        }

                        results.Add(SongBase.Serialize(doc.Document["SongId"] as string, doc.Document["Properties"] as string));
                        AdminMonitor.UpdateTask("readSongs", results.Count);
                    }
                    token = response.ContinuationToken;
                } while (token != null);

                return results;
            }
        }

        #endregion

        #region User
        public ApplicationUser FindUser(string name)
        {
            ApplicationUser user;
            if (_userCache.TryGetValue(name,out user))
                return user;

            user = UserManager.FindByName(name);
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
                var res = UserManager.Create(user, "_This_Is_@_placeh0lder_");
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

        public void ChangeUserName(string oldUserName, string userName)
        {
            var user = UserManager.FindByName(oldUserName);
            if (user == null)
            {
                throw new ArgumentOutOfRangeException($"User {0} doesn't exist",oldUserName);
            }

            Context.TrackChanges(false);
            Context.LazyLoadingEnabled = false;

            foreach (var prop in SongProperties.Where(p => (p.Name == SongBase.UserField || p.Name == SongBase.UserProxy) && p.Value == oldUserName))
            {
                prop.Value = userName;
            }

            Context.TrackChanges(true);
        }

        private void AddRole(string id, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return;

            var key = id + ":" + role;
            if (_roleCache.Contains(key))
                return;

            if (!UserManager.IsInRole(id, role))
            {
                UserManager.AddToRole(id, role);
            }

            _roleCache.Add(key);
        }

        private readonly Dictionary<string, ApplicationUser> _userCache = new Dictionary<string, ApplicationUser>();
        private readonly HashSet<string> _roleCache = new HashSet<string>();

        public int BatchUserLike(ApplicationUser user, bool? like)
        {
            var mod = Modified.Where(m => m.ApplicationUserId == user.Id).Include("Song").Include("Song.SongProperties");
            var count = 0;
            foreach (var m in mod)
            {
                m.Song.EditLike(m, like, this);
                count += 1;
            }

            SaveChanges();
            return count;
        }

        #endregion
        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(_context, n, level);
        }
    }
}
