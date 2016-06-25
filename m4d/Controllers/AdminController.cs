using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Web.Mvc;
using DanceLibrary;
using m4d.Scrapers;
using m4d.Utilities;
using m4d.ViewModels;
using m4dModels;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json;
using Configuration = m4d.Migrations.Configuration;

namespace m4d.Controllers
{
    [Authorize]
    public class AdminController : DMController
    {
        public override string DefaultTheme => AdminTheme;

        #region Commands

        //
        // GET: /Admin/
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult Index()
        {
            return View();
        }

        //
        // GET: /Admin/Tags
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult Tags()
        {
            return View();
        }
        
        //
        // GET: /Admin/Diagnostics
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult Diagnostics()
        {
            SetupDiagnostics();
            return View();
        }

        private void SetupDiagnostics()
        {
            ViewBag.TraceLevel = TraceLevels.General.Level.ToString();
            ViewBag.BotReport = SpiderManager.CreateBotReport();
            ViewBag.SearchIdx = SearchServiceInfo.DefaultId;
            ViewBag.StatsUpdateTime = DanceStatsManager.LastUpdate;
            ViewBag.StatsUpdateSource = DanceStatsManager.Source;
        }

        //
        // GET: /Admin/ResetAdmin
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult ResetAdmin()
        {
            AdminMonitor.CompleteTask(false,"Force Reset");
            SetupDiagnostics();
            return View("Diagnostics");
        }


        //
        // GET: /Admin/InitializaitonTasks
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult InitializationTasks()
        {
            return View();
        }

        //
        // GET: /Admin/UploadBackup
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadBackup()
        {
            return View();
        }

        //
        // GET: /Admin/Scraping
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult Scraping()
        {
            return View();
        }

        //
        // Get: //Reseed
        //[Authorize(Roles = "dbAdmin")]
        [AllowAnonymous]
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
        public ActionResult UpdatePurchase(string songIds = null)
        {
            ViewBag.Name = "Update Purchase Info";

            Database.UpdatePurchaseInfo(songIds);
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
            ViewBag.Message = $"Title Hashes were reseeded ({count})";

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
            var songs = from s in Database.Songs where string.Equals(s.Title, s.Artist) select s;
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
            ViewBag.Message = $"Artists were fixed ({count})";

            return View("Results");
        }

        //
        // Get: //UpdateAlbums
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateAlbums()
        {
            try
            {
                StartAdminTask("UpdateAlbums");
                AdminMonitor.UpdateTask("UpdateAblums");

                ViewBag.Name = "UpdateAlbums";
                var changed = 0;
                var scanned = 0;

                Context.TrackChanges(false);
                var user = Database.FindUser(User.Identity.Name);
                var songs = from s in Database.Songs where s.Title != null select s;
                foreach (var song in songs)
                {
                    changed += Database.CleanupAlbums(user, song);
                    scanned += 1;

                    if (scanned%100 == 0)
                    {
                        AdminMonitor.UpdateTask("UpdateAlbums", scanned);
                        Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                            $"Scanned == {scanned}; Changed={changed}");
                    }

                    if (scanned%1000 == 0)
                    {
                        Context.CheckpointSongs();
                    }
                }
                Context.TrackChanges(true);

                return CompleteAdminTask(true, $"{scanned} songs scanned, {changed} almubs were merged.");
            }
            catch (Exception e)
            {
                return FailAdminTask($"UpdateAlbums: {e.Message}", e);
            }
        }

        //
        // Get: //CleanDeletedSongs
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CleanDeletedSongs()
        {
            try
            {
                StartAdminTask("CleanDeletedSongs");
                var changed = Database.CleanDeletedSongs();
                return CompleteAdminTask(true, $"Songs were cleaned ({changed})");
            }
            catch (Exception e)
            {
                return FailAdminTask("Deleted Songs failed to update", e);
            }
        }

        //
        // Get: //UpdateTagTypes
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTagTypes(bool update = false)
        {
            try
            {
                StartAdminTask("UpdateTagTypes");
                var changed = Database.RebuildTagTypes(update);
                return CompleteAdminTask(true, $"Type counts were changed ({changed})");
            }
            catch (Exception e)
            {
                return FailAdminTask("Tag Types failed to update", e);
            }
        }

