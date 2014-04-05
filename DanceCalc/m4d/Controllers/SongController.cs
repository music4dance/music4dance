using DanceLibrary;
using m4d.Utilities;
using m4d.ViewModels;
using m4dModels;
using PagedList;
using m4d.Context;
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

        public ActionResult Search(string searchString, string dances, string filter)
        {
            SongFilter songFilter = ParseFilter(filter);

            if (string.IsNullOrWhiteSpace(searchString))
            {
                searchString = null;
            }
            if (!string.Equals(searchString, songFilter.SearchString))
            {
                songFilter.SearchString = searchString;
                songFilter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }
            if (!string.Equals(dances, songFilter.Dances))
            {
                songFilter.Dances = dances;
                songFilter.Page = 1;
            }

            return DoIndex(songFilter);
        }

        public ActionResult Sort(string sortOrder, string filter)
        {
            SongFilter songFilter = ParseFilter(filter);

            // TODO: Consider doing something to keep the first song of the page on the
            // page when sort-order changes...
            if (!string.IsNullOrWhiteSpace(sortOrder))
            {
                switch (sortOrder)
                {
                    case "Title":
                        songFilter.SortOrder = String.IsNullOrEmpty(songFilter.SortOrder) ? "Title_desc" : "Title";
                        break;
                    case "Artist":
                        songFilter.SortOrder = songFilter.SortOrder == "Artist" ? "Artist_desc" : "Artist";
                        break;
                    case "Album":
                        songFilter.SortOrder = songFilter.SortOrder == "Album" ? "Album_desc" : "Album";
                        break;
                }
            }

            return DoIndex(songFilter);
        }

        //
        // GET: /Index/

        [AllowAnonymous]
        public ActionResult Index(int? page, string purchase, string filter)
        {
            SongFilter songFilter = ParseFilter(filter);

            if (page.HasValue)
            {
                songFilter.Page = page;
            }

            if (!string.IsNullOrWhiteSpace(purchase))
            {
                songFilter.Purchase = purchase;
            }

            return DoIndex(songFilter);
        }

        private SongFilter ParseFilter(string f, string action="Index")
        {
            if (string.IsNullOrWhiteSpace(f))
            {
                return new SongFilter { Action = action};
            }
            else
            {
                return new SongFilter(f);
            }
        }

        private ActionResult DoIndex(SongFilter filter)
        {
            Trace.WriteLine(string.Format("Entering Song.Index: dances='{0}',sortOrder='{1}',searchString='{2}'", filter.Dances, filter.SortOrder, filter.SearchString));

            // Set up the viewbag
            ViewBag.SongFilter = filter;

            ViewBag.TitleClass = string.Empty;
            ViewBag.ArtistClass = string.Empty;
            ViewBag.AlbumClass = string.Empty;

            IList<SongCounts> songCounts = SongCounts.GetFlatSongCounts(_db);
            var scq = songCounts.Select(s => new { s.DanceId, s.DanceNameAndCount });

            //scq.FirstOrDefault(s => s.DanceId == filter.Dances)
            var scl = new SelectList(scq.AsEnumerable(), "DanceId", "DanceNameAndCount", filter.Dances);
            ViewBag.Dances = scl;

            // Now setup the view
            // Start with all of the songs in the database
            var songs = from s in _db.Songs where s.TitleHash != 0  select s;

            // Filter on purcahse info
            // TODO: Figure out how to get LINQ to do the permutation on contains
            //  any of "AIX"
            if (string.Equals(filter.Purchase,"AIX"))
            {
                songs = songs.Where(s => s.Purchase != null);
            }
            else if (!string.IsNullOrWhiteSpace(filter.Purchase))
            {
                songs = songs.Where(s => s.Purchase.Contains(filter.Purchase));
            }

            // Now limit it down to the ones that are marked as a particular dance or dances
            if (!string.IsNullOrWhiteSpace(filter.Dances) && !string.Equals(filter.Dances,"ALL"))
            {
                string[] danceList = Dances.Instance.ExpandDanceList(filter.Dances);

                songs = songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            }

            // Now limit it by anything that has the serach string in the title, album or artist
            if (!String.IsNullOrEmpty(filter.SearchString))
            {
                songs = songs.Where(
                    s => s.Title.ToUpper().Contains(filter.SearchString.ToUpper()) || 
                    s.Album.ToUpper().Contains(filter.SearchString.ToUpper()) || 
                    s.Artist.ToUpper().Contains(filter.SearchString.ToUpper()));
            }

            // Now sort the list
            string sortAsc = "<span class='glyphicon glyphicon-sort-by-alphabet'></span>";
            string sortDsc = "<span class='glyphicon glyphicon-sort-by-alphabet-alt'></span>";
            switch (filter.SortOrder)
            {
                case "Title":
                default:
                    songs = songs.OrderBy(s => s.Title);
                    ViewBag.TitleSort = sortAsc;
                    break;

                case "Title_desc":
                    songs = songs.OrderByDescending(s => s.Title);
                    ViewBag.TitleSort = sortDsc;
                    break;

                case "Artist":
                    songs = songs.OrderBy(s => s.Artist);
                    ViewBag.ArtistSort = sortAsc;
                    break;

                case "Artist_desc":
                    songs = songs.OrderByDescending(s => s.Artist);
                    ViewBag.ArtistSort = sortDsc;
                    break;

                case "Album":
                    songs = songs.OrderBy(s => s.Album);
                    ViewBag.AlbumSort = sortAsc;
                    break;

                case "Album_desc":
                    songs = songs.OrderByDescending(s => s.Album);
                    ViewBag.AlbumSort = sortDsc;
                    break;

            }

            int pageSize = 25;

            Trace.WriteLine("Exiting Song.Index");

            return View("Index",songs.ToPagedList(filter.Page ?? 1, pageSize));
        }

        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public ActionResult Details(int id = 0, string filter = null)
        {
            SongDetails song = _db.FindSongDetails(id);
            if (song == null)
            {
                return HttpNotFound();
            }

            ViewBag.SongFilter = ParseFilter(filter);
            return View(song);
        }

        //
        // GET: /Song/Create
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create(string filter = null)
        {
            ViewBag.DanceListAdd = GetDances();
            ViewBag.SongFilter = ParseFilter(filter);
            SongDetails sd = new SongDetails();
            ViewBag.BackAction = "Index";
            return View(sd);
        }

        //
        // POST: /Song/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Create(SongDetails song, List<string> addDances, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
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
        public ActionResult Edit(int id = 0, string filter = null)
        {
            SongDetails song = _db.FindSongDetails(id);
            if (song == null)
            {
                return HttpNotFound();
            }

            ViewBag.DanceListRemove = GetDances(song.DanceRatings);
            ViewBag.DanceListAdd = GetDances();

            ViewBag.SongFilter = ParseFilter(filter);
            return View(song);
        }

        //
        // POST: /Song/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(SongDetails song, List<string> addDances, List<string> remDances, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
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
                    ViewBag.BackAction = "Index";
                    return View("Details", edit);
                }
                {
                    // TODO: Check to see if we lose SongFilter through this path (and how to correct if we do)
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
        public ActionResult Delete(int id = 0, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
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
        public ActionResult DeleteConfirmed(int id, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
            Song song = _db.Songs.Find(id);
            string userName = User.Identity.Name;
            ApplicationUser user = _db.Users.FirstOrDefault(u => u.UserName == userName);
            _db.DeleteSong(user,song);
            return RedirectToAction("Index");
        }


        //
        // Merge: /Song/MergeCandidates
        public ActionResult MergeCandidates(int? page, int? level, bool? autoCommit, string filter = null)
        {
            SongFilter songFilter = ParseFilter(filter, "MergeCandidates");
            IList<Song> songs = null;

            if (autoCommit == true)
            {
                songs = _db.FindMergeCandidates(10000, level ?? 1);
            }
            else
            {
                songs = _db.FindMergeCandidates(500, level ?? 1);
            }

            if (page.HasValue)
            {
                songFilter.Page = page;
            }

            if (level.HasValue)
            {
                songFilter.Level = level;
            }

            int pageSize = 25;
            int pageNumber = songFilter.Page ?? 1;

            if (autoCommit.HasValue && autoCommit.Value == true)
            {
                songs = AutoMerge(songs,(int)songFilter.Level);
            }

            ViewBag.SongFilter = songFilter;
            return View("Index", songs.ToPagedList(pageNumber, pageSize));
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult BulkEdit(int[] selectedSongs, string action, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
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
        public ActionResult MergeResults(string SongIds, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
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
            string def = Request.Form[Song.AlbumList];
            if (!string.IsNullOrWhiteSpace(def))
            {
                int.TryParse(def, out defIdx);
            } 

            if (defIdx >= 0 && albumsIn.Count > defIdx)
            {
                AlbumDetails t = albumsIn[defIdx];
                t.Index = 0;
                albumsOut.Add(t);
            }

            int idx = 1;
            for (int i = 0; i < albumsIn.Count; i++)
            {
                if (i != defIdx)
                {
                    string name = Song.AlbumList + "_" + i.ToString();

                    if (Request.Form.AllKeys.Contains(name))
                    {
                        AlbumDetails t = albumsIn[i];
                        t.Index = idx;
                        albumsOut.Add(t);
                        idx += 1;
                    }
                }
            }

            Song song = _db.MergeSongs(user, songList, 
                ResolveStringField(Song.TitleField, songList, Request.Form),
                ResolveStringField(Song.ArtistField, songList, Request.Form),
                ResolveStringField(Song.GenreField, songList, Request.Form),
                ResolveDecimalField(Song.TempoField, songList, Request.Form),
                ResolveIntField(Song.LengthField, songList, Request.Form),
                albumsOut);

            ViewBag.BackAction = "MergeCandidates";

            return View("Details",_db.FindSongDetails(song.SongId));
        }

        // GET: /Song/XboxSearch/5?search=name
        [Authorize(Roles = "canEdit")]
        public ActionResult XboxSearch(int id = 0, string search = null, string filter=null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
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
        public ActionResult ChooseXbox(int songId, string name, string album, string artist, string trackId, string alternateId, string duration, string genre, int? trackNum, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);

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
                ad = new AlbumDetails { Name = album, Track=trackNum, Index=song.GetNextAlbumIndex() };
                song.Albums.Insert(0,ad);
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
        private IList<Song> AutoMerge(IList<Song> songs, int level)
        {
            // Get the logged in user
            string userName = User.Identity.Name;
            ApplicationUser user = _db.Users.FirstOrDefault(u => u.UserName == userName);

            List<Song> ret = new List<Song>();
            List<Song> cluster = null;

            try
            {
                _db.Configuration.AutoDetectChangesEnabled = false;

                foreach (Song song in songs)
                {
                    if (cluster == null)
                    {
                        cluster = new List<Song>();
                        cluster.Add(song);
                    }
                    else if ((level == 0 && song.Equivalent(cluster[0])) || (level == 1 && song.WeakEquivalent(cluster[0])))
                    {
                        cluster.Add(song);
                    }
                    else
                    {
                        if (cluster.Count > 1)
                        {
                            Song s = AutoMerge(cluster, user);
                            ret.Add(s);
                        }
                        else if (cluster.Count == 1)
                        {
                            Trace.WriteLine(string.Format("Bad Merge: {0}", cluster[0].Signature));
                        }

                        cluster = new List<Song>();
                        cluster.Add(song);
                    }
                }
            }
            finally
            {
                _db.Configuration.AutoDetectChangesEnabled = false;
                _db.SaveChanges();
            }

            return ret;
        }

        private Song AutoMerge(List<Song> songs, ApplicationUser user)
        {
            Song song = _db.MergeSongs(user, songs,
                ResolveStringField(Song.TitleField, songs),
                ResolveStringField(Song.ArtistField, songs),
                ResolveStringField(Song.GenreField, songs),
                ResolveDecimalField(Song.TempoField, songs),
                ResolveIntField(Song.LengthField, songs),
                SongDetails.BuildAlbumInfo(songs)
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
                if (s_admAuth == null)
                {
                    string clientId = "Music4Dance";
                    string clientSecret = "3kJ506OgMCD+nmuzUCRrXt/gnJlV07qQuxsEZBMZCqw=";

                    s_admAuth = new AdmAuthentication(clientId, clientSecret);
                    
                }

                return "Bearer " + s_admAuth.GetAccessToken().access_token;
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