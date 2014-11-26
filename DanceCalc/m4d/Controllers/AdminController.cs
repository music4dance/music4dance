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

            var songs = from s in Database.Songs where s.TitleHash != 0 select s;

            foreach (Song song in songs)
            {
                SongDetails sd = new SongDetails(song);
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
            int count = 0;

            var songs = from s in Database.Songs where s.TitleHash != 0 select s;
            foreach (Song song in songs)
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
            int count = 0;

            ApplicationUser user = Database.FindUser(User.Identity.Name);
            var songs = from s in Database.Songs where string.Equals(s.Title,s.Artist) select s;
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
                        Database.EditSong(user, sd, null, null, null);
                        count += 1;
                    }
                }
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
            int count = 0;

            ApplicationUser user = Database.FindUser(User.Identity.Name);
            var songs = from s in Database.Songs where s.Title != null select s;
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

            Database.SaveChanges();

            ViewBag.Success = true;
            ViewBag.Message = string.Format("Albums were fixed ({0})", count);

            return View("Results");
        }

        ////
        //// Get: //UpdateTagTypes
        //[Authorize(Roles = "dbAdmin")]
        //public ActionResult UpdateTagTypes()
        //{
        //    ViewBag.Name = "UpdateTagTypes";

        //    int count = 0;
        //    Context.Configuration.AutoDetectChangesEnabled = false;

        //    Regex didEx = new Regex(@"^([^+-]*)", RegexOptions.Singleline);

        //    ApplicationUser batch = Database.FindUser("batch");
        //    foreach (Song song in Database.Songs)
        //    {
        //        SongDetails sd = new SongDetails(song);

        //        bool retry = true;
        //        foreach (Tag tag in sd.Tags)
        //        {
        //            if (tag.Type.Categories.Contains("Dance"))
        //            {
        //                retry = false;
        //            }
        //        }

        //        if (retry)
        //        {
        //            var map = sd.MapProperyByUsers(Song.DanceRatingField);

        //            foreach (var kv in map)
        //            {
        //                ApplicationUser user = batch;
        //                if (!string.IsNullOrWhiteSpace(kv.Key))
        //                {
        //                    user = Database.FindUser(kv.Key);
        //                }
        //                StringBuilder sb = new StringBuilder();
        //                string separator = string.Empty;
        //                foreach (var v in kv.Value)
        //                {
        //                    Match match = didEx.Match(v);
        //                    if (match.Success)
        //                    {
        //                        string did = match.Value;
        //                        DanceObject dance = null;
        //                        if (Dances.Instance.DanceDictionary.TryGetValue(did, out dance))
        //                        {
        //                            sb.Append(separator);
        //                            sb.Append(dance.Name);
        //                            separator = "|";
        //                        }
        //                    }
        //                }
        //                string tags = sb.ToString();
        //                if (!string.IsNullOrWhiteSpace(tags))
        //                {
        //                    sd = Database.EditSong(user, sd, null, null, tags, false);
        //                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("{0}:{1}", sd.Title, tags));
        //                }
        //            }

        //            count += 1;

        //            if (count % 50 == 0)
        //            {
        //                Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("Song Modified={0}", count));
        //            }
        //        }
        //    }

        //    Context.ChangeTracker.DetectChanges();
        //    Database.SaveChanges();
        //    Context.Configuration.AutoDetectChangesEnabled = true;

        //    ViewBag.Success = true;
        //    ViewBag.Message = string.Format(" Songs were fixed as tags({0})", count);

        //    return View("Results");
        //}


        //
        // Get: //UpdateTagSummaries
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateTagSummaries()
        {
            ViewBag.Name = "UpdateTagTypes";

            int count = 0;

            Context.Configuration.AutoDetectChangesEnabled = false;

            foreach (TagType tt in Database.TagTypes)
            {
                tt.Count = 0;
            }

            foreach (Song song in Database.Songs)
            {
                // TODO: Should we reload from properties or from user tags (or either)...
                count += 1;
            }

            Database.SaveChanges();
            Context.Configuration.AutoDetectChangesEnabled = true;

            ViewBag.Success = true;
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
            Context.Configuration.AutoDetectChangesEnabled = false;

            ApplicationUser batch = Database.FindUser("batch");
            foreach (Song song in Database.Songs)
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
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("Dance Instance: {0}", song.Title));
                        }
                    }
                }

                if (ngs.Count > 0)
                {
                    Database.UpdateDances(batch, song, ngs.Values, false);
                }
            }

            Context.Configuration.AutoDetectChangesEnabled = true;
            Context.ChangeTracker.DetectChanges();
            Database.SaveChanges();

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
            Context.Configuration.AutoDetectChangesEnabled = false;

            ApplicationUser batch = Database.FindUser("batch");
            foreach (Song song in Database.Songs)
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
                                Trace.WriteLineIf(TraceLevels.General.TraceInfo,string.Format("Dance Instance: {0}", song.Title));
                            }
                        }
                    }

                    if (nts.Count > 0)
                    {
                        Database.UpdateDances(batch, song, nts, false);
                    }
                }
            }

            Context.ChangeTracker.DetectChanges();
            Database.SaveChanges();

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

            int count = 0;
            Context.Configuration.AutoDetectChangesEnabled = false;

            SongProperty user = null;
            SongProperty time = null;

            List<SongProperty> deletions = new List<SongProperty>();

            foreach (SongProperty prop in Database.SongProperties)
            {
                switch (prop.BaseName)
                {
                    default:
                        user = null;
                        time = null;
                        break;
                    case Song.UserField:
                        user = prop;
                        break;
                    case Song.TimeField:
                        time = prop;
                        break;
                    case Song.AddedTags:
                    case Song.RemovedTags:
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
            Context.ChangeTracker.DetectChanges();
            Database.SaveChanges();

#if NEWTAG
            foreach (Tag tag in Database.Tags)
            {
                Database.Tags.Remove(tag);
                count += 1;
            }
            Context.ChangeTracker.DetectChanges();
            Database.SaveChanges();

            foreach (TagType tt in Database.TagTypes)
            {
                tt.Count = 0;
            }
            Context.ChangeTracker.DetectChanges();
            Database.SaveChanges();
#endif

            Context.Configuration.AutoDetectChangesEnabled = true;
            foreach (Song song in Database.Songs)
            {
                song.TagSummary.Clean();
            }
            Database.SaveChanges();

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

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"File Uploaded Successfully");

            ViewBag.Name = "Restore Database";
            if (lines.Count > 0)
            {
                if (string.Equals(reloadDatabase,"reload",StringComparison.InvariantCultureIgnoreCase))
                {
                    if (DanceMusicService.IsUserBreak(lines[0]))
                    {
                        RestoreDB(null);

                        int i = lines.FindIndex(l => DanceMusicService.IsDanceBreak(l));
                        List<string> users = lines.GetRange(0, i).ToList();
                        lines.RemoveRange(0, i + 1);

                        i = lines.FindIndex(l => DanceMusicService.IsTagBreak(l));
                        List<string> dances = lines.GetRange(0,i).ToList();
                        lines.RemoveRange(0,i+1);

                        i = lines.FindIndex(l => DanceMusicService.IsSongBreak(l));
                        List<string> tags = lines.GetRange(0,i).ToList();
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

                if (string.Equals(reloadDatabase, "update songs", StringComparison.InvariantCultureIgnoreCase))
                {
                    Database.UpdateSongs(lines);
                }
                else if (string.Equals(reloadDatabase, "update dances", StringComparison.InvariantCultureIgnoreCase))
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

            ApplicationUser user = Database.FindUser(User.Identity.Name);

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

                        if (modified && Database.EditSong(user, edit, null, null, null) != null)
                        {
                            results.Add(m);
                        }
                    }
                }

                Database.SaveChanges();
            }

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
            List<string> lines = UploadFile();

            ViewBag.Name = "Upload Tags";

            if (lines.Count > 0)
            {
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[] cells = line.Split(new char[] { '\t' });
                        if (cells.Length >= 2)
                        {
                            string name = cells[1].Trim();
                            string cat = cells[0].Trim();
                            TagType tt = Database.FindOrCreateTagType(name, cat);
                            if (cells.Length == 3 && !string.IsNullOrWhiteSpace(cells[2]))
                            {
                                tt.PrimaryId = cells[2].Trim() + ':' + cat;
                            }
                        }
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
            List<string> lines = UploadFile();

            ViewBag.Name = "Upload Tags";

            if (lines.Count > 0)
            {
                Context.Configuration.AutoDetectChangesEnabled = false;

                Dictionary<string, string> entries = new Dictionary<string, string>();
                int count = 0;
                int unique = 0;
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        // DATE, SID, user, tag (unqualified)
                        string[] cells = line.Split(new char[] { '\t' });
                        if (cells.Length == 4)
                        {
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
                            string user = cells[2].Trim();
                            string tag = cells[3].Trim();
                            string cat = "Music";

                            List<TagType> types = Database.GetTagTypes(tag).ToList();
                            if (types.Count == 1)
                            {
                                cat = types[0].Category;
                            }
                            else if (user != "batch" && DanceLibrary.Dances.Instance.DanceFromName(tag) != null)
                            {
                                cat = "Dance";
                            }
                            tag += ":" + cat;

                            string id = user + ":" + guid.ToString();
                            string tags = null;
                            if (entries.TryGetValue(id, out tags))
                            {
                                TagList tl = new TagList(tags);
                                tl = tl.Add(new TagList(tag));
                                tags = tl.Summary;
                            }
                            else
                            {
                                tags = tag;
                                unique += 1;
                            }
                            entries[id] = tags;
                            count += 1;

                            if (count % 500 == 0)
                            {
                                Trace.WriteLineIf(/*TraceLevels.General.TraceInfo*/ true, string.Format("Tags Loaded={0}", count));
                            }
                        }
                    }
                }

                count = 0;
                foreach (string k in entries.Keys)
                {
                    string tags = entries[k];
                    string[] rg = k.Split(new char[] { ':' });

                    Guid guid = Guid.Parse(rg[1]);
                    string userName = rg[0];

                    ApplicationUser user = Database.FindUser(userName);
                    Song song = Database.FindSong(guid);

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

                Context.Configuration.AutoDetectChangesEnabled = true;
                Context.ChangeTracker.DetectChanges();
                Database.SaveChanges();
            }

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
            ApplicationUser user = Database.FindOrAddUser(userName,DanceMusicService.PseudoRole);

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
            bool history = !string.IsNullOrWhiteSpace(useLookupHistory);

            IList<string> users = Database.SerializeUsers(true);
            IList<string> tags = Database.SerializeTags(true);
            IList<string> dances = Database.SerializeDances(true);
            IList<string> songs = Database.SerializeSongs(true,history);

            string s = string.Join("\r\n", users) + "\r\n" + string.Join("\r\n", dances) + "\r\n" + string.Join("\r\n", tags) + "\r\n" + string.Join("\r\n", songs);
            var bytes = Encoding.UTF8.GetBytes(s);
            MemoryStream stream = new MemoryStream(bytes);

            DateTime dt = DateTime.Now;
            string h = history ? "+lookup" : string.Empty;
            return File(stream, "text/plain", string.Format("backup-{0:d4}-{1:d2}-{2:d2}{3}.txt",dt.Year,dt.Month,dt.Day,h));
        }

        ////
        //// Get: //BackupTags
        //// This is a massive kludge to get the old style tag info out of the database
        //[Authorize(Roles = "showDiagnostics")]
        //public ActionResult BackupTags()
        //{
        //    StringBuilder sb = new StringBuilder();

        //    var songlist = Database.Songs.OrderBy(t => t.Modified).ThenBy(t => t.SongId);
        //    foreach (Song song in songlist)
        //    {
        //        string user = "batch";
        //        string time = new DateTime(2014, 1, 1).ToString();

        //        foreach (var prop in song.SongProperties)
        //        {
        //            switch (prop.BaseName)
        //            {
        //                case Song.UserField:
        //                    user = prop.Value;
        //                    break;
        //                case Song.TimeField:
        //                    time = prop.Value;
        //                    break;
        //                //case Song.TagField:
        //                //    // Time ID User tags
        //                //    sb.AppendFormat("{0}\tS{1}\t{2}\t{3}\r\n",time,song.SongId.ToString("N"),user,prop.Value);
        //                //    break;
        //            }
        //        }
        //    }


        //    // TODO: If we do more of this kind of thing, we should abstract out the streaming
        //    string s = sb.ToString();
        //    var bytes = Encoding.UTF8.GetBytes(s);
        //    MemoryStream stream = new MemoryStream(bytes);

        //    DateTime dt = DateTime.Now;
        //    return File(stream, "text/plain", string.Format("backup-{0:d4}-{1:d2}-{2:d2}.txt", dt.Year, dt.Month, dt.Day));
        //}

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
            var songs = from s in Database.Songs where s.TitleHash != 0 select s;
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
            songs = from s in Database.Songs where s.TitleHash != 0 select s;
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

            Database.SaveChanges();

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
                Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Rolling Back Database");
                migrator = BuildMigrator();
                migrator.Update(state);
            }
            else
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Wiping Database");
                ObjectContext objectContext = ((IObjectContextAdapter)Context).ObjectContext;
                objectContext.DeleteDatabase();
                migrator = BuildMigrator();
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Starting Migrator Update");
            // Apply all migrations up to a specific migration
            migrator.Update();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Exiting RestoreDB");
        }

        private void ReseedDB()
        {
            m4d.Migrations.Configuration.DoSeed(Context);
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
        /// Reloads a list of songs into the database
        /// </summary>
        /// <param name="lines"></param>
        private void UpdateDB(List<string> lines, bool? batch)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Entering UpdateDB");

            Context.Configuration.AutoDetectChangesEnabled = false;

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Loading Dances");
            Database.Dances.Load();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Loading Songs");

            foreach (string line in lines)
            {
                SongDetails sd = new SongDetails(line);
                Song song = Database.FindSong(sd.SongId);

                string name = sd.ModifiedList.Last().UserName;
                ApplicationUser user = Database.FindOrAddUser(name,DanceMusicService.EditRole);

                if (song == null)
                {
                    song = Database.CreateSong(user,sd);
                }
                else if (sd.IsNull)
                {
                    Database.DeleteSong(user, song);
                }
                else {
                    Database.UpdateSong(user, song, sd, false);
                }
                //song.Modified = DateTime.Now;
            }

            Context.Configuration.AutoDetectChangesEnabled = true;
            Context.ChangeTracker.DetectChanges();
            Database.SaveChanges();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Clearing Song Cache");
            SongCounts.ClearCache();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Exiting ReloadDB");
        }
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
                var songs = from s in Database.Songs where (s.TitleHash == song.TitleHash) select s;

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
            Database.Dispose();
            base.Dispose(disposing);
        }
    }
}