        //
        // Get: //UpdateTagSummaries
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTagSummaries(SongFilter filter = null)
        {
            if (filter == null) filter = SongFilter.Default;

            try
            {
                StartAdminTask("UpdateTagSummaries");
                Context.TrackChanges(false);

                // Do the actual update
                var sumChanged = 0;
                var i = 0;
                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (var s in Database.BuildSongList(filter,DanceMusicService.CruftFilter.AllCruft))
                {
                    AdminMonitor.UpdateTask("Update Song Tags", i++);
                    if (!s.UpdateTagSummaries(Database)) continue;

                    s.Modified = DateTime.Now;

                    sumChanged += 1;
                }

                Context.TrackChanges(true);
                return CompleteAdminTask(true, $" Songs/DanceRatings were fixed as tags({sumChanged})");
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

            var count = 0;
            Context.TrackChanges(false);

            var batch = Database.FindUser("batch");
            foreach (var song in Database.Songs)
            {
                var ngs = new Dictionary<string, DanceRatingDelta>();
                foreach (var dr in song.DanceRatings)
                {
                    var d = Dances.Instance.DanceFromId(dr.DanceId);
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
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Dance Instance: {song.Title}");
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
            ViewBag.Message = $"Dance groups were added ({count})";

            return View("Results");
        }

        //
        // Get: //InferDanceTypes
        [Authorize(Roles = "dbAdmin")]
        public ActionResult InferDanceTypes()
        {
            ViewBag.Name = "InferDanceTypes";

            var groups = new[] {"SWG", "TNG", "FXT", "WLZ"};

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
                    var d = Dances.Instance.DanceFromId(dr.DanceId);
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
                        var di = d as DanceInstance;
                        if (di != null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Dance Instance: {song.Title}");
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
            ViewBag.Message = $"Dance groups were added ({count})";

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
                    case SongBase.UserProxy:
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
            ViewBag.Message = $"Tags were removed ({count})";

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

            var zeroes =
                Database.SongProperties.Where(
                    prop => prop.Name == SongBase.TimeField && prop.Value == "01/01/0001 00:00:00").ToList();

            foreach (var prop in zeroes)
            {
                var times =
                    prop.Song.SongProperties.Where(p => p.Name == SongBase.TimeField && p.Value != "01/01/0001 00:00:00")
                        .OrderBy(p => p.Id)
                        .ToList();

                var np = times.FirstOrDefault();

                var d = np?.ObjectValue as DateTime?;

                if (d == null) continue;

                prop.Value = np.Value;
                prop.Song.Created = d.Value;

                count += 1;
            }
            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = $"Times were wrong {zeroes.Count}, fixed ({count})";

            return View("Results");
        }

        //
        // Get: // TimesFromProperties
        [Authorize(Roles = "dbAdmin")]
        public ActionResult TimesFromProperties(DateTime? from = null, string exclude=null)
        {
            try
            {
                StartAdminTask("TimesFromProperties");
                AdminMonitor.UpdateTask("TimesFromProperties");

                Context.TrackChanges(false);
                var modified = 0;
                var count = 0;
                var songs = Database.Songs.Where(s => s.TitleHash != 0);
                if (from.HasValue)
                {
                    songs = songs.Where(s => s.Modified > from.Value);
                }

                HashSet<string> excludeUsers = null;
                if (!string.IsNullOrWhiteSpace(exclude))
                {
                    excludeUsers = new HashSet<string>(exclude.Split('|'));
                }

                foreach (var song in songs)
                {
                    if (song.SetTimesFromProperties(excludeUsers))
                    {
                        modified += 1;
                    }

                    count += 1;

                    if (count%1000 != 0) continue;

                    AdminMonitor.UpdateTask("TimesFromProperties", count);
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Checkpoint at {count}");

                    Context.CheckpointSongs();
                }
                Context.TrackChanges(true);

                return CompleteAdminTask(true, $"Times were update {modified}, total covered = {count}");
            }
            catch (Exception e)
            {
                return FailAdminTask($"UpdateAlbums: {e.Message}", e);
            }
        }


        //
        // Get: //ClearSongCache
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult ClearSongCache(bool reloadFromStore=false, bool reloadFromFile = false)
        {
            ViewBag.Name = "ClearSongCache";

            DanceStatsManager.ClearCache(reloadFromStore ? Database : null, reloadFromFile);

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

            var tl = (TraceLevel) level;

            TraceLevels.SetGeneralLevel(tl);

            ViewBag.Success = true;
            ViewBag.Message = $"Trace level set: {tl.ToString()}";

            return View("Results");
        }

        //
        // Get: //TestTrace
        [AllowAnonymous]
        public ActionResult TestTrace(string message)
        {
            ViewBag.Name = "Test Trace";

            ViewBag.Success = true;
            ViewBag.Message = $"Trace message sent: '{message}'";

            Trace.WriteLine($"Test Trace ({TraceLevels.General}): '{message}'");
            return View("Results");
        }

        //
        // Get: //DisableTelemetry
        public ActionResult DisableTelemetry(bool disable)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Disable Telemetry: '{disable}'");
            TelemetryConfiguration.Active.DisableTelemetry = disable;

            return RedirectToAction("Diagnostics");
        }

        public ActionResult EnableVerboseTelemetry(bool enable)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Enable Verbose Telemetry: '{enable}'");
            VerboseTelemetry = enable;
            if (enable)
            {
                TelemetryConfiguration.Active.DisableTelemetry = false;
            }

            return RedirectToAction("Diagnostics");
        }

        //
        // Get: //SetUseSql
        public ActionResult SetUseSql(bool flag)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Set Use Sql: '{flag}'");
            SearchServiceInfo.UseSql = flag;

            SiteMapInfo.LoadCategories();

            return RedirectToAction("Diagnostics");
        }

        //
        // Get: //SetSearchIdx
        public ActionResult SetSearchIdx(string id)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Set Search Index: '{id}'");
            SearchServiceInfo.DefaultId = id;

            return RedirectToAction("Diagnostics");
        }

