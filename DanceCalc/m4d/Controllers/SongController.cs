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
using m4d;

namespace m4d.Controllers
{
    public class SimpleDance
    {
        public string ID { get; set; }
        public string Name { get; set; }
    };

    public class SongController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return MusicTheme;
            }
        }

        private DanceMusicContext _db = new DanceMusicContext();

        #region Commands

        [AllowAnonymous]
        public ActionResult Sample()
        {
            return View();
        }

        [AllowAnonymous]
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

            songFilter.Purchase = null;
            songFilter.TempoMin = null;
            songFilter.TempoMax = null;

            return DoIndex(songFilter);
        }

        [AllowAnonymous]
        public ActionResult AdvancedSearch(string searchString, string dances, ICollection<string> services, decimal? tempoMin, decimal? tempoMax, string filter)
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

            string purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }
            songFilter.Purchase = purchase;

            songFilter.TempoMin = tempoMin;
            songFilter.TempoMax = tempoMax;

            ViewBag.AdvancedSearch = true;

            return DoIndex(songFilter);
        }

        [AllowAnonymous]
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
                    case "Tempo":
                        songFilter.SortOrder = songFilter.SortOrder == "Tempo" ? "Tempo_desc" : "Tempo";
                        break;
                    case "Dances":
                        songFilter.SortOrder = "Dances";
                        break;
                }
            }

            return DoIndex(songFilter);
        }

        [AllowAnonymous]
        public ActionResult FilterUser(string user, string filter)
        {
            SongFilter songFilter = ParseFilter(filter);
            if (string.IsNullOrWhiteSpace(user))
            {
                songFilter.User = null;
            }
            else
            {
                songFilter.User = user;
            }
            return DoIndex(songFilter);
        }

        [AllowAnonymous]
        public ActionResult FilterService(ICollection<string> services, string filter)
        {
            SongFilter songFilter = ParseFilter(filter);

            string purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }
            songFilter.Purchase = purchase;
            return DoIndex(songFilter);
        }

        [AllowAnonymous]
        public ActionResult FilterTempo(decimal? tempoMin, decimal? tempoMax, string filter)
        {
            SongFilter songFilter = ParseFilter(filter);
            songFilter.TempoMin = tempoMin;
            songFilter.TempoMax = tempoMax;

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
        
        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public ActionResult Details(Guid? id = null, string filter = null)
        {
            SongDetails song = _db.FindSongDetails(id ?? Guid.Empty);
            if (song == null)
            {
                return HttpNotFound();
            }

            ViewBag.SongFilter = ParseFilter(filter);
            ViewBag.DanceMap = SongCounts.GetDanceMap(_db);
            return View(song);
        }

        //
        // GET: /Song/CreateI
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create(string filter = null)
        {
            ViewBag.ShowMPM = true;
            ViewBag.ShowBPM = true;
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
        public ActionResult Create(SongDetails song, List<string> addDances, string editTags, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
            if (ModelState.IsValid)
            {
                ApplicationUser user = _db.FindUser(User.Identity.Name);
                song.UpdateDanceRatings(addDances, Song.DanceRatingCreate);
                // TOOD: Think about format of editTags...
                song.AddTags(editTags);

                Song newSong = _db.CreateSong(user, song);

                // TODO: Think about if the round-trip is necessary
                if (newSong != null)
                {
                    _db.SaveChanges();
                    song = new SongDetails(newSong);
                }

                ViewBag.DanceMap = SongCounts.GetDanceMap(_db);
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
        public ActionResult Edit(Guid? id = null, string filter = null)
        {
            SongDetails song = _db.FindSongDetails(id??Guid.Empty);
            if (song == null)
            {
                return HttpNotFound();
            }


            SetupEditViewBag(song);

            ViewBag.SongFilter = ParseFilter(filter);
            return View(song);
        }

        private void SetupEditViewBag(SongDetails song)
        {
            ViewBag.ShowMPM = true;
            ViewBag.ShowBPM = true;

            IList<DanceRating> ratingsList = song.RatingsList;
            ViewBag.DanceListRemove = GetDances(ratingsList);
            ViewBag.DanceListAdd = GetDances();

            if (ratingsList.Any(r => r.Dance.Name.Contains("Waltz")))
            {
                ViewBag.paramNumerator = 3;
            }

        }
        //
        // POST: /Song/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        //public ActionResult Edit([ModelBinder(typeof(m4d.Utilities.SongBinder))]SongDetails song, List<string> addDances, List<string> remDances, string filter = null)
        public ActionResult Edit(SongDetails song, List<string> addDances, List<string> remDances, string editTags, string filter = null)
        {
            ViewBag.SongFilter = ParseFilter(filter);
            if (ModelState.IsValid)
            {
//#if DEBUG
//                _db.Dump();
//#endif

                ApplicationUser user = _db.FindUser(User.Identity.Name);

                // EditSong makes a distinction between null and an empty list
                if (addDances == null)
                {
                    addDances = new List<string>();
                }

                if (remDances == null)
                {
                    remDances = new List<string>();
                }

//#if DEBUG
//                _db.Dump();
//#endif
                SongDetails edit = _db.EditSong(user, song, addDances, remDances, editTags);

//#if DEBUG
//                _db.Dump();
//#endif

                if (edit != null)
                {
                    _db.SaveChanges();
                    ViewBag.BackAction = "Index";
                    ViewBag.DanceMap = SongCounts.GetDanceMap(_db);
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

                SetupEditViewBag(songT);

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
        public ActionResult Delete(Guid id, string filter = null)
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
        public ActionResult DeleteConfirmed(Guid id, string filter = null)
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
        [Authorize(Roles = "canEdit")]
        public ActionResult MergeCandidates(int? page, int? level, bool? autoCommit, string filter = null)
        {
            SongFilter songFilter = ParseFilter(filter, "MergeCandidates");
            IList<Song> songs = null;

            BuildDanceList(songFilter);

            if (page.HasValue)
            {
                songFilter.Page = page;
            }

            if (level.HasValue)
            {
                songFilter.Level = level;
            }

            if (autoCommit == true)
            {
                songs = _db.FindMergeCandidates(10000, songFilter.Level ?? 1);
            }
            else
            {
                songs = _db.FindMergeCandidates(500, songFilter.Level ?? 1);
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
        public ActionResult BulkEdit(Guid[] selectedSongs, string action, string filter = null)
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
            List<Guid> ids = SongIds.Split(',').Select(s=>Guid.Parse(s)).ToList();

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
            string def = Request.Form[Song.AlbumListField];
            if (!string.IsNullOrWhiteSpace(def))
            {
                int.TryParse(def, out defIdx);
            }

            int idx = 0;
            if (defIdx >= 0 && albumsIn.Count > defIdx)
            {
                AlbumDetails t = albumsIn[defIdx];
                t.Index = 0;
                albumsOut.Add(t);
                idx = 1;
            }

            for (int i = 0; i < albumsIn.Count; i++)
            {
                if (i != defIdx)
                {
                    string name = Song.AlbumListField + "_" + i.ToString();

                    if (defIdx == -1 || Request.Form.AllKeys.Contains(name))
                    {
                        AlbumDetails t = albumsIn[i];
                        t.Index = idx;
                        albumsOut.Add(t);
                        idx += 1;
                    }
                }
            }

            //TODONEXT: Merge Tags
            string tags = Request.Form["Tags"];
            Song song = _db.MergeSongs(user, songList, 
                ResolveStringField(Song.TitleField, songList, Request.Form),
                ResolveStringField(Song.ArtistField, songList, Request.Form),
                ResolveDecimalField(Song.TempoField, songList, Request.Form),
                ResolveIntField(Song.LengthField, songList, Request.Form),
                tags,
                albumsOut);

            ViewBag.BackAction = "MergeCandidates";
            ViewBag.DanceMap = SongCounts.GetDanceMap(_db);

            return View("Details",_db.FindSongDetails(song.SongId));
        }

        /// <summary>
        /// Batch up searching a music service
        /// </summary>
        /// <param name="type">Music service type (currently X=XBox,A=Amazon,I=ITunes)</param>
        /// <param name="options">May be more complex in future - currently Rn where n is retyr level</param>
        /// <param name="filter">Standard filter for song list</param>
        /// <param name="count">Number of songs to try, 1 is special cased as a user verified single entry</param>
        /// <returns></returns>
        [Authorize(Roles = "canEdit")]
        public ActionResult BatchMusicService(string type= "X", string options = null, string filter=null, int count = 1)
        {
            MusicService service = MusicService.GetService(type);
            if (service == null)
            {
                throw new ArgumentOutOfRangeException("type");
            }

            ViewBag.SearchType = type;
            ViewBag.SongFilter = filter;
            ViewBag.Options = options;
            ViewBag.Error = false;

            ActionResult ar = null;
            int tried = 0;
            int skipped = 0;

            int retryLevel = -1;

            // May do more options in future
            if (!string.IsNullOrWhiteSpace(options) && options.Length > 1 && options[0] == 'R')
            {
                int.TryParse(options.Substring(1), out retryLevel);
            }

            SongFilter songFilter = ParseFilter(filter);
            songFilter.Purchase = "!" + type;

            IQueryable<Song> songs = BuildSongList(songFilter);

            ApplicationUser user = _db.FindUser(User.Identity.Name);

            List<Song> failed = new List<Song>();
            List<SongDetails> succeeded = new List<SongDetails>();

            foreach (Song song in songs)
            {
                // First check to see if we've already failed a search and at what level
                //  failLeve is the LOWEST failure code or -1 if none

                int failLevel = -1;
                SongProperty fail = song.SongProperties.OrderBy(p => p.Value).FirstOrDefault(p => p.Name == Song.FailedLookup && p.Value.StartsWith(type));
                if (fail != null && fail.Value != null && fail.Value.Length > 2)
                {
                    int.TryParse(fail.Value.Substring(2), out failLevel);
                }

                SongDetails sd = new SongDetails(song);

                if (failLevel == retryLevel || count==1)
                {
                    int failcode = -1;

                    IList<ServiceTrack> tracks = null;
                    // First try the full title/artist
                    tracks = FindMusicServiceSong(sd, service);

                    if ((tracks == null || tracks.Count == 0) && !string.Equals(DefaultServiceSearch(sd,true),DefaultServiceSearch(sd,false)))
                    {
                        // Now try cleaned up title/artist (remove punctuation and stuff in parens/brackets)
                        ViewBag.Status = null;
                        ViewBag.Error = false;
                        tracks = FindMusicServiceSong(sd, service, true);
                    }

                    if (tracks != null && tracks.Count > 0)
                    {
                        ServiceTrack foundTrack = null;

                        // First filter out anything that's not a title-artist match (weak)

                        List<ServiceTrack> tracksT = new List<ServiceTrack>(tracks.Count);
                        foreach (ServiceTrack track in tracks)
                        {
                            if (sd.TitleArtistMatch(track.Name, track.Artist))
                            {
                                tracksT.Add(track);
                            }
                        }
                        tracks = tracksT;

                        // Then check for exact album match
                        foreach (ServiceTrack track in tracks)
                        {
                            if (sd.FindAlbum(track.Album) != null)
                            {
                                foundTrack = track;
                                break;
                            }
                        }

                        // If not exact album match and the song has a length, choose an album with the same tempo (delta a few seconds)
                        if (foundTrack == null && sd.Length.HasValue)
                        {
                            foreach (ServiceTrack track in tracks)
                            {
                                int delta = Math.Abs(track.Duration.Value-sd.Length.Value);
                                if ((track.Duration.HasValue && delta < 6) &&
                                    (foundTrack == null || Math.Abs(foundTrack.Duration.Value - sd.Length.Value) > delta))
                                {
                                    foundTrack = track;
                                }
                            }
                        }

                        // If no album name or length match, choose the 'dominant' version of hte title/artist match by clustering lengths
                        //  Note that this degenerates to chosing a single album if that is what is available
                        if (foundTrack == null && !sd.HasRealAblums)
                        {
                            // TODO:  I feel like this may be a redundant check on TitleArtist match
                            tracks = sd.TitleArtistFilter(tracks);
                            foundTrack = SongDetails.FindDominantTrack(tracks);
                            // This may be temporary - we hit this code to try to do cluster matching
                            //  so put in a higher fail level
                            if (failLevel == 1 && foundTrack == null)
                            {
                                failcode = 2;
                            }
                        }

                        
                        if (foundTrack != null)
                        {
                            // Single song lookup and we've found the song
                            if (count == 1)
                            {
                                ar = ChooseMusicService(sd.SongId, service.CID.ToString(), foundTrack.Name, foundTrack.Album, foundTrack.Artist, foundTrack.TrackId, foundTrack.CollectionId, foundTrack.AltId, foundTrack.Duration.ToString(), foundTrack.Genre, foundTrack.TrackNumber, filter);
                            }
                            else
                            {
                                UpdateMusicService(sd, service, foundTrack.Name, foundTrack.Album, foundTrack.Artist, foundTrack.TrackId, foundTrack.CollectionId, foundTrack.AltId, foundTrack.Duration.ToString(), foundTrack.Genre, foundTrack.TrackNumber);
                                succeeded.Add(_db.EditSong(user, sd, null, null, null));
                                tried += 1;
                            }
                        }
                        else
                        {
                            if (failcode == -1)
                            {
                                // We found no tracks
                                if (tracks.Count == 0)
                                {
                                    failcode = 0;
                                }
                                // Multi-song lookup and we found too many tracks
                                else if (count > 1)
                                {
                                    failcode = 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        failcode = 0;
                    }

                    //  Add all failures to the list and increment tried
                    if (failcode >= 0)
                    {
                        // Only add in a new failed code to the DB if the code
                        // is higher than the previous code
                        if (failcode > failLevel)
                        {
                            SongProperty sp = _db.SongProperties.Create();
                            sp.Name = Song.FailedLookup;
                            sp.Value = type + ":" + failcode.ToString();
                            song.SongProperties.Add(sp);
                        }
                        failed.Add(song);
                        tried += 1;
                    }
                }
                else
                {
                    skipped += 1;
                }

                if (count > 1 && tried > count)
                    break;
            }

            _db.SaveChanges();

            if (ar == null)
            {
                ViewBag.Completed = tried <= count;
                ViewBag.Failed = failed;
                ViewBag.Succeeded = succeeded;
                ViewBag.Skipped = skipped;
                return View();
            }
            else
            {
                return ar;
            }
        }

        // GET: /Song/MusicServiceSearch/5?search=name
        [Authorize(Roles = "canEdit")]
        public ActionResult MusicServiceSearch(Guid? id = null, string type="X", string title = null, string artist = null, string filter=null)
        {
            SongDetails song = _db.FindSongDetails(id??Guid.Empty);
            if (song == null)
            {
                return HttpNotFound();
            }

            MusicService service = MusicService.GetService(type);
            if (service == null)
            {
                throw new ArgumentOutOfRangeException("type");
            }

            ServiceSearchResults view = new ServiceSearchResults { ServiceType = type, Song = song };

            ViewBag.DanceMap = SongCounts.GetDanceMap(_db);
            ViewBag.SongFilter = ParseFilter(filter);
            ViewBag.SongTitle = title;
            ViewBag.SongArtist = artist;
            ViewBag.Type = type;
            ViewBag.Error = false;

            view.Tracks = FindMusicServiceSong(song, service, false, title, artist);

            return View(view);
        }

        // ChooseMusicService: /Song/ChooseMusicService
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult ChooseMusicService(Guid songId, string type, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, string genre, int? trackNum, string filter = null)
        {
            MusicService service = MusicService.GetService(type);
            if (service == null)
            {
                throw new ArgumentOutOfRangeException("type");
            }

            ViewBag.SongFilter = ParseFilter(filter);
            SongDetails song = _db.FindSongDetails(songId);
            if (song == null)
            {
                return HttpNotFound();
            }

            SongDetails alt = UpdateMusicService(song, service, name, album, artist, trackId, collectionId, alternateId, duration, genre, trackNum);

            ViewBag.OldSong = alt;

            return View("Edit", song);
        }

        #endregion

        #region General Utilities
        private SongFilter ParseFilter(string f, string action = "Index")
        {
            if (string.IsNullOrWhiteSpace(f))
            {
                return new SongFilter { Action = action };
            }
            else
            {
                return new SongFilter(f);
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
        private ActionResult Delete(IQueryable<Song> songs)
        {
            ApplicationUser user = _db.FindUser(User.Identity.Name);

            foreach (Song song in songs)
            {
                _db.DeleteSong(user, song);
            }

            return RedirectToAction("Index");
        }

        #endregion

        #region Index
        private ActionResult DoIndex(SongFilter filter)
        {
            Trace.WriteLine(string.Format("Entering Song.Index: dances='{0}',sortOrder='{1}',searchString='{2}'", filter.Dances, filter.SortOrder, filter.SearchString));

            var songs = BuildSongList(filter);

            int pageSize = 25;

            Trace.WriteLine("Exiting Song.Index");

            return View("Index", songs.ToPagedList(filter.Page ?? 1, pageSize));
        }

        private void BuildDanceList(SongFilter filter)
        {
            //IList<SongCounts> songCounts = SongCounts.GetFlatSongCounts(_db);
            //var scq = songCounts.Select(s => new { s.DanceId, s.DanceName });

            //ViewBag.Dances = new SelectList(scq.AsEnumerable(), "DanceId", "DanceName", filter.Dances);

            ViewBag.SelectedDances =  Dances.Instance.FromIds(filter.Dances);
            ViewBag.Dances = SongCounts.GetSongCounts(_db);
            ViewBag.DanceMap = SongCounts.GetDanceMap(_db);
        }

        private IQueryable<Song> BuildSongList(SongFilter filter)
        {
            // Set up the viewbag
            ViewBag.SongFilter = filter;

            ViewBag.TitleClass = string.Empty;
            ViewBag.ArtistClass = string.Empty;
            ViewBag.AlbumClass = string.Empty;

            BuildDanceList(filter);

            // Now setup the view
            // Start with all of the songs in the database
            var songs = from s in _db.Songs where s.TitleHash != 0 select s;

#if TRACE
            bool traceVerbose = TraceLevels.General.TraceVerbose;
            int count = 0;
            int lastCount = 0;
            if (traceVerbose)
            {
                count = lastCount = songs.Count();
                Trace.WriteLine(string.Format("Total Songs = {0}", count));
            }
#endif

            // Now if the current user is anonymous, filter out anything that we
            //  don't have purchase info for
            if (!HttpContext.User.IsInRole(DanceMusicContext.EditRole))
            {
                songs = songs.Where(s => s.Purchase != null);
            }

            // Filter by user first since we have a nice key to pull from
            // TODO: This ends up going down a completely different LINQ path
            //  that is requiring some special casing further along, need
            //  to dig into how to manage that better...
            bool userFilter = false;
            if (!string.IsNullOrWhiteSpace(filter.User))
            {
                ApplicationUser user = _db.FindUser(filter.User);
                if (user != null)
                {
                    songs = from m in user.Modified.AsQueryable() where m.Song.TitleHash != 0 select m.Song;
                    userFilter = true;
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs per user = {0}", songs.Count()));
                lastCount = count;
            }
#endif
            // Now limit it down to the ones that are marked as a particular dance or dances
            string[] danceList = null;
            if (!string.IsNullOrWhiteSpace(filter.Dances) && !string.Equals(filter.Dances, "ALL"))
            {
                danceList = Dances.Instance.ExpandDanceList(filter.Dances);

                songs = songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by dance = {0}", songs.Count()));
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
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by tempo = {0}", songs.Count()));
                lastCount = count;
            }
#endif

            // Now limit it by anything that has the serach string in the title, album or artist
            if (!String.IsNullOrEmpty(filter.SearchString))
            {
                if (userFilter)
                {
                    string str = filter.SearchString.ToUpper();
                    songs = songs.Where(
                        s => (s.Title != null && s.Title.ToUpper().Contains(str)) ||
                        (s.Album != null && s.Album.Contains(str)) ||
                        (s.Artist != null && s.Artist.Contains(str)));
                }
                else
                {
                    songs = songs.Where(
                        s => s.Title.Contains(filter.SearchString) ||
                        s.Album.Contains(filter.SearchString) ||
                        s.Artist.Contains(filter.SearchString));
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by search = {0}", songs.Count()));
                lastCount = count;
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
                bool not = false;
                string purch = filter.Purchase;
                if (purch.StartsWith("!"))
                {
                    not = true;
                    purch = purch.Substring(1);
                }

                char[] services = purch.ToCharArray();
                if (services.Length == 1)
                {
                    string c = services[0].ToString();
                    if (not)
                    {
                        songs = songs.Where(s => s.Purchase == null || !s.Purchase.Contains(c));
                    }
                    else
                    {
                        songs = songs.Where(s => s.Purchase != null && s.Purchase.Contains(c));
                    }
                }
                else if (services.Length == 2)
                {
                    string c0 = services[0].ToString();
                    string c1 = services[1].ToString();

                    if (not)
                    {
                        songs = songs.Where(s => s.Purchase == null || (!s.Purchase.Contains(c0) && !s.Purchase.Contains(c1)));
                    }
                    else
                    {
                        songs = songs.Where(s => s.Purchase != null && (s.Purchase.Contains(c0) || s.Purchase.Contains(c1)));
                    }
                    
                }
                else // Better == 3
                {
                    if (not)
                    {
                        songs = songs.Where(s => s.Purchase == null);
                    }
                    else
                    {
                        songs = songs.Where(s => s.Purchase != null);
                    }
                }
            }

#if TRACE
            if (traceVerbose)
            {
                count = songs.Count();
                Trace.WriteLineIf(count != lastCount, string.Format("Songs by purchase = {0}", songs.Count()));
                lastCount = count;
            }
#endif

            // Now sort the list
            string sortAsc = "<span class='glyphicon glyphicon-sort-by-alphabet'></span>";
            string sortDsc = "<span class='glyphicon glyphicon-sort-by-alphabet-alt'></span>";
            string sortNAsc = "<span class='glyphicon glyphicon-sort-by-order'></span>";
            string sortNDsc = "<span class='glyphicon glyphicon-sort-by-order-alt'></span>";

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
                case "Tempo":
                    songs = songs.OrderBy(s => s.Tempo);
                    ViewBag.TempoSort = sortNAsc;
                    break;
                case "Tempo_desc":
                    songs = songs.OrderByDescending(s => s.Tempo);
                    ViewBag.TempoSort = sortNDsc;
                    break;
                case "Dances":
                    // TODO: Better icon for dance order
                    // TODO: Get this working for multi-dance selection
                    {
                        string did = TrySingleId(danceList);
                        if (did != null)
                        {
                            //DanceRating drE = new DanceRating() { Weight = 0 };
                            songs = songs.OrderByDescending(s => s.DanceRatings.FirstOrDefault(dr => dr.DanceId.StartsWith(did)).Weight);
                            ViewBag.TempoSort = sortNDsc;
                        }
                    }
                    break;
            }

            return songs;
        }

        // TODO: This is extremely dependent on the form of the danceIds, just
        //  a temporary kludge until we get multi-select working
        private static string TrySingleId(string[] danceList)
        {
            string ret = null;
            if (danceList != null && danceList.Length > 0)
            {
                ret = danceList[0].Substring(0,3);
                for (int i = 1; i < danceList.Length; i++)
                {
                    if (!string.Equals(ret,danceList[i].Substring(0,3)))
                    {
                        ret = null;
                        break;
                    }
                }
            }

            return ret;
        }
        private void DumpSongs(IQueryable<Song> songs, string purchase)
        {
            Debug.WriteLine(string.Format("------------Purchase == {0} ------------", purchase));
            foreach (Song t in songs)
            {
                Debug.WriteLine(string.Format("{0}: {1}", t.Title, t.Purchase));
            }
        }

        #endregion

        #region MusicService

        private string DefaultServiceSearch(SongDetails song, bool clean)
        {
            if (clean)
                return song.CleanTitle + " " + song.CleanArtist;
            else
                return song.Title + " " + song.Artist;
        }

        private IList<ServiceTrack> FindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null)
        {
            IList<ServiceTrack> tracks = null;
            try
            {
                FixupTitleArtist(song, clean, ref title, ref artist);
                tracks = _db.FindMusicServiceSong(song, service, clean, title, artist);

                if (tracks == null || tracks.Count == 0)
                {
                    ViewBag.Error = true;
                    ViewBag.Status = "No Matches Found";
                }                
            }
            catch (WebException we)
            {
                ViewBag.Error = true;
                ViewBag.Status = we.Message;
            }

            return tracks;
        }

        private void FixupTitleArtist(SongDetails song, bool clean, ref string title, ref string artist)
        {
            if (song != null && artist == null && title == null)
            {
                artist = clean?song.CleanArtist:song.Artist;
                title =  clean?song.CleanTitle:song.Title;
            }

            ViewBag.SongArtist = artist;
            ViewBag.SongTitle = title;
        }

        SongDetails UpdateMusicService(SongDetails song, MusicService service, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, string genre, int? trackNum)
        {
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
                song.Artist = artist;
            }

            AlbumDetails ad = song.FindAlbum(album);
            if (ad != null)
            {
                // If there is a match set up the new info next to the album
                int aidxM = song.Albums.IndexOf(ad);

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
                ad = new AlbumDetails { Name = album, Track = trackNum, Index = song.GetNextAlbumIndex() };
                song.Albums.Insert(0, ad);
            }
            UpdateMusicServicePurchase(ad, service, PurchaseType.Song, trackId, alternateId);
            if (collectionId != null)
            {
                UpdateMusicServicePurchase(ad, service, PurchaseType.Album, collectionId);
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

            if (!string.IsNullOrWhiteSpace(genre))
            {
                // This will assign a genre type to whatever this tag is
                TagType tt = _db.FindOrCreateTagType(genre,"Genre");
                song.AddTags(tt.Value);
                ViewBag.TagValues = tt.Value;
            }

            return alt;
        }
        private void UpdateMusicServicePurchase(AlbumDetails ad, MusicService service, PurchaseType pt, string trackId, string alternateId = null)
        {
            ad.SetPurchaseInfo(pt, service.ID, trackId);
            if (!string.IsNullOrWhiteSpace(alternateId))
            {
                ad.SetPurchaseInfo(pt, ServiceType.AMG, alternateId);
            }
        }
        #endregion

        #region Merge
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
            string tags = string.Join("|", songs.Select(s => s.TagSummary));
            Song song = _db.MergeSongs(user, songs,
                ResolveStringField(Song.TitleField, songs),
                ResolveStringField(Song.ArtistField, songs),
                ResolveDecimalField(Song.TempoField, songs),
                ResolveIntField(Song.LengthField, songs),
                tags,
                SongDetails.BuildAlbumInfo(songs)
                );

            return song;
        }
        private ActionResult Merge(IQueryable<Song> songs)
        {
            SongMerge sm = new SongMerge(songs.ToList());

            return View("Merge", sm);
        }
        private string ResolveStringField(string fieldName, IList<Song> songs, System.Collections.Specialized.NameValueCollection form = null)
        {
            object obj = ResolveMergeField(fieldName, songs, form);

            return obj as string;
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
        #endregion

        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
    }
}