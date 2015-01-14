using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using DanceLibrary;
using m4d.ViewModels;
using m4dModels;
using PagedList;

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

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            object o;
            if (filterContext.ActionParameters.TryGetValue("filter",out o) && o == null)
            {
                o =  SongFilter.Default;
                filterContext.ActionParameters["filter"] = o;
            }

            if (o != null)
            {
                ViewBag.SongFilter = o;
            }

            base.OnActionExecuting(filterContext);
        }

        #region Commands

        [AllowAnonymous]
        public ActionResult Sample()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Search(string searchString, string dances, SongFilter filter)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                searchString = null;
            }
            if (!string.Equals(searchString, filter.SearchString))
            {
                filter.SearchString = searchString;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }
            if (!string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;
            }

            filter.Purchase = null;
            filter.TempoMin = null;
            filter.TempoMax = null;

            return DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult AdvancedSearch(string searchString, string dances, ICollection<string> services, decimal? tempoMin, decimal? tempoMax, SongFilter filter)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                searchString = null;
            }
            if (!string.Equals(searchString, filter.SearchString))
            {
                filter.SearchString = searchString;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }
            if (!string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;
            }

            string purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }

            if (filter.Purchase != purchase)
            {
                filter.Purchase = purchase;
                filter.Page = 1;
            }

            if (filter.TempoMin != tempoMin || filter.TempoMax != tempoMax)
            {
                filter.TempoMin = tempoMin;
                filter.TempoMax = tempoMax;
                filter.Page = 1;
            }

            return DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult Sort(string sortOrder, SongFilter filter)
        {
            filter.SortOrder = SongSort.DoSort(sortOrder, filter.SortOrder);

            return DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterUser(string user, SongFilter filter)
        {
            if (string.IsNullOrWhiteSpace(user))
            {
                filter.User = null;
            }
            else
            {
                filter.User = user;
            }
            return DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterService(ICollection<string> services, SongFilter filter)
        {
            string purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }
            if (filter.Purchase != purchase)
            {
                filter.Purchase = purchase;
                filter.Page = 1;
            }
            return DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterTempo(decimal? tempoMin, decimal? tempoMax, SongFilter filter)
        {
            if (filter.TempoMin != tempoMin || filter.TempoMax != tempoMax)
            {
                filter.TempoMin = tempoMin;
                filter.TempoMax = tempoMax;
                filter.Page = 1;
            }

            return DoIndex(filter);
        }

        //
        // GET: /Index/
        [AllowAnonymous]
        public ActionResult Index(int? page, string purchase, SongFilter filter)
        {
            if (page.HasValue)
            {
                filter.Page = page;
            }

            if (!string.IsNullOrWhiteSpace(purchase))
            {
                filter.Purchase = purchase;
            }

            return DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult Tags(string tags, int? page)
        {
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);

            TagList list = new TagList(tags);
            ICollection<TagType> types = Database.GetTagRings(list);
            ViewBag.Tags = types;

            List<string> tagsExpanded = new List<string>();

            foreach (var tt in types)
            {
                tagsExpanded.Add(tt.Key);
                if (tt.Ring != null)
                {
                    tagsExpanded.AddRange(tt.Ring.Select(sub => sub.Key));
                }
            }

            var songs = from s in Database.Songs where s.TitleHash != 0 && tagsExpanded.Any(val => s.TagSummary.Summary.Contains(val)) orderby s.Title select s;

            return View("Tags", songs.Include("DanceRatings").Include("ModifiedBy").Include("SongProperties").ToPagedList(page ?? 1, 25));
        }
        
        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public ActionResult Details(Guid? id = null, SongFilter filter = null)
        {
            SongDetails song = Database.FindSongDetails(id ?? Guid.Empty, User.Identity.Name);
            if (song == null)
            {
                return HttpNotFound();
            }

            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            return View(song);
        }

        [AllowAnonymous]
        public ActionResult Album(string title)
        {
            AlbumViewModel model = null;

            if (!string.IsNullOrWhiteSpace(title))
            {
                model = AlbumViewModel.Create(title, Database);
            }

            if (model != null)
            {
                ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
                return View("Album", model);
            }
            else
            {
                return HttpNotFound(string.Format("Album '{0}' not found.",title));
            }
        }


        //
        // GET: /Song/CreateI
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create(SongFilter filter = null)
        {
            ViewBag.ShowMPM = true;
            ViewBag.ShowBPM = true;
            ViewBag.DanceListAdd = GetDances();
            SongDetails sd = new SongDetails();
            ViewBag.BackAction = "Index";
            return View(sd);
        }

        //
        // POST: /Song/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Create(SongDetails song, List<string> addDances, string editTags, SongFilter filter = null)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser user = Database.FindUser(User.Identity.Name);

                song.UpdateDanceRatings(addDances, Song.DanceRatingCreate);
                TagList tags = new TagList(editTags).Add(new TagList(Song.TagsFromDances(addDances)));
                song.AddTags(tags.ToString(), user, Database, song);
                Song newSong = Database.CreateSong(user, song);

                // TODO: Think about if the round-trip is necessary
                if (newSong != null)
                {
                    Database.SaveChanges();
                    song = new SongDetails(newSong);
                }

                ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
                return View("Details", song);
            }
            else
            {
                // Add back in the danceratings
                // TODO: This almost certainly doesn't preserve edits...
                SongDetails songT = Database.FindSongDetails(song.SongId, User.Identity.Name);
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
        public ActionResult Edit(Guid? id = null, SongFilter filter = null)
        {
            SongDetails song = Database.FindSongDetails(id ?? Guid.Empty, User.Identity.Name);
            if (song == null)
            {
                return HttpNotFound();
            }

            SetupEditViewBag(song);

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
        public ActionResult Edit(SongDetails song, List<string> addDances, List<string> remDances, string editTags, SongFilter filter = null)
        {
            if (ModelState.IsValid)
            {
//#if DEBUG
//                Database.Dump();
//#endif

                ApplicationUser user = Database.FindUser(User.Identity.Name);

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
//                Database.Dump();
//#endif
                SongDetails edit = Database.EditSong(user, song, addDances, remDances, editTags);

//#if DEBUG
//                Database.Dump();
//#endif

                if (edit != null)
                {
                    Database.SaveChanges();
                    ViewBag.BackAction = "Index";
                    ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
                    return View("Details", edit);
                }
                {
                    return RedirectToAction("Index", new {filter });
                }
            }
            else
            {
                ViewBag.Errors = ModelState.SelectMany(x => x.Value.Errors.Select(z => z.Exception));

                // Add back in the danceratings
                // TODO: This almost certainly doesn't preserve edits...
                SongDetails songT = Database.FindSongDetails(song.SongId, User.Identity.Name);
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
        public ActionResult Delete(Guid id, SongFilter filter = null)
        {
            Song song = Database.Songs.Find(id);
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
        public ActionResult DeleteConfirmed(Guid id, SongFilter filter = null)
        {
            Song song = Database.Songs.Find(id);
            string userName = User.Identity.Name;
            ApplicationUser user = Database.FindUser(userName);
            Database.DeleteSong(user,song);
            return RedirectToAction("Index", new {filter });
        }


        //
        // Merge: /Song/MergeCandidates
        [Authorize(Roles = "canEdit")]
        public ActionResult MergeCandidates(int? page, int? level, bool? autoCommit, SongFilter filter)
        {
            filter.Action = "MergeCandidates";
            IList<Song> songs;

            BuildDanceList(filter);

            if (page.HasValue)
            {
                filter.Page = page;
            }

            if (level.HasValue)
            {
                filter.Level = level;
            }

            if (autoCommit == true)
            {
                songs = Database.FindMergeCandidates(10000, filter.Level ?? 1);
            }
            else
            {
                songs = Database.FindMergeCandidates(500, filter.Level ?? 1);
            }

            int pageSize = 25;
            int pageNumber = filter.Page ?? 1;

            if (autoCommit.HasValue && autoCommit.Value)
            {
                songs = AutoMerge(songs,(int)filter.Level);
            }

            return View("Index", songs.ToPagedList(pageNumber, pageSize));
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult BulkEdit(Guid[] selectedSongs, string action, SongFilter filter = null)
        {
            var songs = from s in Database.Songs
                        where selectedSongs.Contains(s.SongId)
                        select s;

            switch (action)
            {
                case "Merge":
                    return Merge(songs);
                case "Delete":
                    return Delete(songs,filter);
                default:
                    return View("Index");
            }

        }

        //
        // Merge: /Song/Merge
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult MergeResults(string songIds, SongFilter filter = null)
        {
            // See if we can do the actual merge and then return the song details page...
            var ids = songIds.Split(',').Select(s=>Guid.Parse(s)).ToList();

            var songs = from s in Database.Songs
                        where ids.Contains(s.SongId)
                        select s;
            var songList = songs.ToList();

            // Create a merged version of the song (and commit to DB)

            // Get the logged in user
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);

            var song = Database.MergeSongs(user, songList, 
                ResolveStringField(SongBase.TitleField, songList, Request.Form),
                ResolveStringField(SongBase.ArtistField, songList, Request.Form),
                ResolveDecimalField(SongBase.TempoField, songList, Request.Form),
                ResolveIntField(SongBase.LengthField, songList, Request.Form),
                Request.Form[SongBase.AlbumListField], new HashSet<string>(Request.Form.AllKeys));

            ViewBag.BackAction = "MergeCandidates";
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);

            return View("Details",Database.FindSongDetails(song.SongId));
        }

        /// <summary>
        /// Batch up searching a music service
        /// </summary>
        /// <param name="type">Music service type (currently X=XBox,A=Amazon,S=Spotify,I=ITunes)</param>
        /// <param name="options">May be more complex in future - currently Rn where n is retyr level</param>
        /// <param name="filter">Standard filter for song list</param>
        /// <param name="count">Number of songs to try, 1 is special cased as a user verified single entry</param>
        /// <returns></returns>
        [Authorize(Roles = "canEdit")]
        public ActionResult BatchMusicService(string type= "X", string options = null, SongFilter filter=null, int count = 1)
        {
            MusicService service = null;
            if (type != "-")
            {
                service = MusicService.GetService(type);
                filter.Purchase = "!" + type;
                if (service == null)
                {
                    throw new ArgumentOutOfRangeException("type");
                }
            }

            ViewBag.SearchType = type;
            ViewBag.Options = options;
            ViewBag.Error = false;

            int tried = 0;
            int skipped = 0;

            int retryLevel = -1;

            // May do more options in future
            if (!string.IsNullOrWhiteSpace(options) && options.Length > 0)
            {
                switch (options[0])
                {
                    case 'R':
                        int.TryParse(options.Substring(1), out retryLevel);
                        break;
                }
                
            }

            Dictionary<char, ApplicationUser> users = new Dictionary<char, ApplicationUser>();

            List<Song> failed = new List<Song>();
            List<SongDetails> succeeded = new List<SongDetails>();

            Context.TrackChanges(false);

            int page = 0;
            bool done = false;

            while (!done)
            {
                IQueryable<Song> songs = Database.BuildSongList(filter, DanceMusicService.CruftFilter.NoDances).Skip(page * 1000).Take(1000);
                int processed = 0;
                bool modified = false;
                foreach (Song song in songs)
                {
                    processed += 1;
                    // First check to see if we've already failed a search and at what level
                    //  failLeve is the LOWEST failure code or -1 if none

                    int failLevel = -1;
                    SongProperty fail = song.SongProperties.OrderBy(p => p.Value).FirstOrDefault(p => p.Name == Song.FailedLookup && p.Value.StartsWith(type));
                    if (fail != null && fail.Value != null && fail.Value.Length > 2)
                    {
                        int.TryParse(fail.Value.Substring(2), out failLevel);
                    }

                    if (failLevel == retryLevel || count == 1)
                    {
                        SongDetails sd = new SongDetails(song);

                        // Something of a kludge - we're goint to make type ='-' && failcode == 0 mean that we've tried 
                        //  the multi-service lookup...
                        int failcode = service == null ? 0 : -1;

                        SongDetails add = null;
                        if (service == null)
                        {
                            foreach (MusicService serviceT in MusicService.GetSearchableServices())
                            {
                                SongDetails addT = UpdateSongAndService(sd, serviceT, users);
                                if (addT != null)
                                {
                                    add = addT;
                                }
                            }
                        }
                        else
                        {
                            add = UpdateSongAndService(sd, service, users);
                        }

                        if (add != null)
                        {
                            succeeded.Add(add);
                            tried += 1;
                        }
                        else if (failcode == -1)
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
                                SongProperty sp = Database.SongProperties.Create();
                                sp.Name = Song.FailedLookup;
                                sp.Value = type + ":" + failcode.ToString();
                                song.SongProperties.Add(sp);
                            }
                            if (service != null)
                            {
                                failed.Add(song);
                                tried += 1;
                            }
                        }

                        modified = true;
                    }
                    else
                    {
                        skipped += 1;
                    }

                    if (tried > count)
                        break;

                    if ((tried + 1) % 25 == 0)
                    {
                        Trace.WriteLine(string.Format("{0} songs tried.", tried));
                        Context.CheckpointChanges();
                    }
                }

                page += 1;
                if (processed < 1000)
                {
                    done = true;
                }
                if (!modified)
                {
                    ResetContext();
                }
            }

            Context.TrackChanges(true);

            ViewBag.Completed = tried <= count;
            ViewBag.Failed = failed;
            ViewBag.Succeeded = succeeded;
            ViewBag.Skipped = skipped;
            return View();
        }

        private SongDetails UpdateSongAndService(SongDetails sd, MusicService service, Dictionary<char, ApplicationUser> users)
        {
            List<ServiceTrack> found = new List<ServiceTrack>();
            if (service == null)
            {
                foreach (MusicService serviceT in MusicService.GetSearchableServices())
                {
                    found.AddRange(MatchSongAndService(sd, serviceT));
                }
            }
            else
            {
                found.AddRange(MatchSongAndService(sd, service));
            }

            if (found.Count > 0)
            {
                TagList tags = new TagList();
                foreach (ServiceTrack foundTrack in found)
                {
                    UpdateMusicService(sd, MusicService.GetService(foundTrack.Service), foundTrack.Name, foundTrack.Album, foundTrack.Artist, foundTrack.TrackId, foundTrack.CollectionId, foundTrack.AltId, foundTrack.Duration.ToString(), foundTrack.TrackNumber);
                    tags = tags.Add(new TagList(Database.NormalizeTags(foundTrack.Genre, "Music")));
                }
                ApplicationUser user;
                if (!users.TryGetValue(service.CID, out user))
                {
                    user = Database.FindUser("batch-" + service.CID.ToString().ToLower());
                    users[service.CID] = user;
                }

                return Database.EditSong(user, sd, null, null, tags.ToString());
            }
            else
            {
                return null;
            }

        }
        private IList<ServiceTrack> MatchSongAndService(SongDetails sd, MusicService service)
        {
            IList<ServiceTrack> found = new List<ServiceTrack>();

            IList<ServiceTrack> tracks = FindMusicServiceSong(sd, service);
            // First try the full title/artist
            if ((tracks == null || tracks.Count == 0) && !string.Equals(DefaultServiceSearch(sd, true), DefaultServiceSearch(sd, false)))
            {
                // Now try cleaned up title/artist (remove punctuation and stuff in parens/brackets)
                ViewBag.Status = null;
                ViewBag.Error = false;
                tracks = FindMusicServiceSong(sd, service, true);
            }

            if (tracks != null && tracks.Count > 0)
            {
                // First filter out anything that's not a title-artist match (weak)
                tracks = sd.TitleArtistFilter(tracks);

                if (tracks.Count > 0)
                {
                    // Then check for exact album match if we don't have a tempo
                    if (!sd.Length.HasValue)
                    {
                        foreach (ServiceTrack track in tracks)
                        {
                            if (sd.FindAlbum(track.Album) != null)
                            {
                                found.Add(track);
                                break;
                            }
                        }
                    }
                    // If not exact album match and the song has a length, choose all albums with the same tempo (delta a few seconds)
                    else
                    {
                        found = sd.DurationFilter(tracks, 6);
                    }

                    // If no album name or length match, choose the 'dominant' version of the title/artist match by clustering lengths
                    //  Note that this degenerates to chosing a single album if that is what is available
                    if (found.Count == 0 && !sd.HasRealAblums)
                    {
                        ServiceTrack track = SongDetails.FindDominantTrack(tracks);
                        found = SongDetails.DurationFilter(tracks, track.Duration.Value, 6);
                    }
                }
            }

            return found;
        }

        // GET: /Song/MusicServiceSearch/5?search=name
        [Authorize(Roles = "canEdit")]
        public ActionResult MusicServiceSearch(Guid? id = null, string type="X", string title = null, string artist = null, SongFilter filter=null)
        {
            SongDetails song = Database.FindSongDetails(id??Guid.Empty);
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

            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
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
        public ActionResult ChooseMusicService(Guid songId, string type, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, string genre, int? trackNum, SongFilter filter)
        {
            MusicService service = MusicService.GetService(type);
            if (service == null)
            {
                throw new ArgumentOutOfRangeException("type");
            }

            SongDetails song = Database.FindSongDetails(songId);
            if (song == null)
            {
                return HttpNotFound();
            }

            ApplicationUser user = Database.FindUser(User.Identity.Name);
            SongDetails alt = UpdateMusicService(song, service, name, album, artist, trackId, collectionId, alternateId, duration, trackNum);
            song.AddTags(Database.NormalizeTags(genre, "Music"), user, Database, song);

            ViewBag.OldSong = alt;

            return View("Edit", song);
        }

        #endregion

        #region General Utilities
        private MultiSelectList GetDances(IList<DanceRating> ratings = null)
        {
            List<SimpleDance> Dances = new List<SimpleDance>(Database.Dances.Count());

            foreach (Dance d in Database.Dances)
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
        private ActionResult Delete(IQueryable<Song> songs, SongFilter filter)
        {
            ApplicationUser user = Database.FindUser(User.Identity.Name);

            foreach (Song song in songs)
            {
                Database.DeleteSong(user, song);
            }

            return RedirectToAction("Index", new { filter });
        }

        #endregion

        #region Index
        private ActionResult DoIndex(SongFilter filter)
        {
            Trace.WriteLine(string.Format("Entering Song.Index: dances='{0}',sortOrder='{1}',searchString='{2}'", filter.Dances, filter.SortOrder, filter.SearchString));

            var songs = Database.BuildSongList(filter, HttpContext.User.IsInRole(DanceMusicService.EditRole) ? DanceMusicService.CruftFilter.AllCruft : DanceMusicService.CruftFilter.NoCruft);
            BuildDanceList(filter);

            Trace.WriteLine("Exiting Song.Index");
            var list = songs.ToPagedList(filter.Page ?? 1, 25);
            ViewBag.Spotify = Database.GetPurchaseInfo(ServiceType.Spotify,list.ToList());
            
            return View("Index", list);
        }

        private void BuildDanceList(SongFilter filter)
        {
            //IList<SongCounts> songCounts = SongCounts.GetFlatSongCounts(Database);
            //var scq = songCounts.Select(s => new { s.DanceId, s.DanceName });

            //ViewBag.Dances = new SelectList(scq.AsEnumerable(), "DanceId", "DanceName", filter.Dances);

            ViewBag.SelectedDances = Dances.Instance.FromIds(filter.Dances);
            ViewBag.Dances = SongCounts.GetSongCounts(Database);
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
        }

        private void DumpSongs(IQueryable<Song> songs, string purchase)
        {
            Debug.WriteLine(string.Format("------------Purchase == {0} ------------", purchase));
            foreach (Song t in songs)
            {
                Debug.WriteLine("{0}: {1}", t.Title, t.Purchase);
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
                tracks = Context.FindMusicServiceSong(song, service, clean, title, artist);

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

        SongDetails UpdateMusicService(SongDetails song, MusicService service, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, int? trackNum)
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
                //song.Albums.Insert(0, ad);
                song.Albums.Add(ad);
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

            return alt;
        }
        private void UpdateMusicServicePurchase(AlbumDetails ad, MusicService service, PurchaseType pt, string trackId, string alternateId = null)
        {
            ad.SetPurchaseInfo(pt, service.Id, trackId);
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
            ApplicationUser user = Database.FindUser(userName);

            List<Song> ret = new List<Song>();
            List<Song> cluster = null;

            try
            {
                Context.Configuration.AutoDetectChangesEnabled = false;

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
                Context.Configuration.AutoDetectChangesEnabled = false;
                Database.SaveChanges();
            }

            return ret;
        }

        private Song AutoMerge(List<Song> songs, ApplicationUser user)
        {
            Song song = Database.MergeSongs(user, songs,
                ResolveStringField(SongBase.TitleField, songs),
                ResolveStringField(SongBase.ArtistField, songs),
                ResolveDecimalField(SongBase.TempoField, songs),
                ResolveIntField(SongBase.LengthField, songs),
                SongDetails.BuildAlbumInfo(songs)
                );

            return song;
        }
        private ActionResult Merge(IQueryable<Song> songs)
        {
            SongMerge sm = new SongMerge(songs.ToList());

            return View("Merge", sm);
        }
        private string ResolveStringField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            object obj = ResolveMergeField(fieldName, songs, form);

            return obj as string;
        }


        private int? ResolveIntField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            int? ret = ResolveMergeField(fieldName, songs, form) as int?;

            return ret;
        }

        private decimal? ResolveDecimalField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            decimal? ret = ResolveMergeField(fieldName, songs, form) as decimal?;

            return ret;
        }

        private object ResolveMergeField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            // If fieldName doesn't exist, this means that we didn't add a radio button for the field because all the
            //  values were the same.  So just return the value of the first song.

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

            return songs[idx].GetType().GetProperty(fieldName).GetValue(songs[idx]);
        }
        #endregion
    }
}