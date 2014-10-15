using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;

using m4d.Migrations;
using m4d.Context;
using m4d.Scrapers;
using m4d.ViewModels;
using m4dModels;
using DanceLibrary;
using System.Text.RegularExpressions;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using m4d.Utilities;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity;
using System.Data.Entity.Core.Objects;
using EntityFramework.Utilities;

namespace m4d.Controllers
{
    [Authorize]
    [RequireHttps]
    public class AdminController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return AdminTheme;
            }
        }

        private DanceMusicContext _db = new DanceMusicContext();

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
        // Get: //SeedDatabase
        [Authorize(Roles = "dbAdmin")]
        public ActionResult SeedDatabase()
        {
            //List<string> results = new List<string>();

            //bool seeded = false;
            //if (_db.Songs.Any(s => s.Title == "Tea for Two"))
            //{
            //    seeded = true;
            //}

            //if (!seeded)
            //{
            //    foreach (string name in _dbs)
            //    {
            //        string file = string.Format("~/Content/{0}.csv", name);
            //        string path = Server.MapPath(Url.Content(file));

            //        string[] lines = System.IO.File.ReadAllLines(path);

            //        if (lines.Length > 1)
            //        {
            //            BuildSchema(lines);

            //            for (int i = 1; i < lines.Length; i += _chunk)
            //            {
            //                DateTime start = DateTime.Now;
            //                SeedRows(name, lines, i, _chunk);
            //                DateTime end = DateTime.Now;

            //                TimeSpan length = end - start;
            //                string message = string.Format("Songs from ({0} were loaded in: {1}", name, length);
            //                results.Add(message);
            //                Trace.WriteLine(message);
            //            }

            //        }
            //    }
            //}

            //ViewBag.Results = results;
            return View();
        }

        //
        // Get: //Reseed
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Reseed()
        {
            ViewBag.Name = "Reseed Database";
            ReseedDB();
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

            var songs = from s in _db.Songs where s.TitleHash != 0 select s;

            foreach (Song song in songs)
            {
                SongDetails sd = new SongDetails(song);
                song.Purchase = sd.GetPurchaseTags();
            }

            _db.SaveChanges();

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
            int count = 0;

            var songs = from s in _db.Songs where s.TitleHash != 0 select s;
            foreach (Song song in songs)
            {
                if (song.UpdateTitleHash())
                {
                    count += 1;
                }
            }
            _db.SaveChanges();

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
            int count = 0;

            ApplicationUser user = _db.FindUser(User.Identity.Name);
            var songs = from s in _db.Songs where string.Equals(s.Title,s.Artist) select s;
            foreach (Song song in songs)
            {
                var artists = from p in song.SongProperties where p.Name == Song.ArtistField select p;
                var alist = artists.ToList();
                    //song.SongProperties.Select(p => string.Equals(p.BaseName, Song.ArtistField)).ToList();

                if (alist.Count > 1)
                {
                    SongProperty ap = alist[alist.Count - 2];
                    string artist = ap.Value;
                    if (artist != null)
                    {
                        SongDetails sd = new SongDetails(song);
                        sd.Artist = artist;
                        _db.EditSong(user, sd, null, null, null);
                        count += 1;
                    }
                }
            }
            _db.SaveChanges();

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
            int count = 0;

            ApplicationUser user = _db.FindUser(User.Identity.Name);
            var songs = from s in _db.Songs where s.Title != null select s;
            foreach (Song song in songs)
            {
                SongDetails sd = new SongDetails(song);
                string album = sd.Album;
                if (song.Album != album)
                {
                    song.Album = album;
                    count += 1;
                }
            }

            _db.SaveChanges();

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Albums were fixed ({0})", count);

            return View("Results");
        }

        //
        // Get: //UpdateTagTypes
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTagTypes()
        {
            ViewBag.Name = "UpdateTagTypes";

            //_db.TagTypes.Load();
            //int init = _db.TagTypes.Count();

            //// Update Dances
            //foreach (var dance in _db.Dances)
            //{
            //    _db.FindOrCreateTagType(dance.Name, "Dance");
            //}

            //// Update Genres
            //var genres = _db.Songs.Select(s => s.Genre).Distinct();
            //foreach (var genre in genres)
            //{
            //    if (string.IsNullOrWhiteSpace(genre))
            //    {
            //        continue;
            //    }
            //    string[] cells = genre.Split(new char[] {','});
            //    foreach (string cell in cells)
            //    {
            //        _db.FindOrCreateTagType(cell, "Genre");
            //    }
            //}

            //_db.SaveChanges();
            //int final = _db.TagTypes.Count();

            int count = 0;
            using (DanceMusicContext dmc = new DanceMusicContext())
            {
                dmc.Configuration.AutoDetectChangesEnabled = false;

                Regex didEx = new Regex(@"^([^+-]*)", RegexOptions.Singleline);

                ApplicationUser batch = dmc.FindUser("batch");
                foreach (Song song in dmc.Songs)
                {
                    SongDetails sd = new SongDetails(song);

                    bool retry = true;
                    foreach (Tag tag in sd.Tags)
                    {
                        if (tag.Type.Categories.Contains("Dance"))
                        {
                            retry = false;
                        }
                    }

                    if (retry)
                    {
                        var map = sd.MapProperyByUsers(Song.DanceRatingField);

                        foreach (var kv in map)
                        {
                            ApplicationUser user = batch;
                            if (!string.IsNullOrWhiteSpace(kv.Key))
                            {
                                user = dmc.FindUser(kv.Key);
                            }
                            StringBuilder sb = new StringBuilder();
                            string separator = string.Empty;
                            foreach (var v in kv.Value)
                            {
                                Match match = didEx.Match(v);
                                if (match.Success)
                                {
                                    string did = match.Value;
                                    DanceObject dance = null;
                                    if (Dances.Instance.DanceDictionary.TryGetValue(did, out dance))
                                    {
                                        sb.Append(separator);
                                        sb.Append(dance.Name);
                                        separator = "|";
                                    }
                                }
                            }
                            string tags = sb.ToString();
                            if (!string.IsNullOrWhiteSpace(tags))
                            {
                                sd = dmc.EditSong(user, sd, null, null, tags, false);
                                Trace.WriteLine(string.Format("{0}:{1}", sd.Title, tags));
                            }
                        }

                        count += 1;

                        if (count % 50 == 0)
                        {
                            Trace.WriteLine(string.Format("Song Modified={0}", count));
                        }
                    }
                }

                dmc.ChangeTracker.DetectChanges();
                dmc.SaveChanges();
            }


            ViewBag.Success = true;
            //ViewBag.Message = string.Format("Tags Types were created ({0}).  Songs were fixed as tags({1})", final-init, count);
            ViewBag.Message = string.Format(" Songs were fixed as tags({0})", count);

            return View("Results");
        }

        //
        // Get: //AddDanceGroups
        [Authorize(Roles = "dbAdmin")]
        public ActionResult AddDanceGroups()
        {
            ViewBag.Name = "AddDanceGroups";

            var dict = Dances.Instance.DanceDictionary;

            int count = 0;
            using (DanceMusicContext dmc = new DanceMusicContext())
            {
                dmc.Configuration.AutoDetectChangesEnabled = false;

                ApplicationUser batch = dmc.FindUser("batch");
                foreach (Song song in dmc.Songs)
                {
                    Dictionary<string,DanceRatingDelta> ngs = new Dictionary<string,DanceRatingDelta>();
                    foreach (var dr in song.DanceRatings)
                    {
                        DanceObject d = dict[dr.DanceId];
                        DanceType dt = d as DanceType;
                        if (dt != null)
                        {
                            string g = dt.GroupId;
                            if (g != "MSC")
                            {
                                DanceRatingDelta drd = null;
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
                            DanceInstance di = d as DanceInstance;
                            if (di != null)
                            {
                                Trace.WriteLine(string.Format("Dance Instance: {0}", song.Title));
                            }
                        }
                    }

                    if (ngs.Count > 0)
                    {
                        dmc.UpdateDances(batch, song, ngs.Values, false);
                    }
                }

                dmc.ChangeTracker.DetectChanges();
                dmc.SaveChanges();
            }


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

            string[] groups = new string[] {"SWG","TNG","FXT","WLZ"};

            var dict = Dances.Instance.DanceDictionary;

            int count = 0;
            using (DanceMusicContext dmc = new DanceMusicContext())
            {
                dmc.Configuration.AutoDetectChangesEnabled = false;

                ApplicationUser batch = dmc.FindUser("batch");
                foreach (Song song in dmc.Songs)
                {
                    if (song.Tempo != null)
                    {
                        decimal tempo = song.Tempo.Value;
                        List<DanceRatingDelta> nts = new List<DanceRatingDelta>();
                        foreach (var dr in song.DanceRatings)
                        {
                            DanceObject d = dict[dr.DanceId];
                            DanceGroup dg = d as DanceGroup;
                            if (dg != null && groups.Contains(dg.Id))
                            {
                                foreach (var dto in dg.Members)
                                {
                                    DanceType dt = dto as DanceType;
                                    if (dt.TempoRange.ToBpm(dt.Meter).Contains(tempo))
                                    {
                                        // TODO: Consider re-using this code to tag songs as not-strict tempo (but it's actually the dance rating that should be tagged).
                                        DanceRatingDelta drd = new DanceRatingDelta(dt.Id,4);
                                        nts.Add(drd);
                                        count += 1;
                                    }
                                }
                            }
                            else
                            {
                                DanceInstance di = d as DanceInstance;
                                if (di != null)
                                {
                                    Trace.WriteLine(string.Format("Dance Instance: {0}", song.Title));
                                }
                            }
                        }

                        if (nts.Count > 0)
                        {
                            dmc.UpdateDances(batch, song, nts, false);
                        }
                    }
                }

                dmc.ChangeTracker.DetectChanges();
                dmc.SaveChanges();
            }


            ViewBag.Success = true;
            ViewBag.Message = string.Format("Dance groups were added ({0})", count);

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

            Trace.WriteLine(string.Format("Test Trace: '{0}'", message));
            return View("Results");
        }

        //
        // Get: //ScrapeDances
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult ScrapeDances(string id)
        {
            var songs = new List<SongDetails>();

            DanceScraper scraper = DanceScraper.FromName(id);
            IList<string> lines = scraper.Scrape();

            StringBuilder sb = new StringBuilder();
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendFormat("{0}\r\n", line);
                }
            }

            string s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            MemoryStream stream = new MemoryStream(bytes);

            return File(stream, "text/plain", scraper.Name + ".csv");
        }



        //
        // Get: //ReloadDatabase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult ReloadDatabase(string reloadDatabase, bool? batch)
        {
            List<string> lines = UploadFile();

            Trace.WriteLine("File Uploaded Successfully");

            ViewBag.Name = "Restore Database";
            if (lines.Count > 0)
            {
                if (string.Equals(reloadDatabase,"reload",StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.Equals(lines[0],_userHeader,StringComparison.InvariantCultureIgnoreCase))
                    {
                        RestoreDB(null);
                        ReloadUsers(lines);
                        ReloadTags(lines);
                    }
                    else
                    {
                        Trace.WriteLine("Requested full reload, but invalid headers, so just doing songs");
                    }
                }

                if (!string.Equals(reloadDatabase, "update", StringComparison.InvariantCultureIgnoreCase))
                {
                    ReloadDB(lines, batch);
                }
                else
                {
                    UpdateDB(lines, batch);
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

            List<string> lines = UploadFile();

            ViewBag.Name = "Upload Tempi";
            ViewBag.FileId = -1;
            ViewBag.Action = "CommitUploadTempi";

            if (lines.Count > 0)
            {
                IList<SongDetails> songs = SongsFromFile(lines);
                results = MatchSongs(songs,MatchMethod.Tempo);
                ViewBag.FileId = CacheReview(results);
            }

            return View("ReviewBatch", results);
        }


        //
        // Post: //CommitUploadTempi
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult CommitUploadTempi(int fileId)
        {
            IList<LocalMerger> initial = GetReviewById(fileId);
            IList<LocalMerger> results = null;

            ViewBag.Name = "Upload Tempi";
            ViewBag.FileId = fileId;

            ApplicationUser user = _db.FindUser(User.Identity.Name);

            if (initial.Count > 0)
            {
                results = new List<LocalMerger>();

                foreach (LocalMerger m in initial)
                {
                    // We only want to auto-update if there isn't a conflict
                    if (!m.Conflict)
                    {
                        SongDetails sd = m.Left;
                        SongDetails edit = m.Right;

                        bool modified = false;

                        // Handle Scalar values
                        if (!edit.Tempo.HasValue && sd.Tempo.HasValue)
                        {
                            modified = true;
                            edit.Tempo = sd.Tempo;
                        }

                        // Now see if we have new album info
                        if (sd.HasAlbums && edit.FindAlbum(sd.Albums[0].Name) == null)
                        {
                            edit.Albums.Insert(0, sd.Albums[0]);
                            modified = true;
                        }

                        if (modified && _db.EditSong(user, edit, null, null, null) != null)
                        {
                            results.Add(m);
                        }
                    }
                }

                _db.SaveChanges();
            }

            return View("ReviewBatch", results);
        }
        #endregion


        #region Tags
        //
        // Post: //UploadTags
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadTags()
        {
            List<string> lines = UploadFile();

            ViewBag.Name = "Upload Tags";

            if (lines.Count > 0)
            {
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[] cells = line.Split(new char[] { '\t' });
                        if (cells.Length == 2)
                        {
                            _db.FindOrCreateTagType(cells[1], cells[0]);
                        }
                    }
                }
            }

            _db.SaveChanges();
            foreach (TagType tt in _db.TagTypes)
            {
                if (string.IsNullOrWhiteSpace(tt.Categories))
                {
                    tt.AddCategory("Genre");
                }
            }
            _db.SaveChanges();

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

            IList<string> headerList = null;
            if (!string.IsNullOrWhiteSpace(headers))
            {
                 headerList = SongDetails.BuildHeaderMap(headers, ',');
            }
            else
            {
                headerList = HeaderFromList(CleanSeparator(separator), ref songs);
            }

            IList<SongDetails> newSongs = SongsFromList(CleanSeparator(separator), headerList, songs);

            bool hasArtist = false;
            if (!string.IsNullOrEmpty(artist))
            {
                hasArtist = true;
                artist = artist.Trim();
            }

            AlbumDetails ad = null;
            bool hasAlbum = false;
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

            separator = CleanSeparator(separator);

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = User.Identity.Name;
            }
            ApplicationUser user = _db.FindOrAddUser(userName,DanceMusicContext.PseudoRole);

            List<string> dances = null;
            if (!string.IsNullOrWhiteSpace(danceIds))
            {
                dances = new List<string>(danceIds.Split(new char[] { ';' }));
            }

            if (initial.Count > 0)
            {
                bool modified = false;

                foreach (LocalMerger m in initial)
                {
                    List<string> dancesT = dances;
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
                        modified = _db.CreateSong(user, m.Left) != null;
                    }
                    // Any other matchtype should result in a merge, which for now is just adding the dance(s) from
                    //  the new list to the existing song (or adding weight).
                    // Now we're going to potentially add tempo - need a more general solution for this going forward
                    else
                    {
                        modified = _db.AdditiveMerge(user, m.Right.SongId, m.Left, dancesT) != null;
                    }
                }

                if (modified)
                {
                    _db.SaveChanges();
                }
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
        public ActionResult BackupDatabase(string useLookupHistory = null)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("{0}\r\n",_userHeader);
            foreach (ApplicationUser user in _db.Users)
            {
                string userId = user.Id;
                string username = user.UserName;
                string roles = user.GetRoles(_db.RoleDictionary,"|");
                string hash = user.PasswordHash;
                string stamp = user.SecurityStamp;
                string lockout = user.LockoutEnabled.ToString();
                string providers = user.GetProviders();

                sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\r\n", userId, username, roles, hash, stamp, lockout, providers);
            }

            sb.AppendFormat("{0}\r\n", _tagBreak);
            foreach (TagType tt in _db.TagTypes)
            {
                sb.AppendFormat("{0}\t{1}\r\n", tt.Categories, tt.Value);
            }

            sb.AppendFormat("{0}\r\n",_songBreak);
            bool history = !string.IsNullOrWhiteSpace(useLookupHistory);
            var songlist = _db.Songs.OrderBy(t => t.Modified).ThenBy(t => t.SongId);
            foreach (Song song in songlist)
            {
                string[] actions = null;
                if (history)
                {
                    actions = new string[] { Song.FailedLookup };
                }
                string line = song.Serialize(actions);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendFormat("{0}\r\n", line);
                }
            }

            string s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            MemoryStream stream = new MemoryStream(bytes);

            DateTime dt = DateTime.Now;
            string h = history ? "+lookup" : string.Empty;
            return File(stream, "text/plain", string.Format("backup-{0:d4}-{1:d2}-{2:d2}{3}.txt",dt.Year,dt.Month,dt.Day,h));
        }

        private const string _songBreak = "+++++SONGS+++++";
        private const string _tagBreak = "+++++TAGSS+++++";
        private const string _userHeader = "UserId\tUserName\tRoles\tPWHash\tSecStamp\tLockout\tProviders";
        //
        // Get: //RestoreDatabase
        //[Authorize(Roles = "dbAdmin")]
        [AllowAnonymous]
        public ActionResult RestoreDatabase()
        {
            RestoreDB();

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
            var songs = from s in _db.Songs where s.TitleHash != 0 select s;
            string[] danceList = Dances.Instance.ExpandDanceList("WLZ");

            int cwlz = 0;
            songs = songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            List<Song> songsT = songs.ToList();
            songs = songs.Where(s => s.Tempo > 190);
            songsT = songs.ToList();

            foreach (var song in songs)
            {
                decimal newTempo = (song.Tempo.Value / 4) * 3;
                newTempo = Math.Round(newTempo, 2);
                song.Tempo = newTempo;
                cwlz += 1;
            }

            int csmb = 0;
            songs = from s in _db.Songs where s.TitleHash != 0 select s;
            songs = songs.Where(s => s.DanceRatings.Count == 1 && s.DanceRatings.Any(dr => dr.DanceId == "SMB"));
            songsT = songs.ToList();
            songs = songs.Where(s => s.Tempo > 175);
            songsT = songs.ToList();
            foreach (var song in songs)
            {
                decimal newTempo = (song.Tempo.Value / 2);
                newTempo = Math.Round(newTempo, 2);
                song.Tempo = newTempo;
                csmb += 1;
            }

            _db.SaveChanges();

            ViewBag.Name = "Clean Tempi";
            ViewBag.Success = true;
            ViewBag.Message = string.Format("{0} waltzes and {1} sambas fixed",cwlz,csmb);

            return View("Results");
        }

        
        #endregion

        #region Migration-Restore
        private void RestoreDB(string state="InitialCreate")
        {
            DbMigrator migrator = null;

            // Roll back to a specific migration or zero
            if (state != null)
            {
                Trace.WriteLine("Rolling Back Database");
                migrator = BuildMigrator();
                migrator.Update(state);
            }
            else
            {
                Trace.WriteLine("Wiping Database");
                ObjectContext objectContext = ((IObjectContextAdapter)_db).ObjectContext;
                objectContext.DeleteDatabase();
                migrator = BuildMigrator();
            }

            Trace.WriteLine("Starting Migrator Update");
            // Apply all migrations up to a specific migration
            migrator.Update();

            Trace.WriteLine("Exiting RestoreDB");
        }

        private void ReseedDB()
        {
            m4d.Migrations.Configuration.DoSeed(_db);
        }

        private DbMigrator BuildMigrator()
        {
            var configuration = BuildConfiguration();

            return new DbMigrator(configuration);
        }

        private m4d.Migrations.Configuration BuildConfiguration()
        {
            string sqlConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            var connectionInfo = new DbConnectionInfo(sqlConnectionString, "System.Data.SqlClient");

            var configuration = new m4d.Migrations.Configuration
            {
                TargetDatabase = connectionInfo,
                AutomaticMigrationsEnabled = false
            };

            return configuration;
        }
        
        #endregion        

        #region Build-From-Text

        /// <summary>
        /// Reloads a list of users into the database
        /// </summary>
        /// <param name="lines"></param>
        private void ReloadUsers(List<string> lines)
        {
            Trace.WriteLine("Entering ReloadUsers");

            if (lines == null || lines.Count < 1 || !string.Equals(lines[0],_userHeader,StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentOutOfRangeException();
            }

            int fieldCount = _userHeader.Split(new char[] { '\t' }).Length;
            int i = 1;
            while (i < lines.Count)
            {
                string s = lines[i];
                i += 1;

                if (string.Equals(s,_tagBreak,StringComparison.InvariantCultureIgnoreCase))
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
                    ApplicationUser user = _db.FindUser(userName);
                    if (user == null)
                    {
                        user = _db.Users.Create();
                        user.Id = userId;
                        user.UserName = userName;
                        user.PasswordHash = hash;
                        user.SecurityStamp = stamp;
                        user.LockoutEnabled = string.Equals(lockout, "TRUE", StringComparison.InvariantCultureIgnoreCase);

                        if (!string.IsNullOrWhiteSpace(roles))
                        {
                            string[] roleNames = roles.Split(new char[] {'|'},StringSplitOptions.RemoveEmptyEntries);
                            foreach (string roleName in roleNames)
                            {
                                IdentityRole role = _db.Roles.FirstOrDefault(r => r.Name == roleName.Trim());
                                if (role != null)
                                {
                                    IdentityUserRole iur = new IdentityUserRole() { UserId = userId, RoleId = role.Id };
                                    user.Roles.Add(iur);
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(providers))
                        {
                            string[] entries = providers.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 0; j < entries.Length; j += 2)
                            {
                                IdentityUserLogin login = new IdentityUserLogin() { LoginProvider = entries[j], ProviderKey = entries[j + 1], UserId = userId };
                                user.Logins.Add(login);
                            }
                        }

                        _db.Users.Add(user);
                    }
                }
            }

            lines.RemoveRange(0, i);

            Trace.WriteLine("Saving Changes");
            _db.SaveChanges();

            Trace.WriteLine("Exiting ReloadUsers");
        }

        /// <summary>
        /// Reloads a list of users into the database
        /// </summary>
        /// <param name="lines"></param>
        private void ReloadTags(List<string> lines)
        {
            Trace.WriteLine("Entering ReloadTags");

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

                    _db.FindOrCreateTagType(value, category);
                }
            }

            lines.RemoveRange(0, i);

            Trace.WriteLine("Saving Changes");
            _db.SaveChanges();

            Trace.WriteLine("Exiting ReloadTags");
        }


        /// <summary>
        /// Reloads a list of songs into the database
        /// </summary>
        /// <param name="lines"></param>
        private void UpdateDB(List<string> lines, bool? batch)
        {
            Trace.WriteLine("Entering UpdateDB");

            using (DanceMusicContext dmc = new DanceMusicContext())
            {
                dmc.Configuration.AutoDetectChangesEnabled = false;

                // Load the dance List
                Trace.WriteLine("Loading Dances");
                dmc.Dances.Load();

                Trace.WriteLine("Loading Songs");

                foreach (string line in lines)
                {
                    SongDetails sd = new SongDetails(line);
                    Song song = dmc.FindSong(sd.SongId);

                    string name = sd.ModifiedList.Last().UserName;
                    ApplicationUser user = dmc.FindOrAddUser(name,DanceMusicContext.EditRole);

                    if (song == null)
                    {
                        song = dmc.CreateSong(user,sd);
                    }
                    else if (sd.IsNull)
                    {
                        dmc.DeleteSong(user, song);
                    }
                    else {
                        dmc.UpdateSong(user, song, sd, false);
                    }
                    //song.Modified = DateTime.Now;
                }

                dmc.Configuration.AutoDetectChangesEnabled = true;
                dmc.ChangeTracker.DetectChanges();
                dmc.SaveChanges();
            }

            Trace.WriteLine("Clearing Song Cache");
            SongCounts.ClearCache();
            Trace.WriteLine("Exiting ReloadDB");
        }

        /// <summary>
        /// Reloads a list of songs into the database
        /// </summary>
        /// <param name="lines"></param>
        private void ReloadDB(List<string> lines, bool? batch)
        {
            Trace.WriteLine("Entering ReloadDB");
            bool b = batch ?? false;

            using (DanceMusicContext dmc = new DanceMusicContext())
            {
                dmc.Configuration.AutoDetectChangesEnabled = false;

                // Load the dance List
                Trace.WriteLine("Loading Dances");
                dmc.Dances.Load();

                List<Song> songs = new List<Song>();
                List<SongProperty> properties = null;
                List<DanceRating> ratings = null;
                IUserMap bum = dmc;
                if (b)
                {
                    bum = new BatchUserMap(dmc);
                    properties = new List<SongProperty>();
                    ratings = new List<DanceRating>();
                }

                Trace.WriteLine("Loading Songs");

                int c = 0;
                foreach (string line in lines)
                {
                    DateTime time = DateTime.Now;
                    Song song = new Song();
                    song.Created = time;
                    song.Modified = time;

                    song.Load(line, bum, dmc);
                    songs.Add(song);
                    if (b)
                    {
                        properties.AddRange(song.SongProperties);
                        ratings.AddRange(song.DanceRatings);
                    }
                    else
                    {
                        dmc.Entry(song).State = EntityState.Added;
                    }

                    c += 1;
                    if (c % 100 == 0)
                    {
                        Trace.WriteLine(string.Format("{0} songs loaded", c));
                    }

                    if (TraceLevels.General.TraceInfo)
                    {
                        if (song.Length.HasValue && song.Length.Value > 1000)
                        {
                            Trace.WriteLine(string.Format("Long Song: {0} '{1}'", song.Length, song.Title));
                        }
                    }

                    if (!b && (c % 1000 == 0))
                    {
                        Trace.WriteLine("Saving next 1000 songs");
                        dmc.Configuration.AutoDetectChangesEnabled = true;
                        dmc.ChangeTracker.DetectChanges();
                        dmc.SaveChanges();
                        dmc.Configuration.AutoDetectChangesEnabled = false;
                    }
                }

                Trace.WriteLine("Saving Songs");
                if (b)
                {
                    // Until the bug gets fixed in EFBatchOperations to allow for azure, we'll live with the slower method...
                    EFBatchOperation.For(dmc, dmc.Songs).InsertAll(songs);

                    Trace.WriteLine("Saving Properties");
                    EFBatchOperation.For(dmc, dmc.SongProperties).InsertAll(properties);

                    Trace.WriteLine("Saving User Mappings");
                    EFBatchOperation.For(dmc, dmc.Modified).InsertAll((bum as BatchUserMap).GetMappings());

                    Trace.WriteLine("Saving Dance Ratings");
                    EFBatchOperation.For(dmc, dmc.DanceRatings).InsertAll(ratings);
                }
                else
                {
                    Trace.WriteLine("Saving tail");
                    dmc.Configuration.AutoDetectChangesEnabled = true;
                    dmc.ChangeTracker.DetectChanges();
                    dmc.SaveChanges();
                }
            }

            Trace.WriteLine("Clearing Song Cache");
            SongCounts.ClearCache();
            Trace.WriteLine("Exiting ReloadDB");
        }

        //private void BuildSchema(string[] lines)
        //{
        //    _tempoKind = TempoKind.BPM;
        //    _dynamicColumns = new List<int>();

        //    _danceColumn = -1;
        //    _titleColumn = -1;
        //    _artistColumn = -1;
        //    _albumColumn = -1;
        //    _labelColumn = -1;
        //    _tempoColumn = -1;
        //    _altTempoColumn = -1;
        //    _amazoncolumn = -1;
        //    _itunescolumn = -1;

        //    string headerLine = lines[0];
        //    _headers = headerLine.Split(new char[] { '\t' });

        //    // Map the table columns to database columns
        //    int c = 0;
        //    foreach (string header in _headers)
        //    {
        //        if (string.Equals(header, "DANCE", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _danceColumn = c;
        //        }
        //        else if (string.Equals(header, "TITLE", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _titleColumn = c;
        //        }
        //        else if (string.Equals(header, "ARTIST", StringComparison.OrdinalIgnoreCase)|| string.Equals(header, "CONTRIBUTING ARTIST", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _artistColumn = c;
        //        }
        //        else if (string.Equals(header, "ALBUM", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _albumColumn = c;
        //        }
        //        else if (string.Equals(header, "LABEL", StringComparison.OrdinalIgnoreCase) || string.Equals(header, "PUBLISHER", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _labelColumn = c;
        //        }
        //        else if (string.Equals(header, "BPM", StringComparison.OrdinalIgnoreCase) || string.Equals(header, "BEATS-PER-MINUTE", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _tempoColumn = c;
        //        }
        //        else if (string.Equals(header, "MPM", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _tempoKind = TempoKind.MPM;
        //            _tempoColumn = c;
        //        }
        //        else if (string.Equals(header, "MPMS", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _tempoKind = TempoKind.MPM;
        //            _tempoColumn = c;
        //        }
        //        else if (string.Equals(header, "MPMR", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _tempoKind = TempoKind.MPM;
        //            _altTempoColumn = c;
        //        }
        //        else if (string.Equals(header, "AmazonAlbum", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _amazoncolumn = c;
        //        }
        //        else if (string.Equals(header, "ITunes", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _itunescolumn = c;
        //        }
        //        else if (string.Equals(header, "Length", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _lengthcolumn = c;
        //        }
        //        else if (string.Equals(header, "#", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _trackcolumn = c;
        //        }
        //        else
        //        {
        //            _dynamicColumns.Add(c);
        //        }

        //        // TODO: Track # ?

        //        Debug.Assert(string.Equals(header, _headers[c]));
        //        c += 1;
        //    }

        //    Debug.Assert(_headers.Length == c);
        //    Debug.Assert(_titleColumn != -1 && _danceColumn != -1);
        //}

        //private SongDetails SongFromRow(string userName, string line)
        //{
        //    throw new NotImplementedException();
        //}
        // TODO: I like the idea of building transitoory SongDetails objects
        //  from the line data so that I can add in a review phase - but
        //  I'm not going to rip the old stuff out until I get it working
        //private void SeedRows(string userName, string[] lines, int start, int count)
        //{
        //    using (DanceMusicContext dmc = new DanceMusicContext())
        //    {
        //        // TODO: Is this somehow global?  The examples that change this flag have both the using and a try/catch
        //        //  to turn it off.

        //        dmc.Configuration.AutoDetectChangesEnabled = false;
        //        // Load the dance List
        //        dmc.Dances.Load();

        //        // Find or Create a user for these entries
        //        ApplicationUser user = dmc.Users.FirstOrDefault(u => u.UserName == userName);
        //        //if (user == null)
        //        //{
        //        //    user = dmc.Users.Create();
        //        //    user.UserName = Name;
        //        //    user = dmc.Users.Add(user);

        //        //    dmc.SaveChanges();
        //        //}

        //        //dmc.CreateSongProperty(DanceMusicContext.StartBatchLoadCommand,Name);

        //        for (int i = start; i < lines.Length && i < start + count; i++)
        //        {
        //            string[] cells = lines[i].Split(new char[] { '\t' });
        //            if (cells.Length != _headers.Length)
        //            {
        //                Trace.WriteLine(string.Format("Bad Format: {0}", lines[i]));
        //            }

        //            // Find the dance and skip if it doesn't exist
        //            string danceIds = null;

        //            if (cells.Length < _danceColumn)
        //                break;

        //            string danceName = SongDetails.CleanDanceName(cells[_danceColumn]);
        //            if (!DanceRating.DanceMap.TryGetValue(danceName, out danceIds))
        //            {
        //                Trace.WriteLine(string.Format("Dance Not Found: {0}", cells[_danceColumn]));
        //                continue;
        //            }
        //            string[] idList = danceIds.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        //            // Create a song based on the info in each line

        //            string title = SongDetails.CleanText(cells[_titleColumn]);
        //            string artist = null;
        //            if (_artistColumn != -1 && _artistColumn < cells.Length)
        //            {
        //                artist = SongDetails.CleanArtistString(cells[_artistColumn]);
        //            }

        //            string album = null;
        //            if (_albumColumn != -1 && _albumColumn < cells.Length)
        //            {
        //                album = cells[_albumColumn];
        //            }

        //            string label = null;
        //            if (_labelColumn != -1 && _labelColumn < cells.Length)
        //            {
        //                label = cells[_labelColumn];
        //            }

        //            // Add the Tempo
        //            decimal? bpm = null;
        //            if (_tempoColumn != -1 && _tempoColumn < cells.Length)
        //            {
        //                string tempo = null;
        //                tempo = cells[_tempoColumn].Trim();
        //                if (string.IsNullOrEmpty(tempo) && _altTempoColumn != -1)
        //                {
        //                    tempo = cells[_altTempoColumn].Trim();
        //                }

        //                switch (_tempoKind)
        //                {
        //                    case TempoKind.BPM:
        //                        {
        //                            decimal bpmT;
        //                            if (decimal.TryParse(tempo, out bpmT))
        //                            {
        //                                bpm = bpmT;
        //                            }
        //                        }
        //                        break;
        //                    case TempoKind.MPM:
        //                        {
        //                            decimal mpm;
        //                            if (decimal.TryParse(tempo, out mpm))
        //                            {
        //                                DanceObject d = Dances.Instance.DanceDictionary[idList[0]];
        //                                Meter meter = d.Meter;
        //                                Tempo tpo = new Tempo(mpm, new TempoType(_tempoKind, meter));
        //                                Tempo mpt = tpo.Convert(new TempoType(TempoKind.BPM));
        //                                bpm = mpt.Rate;
        //                            }
        //                        }
        //                        break;
        //                    case TempoKind.BPS:
        //                        // TODO: Handle BPS (if i ever see a DB that lists this way
        //                        Debug.Assert(false);
        //                        break;
        //                }
        //            }

        //            //  TODO: We may have purchase info for songs that don't have an album name - 
        //            //    currently we're dropping those on the floor, do we care?

        //            // Fill in album info including publisher and purchase
        //            List<AlbumDetails> albums = null;

        //            if (!string.IsNullOrWhiteSpace(album))
        //            {
        //                AlbumDetails ad = new AlbumDetails() { Name = album, Publisher = label };

        //                if (_amazoncolumn != -1)
        //                {
        //                    string aa = cells[_amazoncolumn];
        //                    aa = aa.Trim();
        //                    if (!string.IsNullOrEmpty(aa))
        //                    {
        //                        ad.SetPurchaseInfo(PurchaseType.Album, ServiceType.Amazon, aa);
        //                    }
        //                }

        //                if (_itunescolumn != -1)
        //                {
        //                    string text = cells[_itunescolumn];

        //                    if (!string.IsNullOrEmpty(text))
        //                    {
        //                        string r1 = @"%252Fid(?<album>\d*)%253Fi%253D(?<song>\d*)";
        //                        string r2 = @"%252FviewAlbum%253Fi%253D(?<album>\d*)%2526id%253D(?<song>\d*)";

        //                        Match match = Regex.Match(text, r1);
        //                        if (!match.Success)
        //                            match = Regex.Match(text, r2);

        //                        bool success = false;
        //                        if (match.Success)
        //                        {
        //                            string aid = match.Groups["album"].Value;
        //                            string sid = match.Groups["song"].Value;

        //                            if (!string.IsNullOrEmpty(aid))
        //                            {
        //                                ad.SetPurchaseInfo(PurchaseType.Album, ServiceType.ITunes, aid);
        //                                success = true;
        //                            }
        //                            if (!string.IsNullOrEmpty(sid))
        //                            {
        //                                ad.SetPurchaseInfo(PurchaseType.Song, ServiceType.ITunes, sid);
        //                                success = true;
        //                            }
        //                        }

        //                        if (!success)
        //                        {
        //                            Trace.WriteLine(string.Format("Bad ITunes:{0}", text));
        //                        }
        //                    }
        //                }

        //                albums = new List<AlbumDetails>();
        //                albums.Add(ad);
        //            }

        //            Song song = dmc.CreateSong(user, title, artist, null, bpm, null, albums);

        //            // Now set up the dance associations

        //            dmc.AddDanceRatings(song, idList);

        //            // TODO: this should move down into the db context class 
        //            // And add in the dynamic columns
        //            foreach (int dc in _dynamicColumns)
        //            {
        //                string value = cells[dc].Trim();

        //                if (!string.IsNullOrEmpty(value))
        //                {
        //                    dmc.CreateSongProperty(song, _headers[dc], value);
        //                }
        //            }
        //        }

        //        //dmc.CreateSongProperty(DanceMusicContext.EndBatchLoadCommand, Name);
        //        dmc.ChangeTracker.DetectChanges();

        //        dmc.SaveChanges();
        //    }
        //}

        //string[] _dbs = new string[] { "JohnCrossan", "LetsDanceDenver", "SalsaSwingBallroom", "SandiegoDJ", "SteveThatDJ", "UsaSwingNet", "WaltersDanceCenter" };
        //private readonly int _chunk = 500;

        //private List<int> _dynamicColumns = new List<int>();
        //private int _danceColumn = -1;
        //private int _titleColumn = -1;
        //private int _artistColumn = -1;
        //private int _albumColumn = -1;
        //private int _labelColumn = -1;
        //private int _tempoColumn = -1;
        //private int _altTempoColumn = -1;
        //private int _amazoncolumn = -1;
        //private int _itunescolumn = -1;
        //private int _trackcolumn = -1;
        //private int _lengthcolumn = -1;
        //private TempoKind _tempoKind = TempoKind.BPM;
        //private string[] _headers; 

        #endregion

        #region Utilities

        private IList<string> HeaderFromList(string separator, ref string songs)
        {
            int cidx = songs.IndexOfAny(System.Environment.NewLine.ToCharArray());
            if (cidx == -1)
            {
                return null;
            }
            string line = songs.Substring(0, cidx);

            var map = SongDetails.BuildHeaderMap(line);

            // Kind of kludgy, but temporary build the header
            //  map to see if it's valid then pass back a comma
            // separated list of headers...
            if (map != null && map.Any(p => p != null))
            {
                songs = songs.Substring(cidx).TrimStart(System.Environment.NewLine.ToCharArray());
                return map;
            }
            else
            {
                return null;
            }
        }

        private IList<SongDetails> SongsFromList(string separator, IList<string> headers, string songText)
        {
            string[] lines = songText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            return SongDetails.CreateFromRows(separator, headers, lines, Song.DanceRatingAutoCreate);
        }

        private IList<SongDetails> SongsFromFile(List<string> lines)
        {
            List<SongDetails> songs = new List<SongDetails>();

            List<string> map = SongDetails.BuildHeaderMap(lines[0]);
            lines.RemoveAt(0);
            return SongDetails.CreateFromRows("\t", map, lines, Song.DanceRatingAutoCreate);
        }
        private enum MatchMethod {None, Tempo, Merge};

        private IList<LocalMerger> MatchSongs(IList<SongDetails> newSongs, MatchMethod method)
        {
            List<LocalMerger> merge = new List<LocalMerger>();

            foreach (SongDetails song in newSongs)
            {
                var songs = from s in _db.Songs where (s.TitleHash == song.TitleHash) select s;

                List<SongDetails> candidates = new List<SongDetails>();
                foreach (Song s in songs)
                {
                    // Title-Artist match at minimum
                    if (string.Equals(Song.CreateNormalForm(s.Artist), Song.CreateNormalForm(song.Artist)))
                    {
                        candidates.Add(new SongDetails(s));
                    }
                }

                SongDetails match = null;
                MatchType type = MatchType.None;

                if (candidates.Count > 0)
                {
                    // Now we have a list of existing songs that are a title-artist match to our new song - so see
                    //  if we have a title-artist-album match

                    if (song.HasAlbums)
                    {
                        foreach (SongDetails s in candidates)
                        {
                            if (s.FindAlbum(song.Albums[0].Name) != null)
                            {
                                match = s;
                                type = MatchType.Exact;
                                break;
                            }
                        }
                    }

                    // If not, try for a length match
                    if (match == null && song.Length.HasValue)
                    {
                        foreach (SongDetails s in candidates)
                        {
                            if (s.Length.HasValue && Math.Abs(s.Length.Value - song.Length.Value) < 5)
                            {
                                match = s;
                                type = MatchType.Length;
                                break;
                            }
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

                LocalMerger m = new LocalMerger { Left = song, Right = match, MatchType = type, Conflict = false };
                switch (method)
                {
                    case MatchMethod.Tempo:
                        if (match == null)
                        {
                            m = null;
                        }
                        else
                        {
                            m.Conflict = song.TempoConflict(match, 3);
                        }
                        break;
                    case MatchMethod.Merge:
                        // Do we need to do anything special here???
                        break;
                }
                if (m != null)
                {
                    merge.Add(m);
                }
            }

            return merge;
        }

        List<string> UploadFile() 
        {
            List<string> lines = new List<string>();

            HttpFileCollectionBase files = Request.Files;
            if (files.Count == 1)
            {
                string key = files.AllKeys[0];
                ViewBag.Key = key;
                ViewBag.Size = files[key].ContentLength;
                ViewBag.ContentType = files[key].ContentType;

                HttpPostedFileBase file = Request.Files.Get(0);
                System.IO.Stream stream = file.InputStream;

                TextReader tr = new StreamReader(stream);

                string s = null;
                while ((s = tr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        lines.Add(s);
                    }
                }
            }

            return lines;
        }

        int CacheReview(IList<LocalMerger> review)
        {
            int ret = -1;
            lock (s_reviews)
            {
                s_reviews.Add(review);
                ret = s_reviews.Count - 1;
            }

            return ret;
        }

        IList<LocalMerger> GetReviewById(int id)
        {
            return s_reviews[id];
        }

        // This is pretty kludgy as this is a basically a temporary
        //  store that only gets recycled on restart - but since
        //  for now it's being using primarily on the short running
        //  dev instance, it doesn't seem worthwhile to do anything
        //  more sophisticated
        static IList<IList<LocalMerger>> s_reviews = new List<IList<LocalMerger>>();

        #endregion

        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
    }
}