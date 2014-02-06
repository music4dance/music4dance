using DanceLibrary;
using music4dance.Utilities;
using music4dance.ViewModels;
using PagedList;
using SongDatabase.Models;
using System;
using System.Collections.Generic;
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
            var songs = from s in _db.Songs select s;

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
            Song song = _db.Songs.Find(id);
            if (song == null)
            {
                return HttpNotFound();
            }
            ViewBag.BackAction = "Index";
            
            SongDetails sd = new SongDetails(song);

            return View(sd);
        }

        //
        // GET: /Song/Create
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create()
        {
            return View();
        }

        //
        // POST: /Song/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create(Song song)
        {
            if (ModelState.IsValid)
            {
                _db.Songs.Add(song);
                _db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(song);
        }

        //
        // GET: /Song/Edit/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(int id = 0)
        {
            Song song = _db.Songs.Find(id);
            if (song == null)
            {
                return HttpNotFound();
            }


            SongDetails sd = new SongDetails(song);

            ViewBag.DanceList = GetDances(sd.DanceRatings);

            return View(sd);
        }

        private MultiSelectList GetDances(IList<DanceRating> ratings)
        {
            //var dances =
            //    from dance in _db.Dances
            //    select new { ID = dance.Id, Name = dance.Name };

            //var dl = _db.Dances.ToList();

            List<SimpleDance> Dances = new List<SimpleDance>(_db.Dances.Count());

            foreach (Dance d in _db.Dances)
            {
                Dances.Add(new SimpleDance() { ID = d.Id, Name = d.Info.Name });
            }

            List<string> selected = new List<string>(ratings.Count());
            foreach (DanceRating dr in ratings)
            {
                selected.Add(dr.DanceId);
            }

            return new MultiSelectList(Dances, "ID", "Name", selected.ToArray());
        }

        //
        // POST: /Song/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(SongDetails song, List<string> dances)
        {
            if (ModelState.IsValid)
            {
                //This seemed promising, but it appears that these property lists aren't updated when
                // we need them

                //System.Data.Entity.Infrastructure.DbPropertyValues oldValues = db.Entry(song).OriginalValues;
                //foreach (string name in oldValues.PropertyNames)
                //{
                //    Debug.WriteLine(string.Format("{0}={1}",name,oldValues[name]));
                //}

                // TODO: Get the user that is logged in stuffed into userProfile
#if DEBUG
                _db.Dump();
#endif

                string userName = User.Identity.Name;
                UserProfile user = _db.UserProfiles.FirstOrDefault(u => u.UserName == userName);

#if DEBUG
                _db.Dump();
#endif
                //TODO: How do we change the dance ratings?  Do we enforce at least one dance???
                _db.EditSong(user, song);

#if DEBUG
                _db.Dump();
#endif
                return RedirectToAction("Index");
            }
            else
            {
                ViewBag.DanceList = GetDances(song.DanceRatings);

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
            UserProfile user = _db.UserProfiles.FirstOrDefault(u => u.UserName == userName);
            _db.DeleteSong(user,song);
            return RedirectToAction("Index");
        }


        //
        // Merge: /Song/MergeCandidates
        public ActionResult MergeCandidates(int? page, int? level, bool? autoCommit)
        {
            IList<Song> songs = _db.FindMergeCandidates(500,level ?? 1);

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

        private IList<Song> AutoMerge(IList<Song> songs)
        {
            // Get the logged in user
            string userName = User.Identity.Name;
            UserProfile user = _db.UserProfiles.FirstOrDefault(u => u.UserName == userName);

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
                    Song s = AutoMerge(cluster,user);
                    ret.Add(s);

                    cluster = new List<Song>();
                    cluster.Add(song);
                }
            }

            return ret;
        }

        private Song AutoMerge(List<Song> songs, UserProfile user)
        {

            Song song = _db.MergeSongs(user, songs,
                ResolveStringField(DanceMusicContext.TitleField, songs),
                ResolveStringField(DanceMusicContext.ArtistField, songs),
                ResolveStringField(DanceMusicContext.AlbumField, songs),                
                ResolveStringField(DanceMusicContext.GenreField, songs),
                ResolveDecimalField(DanceMusicContext.TempoField, songs),
                ResolveIntField(DanceMusicContext.LengthField, songs)
                );

            // ResolveStringField(DanceMusicContext.PublisherField, songs),
            // ResolveIntField(DanceMusicContext.TrackField, songs),
            // ResolveMultiStringField(DanceMusicContext.PurchaseField, songs)

            return song;            
        }

        //
        // Merge: /Song/Merge
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Merge()
        {
            List<int> songIds = new List<int>();

            if (Request.Form != null)
            {
                foreach (string key in Request.Form.AllKeys)
                {
                    string[] rgs = key.Split(new char[] { '-' });
                    int id = 0;

                    if (rgs.Length == 2 && string.Equals(rgs[0], "Merge", StringComparison.Ordinal) && int.TryParse(rgs[1], out id))
                    {
                        string value = Request.Form[key];
                        if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
                        {
                            songIds.Add(id);
                        }
                    }
                }
            }

            var songs = from s in _db.Songs
                        where songIds.Contains(s.SongId)
                        select s;

            SongMerge sm = new SongMerge(songs.ToList());

            return View(sm);
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
            UserProfile user = _db.UserProfiles.FirstOrDefault(u => u.UserName == userName);

            string album = ResolveStringField(DanceMusicContext.AlbumField, songList, Request.Form);

            string albumDefault = album;

            // TODO:SONGDETAIL - Fix the merging to handle properties....
            for (int i = 0; i < songList.Count; i++)
            {
                string name = DanceMusicContext.AlbumField + "_" + i.ToString();
                
                if (Request.Form.AllKeys.Contains(name))
                {
                    string albumNew = songList[i].Album;
                    if (!string.Equals(albumDefault,albumNew))
                    {
                        album += "|" + albumNew;
                    }
                }
            }

            Song song = _db.MergeSongs(user, songList, 
                ResolveStringField(DanceMusicContext.TitleField, songList, Request.Form),
                ResolveStringField(DanceMusicContext.ArtistField, songList, Request.Form),
                album,
                ResolveStringField(DanceMusicContext.GenreField, songList, Request.Form),
                ResolveDecimalField(DanceMusicContext.TempoField, songList, Request.Form),
                ResolveIntField(DanceMusicContext.LengthField, songList, Request.Form));

            ViewBag.BackAction = "MergeCandidates";

            return View("Details",song);
        }

        private string ResolveStringField(string fieldName, IList<Song> songs, System.Collections.Specialized.NameValueCollection form=null)
        {
            object obj = ResolveMergeField(fieldName,songs,form);

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
                for (int i = 0; i < songs.Count; i++ )
                {
                    Song song = songs[i];

                    if (song.GetType().GetProperty(fieldName).GetValue(song) != null)
                    {
                        idx = i;
                    }
                }
            }

            ret = songs[idx].GetType().GetProperty(fieldName).GetValue(songs[idx]);
            return ret;
        }

        // GET: /Song/XboxSearch/5?search=name
        [Authorize(Roles = "canEdit")]
        public ActionResult XboxSearch(int id = 0, string search = null)
        {
            Song song = _db.Songs.Find(id);
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
        public ActionResult ChooseXbox(int songId, string name, string album, string artist, string trackId, string alternateId, string duration, int? trackNum)
        {
            // TODO:SONGDETAIL - Add back in track/publisher/purchase info on per-album basis

            Song song = _db.Songs.Find(songId);
            if (song == null)
            {
                return HttpNotFound();
            }

            // This is a very transitory object to hold the old values for a semi-automated edit
            Song alt = new Song();

            if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, song.Title))
            {
                alt.Title = song.Title;
                song.Title = name;
            }

            if (!string.IsNullOrWhiteSpace(album) && !string.Equals(album, song.Album))
            {
                alt.Album = song.Album;
                song.Album = album + "|" + alt.Album;
            }

            if (!string.IsNullOrWhiteSpace(artist) && !string.Equals(artist, song.Artist))
            {
                alt.Artist = song.Artist;
                song.Artist = name;
            }


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
            // TODO: Get Genre in here

            //if (!string.IsNullOrWhiteSpace(trackId) )
            //{
            //    // TODO: Hande the case where there are already Xbox/Amg ids

            //    string newId = song.Purchase;
            //    if (string.IsNullOrWhiteSpace(newId)) 
            //        newId = string.Empty;
            //    else if (newId[newId.Length-1] != ';')
            //        newId += ';';

            //    newId = newId + "ZS=" + trackId;

            //    if (!string.IsNullOrWhiteSpace(alternateId))
            //    {
            //        newId += ";MS=" + alternateId;
            //    }

            //    alt.Purchase = song.Purchase;
            //    song.Purchase = newId;
            //}

            ViewBag.OldSong = alt;

            return View("Edit", song);
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