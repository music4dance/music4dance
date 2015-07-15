using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Mvc;
using DanceLibrary;
using m4d.Context;
using m4d.Scrapers;
using m4dModels;
using Owin.Security.Providers.BattleNet;
using Configuration = m4d.Migrations.Configuration;

namespace m4d.Controllers
{
    [Authorize]
    public class AdminController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return AdminTheme;
            }
        }


        #region Commands
        //
        // GET: /Admin/
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult Index()
        {
            ViewBag.TraceLevel = TraceLevels.General.Level.ToString();
            return View();
        }

        //
        // Get: //Reseed
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Reseed()
        {
            ViewBag.Name = "Reseed Database";
            ReseedDb();
            ViewBag.Success = true;
            ViewBag.Message = "Database was successfully reseeded";

            return View("Results");
        }

        //
        // Get: //UpdatePurchase
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdatePurchase()
        {
            ViewBag.Name = "Update Purchase Info";

            var songs = from s in Database.Songs where s.TitleHash != 0 select s;

            foreach (var song in songs)
            {
                var sd = new SongDetails(song);
                song.Purchase = sd.GetPurchaseTags();
            }

            Database.SaveChanges();

            ViewBag.Success = true;
            ViewBag.Message = "Purchase info was successully updated";

            return View("Results");
        }

        //
        // Get: //UpdateTitleHash
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTitleHash()
        {
            ViewBag.Name = "UpdateTitleHash";
            var count = 0;

            var songs = from s in Database.Songs select s;
            foreach (var song in songs)
            {
                if (song.UpdateTitleHash())
                {
                    count += 1;
                }
            }
            Database.SaveChanges();

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Title Hashes were reseeded ({0})", count);

            return View("Results");
        }

        //
        // Get: //FixArtists
        [Authorize(Roles = "dbAdmin")]
        public ActionResult FixArtists()
        {
            ViewBag.Name = "FixArtists";
            var count = 0;

            var user = Database.FindUser(User.Identity.Name);
            var songs = from s in Database.Songs where string.Equals(s.Title,s.Artist) select s;
            foreach (var song in songs)
            {
                var artists = from p in song.SongProperties where p.Name == SongBase.ArtistField select p;
                var alist = artists.ToList();
                    //song.SongProperties.Select(p => string.Equals(p.BaseName, Song.ArtistField)).ToList();

                if (alist.Count <= 1) continue;

                var ap = alist[alist.Count - 2];
                var artist = ap.Value;
                if (artist == null) continue;

                var sd = new SongDetails(song) {Artist = artist};
                Database.EditSong(user, sd, null, false);
                count += 1;
            }
            Database.SaveChanges();

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Artists were fixed ({0})", count);

            return View("Results");
        }

        //
        // Get: //UpdateAlbums
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateAlbums()
        {
            ViewBag.Name = "UpdateAlbums";
            var changed = 0;
            var merged = 0;
            var scanned = 0;

            Context.TrackChanges(false);
            var user = Database.FindUser(User.Identity.Name);
            var songs = from s in Database.Songs where s.Title != null select s;
            foreach (var song in songs)
            {
                var sd = new SongDetails(song);

                var albums = AlbumDetails.MergeAlbums(sd.Albums, sd.Artist, true);
                if (albums.Count != sd.Albums.Count)
                {
                    sd.Albums = albums.ToList();
                    Database.EditSong(user, sd, null, false);
                    merged += 1;
                }
                else
                {
                    var album = sd.Album;
                    if (song.Album != album)
                    {
                        song.Album = album;
                        changed += 1;
                    }
                }

                scanned += 1;

                if (scanned % 100 == 0)
                {
                    Trace.WriteLine(string.Format("Scanned == {0}; Changed={1}; Merged={2}", scanned, changed, merged));
                }
            }
            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Albums were fixed ({0}) and Albums were merged ({1})", changed, merged);

            return View("Results");
        }

        //
        // Get: //UpdateTagTypes
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTagTypes(bool update=false)
        {
            var oldCounts = Database.TagTypes.ToDictionary(tt => tt.Key.ToUpper(), tt => tt.Count);
            var newCounts = new Dictionary<string, Dictionary<string,int>>();

            // Compute the tag type count based on the user tags
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var ut in Database.Tags)
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
                    Trace.WriteLine(string.Format("A\t{0}\t\t{1}", key, val));
                    if (update)
                    {
                        var tt = Database.TagTypes.Create();
                        tt.Key = nc.Value.Keys.First();
                        tt.Count = val;
                        Database.TagTypes.Add(tt);
                    }
                    changed += 1;
                }
                else 
                {
                    if (val != oldCounts[key])
                    {
                        Trace.WriteLine(string.Format("C\t{0}\t{1}\t{2}", key, oldCounts[key], val));
                        if (update)
                        {
                            var tt = Database.TagTypes.Find(key);
                            tt.Count = val;
                        }
                        changed += 1;
                    }
                    oldCounts.Remove(key);
                }
            }

            foreach (var oc in oldCounts.Where(oc => oc.Value > 0))
            {
                Trace.WriteLine(string.Format("R\t{0}\t{1}\t", oc.Key, oc.Value));
                if (update)
                {
                    var tt = Database.TagTypes.Find(oc.Key);
                    tt.Count = 0;
                }
                changed += 1;
            }

            if (update)
            {
                Context.TrackChanges(true);
            }

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Type counts were changed ({0})", changed);
            return View("Results");

        }

        //
        // Get: //UpdateTagSummaries
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTagSummaries()
        {
            try
            {
                StartAdminTask("UpdateTagSummaries");
                Context.TrackChanges(false);

                var counts = Database.TagTypes.ToDictionary(tt => tt.Key, tt => tt.Count);

                // Do the actual update
                var sumChanged = 0;
                var i = 0;
                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (var s in Database.Songs)
                {
                    AdminMonitor.UpdateTask("Update Song Tags", i++);
                    if (!s.UpdateTagSummary(Database)) continue;

                    sumChanged += 1;
                }

                i = 0;
                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (var dr in Database.DanceRatings)
                {
                    AdminMonitor.UpdateTask("Update Dance Rating Tags", i++);
                    if (!dr.UpdateTagSummary(Database)) continue;

                    sumChanged += 1;
                }

                // Validate that we didn't change counts...
                AdminMonitor.UpdateTask("Validate Counts");
                var typeChanged = 0;
                foreach (var tt in Database.TagTypes)
                {
                    int c;
                    if (counts.TryGetValue(tt.Key, out c) && c == tt.Count) continue;

                    typeChanged += 1;
                    break;
                }

                if (typeChanged > 0)
                {
                    return CompleteAdminTask(false,string.Format("Changed underlying count unexpectedly ({0})", typeChanged));
                }

                Context.TrackChanges(true);
                return CompleteAdminTask(true, string.Format(" Songs/DanceRatings were fixed as tags({0})", sumChanged));
            }
            catch (Exception e)
            {
                return FailAdminTask("Tag summaries failed to rebuild", e);
            }
        }

        //
        // Get: //AddDanceGroups
        [Authorize(Roles = "dbAdmin")]
        public ActionResult AddDanceGroups()
        {
            ViewBag.Name = "AddDanceGroups";

            var dict = Dances.Instance.DanceDictionary;

            var count = 0;
            Context.TrackChanges(false);

            var batch = Database.FindUser("batch");
            foreach (var song in Database.Songs)
            {
                var ngs = new Dictionary<string,DanceRatingDelta>();
                foreach (var dr in song.DanceRatings)
                {
                    var d = dict[dr.DanceId];
                    var dt = d as DanceType;
                    if (dt != null)
                    {
                        var g = dt.GroupId;
                        if (g != "MSC")
                        {
                            DanceRatingDelta drd;
                            if (ngs.TryGetValue(g, out drd))
                            {
                                drd.Delta += 2;
                            }
                            else
                            {
                                drd = new DanceRatingDelta(g, 3);
                                ngs.Add(g, drd);
                            }
                        }
                        count += 1;
                    }
                    else 
                    {
                        var di = d as DanceInstance;
                        if (di != null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("Dance Instance: {0}", song.Title));
                        }
                    }
                }

                if (ngs.Count > 0)
                {
                    Database.UpdateDances(batch, song, ngs.Values, false);
                }
            }

            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Dance groups were added ({0})", count);

            return View("Results");
        }

        //
        // Get: //InferDanceTypes
        [Authorize(Roles = "dbAdmin")]
        public ActionResult InferDanceTypes()
        {
            ViewBag.Name = "InferDanceTypes";

            var groups = new[] {"SWG","TNG","FXT","WLZ"};

            var dict = Dances.Instance.DanceDictionary;

            var count = 0;
            Context.TrackChanges(false);

            var batch = Database.FindUser("batch");
            foreach (var song in Database.Songs)
            {
                if (song.Tempo == null) continue;

                var tempo = song.Tempo.Value;
                var nts = new List<DanceRatingDelta>();
                foreach (var dr in song.DanceRatings)
                {
                    var d = dict[dr.DanceId];
                    var dg = d as DanceGroup;
                    if (dg != null && groups.Contains(dg.Id))
                    {
                        foreach (var dto in dg.Members)
                        {
                            var dt = dto as DanceType;
                            if (dt == null || !dt.TempoRange.ToBpm(dt.Meter).Contains(tempo)) continue;

                            if (song.DanceRatings.Any(x => x.DanceId == dt.Id)) continue;

                            // TODO: Consider re-using this code to tag songs as not-strict tempo (but it's actually the dance rating that should be tagged).
                            var drd = new DanceRatingDelta(dt.Id, 4);
                            nts.Add(drd);
                            count += 1;
                        }
                    }
                    else
                    {
                        DanceInstance di = d as DanceInstance;
                        if (di != null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("Dance Instance: {0}", song.Title));
                        }
                    }
                }

                if (nts.Count > 0)
                {
                    Database.UpdateDances(batch, song, nts, false);
                }
            }

            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Dance groups were added ({0})", count);

            return View("Results");
        }


        //
        // Get: //CleanTags
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CleanTags()
        {
            ViewBag.Name = "CleanTags";

            var count = 0;
            Context.TrackChanges(false);

            SongProperty user = null;
            SongProperty time = null;

            var deletions = new List<SongProperty>();

            foreach (var prop in Database.SongProperties)
            {
                switch (prop.BaseName)
                {
                    default:
                        user = null;
                        time = null;
                        break;
                    case SongBase.UserField:
                        user = prop;
                        break;
                    case SongBase.TimeField:
                        time = prop;
                        break;
                    case SongBase.AddedTags:
                    case SongBase.RemovedTags:
                        // Time ID User tags
                        if (user != null && user.SongId == prop.SongId)
                        {
                            deletions.Add(user);
                        }
                        if (time != null && time.SongId == prop.SongId)
                        {
                            deletions.Add(time);
                        }
                        deletions.Add(prop);
                        user = null;
                        time = null;
                        count += 1;
                        break;
                }
            }

            foreach (var prop in deletions)
            {
                Database.SongProperties.Remove(prop);
            }
            Context.CheckpointChanges();

            foreach (var tag in Database.Tags)
            {
                Database.Tags.Remove(tag);
                count += 1;
            }
            Context.CheckpointChanges();

            foreach (var tt in Database.TagTypes)
            {
                tt.Count = 0;
            }
            Context.CheckpointChanges();

            foreach (var song in Database.Songs)
            {
                song.TagSummary.Clean();
            }
            Context.TrackChanges(false);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Tags were removed ({0})", count);

            return View("Results");
        }

        //
        // Get: //FixZeroTime
        [Authorize(Roles = "dbAdmin")]
        public ActionResult FixZeroTime()
        {
            ViewBag.Name = "FixZeroTime";

            Context.TrackChanges(false);
            var count = 0;

            var zeroes = Database.SongProperties.Where(prop => prop.Name == SongBase.TimeField && prop.Value == "01/01/0001 00:00:00").ToList();

            foreach (var prop in zeroes)
            {
                var times = prop.Song.SongProperties.Where(p => p.Name == SongBase.TimeField && p.Value != "01/01/0001 00:00:00").OrderBy(p => p.Id).ToList();

                var np = times.FirstOrDefault();

                if (np == null) continue;

                var d =  np.ObjectValue as DateTime?;

                if (!d.HasValue) continue;

                prop.Value = np.Value;
                prop.Song.Created = d.Value;

                count += 1;
            }
            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Times were wrong {0}, fixed ({1})", zeroes.Count, count);

            return View("Results");
        }

        //
        // Get: // TimesFromProperties
        [Authorize(Roles = "dbAdmin")]
        public ActionResult TimesFromProperties()
        {
            ViewBag.Name = "TimesFromProperties";

            Context.TrackChanges(false);
            var modified = 0;
            var count = 0;

            foreach (var song in Database.Songs.Where(s => s.TitleHash != 0))
            {
                if (song.SetTimesFromProperties())
                {
                    modified += 1;
                }

                count += 1;

                if (count%1000 != 0) continue;

                Trace.WriteLine(string.Format("Checkpoint at {0}", count));
                Context.CheckpointChanges();
            }
            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Times were update {0}, total convered = {1}", modified,count);

            return View("Results");
        }


        //
        // Get: //ClearSongCache
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult ClearSongCache()
        {
            ViewBag.Name = "ClearSongCache";

            SongCounts.ClearCache();

            ViewBag.Success = true;
            ViewBag.Message = "Cache was cleared";

            return View("Results");
        }

        //
        // Get: //SetTraceLevel
        [AllowAnonymous]
        public ActionResult SetTraceLevel(int level)
        {
            ViewBag.Name = "Set Trace Level";

            TraceLevel tl = (TraceLevel)level;

            TraceLevels.SetGeneralLevel(tl);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Trace level set: {0}", tl.ToString());

            return View("Results");
        }

        //
        // Get: //TestTrace
        [AllowAnonymous]
        public ActionResult TestTrace(string message)
        {
            ViewBag.Name = "Test Trace";

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Trace message sentt: '{0}'", message);

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("Test Trace: '{0}'", message));
            return View("Results");
        }

        //
        // Get: //SpotifyRegions
        [Authorize(Roles = "dbAdmin")]
        public ActionResult SpotifyRegions(int count=int.MaxValue, int start=0, string region="US")
        {
            ViewBag.Name = "SpotifyRegions";
            var changed = 0;
            var updated = 0;
            var skipped = 0;
            var failed = 0;

            Context.TrackChanges(false);
            var properties = from p in Database.SongProperties
                where p.Name.StartsWith("Purchase") && p.Name.EndsWith(":SS")
                select p;

            var spotify = MusicService.GetService(ServiceType.Spotify);
            foreach (var prop in properties)
            {
                if (skipped < start)
                {
                    skipped += 1;
                    continue;
                }
                string[] regions = null;
                var id = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out regions);
                if (null == regions)
                {
                    var track = Context.GetMusicServiceTrack(prop.Value, spotify);
                    if (track.AvailableMarkets == null)
                    {
                        failed += 1;
                    }
                    else
                    {
                        prop.Value = PurchaseRegion.FormatIdAndRegionInfo(track.TrackId, track.AvailableMarkets);
                        regions = track.AvailableMarkets;
                        changed += 1;
                    }
                }

                if (regions == null || string.IsNullOrWhiteSpace(region) || regions.Contains(region))
                {
                    skipped += 1;
                }
                else if (!string.IsNullOrWhiteSpace(region))
                {
                    var track = Context.CoerceTrackRegion(id, spotify, region);
                    if (track != null)
                    {
                        prop.Value = PurchaseRegion.FormatIdAndRegionInfo(track.TrackId,
                            PurchaseRegion.MergeRegions(regions, track.AvailableMarkets));
                        updated += 1;
                        
                    }
                    else
                    {
                        failed += 1;
                    }
                }

                if ((changed+updated) % 100 == 99)
                {
                    Trace.WriteLine(string.Format("Skipped == {0}; Changed={1}; Updated={2}; Failed={3}", skipped, changed, updated, failed));
                    Thread.Sleep(5000);
                }

                if (changed + failed > count)
                    break;
            }
            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Updated purchase info: Skipped == {0}; Changed={1}; Udpated={2}; Failed={3}", skipped, changed, updated, failed);

            return View("Results");
        }

        private static bool IsSorted(string[] arr)
        {
            for (var i = 1; i < arr.Length; i++)
            {
                if (string.Compare(arr[i - 1],arr[i],StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return false;
                }
            }
            return true;
        }
        //
        // Get: //SpotifyRegions
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RegionStats(int count = int.MaxValue, int start = 0, string region = "US")
        {
            var counts = new Dictionary<string, int>();
            var codes = new HashSet<string>();
            var c = 0;
            var unsorted = 0;
            foreach (var prop in Database.SongProperties.Where(p => p.Name.StartsWith("Purchase:") && p.Name.EndsWith(":SS")))
            {
                string[] regions;
                PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out regions);

                if (regions == null) continue;

                if (!IsSorted(regions))
                    unsorted += 1;

                foreach (var code in regions)
                {
                    codes.Add(code);
                }

                var r = PurchaseRegion.FormatRegionInfo(regions);
                int citem;
                counts[r] = counts.TryGetValue(r, out citem) ? citem + 1 : 1;

                c += 1;
            }

            var unique = 0;
            foreach (var pair in counts)
            {
                Trace.WriteLine(string.Format("{0}\t{1}",pair.Value,pair.Key));
                unique += 1;
            }

            var cc = 0;
            foreach (var code in codes.OrderBy(x => x))
            {
                Trace.Write(code + ",");
                cc += 1;
            }
            Trace.WriteLine("");

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Region Stats: Total == {0}; Unsorted={1}; Unique={2}, Codes={3}", c, unsorted, unique,cc);

            return View("Results");
        }

        //
        // Get: //ScrapeDances
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult ScrapeDances(string id)
        {
            var songs = new List<SongDetails>();

            var scraper = DanceScraper.FromName(id);
            var lines = scraper.Scrape();

            var sb = new StringBuilder();
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                sb.AppendFormat("{0}\r\n", line);
            }

            var s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

            return File(stream, "text/plain", scraper.Name + ".csv");
        }

        //
        // Get: //RebuildUserTags
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RebuildUserTags(bool update=false, string songIds=null)
        {
            Database.RebuildUserTags(User.Identity.Name,update,songIds);

            ViewBag.Success = true;
            ViewBag.Message = "User Tags were successfully rebuilt";

            return View("Results");
        }

        //
        // Get: //RebuildDanceInfo
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RebuildDanceInfo()
        {
            try
            {
                StartAdminTask("RebuildDanceInfo");

                InternalRebuildDances(true);

                InternalRebuildDanceTags();
                SongCounts.ClearCache();

                return CompleteAdminTask(true, "Finished rebuilding Dance Info");
            }
            catch (Exception e)
            {
                return FailAdminTask("Dance info failed to rebuild", e);
            }
        }

        //
        // Get: //RebuildDances
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RebuildDances()
        {
            try
            {
                StartAdminTask("RebuildDances");

                InternalRebuildDances(false);

                return CompleteAdminTask(true, "Finished rebuilding Dances");
            }
            catch (Exception e)
            {
                return FailAdminTask("Dances failed to rebuild", e);
            }
        }

        //
        // Get: //RebuildDances
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RebuildDanceTags()
        {
            try
            {
                StartAdminTask("RebuildDanceTags");

                InternalRebuildDanceTags();

                return CompleteAdminTask(true,"Finished rebuilding Dance Tags");
            }
            catch (Exception e)
            {
                return FailAdminTask("Dances Tags failed to rebuild", e);
            }
        }

        //
        // Get: //ReloadDatabase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult ReloadDatabase(string reloadDatabase, bool? batch)
        {
            try
            {
                StartAdminTask("ReloadDatabase");
                AdminMonitor.UpdateTask("UploadFile");

                var lines = UploadFile();

                AdminMonitor.UpdateTask("Compute Type");

                if (lines.Count > 0)
                {
                    if (string.Equals(reloadDatabase, "reload", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (DanceMusicService.IsCompleteBackup(lines))
                        {
                            AdminMonitor.UpdateTask("Wipe Database");
                            RestoreDb(null);

                            var i = lines.FindIndex(DanceMusicService.IsDanceBreak);
                            var users = lines.GetRange(0, i).ToList();
                            lines.RemoveRange(0, i + 1);

                            i = lines.FindIndex(DanceMusicService.IsTagBreak);
                            var dances = lines.GetRange(0, i).ToList();
                            lines.RemoveRange(0, i + 1);

                            i = lines.FindIndex(DanceMusicService.IsSongBreak);
                            var tags = lines.GetRange(0, i).ToList();
                            lines.RemoveRange(0, i + 1);

                            Database.LoadUsers(users);
                            Database.LoadDances(dances);
                            Database.LoadTags(tags);

                            reloadDatabase = "loadSongs";
                        }
                        else if (DanceMusicService.IsSongBreak(lines[0]))
                        {
                            reloadDatabase = "loadSongs";
                        }
                        else if (DanceMusicService.IsUserBreak(lines[0]))
                        {
                            reloadDatabase = "users";
                        }
                        else if (DanceMusicService.IsDanceBreak(lines[0]))
                        {
                            reloadDatabase = "dances";
                        }
                        else if (DanceMusicService.IsTagBreak(lines[0]))
                        {
                            reloadDatabase = "tags";
                        }
                    }

                    if (string.Equals(reloadDatabase, "songs", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Database.UpdateSongs(lines);
                    }
                    else if (string.Equals(reloadDatabase, "users", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Database.LoadUsers(lines);
                    }
                    else if (string.Equals(reloadDatabase, "dances", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Database.LoadDances(lines);
                    }
                    else if (string.Equals(reloadDatabase, "tags", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Database.LoadTags(lines);
                    }
                    else if (string.Equals(reloadDatabase, "loadSongs", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Database.LoadSongs(lines);
                    }

                    SongCounts.ClearCache();

                    return CompleteAdminTask(true, "Database restored: " + reloadDatabase);
                }

                return CompleteAdminTask(false, "Empty File or Bad File Format" + reloadDatabase);
            }
            catch (Exception e)
            {
                return FailAdminTask(string.Format("{0}: {1}", reloadDatabase, e.Message), e);
            }
        }


        //
        // Get: //RebuildDances
        [Authorize(Roles = "dbAdmin")]
        public ActionResult AdminStatus()
        {
            return View(AdminMonitor.Status);
        }

        #region Tempi
        //
        // Post: //UploadTempi
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadTempi()
        {
            IList<LocalMerger> results = null;

            var lines = UploadFile();

            ViewBag.Name = "Upload Tempi";
            ViewBag.FileId = -1;
            ViewBag.Action = "CommitUploadTempi";

            var user = Database.FindUser(User.Identity.Name);

            if (lines.Count <= 0) return View("Error");

            var songs = SongsFromFile(user,lines);
            results = Database.MatchSongs(songs,DanceMusicService.MatchMethod.Tempo);
            ViewBag.FileId = CacheReview(results);

            return View("ReviewBatch", results);
        }


        //
        // Post: //CommitUploadTempi
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CommitUploadTempi(int fileId)
        {
            var initial = GetReviewById(fileId);
            IList<LocalMerger> results = null;

            ViewBag.Name = "Upload Tempi";
            ViewBag.FileId = fileId;

            var user = Database.FindUser(User.Identity.Name);

            Context.TrackChanges(false);
            var changed = 0;
            var added = 0;

            if (initial.Count <= 0) return View("Error");

            results = new List<LocalMerger>();
                
            foreach (var m in initial)
            {
                var sd = m.Left;
                // If there is an existing song
                    
                if (m.Right != null)
                {
                    var add = Database.AdditiveMerge(user, m.Right.SongId, sd, null, false);
                    if (add)
                    {
                        m.Right = Database.FindSongDetails(m.Right.SongId,user.UserName);;
                        results.Add(m);
                        changed += 1;
                    }
                }
                // Otherwise add it
                else
                {
                    var add = Database.CreateSong(user, sd, null, SongBase.CreateCommand, null, false);
                    if (add != null)
                    {
                        Database.Songs.Add(add);
                    }
                    added += 1;
                }

                if (((added + changed) % 100) == 0)
                {
                    Trace.WriteLine(string.Format("Tempo updated: {0}", added+changed));
                }
            }

            Context.TrackChanges(true);

            return View("ReviewBatch", results);
        }
        #endregion


        #region Tags
        //
        // Post: //UploadTagsTypes
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadTagTypes()
        {
            var lines = UploadFile();

            ViewBag.Name = "Upload Tags";

            if (lines.Count > 0)
            {
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cells = line.Split('\t');
                    if (cells.Length < 2) continue;

                    var name = cells[1].Trim();
                    var cat = cells[0].Trim();
                    var tt = Database.FindOrCreateTagType(name, cat);
                    if (cells.Length == 3 && !string.IsNullOrWhiteSpace(cells[2]))
                    {
                        tt.PrimaryId = cells[2].Trim() + ':' + cat;
                    }
                }
            }

            Database.SaveChanges();
            return View("Results");
        }

        //
        // Post: //UploadTags
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadTags()
        {
            var lines = UploadFile();

            ViewBag.Name = "Upload Tags";

            if (lines.Count <= 0) return View("Error");

            Context.TrackChanges(false);

            var entries = new Dictionary<string, string>();
            var count = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // DATE, SID, user, tag (unqualified)
                var cells = line.Split('\t');
                if (cells.Length != 4) continue;

                DateTime dt;
                if (!DateTime.TryParse(cells[0], out dt))
                {
                    continue;
                }
                Guid guid;
                if (!Guid.TryParse(cells[1].Substring(1), out guid))
                {
                    continue;
                }
                var user = cells[2].Trim();
                var tag = cells[3].Trim();
                var cat = "Music";

                var types = Database.GetTagTypes(tag).ToList();
                if (types.Count == 1)
                {
                    cat = types[0].Category;
                }
                else if (user != "batch" && Dances.Instance.DanceFromName(tag) != null)
                {
                    cat = "Dance";
                }
                tag += ":" + cat;

                var id = user + ":" + guid.ToString();
                string tags;
                if (entries.TryGetValue(id, out tags))
                {
                    TagList tl = new TagList(tags);
                    tl = tl.Add(new TagList(tag));
                    tags = tl.Summary;
                }
                else
                {
                    tags = tag;
                }
                entries[id] = tags;
                count += 1;

                if (count % 500 == 0)
                {
                    Trace.WriteLineIf(/*TraceLevels.General.TraceInfo*/ true, string.Format("Tags Loaded={0}", count));
                }
            }

            count = 0;
            foreach (var k in entries.Keys)
            {
                var tags = entries[k];
                var rg = k.Split(':');

                var guid = Guid.Parse(rg[1]);
                var userName = rg[0];

                var user = Database.FindUser(userName);
                var song = Database.FindSong(guid);

                if (user != null && song != null)
                {
                    song.CreateEditProperties(user, SongBase.EditCommand, Database);
                    song.AddTags(tags, user, Database, song);
                }

                count += 1;

                if (count % 500 == 0)
                {
                    Trace.WriteLineIf(/*TraceLevels.General.TraceInfo*/ true, string.Format("Tags Saved={0}", count));
                }
            }

            Context.TrackChanges(true);

            return View("Results");
        }


        #endregion


        #region Catalog

        //
        // Get: //UploadCatalog
        [HttpGet]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadCatalog(string separator=null, string headers=null, string dances=null, string artist=null, string album=null, string user=null, string tags=null)
        {
            // TODO:  This is probably a case where creating a viewmodel would be the right things to do...
            if (!string.IsNullOrEmpty(separator))
            {
                ViewBag.Separator = separator;
            }
            if (!string.IsNullOrEmpty(headers))
            {
                ViewBag.Headers = headers;
            }
            if (!string.IsNullOrEmpty(dances))
            {
                ViewBag.Dances = dances;
            }
            if (!string.IsNullOrEmpty(artist))
            {
                ViewBag.Artist = artist;
            }
            if (!string.IsNullOrEmpty(album))
            {
                ViewBag.Album = album;
            }
            if (!string.IsNullOrEmpty(user))
            {
                ViewBag.User = user;
            }
            if (!string.IsNullOrEmpty(tags))
            {
                ViewBag.Tags = tags;
            }
            return View();
        }

        //
        // Post: //UploadCatalog
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadCatalog(string songs, string separator, string headers, string user, string dances, string artist, string album, string tags)
        {
            ViewBag.Name = "Upload Catalog";

            if (string.IsNullOrWhiteSpace(songs) || string.IsNullOrWhiteSpace(separator))
            {
                // TODO: We should validate this on the client side - only way I know to do this is to have a full on class to
                // represent the fields, is there a lighter way???
                ViewBag.Success = false;
                ViewBag.Message = "Must have non-empty songs and separator fields";
                return View("Results");
            }
            IList<LocalMerger> results = null;

            var headerList = !string.IsNullOrWhiteSpace(headers) ? SongDetails.BuildHeaderMap(headers, ',') : HeaderFromList(CleanSeparator(separator), ref songs);

            var appuser = Database.FindUser(user);
           
            var newSongs = SongsFromList(appuser,CleanSeparator(separator), headerList, songs);

            var hasArtist = false;
            if (!string.IsNullOrEmpty(artist))
            {
                hasArtist = true;
                artist = artist.Trim();
            }

            AlbumDetails ad = null;
            var hasAlbum = false;
            if (!string.IsNullOrEmpty(album))
            {
                hasAlbum = true;
                album = album.Trim();
                ad = new AlbumDetails { Name = album };
            }

            tags = !string.IsNullOrEmpty(tags) ? tags.Trim() : null;

            if (hasArtist || hasAlbum || (tags != null))
            {
                foreach (var sd in newSongs)
                {
                    if (hasArtist && string.IsNullOrEmpty(sd.Artist))
                    {
                        sd.Artist = artist;
                    }
                    if (hasAlbum && string.IsNullOrEmpty(sd.Album))
                    {
                        sd.Albums.Add(ad);
                    }
                    if (tags != null)
                    {
                        sd.AddTags(tags,appuser,Database,sd,false);
                    }
                }
            }
            ViewBag.UserName = user;
            ViewBag.Dances = dances;
            ViewBag.Separator = separator;
            ViewBag.Headers = headers;
            ViewBag.Artist = artist;
            ViewBag.Album = album;
            ViewBag.Action = "CommitUploadCatalog";

            // ReSharper disable once InvertIf
            if (newSongs.Count > 0)
            {
                results = Database.MatchSongs(newSongs,DanceMusicService.MatchMethod.Merge);
                ViewBag.FileId = CacheReview(results);
            }

            return View("ReviewBatch", results);
        }


        //
        // Post: //CommitUploadCatalog
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CommitUploadCatalog(int fileId, string userName, string danceIds, string headers, string separator)
        {
            IList<LocalMerger> initial =  GetReviewById(fileId);

            ViewBag.Name = "Upload Catalog";
            ViewBag.FileId = fileId;
            ViewBag.User = userName;
            ViewBag.Dances = danceIds;
            ViewBag.Headers = headers;
            ViewBag.Separator = separator;

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = User.Identity.Name;
            }
            var user = Database.FindOrAddUser(userName,DanceMusicService.PseudoRole);

            List<string> dances = null;
            if (!string.IsNullOrWhiteSpace(danceIds))
            {
                dances = new List<string>(danceIds.Split(';'));
            }

            if (initial.Count <= 0) return View("Error");

            var modified = Database.MergeCatalog(user, initial, dances);

            if (modified)
            {
                Database.SaveChanges();
            }

            return View("UploadCatalog");
        }

        //
        // Get: //ScrapeSpotify
        [HttpGet]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult ScrapeSpotify(string url, string user, string dances, string tags)
        {
            ViewBag.Name = "Scrape Spotify";

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(user))
            {
                ViewBag.Success = false;
                ViewBag.Message = "Must have non-empty user and url fields";
                return View("Results");
            }

            var appuser = Database.FindOrAddUser(user, DanceMusicService.PseudoRole);

            var service = MusicService.GetService(ServiceType.Spotify);

            var tracks = ((DanceMusicContext)Database.Context).LookupServiceTracks(service, url, User);

            var newSongs = SongsFromTracks(appuser,tracks);

            if (!string.IsNullOrEmpty(tags))
            {
                foreach (var sd in newSongs)
                {
                    sd.AddTags(tags, appuser, Database, sd, false);
                }
            }

            ViewBag.UserName = user;
            ViewBag.Dances = dances;
            ViewBag.Action = "CommitUploadCatalog";

            IList<LocalMerger> results = null;
            // ReSharper disable once InvertIf
            if (newSongs.Count > 0)
            {
                results = Database.MatchSongs(newSongs, DanceMusicService.MatchMethod.Merge);
                ViewBag.FileId = CacheReview(results);
            }

            return View("ReviewBatch", results);
        }

        static string CleanSeparator(string separator)
        {
            if (string.IsNullOrWhiteSpace(separator))
            {
                separator = " - ";
            }
            else if (separator.Contains(@"\t"))
            {
                separator = separator.Replace(@"\t", "\t");
            }

            return separator;
        }

        #endregion


        //
        // Get: //BackupDatabase
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BackupDatabase(bool users = true, bool tags = true, bool dances = true, bool songs = true, string useLookupHistory = null)
        {
            try
            {
                StartAdminTask("Backup");

                var history = !string.IsNullOrWhiteSpace(useLookupHistory);

                var dt = DateTime.Now;
                var h = history ? "-lookup" : string.Empty;
                var fname = string.Format("backup-{0:d4}-{1:d2}-{2:d2}{3}.txt", dt.Year, dt.Month, dt.Day, h);
                var path = Path.Combine(Server.MapPath("~/content"),fname);

                using (var file = System.IO.File.CreateText(path))
                {
                    if (users)
                    {
                        var n = 0;
                        AdminMonitor.UpdateTask("users");
                        foreach (var line in Database.SerializeUsers())
                        {
                            file.WriteLine(line);
                            AdminMonitor.UpdateTask("users", ++n);
                        }
                    }

                    if (dances)
                    {
                        var n = 0;
                        AdminMonitor.UpdateTask("dances");
                        foreach (var line in Database.SerializeDances())
                        {
                            file.WriteLine(line);
                            AdminMonitor.UpdateTask("dances", ++n);
                        }
                    }

                    if (tags)
                    {
                        var n = 0;
                        AdminMonitor.UpdateTask("tags");
                        foreach (var line in Database.SerializeTags())
                        {
                            file.WriteLine(line);
                            AdminMonitor.UpdateTask("tags", ++n);
                        }
                    }

                    if (songs)
                    {
                        Context.Configuration.LazyLoadingEnabled = false;
                        var n = 0;
                        AdminMonitor.UpdateTask("songs");
                        var lines = Database.SerializeSongs(true, history);
                        AdminMonitor.UpdateTask("writeSongs");
                        foreach (var line in lines)
                        {
                            file.WriteLine(line);
                            AdminMonitor.UpdateTask("writeSongs", ++n);
                        }
                        Context.Configuration.LazyLoadingEnabled = true;
                    }
                }

                return File("~/content/" + fname, System.Net.Mime.MediaTypeNames.Text.Plain,fname);
                //AdminMonitor.CompleteTask(true, "Backup complete to: " + path);
                //var res = new FilePathResult("~/content/" + fname,System.Net.Mime.MediaTypeNames.Text.Plain);
                //res.FileDownloadName = fname;
                //return res;
            }
            catch (Exception e)
            {
                return FailAdminTask("Failed to backup database", e);
                //AdminMonitor.CompleteTask(false, "Failed to backup database", e);

                //var bytes = Encoding.UTF8.GetBytes(e.Message);
                //var stream = new MemoryStream(bytes);

                //var dt = DateTime.Now;
                //return File(stream, "text/plain", string.Format("backup-error-{0:d4}-{1:d2}-{2:d2}.txt", dt.Year, dt.Month, dt.Day));
            }
        }

        //
        // Get: //BackupTail
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BackupTail(int count = 100, DateTime? from = null, string filter = null)
        {
            SongFilter songFilter = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                songFilter = new SongFilter(filter);
            }

            var songs = Database.SerializeSongs(true, true, count, from, songFilter);

            var s = string.Join("\r\n", songs);
            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

            var dt = DateTime.Now;
            return File(stream, "text/plain", string.Format("tail-{0:d4}-{1:d2}-{2:d2}.txt", dt.Year, dt.Month, dt.Day));
        }

        //
        // Get: //BackupDelta
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BackupDelta()
        {
            var lines = UploadFile();

            var exclusions = new HashSet<Guid>();
            foreach (var line in lines)
            {
                Guid guid;
                if (Guid.TryParse(line, out guid))
                {
                    exclusions.Add(guid);
                }
            }

            var songs = Database.SerializeSongs(true, true, -1, null, null, exclusions);

            var s = string.Join("\r\n", songs);
            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

            var dt = DateTime.Now;
            return File(stream, "text/plain", string.Format("tail-{0:d4}-{1:d2}-{2:d2}.txt", dt.Year, dt.Month, dt.Day));
        }


        //
        // Get: //RestoreDatabase
        //[Authorize(Roles = "dbAdmin")]
        [AllowAnonymous]
        public ActionResult RestoreDatabase()
        {
            RestoreDb();

            ViewBag.Name = "Restore Database";
            ViewBag.Success = true;
            ViewBag.Message = "Database was successfully restored.";

            return View("Results");
        }

        //
        // Get: //CleanTempi
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CleanTempi()
        {
            var songs = from s in Database.Songs where s.TitleHash != 0 select s;
            var danceList = Dances.Instance.ExpandDanceList("WLZ");

            var cwlz = 0;
            songs = songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            //List<Song> songsT = songs.ToList();
            songs = songs.Where(s => s.Tempo > 190);
            //songsT = songs.ToList();

            foreach (var song in songs)
            {
                var newTempo = (song.Tempo.Value / 4) * 3;
                newTempo = Math.Round(newTempo, 2);
                song.Tempo = newTempo;
                cwlz += 1;
            }

            var csmb = 0;
            songs = from s in Database.Songs where s.TitleHash != 0 select s;
            songs = songs.Where(s => s.DanceRatings.Count == 1 && s.DanceRatings.Any(dr => dr.DanceId == "SMB"));
            //songsT = songs.ToList();
            songs = songs.Where(s => s.Tempo > 175);
            //songsT = songs.ToList();
            foreach (var song in songs)
            {
                decimal newTempo = (song.Tempo.Value / 2);
                newTempo = Math.Round(newTempo, 2);
                song.Tempo = newTempo;
                csmb += 1;
            }

            Database.SaveChanges();

            ViewBag.Name = "Clean Tempi";
            ViewBag.Success = true;
            ViewBag.Message = string.Format("{0} waltzes and {1} sambas fixed",cwlz,csmb);

            return View("Results");
        }

        //
        // Get: //CleanLookupHistory
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CleanLookupHistory()
        {
            var properties = from sp in Database.SongProperties where sp.Name == Song.FailedLookup select sp;

            Context.TrackChanges(false);
            var c = 0;
            foreach (var property in properties)
            {
                Database.SongProperties.Remove(property);
                c += 1;
            }

            Context.TrackChanges(true);

            ViewBag.Name = "Clean Lookup History";
            ViewBag.Success = true;
            ViewBag.Message = string.Format("{0} lookup records deleted", c);

            return View("Results");
        }        
        #endregion

        #region Migration-Restore

        private void RestoreDb(string state = "InitialCreate")
        {
            DbMigrator migrator;

            // Roll back to a specific migration or zero
            if (state != null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Rolling Back Database");
                migrator = BuildMigrator();
                migrator.Update(state);
            }
            else
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Wiping Database");
                var objectContextAdapter = Context as IObjectContextAdapter;
                if (objectContextAdapter != null)
                {
                    var objectContext = objectContextAdapter.ObjectContext;
                    objectContext.DeleteDatabase();
                }
                migrator = BuildMigrator();
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Starting Migrator Update");
            // Apply all migrations up to a specific migration
            migrator.Update();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting RestoreDB");
        }
        private void ReseedDb()
        {
            Configuration.DoSeed(Context);
        }

        private DbMigrator BuildMigrator()
        {
            var configuration = BuildConfiguration();

            return new DbMigrator(configuration);
        }

        private static Configuration BuildConfiguration()
        {
            var sqlConnectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            var connectionInfo = new DbConnectionInfo(sqlConnectionString, "System.Data.SqlClient");

            var configuration = new Configuration
            {
                TargetDatabase = connectionInfo,
                AutomaticMigrationsEnabled = false
            };

            return configuration;
        }
        #endregion

        #region Utilities

        private void InternalRebuildDances(bool checkpoint)
        {
            // Clear out the Top10s
            Context.Database.ExecuteSqlCommand("TRUNCATE TABLE dbo.TopNs");

            Context.TrackChanges(false);

            // TODO: Add include/exclude as permanent fixtures in the header and link them to appropriate cloud
            //  Think about how we might fix the multi-categorization problem (LTN vs. International Latin)
            //  Actually replace SongCounts

            // Get the Max Weight and Count of songs for each dance

            var index = 0;
            foreach (var dance in Database.Dances.Include("TopSongs.Song.DanceRatings"))
            {
                AdminMonitor.UpdateTask("UpdateDance = " + dance.Name,index++);

                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Computing info for " + dance.Name);
                dance.SongCount = dance.DanceRatings.Select(dr => dr.Song.Purchase).Count(p => p != null);
                dance.MaxWeight = (dance.SongCount == 0) ? 0 : dance.DanceRatings.Max(dr => dr.Weight);

                var filter = new SongFilter
                {
                    SortOrder = "Dances_10",
                    Dances = dance.Id,
                    TempoMax = 500,
                    TempoMin = 1
                };
                var songs = Database.BuildSongList(filter).ToList();

                if (songs.Count < 10)
                {
                    filter.TempoMax = null;
                    filter.TempoMin = null;
                    songs = Database.BuildSongList(filter).ToList();
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

            if (checkpoint)
            {
                Context.CheckpointChanges();
            }

            Context.TrackChanges(true);
        }

        private void InternalRebuildDanceTags()
        {
            Context.TrackChanges(false);

            var index = 0;
            foreach (var dance in Database.Dances)
            {
                AdminMonitor.UpdateTask("UpdateTags: Dance = " + dance.Name, index++);

                var dT = dance;
                var acc = new TagAccumulator();

                foreach (var rating in Database.DanceRatings.Where(dr => dr.DanceId == dT.Id).Include("Song"))
                {
                    acc.AddTags(rating.TagSummary);
                    acc.AddTags(rating.Song.TagSummary);
                }
                dance.SongTags = acc.TagSummary();

                Context.CheckpointSongs();
            }

            Context.TrackChanges(true);
        }
        private IList<string> HeaderFromList(string separator, ref string songs)
        {
            var cidx = songs.IndexOfAny(Environment.NewLine.ToCharArray());
            if (cidx == -1)
            {
                return null;
            }
            var line = songs.Substring(0, cidx);

            var map = SongDetails.BuildHeaderMap(line);

            // Kind of kludgy, but temporary build the header
            //  map to see if it's valid then pass back a cownomma
            // separated list of headers...
            if (map == null || map.All(p => p == null)) return null;

            songs = songs.Substring(cidx).TrimStart(Environment.NewLine.ToCharArray());
            return map;
        }

        private IList<SongDetails> SongsFromList(ApplicationUser user, string separator, IList<string> headers, string songText)
        {
            var lines = songText.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            return SongDetails.CreateFromRows(user, separator, headers, lines, Song.DanceRatingAutoCreate);
        }

        private IList<SongDetails> SongsFromTracks(ApplicationUser user, IEnumerable<ServiceTrack> tracks)
        {
            var sds = new List<SongDetails>();
            foreach (var track in tracks)
            {
                sds.Add(SongDetails.CreateFromTrack(user, track));
            }
            return sds;
        }

        private static IList<SongDetails> SongsFromFile(ApplicationUser user, IList<string> lines)
        {
            var songs = new List<SongDetails>();

            var map = SongDetails.BuildHeaderMap(lines[0]);
            lines.RemoveAt(0);
            return SongDetails.CreateFromRows(user, "\t", map, lines, SongBase.DanceRatingAutoCreate);
        }

        List<string> UploadFile() 
        {
            var lines = new List<string>();

            var files = Request.Files;
            if (files.Count != 1) return lines;

            var key = files.AllKeys[0];
            ViewBag.Key = key;
            // ReSharper disable once PossibleNullReferenceException
            ViewBag.Size = files[key].ContentLength;
            ViewBag.ContentType = files[key].ContentType;

            var file = Request.Files.Get(0);
            // ReSharper disable once PossibleNullReferenceException
            var stream = file.InputStream;

            TextReader tr = new StreamReader(stream);

            string s;
            while ((s = tr.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    lines.Add(s);
                }
            }

            return lines;
        }

        static int CacheReview(IList<LocalMerger> review)
        {
            int ret;
            lock (s_reviews)
            {
                s_reviews.Add(review);
                ret = s_reviews.Count - 1;
            }
            return ret;
        }

        IList<LocalMerger> GetReviewById(int id)
        {
            lock (s_reviews)
            {
                return s_reviews[id];
            }
        }

        // This is pretty kludgy as this is a basically a temporary
        //  store that only gets recycled on restart - but since
        //  for now it's being using primarily on the short running
        //  dev instance, it doesn't seem worthwhile to do anything
        //  more sophisticated
        static readonly IList<IList<LocalMerger>> s_reviews = new List<IList<LocalMerger>>();

        #endregion

        #region AdminTaskHelpers
        void StartAdminTask(string name)
        {
            ViewBag.Name = name;
            if (!AdminMonitor.StartTask(name))
            {
                throw new AdminTaskException(name + "failed to start because there is already an admin task running");
            }
        }

        private ActionResult CompleteAdminTask(bool completed, string message)
        {
            ViewBag.Success = completed;
            ViewBag.Message = message;
            AdminMonitor.CompleteTask(completed, message);

            return View("Results");
        }

        private ActionResult FailAdminTask(string message, Exception e)
        {
            ViewBag.Success = false;
            ViewBag.Message = message;

            if (!(e is AdminTaskException))
            {
                AdminMonitor.CompleteTask(false, message, e);
            }

            return View("Results");
        }
        #endregion
    }
}