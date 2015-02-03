using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using DanceLibrary;
using m4d.Scrapers;
using m4d.ViewModels;
using System.Web;
using m4dModels;
using Configuration = m4d.Migrations.Configuration;

namespace m4d.Controllers
{
    [Authorize]
    //[RequireHttps]
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
                Database.EditSong(user, sd, null, null, null);
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
                    Database.EditSong(user, sd, null, null, null, false);
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
        // Get: //UpdateTagSummaries
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTagSummaries()
        {
            ViewBag.Name = "UpdateTagSummaries";

            var count = 0;

            Context.TrackChanges(false);

            var counts = Database.TagTypes.ToDictionary(tt => tt.Key, tt => tt.Count);

            // Do the actual update
            foreach (var song in Database.Songs)
            {
                song.UpdateTagSummary(Database);
                count += 1;
            }

            // Validate that we didn't change counts...
            var changed = false;
            foreach (var tt in Database.TagTypes)
            {
                int c;
                // ReSharper disable once InvertIf
                if (!counts.TryGetValue(tt.Key, out c) || c != tt.Count)
                {
                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                ViewBag.Success = false;
                ViewBag.Message = "Changed underlying count unexpectedly";
            }
            else
            {
                Context.TrackChanges(true);

                ViewBag.Success = true;
                ViewBag.Message = string.Format(" Songs were fixed as tags({0})", count);

            }
            return View("Results");
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

                            // TODO: Consider re-using this code to tag songs as not-strict tempo (but it's actually the dance rating that should be tagged).
                            var drd = new DanceRatingDelta(dt.Id,4);
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
        [Authorize(Roles = "showDiagnostics")]
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
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult TestTrace(string message)
        {
            ViewBag.Name = "Test Trace";

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Trace message sentt: '{0}'", message);

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("Test Trace: '{0}'", message));
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
        // Get: //ReloadDatabase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult ReloadDatabase(string reloadDatabase, bool? batch)
        {
            var lines = UploadFile();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"File Uploaded Successfully");

            ViewBag.Name = "Restore Database";
            if (lines.Count > 0)
            {
                if (string.Equals(reloadDatabase,"reload",StringComparison.InvariantCultureIgnoreCase))
                {
                    if (DanceMusicService.IsUserBreak(lines[0]))
                    {
                        RestoreDb(null);

                        var i = lines.FindIndex(DanceMusicService.IsDanceBreak);
                        var users = lines.GetRange(0, i).ToList();
                        lines.RemoveRange(0, i + 1);

                        i = lines.FindIndex(DanceMusicService.IsTagBreak);
                        var dances = lines.GetRange(0,i).ToList();
                        lines.RemoveRange(0,i+1);

                        i = lines.FindIndex(DanceMusicService.IsSongBreak);
                        var tags = lines.GetRange(0,i).ToList();
                        lines.RemoveRange(0,i+1);

                        Database.LoadUsers(users);
                        Database.LoadDances(dances);
                        Database.LoadTags(tags);
                    }
                    else
                    {
                        Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Requested full reload, but invalid headers, so just doing songs");
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
                else 
                {
                    Database.LoadSongs(lines);
                }

                SongCounts.ClearCache(); 

                ViewBag.Success = true;
                ViewBag.Message = "Database was successfully restored and reloaded";
            }
            else
            {
                ViewBag.Success = false;
                ViewBag.Message = "Failed to reload database";
            }


            return View("Results");
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
            results = MatchSongs(songs,MatchMethod.Tempo);
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
                    if (add != null)
                    {
                        m.Right = add;
                        results.Add(m);
                    }
                    changed += 1;
                }
                // Otherwise add it
                else
                {
                    var add = Database.CreateSong(user, sd, SongBase.CreateCommand, null, false);
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
        public ActionResult UploadCatalog(string separator=null, string headers=null, string dances=null, string artist=null, string album=null, string user=null)
        {
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
            return View();
        }

        //
        // Post: //UploadCatalog
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadCatalog(string songs, string separator, string headers, string user, string dances, string artist, string album)
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

            if (hasArtist || hasAlbum)
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
                results = MatchSongs(newSongs, MatchMethod.Merge);
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

            var modified = false;

            foreach (var m in initial)
            {
                List<string> dancesT;
                if (m.Left.DanceRatings != null && m.Left.DanceRatings.Count > 0)
                {
                    dancesT = m.Left.DanceRatings.Select(dr => dr.DanceId).ToList();
                }
                else
                {
                    dancesT = dances;
                }

                // Matchtype of none indicates a new (to us) song, so just add it
                if (m.MatchType == MatchType.None)
                {
                    m.Left.UpdateDanceRatings(dancesT, Song.DanceRatingAutoCreate);
                    modified = Database.CreateSong(user, m.Left) != null;
                }
                // Any other matchtype should result in a merge, which for now is just adding the dance(s) from
                //  the new list to the existing song (or adding weight).
                // Now we're going to potentially add tempo - need a more general solution for this going forward
                else
                {
                    modified = Database.AdditiveMerge(user, m.Right.SongId, m.Left, dancesT) != null;
                }
            }

            if (modified)
            {
                Database.SaveChanges();
            }

            return View("UploadCatalog");
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
            var history = !string.IsNullOrWhiteSpace(useLookupHistory);

            var s = (users ? string.Join("\r\n", Database.SerializeUsers()) + "\r\n" : string.Empty) +
                    (dances ? string.Join("\r\n", Database.SerializeDances()) + "\r\n" : string.Empty) +
                    (tags ? string.Join("\r\n", Database.SerializeTags()) + "\r\n" : string.Empty) +
                    (tags ? string.Join("\r\n", Database.SerializeSongs(true,history)) + "\r\n" : string.Empty);

            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

            var dt = DateTime.Now;
            var h = history ? "+lookup" : string.Empty;
            return File(stream, "text/plain", string.Format("backup-{0:d4}-{1:d2}-{2:d2}{3}.txt",dt.Year,dt.Month,dt.Day,h));
        }

        //
        // Get: //BackupDatabase
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BackupTail(int count = 100)
        {
            var songs = Database.SerializeSongs(true, true, count);

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

        private static IList<SongDetails> SongsFromFile(ApplicationUser user, IList<string> lines)
        {
            var songs = new List<SongDetails>();

            var map = SongDetails.BuildHeaderMap(lines[0]);
            lines.RemoveAt(0);
            return SongDetails.CreateFromRows(user, "\t", map, lines, SongBase.DanceRatingAutoCreate);
        }
        private enum MatchMethod {None, Tempo, Merge};

        private IList<LocalMerger> MatchSongs(IList<SongDetails> newSongs, MatchMethod method)
        {
            var merge = new List<LocalMerger>();

            foreach (var song in newSongs)
            {
                var songT = song;
                var songs = from s in Database.Songs where (s.TitleHash == songT.TitleHash) select s;

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
                        foreach (var s in candidates.Where(s => s.Length.HasValue && Math.Abs(s.Length.Value - songD.Length.Value) < 5))
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
    }
}