using DanceLibrary;
using m4d.Utilities;
using m4d.ViewModels;
using PagedList;
using m4d.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace music4dance.Controllers
{
    public class SimpleDance
    {
        public string ID { get; set; }
        public string Name { get; set; }
    };

    public class SongController : Controller
    {
        private DanceMusicContext _db = new DanceMusicContext();

        //
        // GET: /Song/

        [AllowAnonymous]
        public ActionResult Index(string dances, string sortOrder, string currentFilter, string searchString, int? page, int? level)
        {
            // Set up the viewbag
            ViewBag.ActionName = "Index";

            ViewBag.CurrentSort = sortOrder;
            if (dances == "ALL")
                dances = null;
            ViewBag.CurrentDances = dances;

            ViewBag.TitleSort = String.IsNullOrEmpty(sortOrder) ? "Title_desc" : "";
            ViewBag.ArtistSort = sortOrder == "Artist" ? "Artist_desc" : "Artist";
            ViewBag.AlbumSort = sortOrder == "Album" ? "Album_desc" : "Album";

            ViewBag.TitleClass = string.Empty;
            ViewBag.ArtistClass = string.Empty;
            ViewBag.AlbumClass = string.Empty;

            ViewBag.Level = level ?? 1;

            // Set up search string
            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }
            ViewBag.CurrentFilter = searchString;

            IList<SongCounts> songCounts = SongCounts.GetFlatSongCounts(_db);
            var scq = songCounts.Select(s => new { s.DanceId, s.DanceNameAndCount });
            var scl = new SelectList(scq.AsEnumerable(), "DanceId", "DanceNameAndCount");
            ViewBag.Dances = scl;

            // Now setup the view
            // Start with all of the songs in the database
            var songs = from s in _db.Songs where s.TitleHash != 0  select s;

            // Now limit it down to the ones that are marked as a particular dance or dances
            if (!string.IsNullOrWhiteSpace(dances))
            {
                string[] danceList = Dances.Instance.ExpandDanceList(dances);

                songs = songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            }

            // Now limit it by anything that has the serach string in the title, album or artist
            if (!String.IsNullOrEmpty(searchString))
            {
                songs = songs.Where(
                    s => s.Title.ToUpper().Contains(searchString.ToUpper()) || 
                    s.Album.ToUpper().Contains(searchString.ToUpper()) || 
                    s.Artist.ToUpper().Contains(searchString.ToUpper()));
            }

            // Now sort the list
            switch (sortOrder)
            {
                case "Title_desc":
                    songs = songs.OrderByDescending(s => s.Title);
                    ViewBag.TitleClass = "class=desc";
                    break;

                case "Artist":
                    songs = songs.OrderBy(s => s.Artist);
                    ViewBag.ArtistClass = "class=asc";
                    break;

                case "Artist_desc":
                    songs = songs.OrderByDescending(s => s.Artist);
                    ViewBag.ArtistClass = "class=desc";
                    break;

                case "Album":
                    songs = songs.OrderBy(s => s.Album);
                    ViewBag.AlbumClass = "class=desc";
                    break;

                case "Album_desc":
                    songs = songs.OrderByDescending(s => s.Album);
                    ViewBag.AlbumClass = "class=asc";
                    break;

                default:
                    songs = songs.OrderBy(s => s.Title);
                    ViewBag.TitleClass = "class=asc";
                    break;

            }

            int pageSize = 25;
            int pageNumber = (page ?? 1);

            return View(songs.ToPagedList(pageNumber, pageSize));
        }

        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public ActionResult Details(int id = 0)
        {
            SongDetails song = _db.FindSongDetails(id);
            if (song == null)
            {
                return HttpNotFound();
            }
            ViewBag.BackAction = "Index";

            return View(song);
        }

        //
        // GET: /Song/Create
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create()
        {
            ViewBag.DanceListAdd = GetDances();
            SongDetails sd = new SongDetails();
            return View(sd);
        }

        //
        // POST: /Song/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Create(SongDetails song, List<string> addDances)
        {
            if (ModelState.IsValid)
            {

                ApplicationUser user = _db.FindUser(User.Identity.Name);
                Song newSong = _db.CreateSong(user, song, addDances);

                // TODO: Think about if the round-trip is necessary
                if (newSong != null)
                {
                    song = new SongDetails(newSong);
                }

                return View("Details", song);
            }
            else
            {
                // Add back in the danceratings
                // TODO: This almost certainly doesn't preserve edits...
                SongDetails songT = _db.FindSongDetails(song.SongId);
                ViewBag.DanceListAdd = GetDances();

                // Clean out empty albums
                for (int i = 0; i < song.Albums.Count; )
                {
                    if (string.IsNullOrWhiteSpace(song.Albums[i].Name))
                    {
                        song.Albums.RemoveAt(i);
                    }
                    else
                    {
                        i += 1;
                    }

                }

                return View(song);
            }
        }

        //
        // GET: /Song/Edit/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(int id = 0)
        {
            SongDetails song = _db.FindSongDetails(id);
            if (song == null)
            {
                return HttpNotFound();
            }

            ViewBag.DanceListRemove = GetDances(song.DanceRatings);
            ViewBag.DanceListAdd = GetDances();

            return View(song);
        }

        //
        // POST: /Song/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(SongDetails song, List<string> addDances, List<string> remDances)
        {
            if (ModelState.IsValid)
            {
//#if DEBUG
//                _db.Dump();
//#endif

                ApplicationUser user = _db.FindUser(User.Identity.Name);

//#if DEBUG
//                _db.Dump();
//#endif
                SongDetails edit = _db.EditSong(user, song, addDances, remDances);

//#if DEBUG
//                _db.Dump();
//#endif

                if (edit != null)
                {
                    return View("Details", edit);
                }
                {
                    return RedirectToAction("Index");
                }
            }
            else
            {
                var errors = ModelState.SelectMany(x => x.Value.Errors.Select(z => z.Exception));


                // Add back in the danceratings
                // TODO: This almost certainly doesn't preserve edits...
                SongDetails songT = _db.FindSongDetails(song.SongId);
                ViewBag.DanceListRemove = GetDances(songT.DanceRatings);
                ViewBag.DanceListAdd = GetDances();

                // Clean out empty albums
                for (int i = 0; i < song.Albums.Count;  )
                {
                    if (string.IsNullOrWhiteSpace(song.Albums[i].Name))
                    {
                        song.Albums.RemoveAt(i);
                    }
                    else
                    {
                        i += 1;
                    }

                }

                return View(song);
            }
        }

        //
        // GET: /Song/Delete/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Delete(int id = 0)
        {
            Song song = _db.Songs.Find(id);
            if (song == null)
            {
                return HttpNotFound();
            }
            return View(song);
        }

        //
        // POST: /Song/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult DeleteConfirmed(int id)
        {
            Song song = _db.Songs.Find(id);
            string userName = User.Identity.Name;
            ApplicationUser user = _db.Users.FirstOrDefault(u => u.UserName == userName);
            _db.DeleteSong(user,song);
            return RedirectToAction("Index");
        }


        //
        // Merge: /Song/MergeCandidates
        public ActionResult MergeCandidates(int? page, int? level, bool? autoCommit)
        {
            IList<Song> songs = null;

            if (autoCommit == true)
            {
                songs = _db.FindMergeCandidates(10000, level ?? 1);
            }
            else
            {
                songs = _db.FindMergeCandidates(500, level ?? 1);
            }

            int pageSize = 25;
            int pageNumber = page ?? 1;

            ViewBag.ActionName = "MergeCandidates";
            ViewBag.Level = level ?? 1;

            if (autoCommit.HasValue && autoCommit.Value == true)
            {
                songs = AutoMerge(songs);

            }

            return View("Index", songs.ToPagedList(pageNumber, pageSize)); 
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult BulkEdit(int[] selectedSongs, string action)
        {
            var songs = from s in _db.Songs
                        where selectedSongs.Contains(s.SongId)
                        select s;

            switch (action)
            {
                case "Merge":
                    return Merge(songs);
                case "Delete":
                    return Delete(songs);
                default:
                    return View("Index");
            }

        }

        //
        // Merge: /Song/Merge
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult MergeResults(string SongIds)
        {
            // See if we can do the actual merge and then return the song details page...
            List<int> ids = SongIds.Split(',').Select(s=>int.Parse(s)).ToList();

            var songs = from s in _db.Songs
                        where ids.Contains(s.SongId)
                        select s;
            List<Song> songList = songs.ToList();

            // Create a merged version of the song (and commit to DB)

            // Get the logged in user
            string userName = User.Identity.Name;
            ApplicationUser user = _db.Users.FirstOrDefault(u => u.UserName == userName);

            List<SongDetails> details = songList.Select(s => new SongDetails(s)).ToList();
            List<AlbumDetails> albumsIn = new List<AlbumDetails>();
            List<AlbumDetails> albumsOut = new List<AlbumDetails>();

            foreach (SongDetails sd in details)
            {
                albumsIn.AddRange(sd.Albums);
            }

            int defIdx = -1;
            string def = Request.Form[DanceMusicContext.AlbumList];
            if (!string.IsNullOrWhiteSpace(def))
            {
                int.TryParse(def, out defIdx);
            } 

            if (defIdx >= 0 && albumsIn.Count > defIdx)
            {
                albumsOut.Add(albumsIn[defIdx]);
            }

            for (int i = 0; i < albumsIn.Count; i++)
            {
                if (i != defIdx)
                {
                    string name = DanceMusicContext.AlbumList + "_" + i.ToString();

                    if (Request.Form.AllKeys.Contains(name))
                    {
                        albumsOut.Add(albumsIn[i]);
                    }
                }
            }

            Song song = _db.MergeSongs(user, songList, 
                ResolveStringField(DanceMusicContext.TitleField, songList, Request.Form),
                ResolveStringField(DanceMusicContext.ArtistField, songList, Request.Form),
                ResolveStringField(DanceMusicContext.GenreField, songList, Request.Form),
                ResolveDecimalField(DanceMusicContext.TempoField, songList, Request.Form),
                ResolveIntField(DanceMusicContext.LengthField, songList, Request.Form),
                albumsOut);

            ViewBag.BackAction = "MergeCandidates";

            return View("Details",_db.FindSongDetails(song.SongId));
        }

        // GET: /Song/XboxSearch/5?search=name
        [Authorize(Roles = "canEdit")]
        public ActionResult XboxSearch(int id = 0, string search = null)
        {
            SongDetails song = _db.FindSongDetails(id);
            if (song == null)
            {
                return HttpNotFound();
            }

            HttpWebRequest request = null;
            HttpWebResponse response = null;

            string responseString = null;

            // Make Music database request
            if (search == null)
                search = song.Title + " " + song.Artist;
            string searchEnc = System.Uri.EscapeDataString(search);

            string req = string.Format("https://music.xboxlive.com/1/content/music/search?q={0}&filters=tracks",searchEnc);
            request = (HttpWebRequest)WebRequest.Create(req);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";
            request.Headers.Add("Authorization", XboxAuthorization);

            ViewBag.Search = search;
            ViewBag.Error = false;
            try {
                using (response = (HttpWebResponse)request.GetResponse())
                {
                    ViewBag.Status = response.StatusCode.ToString();

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var sr = new StreamReader(response.GetResponseStream()))
                        {
                            responseString = sr.ReadToEnd();
                        }

                        ViewBag.ResultString = responseString;

                        responseString = responseString.Replace(@"""music.amg""", @"""music_amg""");
                        var results = System.Web.Helpers.Json.Decode(responseString);
                        ViewBag.Results = results;
                    }
                }
            }
            catch (WebException we)
            {
                ViewBag.Error = true;
                ViewBag.Status = we.Message;
            }

            return View(song);
        }

        // ChooseXbox: /Song/ChooseXbox
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult ChooseXbox(int songId, string name, string album, string artist, string trackId, string alternateId, string duration, string genre, int? trackNum)
        {
            SongDetails song = _db.FindSongDetails(songId);
            if (song == null)
            {
                return HttpNotFound();
            }

            // This is a very transitory object to hold the old values for a semi-automated edit
            SongDetails alt = new SongDetails();

            if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, song.Title))
            {
                alt.Title = song.Title;
                song.Title = name;
            }

            if (!string.IsNullOrWhiteSpace(artist) && !string.Equals(artist, song.Artist))
            {
                alt.Artist = song.Artist;
                song.Artist = name;
            }

            AlbumDetails ad = song.FindAlbum(album);
            if (ad != null)
            {
                // If there is a match set up the new info next to the album
                int aidxM = song.Albums.IndexOf(ad);
                alt.Albums = new List<AlbumDetails>();

                for (int aidx = 0; aidx < song.Albums.Count; aidx++)
                {
                    if (aidx == aidxM)
                    {
                        AlbumDetails adA = new AlbumDetails(ad);
                        if (!string.Equals(album, ad.Name))
                        {
                            adA.Name = ad.Name;
                            ad.Name = album;
                        }

                        if (trackNum != ad.Track)
                        {
                            adA.Track = ad.Track;
                            ad.Track = trackNum;
                        }
                        alt.Albums.Add(adA);
                    }
                    else
                    {
                        alt.Albums.Add(new AlbumDetails());
                    }
                }
            }
            else 
            {
                // Otherwise just add an album
                ad = new AlbumDetails { Name = album, Track=trackNum };
                song.Albums.Add(ad);
            }
            UpdateXboxPurchase(ad, trackId, alternateId);

            if (!string.IsNullOrWhiteSpace(duration))
            {
                try
                {
                    SongDuration sd = new SongDuration(duration);

                    int length = decimal.ToInt32(sd.Length);

                    if (length != song.Length)
                    {
                        alt.Length = song.Length;
                        song.Length = length;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {

                }
            }

            // TODO: Should we handle multiple genres?
            //if (genre != null && genre.Length > 0 && !string.IsNullOrWhiteSpace(genre[0]) && !string.Equals(genre[0], song.Genre))
            if (!string.IsNullOrWhiteSpace(genre) && !string.Equals(genre, song.Genre))
            {
                alt.Genre = song.Genre;
                song.Genre = genre;
            }

            ViewBag.OldSong = alt;

            return View("Edit", song);
        }

        private void UpdateXboxPurchase(AlbumDetails ad, string trackId, string alternateId)
        {
            ad.SetPurchaseInfo(PurchaseType.Song, MusicService.XBox, trackId);
            if (!string.IsNullOrWhiteSpace(alternateId))
            {
                ad.SetPurchaseInfo(PurchaseType.Song, MusicService.AMG, alternateId);
            }
        }

        private MultiSelectList GetDances(IList<DanceRating> ratings = null)
        {
            List<SimpleDance> Dances = new List<SimpleDance>(_db.Dances.Count());

            foreach (Dance d in _db.Dances)
            {
                Dances.Add(new SimpleDance() { ID = d.Id, Name = d.Info.Name });
            }

            string[] selarr = null;

            if (ratings != null)
            {
                List<string> selected = new List<string>(ratings.Count());
                foreach (DanceRating dr in ratings)
                {
                    selected.Add(dr.DanceId);
                }

                selarr = selected.ToArray();
            }

            return new MultiSelectList(Dances, "ID", "Name", selarr);
        }
        private IList<Song> AutoMerge(IList<Song> songs)
        {
            //using (DanceMusicContext dmc = new DanceMusicContext())
            //{
            //    // TODO: Is this somehow global?  The examples that change this flag have both the using and a try/catch
            //    //  to turn it off.

            //    dmc.Configuration.AutoDetectChangesEnabled = false;

            DanceMusicContext dmc = _db;

            // Get the logged in user
            string userName = User.Identity.Name;
            ApplicationUser user = dmc.Users.FirstOrDefault(u => u.UserName == userName);

            List<Song> ret = new List<Song>();
            List<Song> cluster = null;

            foreach (Song song in songs)
            {
                if (cluster == null)
                {
                    cluster = new List<Song>();
                    cluster.Add(song);
                }
                else if (song.Equivalent(cluster[0]))
                {
                    cluster.Add(song);
                }
                else
                {
                    if (cluster.Count > 1)
                    {
                        Song s = AutoMerge(dmc, cluster, user);
                        ret.Add(s);
                    }
                    else if (cluster.Count == 1)
                    {
                        Debug.WriteLine(string.Format("Bad Merge: {0}", cluster[0].Signature));
                    }

                    cluster = new List<Song>();
                    cluster.Add(song);
                }
            }

            return ret;
            //}
        }

        private Song AutoMerge(DanceMusicContext dmc, List<Song> songs, ApplicationUser user)
        {

            // Note that automerging will only work for single album cases

            Song song = dmc.MergeSongs(user, songs,
                ResolveStringField(DanceMusicContext.TitleField, songs),
                ResolveStringField(DanceMusicContext.ArtistField, songs),
                ResolveStringField(DanceMusicContext.GenreField, songs),
                ResolveDecimalField(DanceMusicContext.TempoField, songs),
                ResolveIntField(DanceMusicContext.LengthField, songs),
                SongDetails.BuildAlbumInfo(songs[0])
                );

            return song;
        }
        private ActionResult Merge(IQueryable<Song> songs)
        {
            SongMerge sm = new SongMerge(songs.ToList());

            return View("Merge", sm);
        }

        private ActionResult Delete(IQueryable<Song> songs)
        {
            ApplicationUser user = _db.FindUser(User.Identity.Name);

            foreach (Song song in songs)
            {
                _db.DeleteSong(user, song);
            }

            return RedirectToAction("Index");
        }


        private string ResolveStringField(string fieldName, IList<Song> songs, System.Collections.Specialized.NameValueCollection form = null)
        {
            object obj = ResolveMergeField(fieldName, songs, form);

            return obj as string;
        }

        private string ResolveMultiStringField(string fieldName, IList<Song> songs)
        {
            HashSet<string> hs = new HashSet<string>();

            foreach (Song song in songs)
            {
                string s = song.GetType().GetProperty(fieldName).GetValue(song) as string;

                if (s != null)
                {
                    string[] values = s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string v in values)
                    {
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            hs.Add(v);
                        }
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            string sep = string.Empty;

            foreach (string v in hs)
            {
                sb.Append(sep);
                sb.Append(v);
                sep = ";";
            }

            return sb.ToString();
        }

        private int? ResolveIntField(string fieldName, IList<Song> songs, System.Collections.Specialized.NameValueCollection form = null)
        {
            int? ret = ResolveMergeField(fieldName, songs, form) as int?;

            return ret;
        }

        private decimal? ResolveDecimalField(string fieldName, IList<Song> songs, System.Collections.Specialized.NameValueCollection form = null)
        {
            decimal? ret = ResolveMergeField(fieldName, songs, form) as decimal?;

            return ret;
        }

        private object ResolveMergeField(string fieldName, IList<Song> songs, System.Collections.Specialized.NameValueCollection form = null)
        {
            // If fieldName doesn't exist, this means that we didn't add a radio button for the field because all the
            //  values were the same.  So just return the value of the first song.
            object ret = null;

            // if form is != null we disambiguate based on form otherwise it's the first non-null field

            int idx = 0;
            if (form != null)
            {
                string s = form[fieldName];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    int.TryParse(s, out idx);
                }
            }
            else
            {
                for (int i = 0; i < songs.Count; i++)
                {
                    Song song = songs[i];

                    if (song.GetType().GetProperty(fieldName).GetValue(song) != null)
                    {
                        idx = i;
                        break;
                    }
                }
            }

            ret = songs[idx].GetType().GetProperty(fieldName).GetValue(songs[idx]);
            return ret;
        }

        private static string XboxAuthorization
        {
            get
            {
                if (s_token == null)
                {
                    string clientId = "Music4Dance";
                    string clientSecret = "3kJ506OgMCD+nmuzUCRrXt/gnJlV07qQuxsEZBMZCqw=";

                    s_admAuth = new AdmAuthentication(clientId, clientSecret);
                    s_token = s_admAuth.GetAccessToken();
                }

                return "Bearer " + s_token.access_token;
            }
        }

        private static AdmAuthentication s_admAuth = null;
        private static AdmAccessToken s_token = null;
        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
    }
}