        //
        // Get: //CompressRegions
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CompressRegions()
        {
            Context.TrackChanges(false);
            var c = 0;
            var bytes = 0;
            var fxd = 0;

            foreach (var prop in Database.SongProperties.Where(p => p.Name.StartsWith("Purchase:") && p.Name.EndsWith(":SS")))
            {
                string[] regions;
                var id = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out regions);

                if (regions == null)
                {
                    var fix = PurchaseRegion.FixRegionInfo(prop.Value);
                    if (fix != null)
                    {
                        bytes += prop.Value.Length - fix.Length;
                        prop.Value = fix;
                        fxd += 1;
                    }
                    continue;
                }

                var newValue = PurchaseRegion.FormatIdAndRegionInfo(id, regions);

                if (string.Equals(prop.Value, newValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bytes += prop.Value.Length - newValue.Length;
                prop.Value = newValue;

                c += 1;
            }

            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message = $"Regions: Compressed == {c}; Fixed == {fxd}; Bytes={bytes}";

            return View("Results");
        }


        //
        // Get: //SpotifyRegions
        [Authorize(Roles = "dbAdmin")]
        public ActionResult SpotifyRegions(int count = int.MaxValue, int start = 0, string region = "US")
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
                string[] regions;
                var id = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out regions);

                if (null == regions)
                {
                    // Not sure how we are getting duplicate region info, but this will fix it
                    var fix = PurchaseRegion.FixRegionInfo(prop.Value);
                    if (fix != null)
                    {
                        prop.Value = fix;
                        changed += 1;
                    }
                    else
                    {
                        var track = MusicServiceManager.GetMusicServiceTrack(prop.Value, spotify);
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
                }

                if (regions == null || string.IsNullOrWhiteSpace(region) || regions.Contains(region))
                {
                    skipped += 1;
                }
                else if (!string.IsNullOrWhiteSpace(region))
                {
                    var track = MusicServiceManager.CoerceTrackRegion(id, spotify, region);
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

                if ((changed + updated)%100 == 99)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                        $"Skipped == {skipped}; Changed={changed}; Updated={updated}; Failed={failed}");
                    Thread.Sleep(5000);
                }

