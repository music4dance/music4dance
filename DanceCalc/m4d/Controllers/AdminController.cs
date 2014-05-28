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
using m4d.ViewModels;
using m4dModels;
using DanceLibrary;
using System.Text.RegularExpressions;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;

namespace m4d.Controllers
{
    public class AdminController : Controller
    {
        private DanceMusicContext _db = new DanceMusicContext();

        #region Commands
        //
        // GET: /Admin/
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult Index()
        {
            return View();
        }

        //
        // Get: //SeedDatabase
        [Authorize(Roles = "dbAdmin")]
        public ActionResult SeedDatabase()
        {
            List<string> results = new List<string>();

            bool seeded = false;
            if (_db.Songs.Any(s => s.Title == "Tea for Two"))
            {
                seeded = true;
            }

            if (!seeded)
            {
                BuildDanceMap();

                foreach (string name in _dbs)
                {
                    string file = string.Format("~/Content/{0}.csv", name);
                    string path = Server.MapPath(Url.Content(file));

                    string[] lines = System.IO.File.ReadAllLines(path);

                    if (lines.Length > 1)
                    {
                        BuildSchema(lines);

                        for (int i = 1; i < lines.Length; i += _chunk)
                        {
                            DateTime start = DateTime.Now;
                            SeedRows(name, lines, i, _chunk);
                            DateTime end = DateTime.Now;

                            TimeSpan length = end - start;
                            string message = string.Format("Songs from ({0} were loaded in: {1}", name, length);
                            results.Add(message);
                            Trace.WriteLine(message);
                        }

                    }
                }
            }

            ViewBag.Results = results;
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
        // Get: //ReloadDatabase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult ReloadDatabase()
        {
            List<string> lines = UploadFile();

            ViewBag.Name = "Restore Database";
            if (lines.Count > 0)
            {
                RestoreDB();

                ReloadDB(lines);

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

        //
        // Get: //UploadTempoes
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UploadTempoes(bool commit = false, int fileId = -1)
        {
            IList<LocalMerger> results = null;
            if (commit && fileId != -1)
            {
                results = CommitUploadTempoes(fileId); 
            }
            else 
            {
                results = ReviewUploadTempoes();
            }

            return View(results);
        }

        private IList<LocalMerger> ReviewUploadTempoes()
        {
            IList<LocalMerger> results = null;

            int fileId = UploadAndCache();

            if (fileId != -1)
            {
                List<string> lines = GetFileById(fileId);

                ViewBag.Name = "Upload Tempoes";
                ViewBag.FileId = fileId;

                if (lines.Count > 0)
                {
                    results = MatchSongs(lines);
                }
            }

            return results;
        }

        private IList<LocalMerger> CommitUploadTempoes(int fileId)
        {
            IList<LocalMerger> initial = null;
            IList<LocalMerger> results = null;
            List<string> lines = GetFileById(fileId);

            ViewBag.Name = "Upload Tempoes";
            ViewBag.FileId = fileId;

            ApplicationUser user = _db.FindUser(User.Identity.Name);

            if (lines.Count > 0)
            {
                initial = MatchSongs(lines);
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
                        if (string.IsNullOrWhiteSpace(edit.Genre) && ! string.IsNullOrWhiteSpace(sd.Genre))
                        {
                            modified = true;
                            edit.Genre = sd.Genre;
                        }

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

                        if (modified && _db.EditSong(user, edit,null,null) != null)
                        {
                            results.Add(m);
                        }
                    }
                }
            }

            return results;
        }

        private IList<LocalMerger> MatchSongs(List<string> lines)
        {
            List<LocalMerger> merge = new List<LocalMerger>();

            List<string> map = SongDetails.BuildHeaderMap(lines[0]);
            for (int i = 1; i < lines.Count; i++ )
            {
                SongDetails song = SongDetails.CreateFromRow(map, lines[i]);

                var songs = from s in _db.Songs where (s.TitleHash == song.TitleHash) select s;

                List<SongDetails> candidates = new List<SongDetails>();
                foreach (Song s in songs)
                {
                    // Title-Artist match at minimum
                    if (string.Equals(Song.CreateNormalForm(s.Artist),Song.CreateNormalForm(song.Artist)))
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

                    // Otherwise, if there is only one candidate and it doesn't have any 'real'
                    //  albums, we will choose it
                    if (match == null && candidates.Count == 1 && !candidates[0].HasRealAblums)
                    {
                        type = MatchType.Weak;
                        match = candidates[0];
                    }
                }

                if (match != null)
                {
                    LocalMerger m = new LocalMerger { Left = song, Right = match, MatchType =  type, Conflict = false };
                    if (song.TempoConflict(match,3))
                    {
                        m.Conflict = true;
                    }
                    merge.Add(m);
                }
            }

            return merge;
        }

        //
        // Get: //BackupDatabase
        [Authorize(Roles = "showDiagnostics")]
        public ActionResult BackupDatabase(string useLookupHistory = null)
        {
            StringBuilder sb = new StringBuilder();

            foreach (Song song in _db.Songs)
            {
                string[] actions = null;
                if (!string.IsNullOrWhiteSpace(useLookupHistory))
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

            return File(stream, "text/plain", "backup.txt");
        }

        //
        // Get: //RestoreDatabase
        [Authorize(Roles = "dbAdmin")]
        public ActionResult RestoreDatabase()
        {
            RestoreDB();

            ViewBag.Name = "Restore Database";
            ViewBag.Success = true;
            ViewBag.Message = "Database was successfully restored.";

            return View("Results");
        }
        
        #endregion

        #region Migration-Restore
        private void RestoreDB()
        {
            var migrator = BuildMigrator();

            // Roll back to a specific migration
            migrator.Update("InitialCreate");

            // Apply all migrations up to a specific migration
            migrator.Update();
        }

        private void ReseedDB()
        {
            Configuration.DoSeed(_db);
        }
        private void BuildDanceMap()
        {
            foreach (DanceObject d in Dance.DanceLibrary.DanceDictionary.Values)
            {
                string name = CleanName(d.Name);
                _danceMap.Add(name, d.Id);
            }
        }

        private DbMigrator BuildMigrator()
        {
            var configuration = BuildConfiguration();

            return new DbMigrator(configuration);
        }

        private Configuration BuildConfiguration()
        {
            string sqlConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            var connectionInfo = new DbConnectionInfo(sqlConnectionString, "System.Data.SqlClient");

            var configuration = new Configuration
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
        private void ReloadDB(List<string> lines)
        {
            using (DanceMusicContext dmc = new DanceMusicContext())
            {
                // TODO: Is this somehow global?  The examples that change this flag have both the using and a try/catch
                //  to turn it off.

                dmc.Configuration.AutoDetectChangesEnabled = false;
                // Load the dance List
                dmc.Dances.Load();

                foreach (string line in lines)
                {
                    DateTime time = DateTime.Now;
                    Song song = dmc.Songs.Create();
                    song.Created = time;
                    song.Modified = time;
                    dmc.Songs.Add(song);
                    dmc.SaveChanges();

                    song.Load(line, dmc);

                    dmc.UpdateUsers(song);
                }

                HashSet<string> map = new HashSet<string>();

                foreach (Song song in dmc.Songs)
                {
                    if (song.ModifiedBy != null)
                        foreach (ModifiedRecord us in song.ModifiedBy)
                        {
                            string s = string.Format("{0}:{1}", song.SongId, us.ApplicationUserId);
                            if (map.Contains(s))
                            {
                                Trace.WriteLine(string.Format("Duplicate: '{0}'", s));
                            }
                            else
                            {
                                map.Add(s);
                            }
                        }
                }

                dmc.Configuration.AutoDetectChangesEnabled = true;
                dmc.SaveChanges();
            }
        }

        private void BuildSchema(string[] lines)
        {
            _tempoKind = TempoKind.BPM;
            _dynamicColumns = new List<int>();

            _danceColumn = -1;
            _titleColumn = -1;
            _artistColumn = -1;
            _albumColumn = -1;
            _labelColumn = -1;
            _tempoColumn = -1;
            _altTempoColumn = -1;
            _amazoncolumn = -1;
            _itunescolumn = -1;

            string headerLine = lines[0];
            _headers = headerLine.Split(new char[] { '\t' });

            // Map the table columns to database columns
            int c = 0;
            foreach (string header in _headers)
            {
                if (string.Equals(header, "DANCE", StringComparison.OrdinalIgnoreCase))
                {
                    _danceColumn = c;
                }
                else if (string.Equals(header, "TITLE", StringComparison.OrdinalIgnoreCase))
                {
                    _titleColumn = c;
                }
                else if (string.Equals(header, "ARTIST", StringComparison.OrdinalIgnoreCase)|| string.Equals(header, "CONTRIBUTING ARTIST", StringComparison.OrdinalIgnoreCase))
                {
                    _artistColumn = c;
                }
                else if (string.Equals(header, "ALBUM", StringComparison.OrdinalIgnoreCase))
                {
                    _albumColumn = c;
                }
                else if (string.Equals(header, "LABEL", StringComparison.OrdinalIgnoreCase) || string.Equals(header, "PUBLISHER", StringComparison.OrdinalIgnoreCase))
                {
                    _labelColumn = c;
                }
                else if (string.Equals(header, "BPM", StringComparison.OrdinalIgnoreCase) || string.Equals(header, "BEATS-PER-MINUTE", StringComparison.OrdinalIgnoreCase))
                {
                    _tempoColumn = c;
                }
                else if (string.Equals(header, "MPM", StringComparison.OrdinalIgnoreCase))
                {
                    _tempoKind = TempoKind.MPM;
                    _tempoColumn = c;
                }
                else if (string.Equals(header, "MPMS", StringComparison.OrdinalIgnoreCase))
                {
                    _tempoKind = TempoKind.MPM;
                    _tempoColumn = c;
                }
                else if (string.Equals(header, "MPMR", StringComparison.OrdinalIgnoreCase))
                {
                    _tempoKind = TempoKind.MPM;
                    _altTempoColumn = c;
                }
                else if (string.Equals(header, "AmazonAlbum", StringComparison.OrdinalIgnoreCase))
                {
                    _amazoncolumn = c;
                }
                else if (string.Equals(header, "ITunes", StringComparison.OrdinalIgnoreCase))
                {
                    _itunescolumn = c;
                }
                else if (string.Equals(header, "Length", StringComparison.OrdinalIgnoreCase))
                {
                    _lengthcolumn = c;
                }
                else if (string.Equals(header, "#", StringComparison.OrdinalIgnoreCase))
                {
                    _trackcolumn = c;
                }
                else
                {
                    _dynamicColumns.Add(c);
                }

                // TODO: Track # ?

                Debug.Assert(string.Equals(header, _headers[c]));
                c += 1;
            }

            Debug.Assert(_headers.Length == c);
            Debug.Assert(_titleColumn != -1 && _danceColumn != -1);
        }

        private SongDetails SongFromRow(string userName, string line)
        {
            throw new NotImplementedException();
        }
        // TODO: I like the idea of building transitoory SongDetails objects
        //  from the line data so that I can add in a review phase - but
        //  I'm not going to rip the old stuff out until I get it working
        private void SeedRows(string userName, string[] lines, int start, int count)
        {
            using (DanceMusicContext dmc = new DanceMusicContext())
            {
                // TODO: Is this somehow global?  The examples that change this flag have both the using and a try/catch
                //  to turn it off.

                dmc.Configuration.AutoDetectChangesEnabled = false;
                // Load the dance List
                dmc.Dances.Load();

                // Find or Create a user for these entries
                ApplicationUser user = dmc.Users.FirstOrDefault(u => u.UserName == userName);
                //if (user == null)
                //{
                //    user = dmc.Users.Create();
                //    user.UserName = Name;
                //    user = dmc.Users.Add(user);

                //    dmc.SaveChanges();
                //}

                //dmc.CreateSongProperty(DanceMusicContext.StartBatchLoadCommand,Name);

                for (int i = start; i < lines.Length && i < start + count; i++)
                {
                    string[] cells = lines[i].Split(new char[] { '\t' });
                    if (cells.Length != _headers.Length)
                    {
                        Trace.WriteLine(string.Format("Bad Format: {0}", lines[i]));
                    }

                    // Find the dance and skip if it doesn't exist
                    string danceIds = null;

                    if (cells.Length < _danceColumn)
                        break;

                    string danceName = CleanName(cells[_danceColumn]);
                    if (!_danceMap.TryGetValue(danceName, out danceIds))
                    {
                        Trace.WriteLine(string.Format("Dance Not Found: {0}", cells[_danceColumn]));
                        continue;
                    }
                    string[] idList = danceIds.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    // Create a song based on the info in each line

                    string title = CleanText(cells[_titleColumn]);
                    string artist = null;
                    if (_artistColumn != -1 && _artistColumn < cells.Length)
                    {
                        artist = CleanArtist(cells[_artistColumn]);
                    }

                    string album = null;
                    if (_albumColumn != -1 && _albumColumn < cells.Length)
                    {
                        album = cells[_albumColumn];
                    }

                    string label = null;
                    if (_labelColumn != -1 && _labelColumn < cells.Length)
                    {
                        label = cells[_labelColumn];
                    }

                    // Add the Tempo
                    decimal? bpm = null;
                    if (_tempoColumn != -1 && _tempoColumn < cells.Length)
                    {
                        string tempo = null;
                        tempo = cells[_tempoColumn].Trim();
                        if (string.IsNullOrEmpty(tempo) && _altTempoColumn != -1)
                        {
                            tempo = cells[_altTempoColumn].Trim();
                        }

                        switch (_tempoKind)
                        {
                            case TempoKind.BPM:
                                {
                                    decimal bpmT;
                                    if (decimal.TryParse(tempo, out bpmT))
                                    {
                                        bpm = bpmT;
                                    }
                                }
                                break;
                            case TempoKind.MPM:
                                {
                                    decimal mpm;
                                    if (decimal.TryParse(tempo, out mpm))
                                    {
                                        DanceObject d = Dances.Instance.DanceDictionary[idList[0]];
                                        Meter meter = d.Meter;
                                        Tempo tpo = new Tempo(mpm, new TempoType(_tempoKind, meter));
                                        Tempo mpt = tpo.Convert(new TempoType(TempoKind.BPM));
                                        bpm = mpt.Rate;
                                    }
                                }
                                break;
                            case TempoKind.BPS:
                                // TODO: Handle BPS (if i ever see a DB that lists this way
                                Debug.Assert(false);
                                break;
                        }
                    }

                    //  TODO: We may have purchase info for songs that don't have an album name - 
                    //    currently we're dropping those on the floor, do we care?

                    // Fill in album info including publisher and purchase
                    List<AlbumDetails> albums = null;

                    if (!string.IsNullOrWhiteSpace(album))
                    {
                        AlbumDetails ad = new AlbumDetails() { Name = album, Publisher = label };

                        if (_amazoncolumn != -1)
                        {
                            string aa = cells[_amazoncolumn];
                            aa = aa.Trim();
                            if (!string.IsNullOrEmpty(aa))
                            {
                                ad.SetPurchaseInfo(PurchaseType.Album, ServiceType.Amazon, aa);
                            }
                        }

                        if (_itunescolumn != -1)
                        {
                            string text = cells[_itunescolumn];

                            if (!string.IsNullOrEmpty(text))
                            {
                                string r1 = @"%252Fid(?<album>\d*)%253Fi%253D(?<song>\d*)";
                                string r2 = @"%252FviewAlbum%253Fi%253D(?<album>\d*)%2526id%253D(?<song>\d*)";

                                Match match = Regex.Match(text, r1);
                                if (!match.Success)
                                    match = Regex.Match(text, r2);

                                bool success = false;
                                if (match.Success)
                                {
                                    string aid = match.Groups["album"].Value;
                                    string sid = match.Groups["song"].Value;

                                    if (!string.IsNullOrEmpty(aid))
                                    {
                                        ad.SetPurchaseInfo(PurchaseType.Album, ServiceType.ITunes, aid);
                                        success = true;
                                    }
                                    if (!string.IsNullOrEmpty(sid))
                                    {
                                        ad.SetPurchaseInfo(PurchaseType.Song, ServiceType.ITunes, sid);
                                        success = true;
                                    }
                                }

                                if (!success)
                                {
                                    Trace.WriteLine(string.Format("Bad ITunes:{0}", text));
                                }
                            }
                        }

                        albums = new List<AlbumDetails>();
                        albums.Add(ad);
                    }

                    Song song = dmc.CreateSong(user, title, artist, null, bpm, null, albums);

                    // Now set up the dance associations

                    dmc.AddDanceRatings(song, idList);

                    // TODO: this should move down into the db context class 
                    // And add in the dynamic columns
                    foreach (int dc in _dynamicColumns)
                    {
                        string value = cells[dc].Trim();

                        if (!string.IsNullOrEmpty(value))
                        {
                            dmc.CreateSongProperty(song, _headers[dc], value);
                        }
                    }
                }

                //dmc.CreateSongProperty(DanceMusicContext.EndBatchLoadCommand, Name);
                dmc.ChangeTracker.DetectChanges();

                dmc.SaveChanges();
            }
        }

        static private string CleanName(string name)
        {
            string up = name.ToUpper();

            string[] parts = up.Split(new char[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            string ret = string.Join("", parts);

            if (ret.LastIndexOf('S') == ret.Length - 1)
            {
                int truncate = 1;
                if (ret.LastIndexOf('E') == ret.Length - 2)
                {
                    if (ret.Length > 2)
                    {
                        char ch = ret[ret.Length - 3];
                        if (ch != 'A' && ch != 'E' && ch != 'I' && ch != 'O' && ch != 'U')
                        {
                            truncate = 2;
                        }
                    }
                }
                ret = ret.Substring(0, ret.Length - truncate);
            }

            return ret;
        }

        static private string CleanText(string text)
        {
            text = text.Replace("&nbsp;", " ");
            text = text.Replace("&nbsp", " ");
            text = text.Replace("\r", " ");
            text = text.Replace("\n", " ");
            text = text.Replace("\t", " ");
            text = text.Replace("&quot;", "\"");
            text = text.Replace("&quot", "\"");
            text = text.Replace("&amp;", "&");

            // TODO: is it worth doing a generic unicode replace?
            text = text.Replace("&#39;", "'");
            text = text.Replace("&#333;", "ō");

            text = text.Trim();

            if (text.Contains("  "))
            {
                StringBuilder sb = new StringBuilder(text.Length + 1);

                bool space = false;
                foreach (char c in text)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        if (!space)
                        {
                            sb.Append(c);
                        }
                        space = true;
                    }
                    else
                    {
                        sb.Append(c);
                        space = false;
                    }
                }

                text = sb.ToString();
            }

            return text;
        }

        static private string Unsort(string name)
        {
            string[] parts = name.Split(new char[] { ',' });
            if (parts.Length == 1)
            {
                return parts[0].Trim();
            }
            else if (parts.Length == 2)
            {
                return string.Format("{0} {1}", parts[1].Trim(), parts[0].Trim());
            }
            else
            {
                Trace.WriteLine(string.Format("Unusual Sort: {0}", name));
                return name;
            }
        }

        static private string CleanArtist(string name)
        {
            if (name.IndexOf(',') != -1)
            {
                string[] parts = new string[] { name };
                if (name.IndexOf('&') != -1)
                    parts = name.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                else if (name.IndexOf('/') != -1)
                    parts = name.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                string separator = string.Empty;
                StringBuilder sb = new StringBuilder();

                foreach (string s in parts)
                {
                    string u = Unsort(s);
                    sb.Append(separator);
                    sb.Append(u);
                    separator = " & ";
                }

                name = sb.ToString();
            }

            return name;
        }


        string[] _dbs = new string[] { "JohnCrossan", "LetsDanceDenver", "SalsaSwingBallroom", "SandiegoDJ", "SteveThatDJ", "UsaSwingNet", "WaltersDanceCenter" };
        private readonly int _chunk = 500;

        private Dictionary<string, string> _danceMap = new Dictionary<string, string>()
        {
            {"CROSSSTEPWALTZ","SWZ"}, {"SLOWANDCROSSSTEPWALTZ","SWZ"},
            {"SOCIALTANGO","TNG"},
            {"VIENNESE","VWZ"},{"MODERATETOFASTWALTZ","VWZ"},
            {"SLOWDANCEFOXTROT","SFT"},
            {"FOXTROTSLOWDANCE","SFT"},
            {"FOXTROTSANDTRIPLESWING","SFT,ECS"},
            {"FOXTROTTRIPLESWING","SFT,ECS"},
            {"TRIPLESWINGFOXTROT","SFT,ECS"},
            {"TRIPLESWING","ECS"},
            {"WCSWING","WCS"},
            {"SINGLESWING","SWG"},
            {"SINGLETIMESWING","SWG"},
            {"STREETSWING","HST"},
            {"HUSTLESTREETSWING","HST"},
            {"HUSTLECHACHA","HST,CHA"},
            {"CHACHAHUSTLE","HST,CHA"},
            {"CLUBTWOSTEP","NC2"},{"NIGHTCLUB2STEP","NC2"},
            {"TANGOARGENTINO","ATN"},
            {"MERENGUETECHNOMERENGUE","MRG"},
            {"RUMBABOLERO", "RMB,BOL" },
            {"RUMBATWOSTEP", "RMB,NC2" },
            {"SLOWDANCERUMBA", "RMB" },
            {"RUMBASLOWDANCE", "RMB" },
            {"SWINGSEASTANDWESTCOASTLINDYHOPANDJIVE", "SWG"},
            {"TRIPLESWINGTWOSTEP", "SWG,NC2"},
            {"TWOSTEPFOXTROTSINGLESWING", "SWG,FXT,NC2"},
            {"SWINGANDLINDYHOP", "ECS,LHP"},
            {"POLKATECHNOPOLKA", "PLK"},
            {"SALSAMAMBO", "SLS,MBO"},
            {"LINDY", "LHP"}
        };
        private List<int> _dynamicColumns = new List<int>();
        private int _danceColumn = -1;
        private int _titleColumn = -1;
        private int _artistColumn = -1;
        private int _albumColumn = -1;
        private int _labelColumn = -1;
        private int _tempoColumn = -1;
        private int _altTempoColumn = -1;
        private int _amazoncolumn = -1;
        private int _itunescolumn = -1;
        private int _trackcolumn = -1;
        private int _lengthcolumn = -1;
        private TempoKind _tempoKind = TempoKind.BPM;
        private string[] _headers; 
        #endregion

        #region Utilities
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

        int UploadAndCache()
        {
            int ret = -1;
            List<string> lines = UploadFile();

            if (lines.Count > 0)
            {
                lock (s_files)
                {
                    s_files.Add(lines);
                    ret = s_files.Count - 1;
                }
            }
            return ret;
        }
        List<string> GetFileById(int id)
        {
            return s_files[id];
        }

        // This is pretty kludgy as this is a basically a temporary
        //  store that only gets recycled on restart - but since
        //  for now it's being using primarily on the short running
        //  dev instance, it doesn't seem worthwhile to do anything
        //  more sophisticated
        static List<List<string>> s_files = new List<List<string>>();

        #endregion

        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
    }
}