                if (changed + failed > count)
                    break;
            }
            Context.TrackChanges(true);

            ViewBag.Success = true;
            ViewBag.Message =
                $"Updated purchase info: Skipped == {skipped}; Changed={changed}; Udpated={updated}; Failed={failed}";

            return View("Results");
        }

        private static bool IsSorted(string[] arr)
        {
            for (var i = 1; i < arr.Length; i++)
            {
                if (string.Compare(arr[i - 1], arr[i], StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return false;
                }
            }
            return true;
        }

        //
        // Get: //SpotifyRegions
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RegionStats()
        {
            var counts = new Dictionary<string, int>();
            var codes = new HashSet<string>();
            var c = 0;
            var unsorted = 0;
            foreach (
                var prop in Database.SongProperties.Where(p => p.Name.StartsWith("Purchase:") && p.Name.EndsWith(":SS"))
                )
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
                Trace.WriteLine($"{pair.Value}\t{pair.Key}");
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
            ViewBag.Message = $"Region Stats: Total == {c}; Unsorted={unsorted}; Unique={unique}, Codes={cc}";

            return View("Results");
        }

        //
        // Get: //ScrapeDances
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult ScrapeDances(string id, string parameter=null)
        {
            var scraper = DanceScraper.FromName(id);
            string extra = string.Empty;
            if (!string.IsNullOrEmpty(parameter))
            {
                scraper.Parameter = parameter;
                extra = "-" + parameter.ToLower().Replace(' ', '-');
            }
            var lines = scraper.Scrape();

            var sb = new StringBuilder();
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                sb.AppendFormat("{0}\r\n", line);
            }

            var s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

            return File(stream, "text/plain", scraper.Name + extra + ".csv");
        }

        //
        // Get: //UpdateUsernames
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateUsernames(bool makePublic)
        {
            try
            {
                StartAdminTask("UpdateUsernames");
                AdminMonitor.UpdateTask("UploadFile");

                var lines = UploadFile();

                if (!lines.Any()) return CompleteAdminTask(false, "Empty File or Bad File Format");

                var c = 0;

                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (var line in lines)
                {
                    var cells = line.Split(new[] {'\t',' '},StringSplitOptions.RemoveEmptyEntries);
                    if (cells.Length != 2) continue;

                    AdminMonitor.UpdateTask($"Convert {cells[0]} to {cells[1]}");

                    if (string.Equals(cells[0], cells[1]))
                        continue;

                    if (UserManager.FindByName(cells[1]) != null)
                    {
                        Trace.WriteLine($"Can't rename {cells[0]} because {cells[1]} is already in use.");
                        continue;
                    }

                    var user = UserManager.FindByName(cells[0]);
                    if (user == null)
                    {
                        Trace.WriteLine($"Can't rename {cells[0]} because it doesn't exist.");
                        continue;
                    }

                    user.UserName = cells[1];
                    if (makePublic)
                    {
                        user.Privacy = 255;
                    }
                    Database.ChangeUserName(cells[0],cells[1]);
                    Database.SaveChanges();

                    c += 1;
                }

                return CompleteAdminTask(true, $"updated {c} users");
            }
            catch (Exception e )
            {
                return FailAdminTask($"UpdateUsernames: {e.Message}", e);
            }
        }


        //
        // Get: //RebuildUserTags
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RebuildUserTags(bool update=false, string songIds=null, string filter=null)
        {
            SongFilter songFilter = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                songFilter = new SongFilter(filter);
            }

            Database.RebuildUserTags(User.Identity.Name,update,songIds,songFilter);

            ViewBag.Success = true;
            ViewBag.Message = "User Tags were successfully rebuilt";

            return View("Results");
        }

        //
        // Get: //RebuildTags
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RebuildTags(string songIds = null, string filter = null)
        {
            SongFilter songFilter = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                songFilter = new SongFilter(filter);
            }

            Database.RebuildTags(User.Identity.Name, songIds, songFilter);

            ViewBag.Success = true;
            ViewBag.Message = "Tags were successfully rebuilt";

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

                Database.RebuildDanceInfo();
                RecomputeMarker.SetMarker("danceinfo");

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

                Database.RebuildDances();

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

                Database.RebuildDanceTags();

                return CompleteAdminTask(true,"Finished rebuilding Dance Tags");
            }
            catch (Exception e)
            {
                return FailAdminTask("Dances Tags failed to rebuild", e);
            }
        }

        //
        // Get: //AzureFacets
        [Authorize(Roles = "dbAdmin")]
        public ActionResult AzureFacets(string categories, int count)
        {
            try
            {
                StartAdminTask("BuildFacets");

                var facets = Database.GetTagFacets(categories, count);

                foreach (var facet in facets)
                {
                    Trace.WriteLine($"------------------{facet.Key}----------------");
                    foreach (var value in facet.Value)
                    {
                        Trace.WriteLine($"{value.Value}: {value.Count}");
                    }
                }

                return CompleteAdminTask(true, "Finished rebuilding Dance Tags");
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
                    var users = TryGetSection(lines,DanceMusicService.IsUserBreak);
                    var dances = TryGetSection(lines, DanceMusicService.IsDanceBreak);
                    var tags = TryGetSection(lines, DanceMusicService.IsTagBreak);
                    var searches = TryGetSection(lines, DanceMusicService.IsSearchBreak);
                    var songs = TryGetSection(lines, DanceMusicService.IsSongBreak);

                    var admin = string.Equals(reloadDatabase, "admin", StringComparison.InvariantCultureIgnoreCase);
                    var reload = false;
                    if (string.Equals(reloadDatabase, "reload", StringComparison.InvariantCultureIgnoreCase))
                    {
                        reload = true;
                        if (users != null && dances != null && tags != null && songs != null)
                        {
                            AdminMonitor.UpdateTask("Wipe Database");
                            RestoreDb(null);
                        }
                    }

                    if (users != null) Database.LoadUsers(users);
                    if (dances != null) Database.LoadDances(dances);
                    if (tags != null) Database.LoadTags(tags);
                    if (searches != null) Database.LoadSearches(searches);
                    if (songs != null)
                    {
                        if (reload)
                            Database.LoadSongs(songs);
                        else if (admin)
                            Database.AdminUpdate(songs);
                        else
                            Database.UpdateSongs(songs);
                        DanceStatsManager.ClearCache();
                    }

                    return CompleteAdminTask(true, "Database restored");
                }

                return CompleteAdminTask(false, "Empty File or Bad File Format");
            }
            catch (Exception e)
            {
                return FailAdminTask($"{reloadDatabase}: {e.Message}", e);
            }
        }

        private static List<string> TryGetSection(List<string> lines, Predicate<string> start)
        {
            var breaks = new Predicate<string>[] {DanceMusicService.IsDanceBreak, DanceMusicService.IsTagBreak, DanceMusicService.IsSearchBreak, DanceMusicService.IsSongBreak};

            if (!start(lines[0])) return null;

            var i = -1;
            foreach (var b in breaks)
            {
                i = lines.FindIndex(1, b);
                if (i != -1) break;
            }

            if (i == -1) return lines;

            var ret = lines.GetRange(0, i).ToList();
            lines.RemoveRange(0, i);

            return ret;
        }

        //
        // Get: //AdminStatus
        [Authorize(Roles = "dbAdmin")]
        public ActionResult FlushTelemetry()
        {
            ViewBag.Name = "Flush Telemetry";

            TelemetryClient.Flush();

            ViewBag.Success = true;
            ViewBag.Message = "Telemetry has been flushed";

            return View("Results");
        }

        //
        // Get: //AdminStatus
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
            var lines = UploadFile();

            ViewBag.Name = "Upload Tempi";
            ViewBag.FileId = -1;
            ViewBag.Action = "CommitUploadTempi";

            var user = Database.FindUser(User.Identity.Name);

            if (lines.Count <= 0) return View("Error");

            var songs = SongsFromFile(user,lines);
            var results = Database.MatchSongs(songs,DanceMusicService.MatchMethod.Tempo);
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

            ViewBag.Name = "Upload Tempi";
            ViewBag.FileId = fileId;

            var user = Database.FindUser(User.Identity.Name);

            Context.TrackChanges(false);
            var changed = 0;
            var added = 0;

            if (initial.Count <= 0) return View("Error");

            IList<LocalMerger> results = new List<LocalMerger>();
                
            foreach (var m in initial)
            {
                var sd = m.Left;
                // If there is an existing song
                    
                if (m.Right != null)
                {
                    var add = Database.AdditiveMerge(user, m.Right.SongId, sd, null, false);
                    if (add)
                    {
                        m.Right = Database.FindSongDetails(m.Right.SongId,user.UserName);
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
                    Trace.WriteLineIf(TraceLevels.General.TraceError, $"Tempo updated: {added + changed}");
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
                foreach (var line in lines)
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
                    var tl = new TagList(tags);
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
                    Trace.WriteLineIf(/*TraceLevels.General.TraceInfo*/ true, $"Tags Loaded={count}");
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
                    Trace.WriteLineIf(/*TraceLevels.General.TraceInfo*/ true, $"Tags Saved={count}");
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

            IList<string> lines = null;

            if (string.IsNullOrWhiteSpace(songs) || string.IsNullOrWhiteSpace(separator))
            {
                lines = UploadFile();

                if (lines == null || lines.Count < 2)
                {
                    // TODO: We should validate this on the client side - only way I know to do this is to have a full on class to
                    // represent the fields, is there a lighter way???
                    ViewBag.Success = false;
                    ViewBag.Message = "Must have non-empty songs and separator fields or a valid file";
                    return View("Results");
                }

                separator = "\t";
            }

            IList<LocalMerger> results = null;

            var appuser = Database.FindUser(user);

            if (lines == null)
            {
                lines = FileToLines(songs);
            }

            var headerList = !string.IsNullOrWhiteSpace(headers) ? SongDetails.BuildHeaderMap(headers, ',') : HeaderFromList(CleanSeparator(separator), lines);

            var newSongs = SongDetails.CreateFromRows(appuser, separator, headerList, lines, Database.TagMap, SongBase.DanceRatingCreate);

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
            TagList tagList = null;
            if (tags != null)
            {
                tagList = newSongs[0].VerifyTags(tags, false);
                if (tagList == null)
                {
                    ViewBag.ErrorMessage = $"Invalid Tag List: {tags}";
                    return View("Error");
                }
            }

            if (hasArtist || hasAlbum || (tagList != null))
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
                    if (tagList != null)
                    {
                        sd.AddTags(tagList,appuser,Database,sd,false);
                    }
                }
            }
            ViewBag.UserName = user;
            ViewBag.Dances = dances;
            ViewBag.Separator = separator;
            ViewBag.Headers = headers;
            ViewBag.Artist = artist;
            ViewBag.Album = album;
            ViewBag.Tags = tags;
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
            var initial =  GetReviewById(fileId);

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
        public ActionResult ScrapeSpotify(string url, string user, string dances, string songTags=null, string danceTags=null)
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

            var tracks = MusicServiceManager.LookupServiceTracks(service, url, User);

            var newSongs = SongsFromTracks(appuser,tracks,dances,songTags,danceTags);


            ViewBag.UserName = user;
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
        // Get: //IndexBackup
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult IndexBackup(string name="default", int count = -1, DateTime? from = null, string filter = null)
        {
            try
            {
                StartAdminTask("Index Backup");

                var dt = DateTime.Now;
                var fname = $"index-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.txt";
                var path = Path.Combine(Server.MapPath("~/App_Data"), fname);

                var n = 0;
                using (var file = System.IO.File.CreateText(path))
                {
                        var lines = Database.BackupIndex(name,count,from,filter);
                        foreach (var line in lines)
                        {
                            file.WriteLine(line);
                            AdminMonitor.UpdateTask("writeSongs", ++n);
                        }
                }

                AdminMonitor.CompleteTask(true, $"Backup ({n} songs) complete to: {path}");
                return File("~/app_data/" + fname, MediaTypeNames.Text.Plain, fname);
            }
            catch (Exception e)
            {
                return FailAdminTask("Failed to backup index", e);
            }

        }

        //
        // Get: //DanceStatistics
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult DanceStatistics(string source = null, bool save=true)
        {
            DanceStatsInstance instance;
            source = string.IsNullOrWhiteSpace(source) ? null : source;
            switch (source)
            {
                default:
                    instance = DanceStatsManager.LoadFromAzure(Database, source, save);
                    break;
                case "sql":
                    instance = DanceStatsManager.LoadFromSql(Database,save);
                    break;
                case null:
                    instance = DanceStatsManager.GetInstance(Database);
                    break;
            }

            return new JsonNetResult(
                instance.Tree,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
                },
                Formatting.Indented);
        }

        //
        // Get: //BackupDatabase
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BackupDatabase(bool users = true, bool tags = true, bool dances = true, bool searches=true, bool songs = true, string useLookupHistory = null)
        {
            try
            {
                StartAdminTask("Backup");

                var history = !string.IsNullOrWhiteSpace(useLookupHistory);

                var dt = DateTime.Now;
                var h = history ? "-lookup" : string.Empty;
                var fname = $"backup-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}{h}.txt";
                var path = Path.Combine(Server.MapPath("~/App_Data"),fname);

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

                    if (searches)
                    {
                        var n = 0;
                        AdminMonitor.UpdateTask("searches");
                        foreach (var line in Database.SerializeSearches())
                        {
                            file.WriteLine(line);
                            AdminMonitor.UpdateTask("searches", ++n);
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

                AdminMonitor.CompleteTask(true, "Backup complete to: " + path);
                return File("~/app_data/" + fname, MediaTypeNames.Text.Plain,fname);
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
            var users = Database.SerializeUsers(true,from);
            var dances = Database.SerializeDances(true, from);

            SongFilter songFilter = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                songFilter = new SongFilter(filter);
            }

            var songs = Database.SerializeSongs(true, true, count, from, songFilter);

            var s = string.Empty;
            if (users.Count > 0)
            {
                s += string.Join("\r\n", users) + "\r\n"; 
            }
            if (dances.Count > 0)
            {
                s += string.Join("\r\n", dances) + "\r\n";
            }
            s += string.Join("\r\n", songs);

            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

            var dt = DateTime.Now;
            return File(stream, "text/plain", $"tail-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.txt");
        }


        //
        // Get: //BackupJson
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BackupJson(SongFilter filter)
        {
            var songs = Database.BuildSongList(filter, DanceMusicService.CruftFilter.AllCruft);

            var lines = new List<string>();
            foreach (var song in songs)
            {
                lines.Add(new SongDetails(song,null,Database).ToJson());
            }

            var s = "[\r\n" + string.Join(",\r\n", lines) + "\r\n]";

            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

            var dt = DateTime.Now;
            return File(stream, "text/json", $"json-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.txt");
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
            return File(stream, "text/plain", $"tail-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.txt");
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
        // Get: //UpdateDatabase
        [AllowAnonymous]
        public ActionResult UpdateDatabase(string state=null)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Updating Database");
            var configuration = new Configuration();
            var migrator = new DbMigrator(configuration);
            migrator.Update(state);

            ViewBag.Name = "Update Database";
            ViewBag.Success = true;
            ViewBag.Message = "Database was successfully updated.";

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
                if (song.Tempo != null)
                {
                    var newTempo = (song.Tempo.Value / 4) * 3;
                    newTempo = Math.Round(newTempo, 2);
                    song.Tempo = newTempo;
                }
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
                if (song.Tempo != null)
                {
                    var newTempo = (song.Tempo.Value / 2);
                    newTempo = Math.Round(newTempo, 2);
                    song.Tempo = newTempo;
                }
                csmb += 1;
            }

            Database.SaveChanges();

            ViewBag.Name = "Clean Tempi";
            ViewBag.Success = true;
            ViewBag.Message = $"{cwlz} waltzes and {csmb} sambas fixed";

            return View("Results");
        }

        //
        // Get: //CleanLookupHistory
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CleanLookupHistory()
        {
            var properties = from sp in Database.SongProperties where sp.Name == SongBase.FailedLookup select sp;

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
            ViewBag.Message = $"{c} lookup records deleted";

            return View("Results");
        }

        //
        // Get: //CleanAlbums
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CleanAlbums()
        {
            var properties = from sp in Database.SongProperties where sp.Name == SongBase.AlbumOrder select sp;

            Context.TrackChanges(false);

            var dups = new Dictionary<Guid, SongProperty>();
            var cBogus = 0;
            var cRedundant = 0;
            foreach (var property in properties)
            {
                var reorder = property.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                if (reorder.Count != reorder.Distinct().Count())
                {
                    Database.SongProperties.Remove(property);
                    cBogus += 1;
                }
                else
                {
                    SongProperty temp;
                    if (dups.TryGetValue(property.SongId, out temp))
                    {
                        Database.SongProperties.Remove(temp);
                        cRedundant += 1;
                    }
                    dups[property.SongId] = property;
                }
            }

            dups.Clear();
            Context.TrackChanges(true);

            ViewBag.Name = "Clean album reorders";
            ViewBag.Success = true;
            ViewBag.Message = $"{cBogus} bogus and {cRedundant} album reorders deleted";

            return View("Results");
        }
        #endregion

        #region Search
        //
        // Get: //ResetSearchIdx
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult ResetSearchIdx(string id = "default")
        {
            try
            {
                StartAdminTask("ResetIndex");

                var success = Database.ResetIndex(id);

                if (success)
                    RecomputeMarker.SetMarker("indexsongs", DateTime.MinValue);

                ViewBag.Name = "Reset Index";
                ViewBag.Success = success;
                ViewBag.Message = @"Index Reset";

                return CompleteAdminTask(success, @"Index Reset");
            }
            catch (Exception e)
            {
                return FailAdminTask($"Reset: {e.Message}", e);
            }
        }

        //
        // Get: //BuildSearchIdx
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BuildSearchIdx(string id = "default", int count = 100, DateTime? from = null, bool rebuild = false, string filter = null)
        {
            try
            {
                StartAdminTask("BuildIndex");

                var name = "indexsongs-" + id;

                SongFilter songFilter = null;
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    songFilter = new SongFilter(filter);
                }

                if (!from.HasValue)
                {
                    from = RecomputeMarker.GetMarker(name);
                }

                var info = Database.IndexSongs(count, from, rebuild, songFilter);

                if (info.Succeeded > 0)
                {
                    RecomputeMarker.SetMarker(name, info.LastTime);
                }

                ViewBag.Name = "Indexed Songs " + id;
                ViewBag.Success = true;

                ViewBag.Message = $"{info.Succeeded} songs indexed, {info.Failed} failed. {(info.Message ?? string.Empty)}";

                return CompleteAdminTask(true, $"{info.Succeeded} songs indexed ({id}).");
            }
            catch (Exception e)
            {
                return FailAdminTask($"BuildIndex: {e.Message}", e);
            }
        }

        //
        // Get: //UpdateSearchIdx
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult UpdateSearchIdx(string id = "default")
        {
            try
            {
                StartAdminTask("UpdateIndex");

                var count = Database.UpdateAzureIndex(id);

                ViewBag.Name = "Update Song Index";
                ViewBag.Success = true;

                ViewBag.Message = $"{count} songs indexed";

                return CompleteAdminTask(true, $"{count} songs indexed.");
            }
            catch (Exception e)
            {
                return FailAdminTask($"UpdateIndex: {e.Message}", e);
            }
        }

        //
        // Get: //CleanupProperties
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult CleanupProperties(int count = 100, string filter = null)
        {
            try
            {
                StartAdminTask("CleanupProperties");

                SongFilter songFilter = null;
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    songFilter = new SongFilter(filter);
                }

                var from = RecomputeMarker.GetMarker("propertycleanup");

                var info = Database.CleanupProperties(count, from, songFilter);

                if (info.Succeeded > 0)
                {
                    RecomputeMarker.SetMarker("propertycleanup", info.LastTime);
                }

                ViewBag.Name = "Cleaned up Properties";
                ViewBag.Success = true;

                ViewBag.Message = $"{info.Succeeded} songs cleanded, {info.Failed} failed.";

                return CompleteAdminTask(true, $"{info.Succeeded} songs cleaned up. {info.Message}");
            }
            catch (Exception e)
            {
                return FailAdminTask($"CleanupProperties: {e.Message}", e);
            }
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

        private IList<string> HeaderFromList(string separator, IList<string> songs)
        {
            if (separator == null) throw new ArgumentNullException(nameof(separator));
            if (songs.Count < 2) throw new ArgumentOutOfRangeException(nameof(songs));
            var line = songs[0];

            var map = SongDetails.BuildHeaderMap(line);

            // Kind of kludgy, but temporary build the header
            //  map to see if it's valid then pass back a cownomma
            // separated list of headers...
            if (map == null || map.All(p => p == null)) return null;

            songs.RemoveAt(0);
            return map;
        }

        private static IList<string> FileToLines(string file)
        {
            return file.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private IList<SongDetails> SongsFromTracks(ApplicationUser user, IEnumerable<ServiceTrack> tracks, string dances, string songTags, string danceTags)
        {
            return tracks.Where(track => !string.IsNullOrEmpty(track.Artist)).Select(track => SongDetails.CreateFromTrack(user, track, dances, songTags, danceTags, Database.TagMap)).ToList();
        }

        private IList<SongDetails> SongsFromFile(ApplicationUser user, IList<string> lines)
        {
            var map = SongDetails.BuildHeaderMap(lines[0]);
            lines.RemoveAt(0);
            return SongDetails.CreateFromRows(user, "\t", map, lines, Database.TagMap, SongBase.DanceRatingCreate);
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
            lock (Reviews)
            {
                Reviews.Add(review);
                ret = Reviews.Count - 1;
            }
            return ret;
        }

        IList<LocalMerger> GetReviewById(int id)
        {
            lock (Reviews)
            {
                return Reviews[id];
            }
        }

        // This is pretty kludgy as this is a basically a temporary
        //  store that only gets recycled on restart - but since
        //  for now it's being using primarily on the short running
        //  dev instance, it doesn't seem worthwhile to do anything
        //  more sophisticated
        static readonly IList<IList<LocalMerger>> Reviews = new List<IList<LocalMerger>>();

        #endregion
    }
}