using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Web.Helpers;
using m4d.Controllers;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

// Let's see if we can mock up a recoverable log file by spitting out
// something resembling a tab-separated flat list of songs items with a
// command associated with each line.  Might add a checkpoint command
// into the songproperties table as well...

// COMMAND  User    Title   Artist  Album   Publisher   Tempo   Length  Track   Genre   Purchase    DanceRating Custom

// Kill Publisher Track Purchase -> do these move to custom


namespace m4d.Context
{
    public class DanceMusicContext : IdentityDbContext<ApplicationUser>, IDanceMusicContext
    {
        #region Construction

        public static DanceMusicContext Create()
        {
            return new DanceMusicContext();
        }

        public DanceMusicContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
            Database.CommandTimeout = 360;
            if (!DMController.VerboseTelemetry) return;

            var properties = new Dictionary<string, string> { { "id", _id.ToString() } };
            DMController.TelemetryClient.TrackEvent("CreateDbContext", properties);
        }

        private static DbConnection CreateConnection(string nameOrConnectionString)
        {
            var connectionStringSetting =
                ConfigurationManager.ConnectionStrings[nameOrConnectionString];
            string connectionString;
            string providerName;

            if (connectionStringSetting != null)
            {
                connectionString = connectionStringSetting.ConnectionString;
                providerName = connectionStringSetting.ProviderName;
            }
            else
            {
                providerName = "System.Data.SqlClient";
                connectionString = nameOrConnectionString;
            }

            return CreateConnection(connectionString, providerName);
        }

        private Guid _id = Guid.NewGuid();

        private static DbConnection CreateConnection(string connectionString, string providerInvariantName)
        {
            var factory = DbProviderFactories.GetFactory(providerInvariantName);
            var connection = factory.CreateConnection();
            if (connection != null)
            {
                connection.ConnectionString = connectionString;
                return connection;
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (DMController.VerboseTelemetry)
            {
                var properties = new Dictionary<string, string> { { "disposing", disposing.ToString() }, { "id", _id.ToString() } };
                DMController.TelemetryClient.TrackEvent("DisposeDbContext", properties);
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Properties
        public DbSet<Song> Songs { get; set; }
        public DbSet<SongProperty> SongProperties { get; set; }
        public DbSet<Dance> Dances { get; set; }
        public DbSet<DanceRating> DanceRatings { get; set; }
        // ReSharper disable once InconsistentNaming
        public DbSet<TopN> TopNs { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<TagType> TagTypes { get; set; }
        public DbSet<SongLog> Log { get; set; }
        public DbSet<ModifiedRecord> Modified { get; set; }
        public DbSet<Search> Searches { get; set; }
        #endregion

        #region Events
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<SongBase>();
            modelBuilder.Ignore<SongDetails>();
            modelBuilder.Ignore<DanceRatingInfo>();
            modelBuilder.Ignore<TaggableObject>();

            modelBuilder.Properties<DateTime>().Configure(c => c.HasColumnType("datetime2"));

            modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
            modelBuilder.Entity<Song>().Ignore(song => song.CurrentLog);
            modelBuilder.Entity<Song>().Ignore(song => song.AlbumName);

            modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);
            // ReSharper disable once SimilarAnonymousTypeNearby
            modelBuilder.Entity<DanceRating>().HasKey(dr => new { dr.SongId, dr.DanceId });
            // ReSharper disable once SimilarAnonymousTypeNearby
            modelBuilder.Entity<TopN>().HasKey(tn => new { tn.DanceId, tn.SongId });

            modelBuilder.Entity<TaggableObject>().Ignore(to => to.TagId);
            //modelBuilder.Entity<TaggableObject>().HasKey(to => to.TagIdBase);
            modelBuilder.Entity<Tag>().HasKey(t => new { t.UserId, t.Id });
            modelBuilder.Entity<TagType>().HasKey(tt => tt.Key);
            modelBuilder.Entity<TagType>().Ignore(tt => tt.Value);
            modelBuilder.Entity<TagType>().Ignore(tt => tt.Category);
            modelBuilder.Entity<TagType>().HasOptional(x => x.Primary)
                .WithMany(x => x.Ring)
                .HasForeignKey(x => x.PrimaryId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ModifiedRecord>().HasKey(t => new { t.ApplicationUserId, t.SongId });
            modelBuilder.Entity<ModifiedRecord>().Ignore(mr => mr.UserName);

            modelBuilder.Entity<DanceLink>().HasKey(dl => dl.Id);

            modelBuilder.Entity<ApplicationUser>().Property(u => u.Region).HasMaxLength(2);
            modelBuilder.Entity<ApplicationUser>().Property(u => u.ServicePreference).HasMaxLength(10);

            modelBuilder.Entity<Search>().Property(u => u.Query).IsRequired();
            modelBuilder.Entity<Search>().Ignore(u => u.Filter);

            base.OnModelCreating(modelBuilder);
        }

        #endregion

        #region MusicService
        // Obviously not the clean abstraction, but Amazon is different enough that my abstraction
        //  between itunes and groove doesn't work.   So I'm going to shoe-horn this in to get it working
        //  and refactor later.

        public IList<ServiceTrack> FindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null, string album = null, string region = null)
        {
            IList<ServiceTrack> list;

            if (service != null)
            {
                list = DoFindMusicServiceSong(song, service, clean, title, artist, region);
            }
            else
            {
                var acc = new List<ServiceTrack>();
                foreach (var s in MusicService.GetSearchableServices())
                {
                    var tracks = DoFindMusicServiceSong(song, s, clean, title, artist, region);

                    if (tracks != null) acc.AddRange(tracks);
                }

                list = acc;
            }

            if (list == null) return null;

            list = FilterKaraoke(list);


            list = song != null ? song.RankTracks(list) : SongDetails.RankTracksByCluster(list, album);

            return list;
        }

        public ServiceTrack GetMusicServiceTrack(string id, MusicService service, string region=null)
        {
            if (service.Id == ServiceType.Amazon)
                return AmazonFetcher.LookupTrack(id);

            var request = service.BuildTrackRequest(id, region);
            dynamic results = GetMusicServiceResults(request, service);
            return service.ParseTrackResults(results);
        }

        public IList<ServiceTrack> LookupServiceTracks(MusicService service, string url, IPrincipal principal)
        {
            dynamic results = GetMusicServiceResults(service.BuildLookupRequest(url), service, principal);
            IList<ServiceTrack> tracks = service.ParseSearchResults(results);
            while ((results = NextMusicServiceResults(results, service, principal)) != null) 
            {
                var t = (tracks as List<ServiceTrack>) ?? tracks.ToList();
                t.AddRange(service.ParseSearchResults(results));
                tracks = t;
            }

            if (tracks == null) return null;

            ComputeTrackPurchaseInfo(service,tracks);

            return tracks;
        }

        public ServiceTrack CoerceTrackRegion(string id, MusicService service, string region)
        {
            if (string.IsNullOrWhiteSpace(region)) return null;

            var track = GetMusicServiceTrack(id, service, region);

            if (track == null) return null;

            return track.IsPlayable == false ? null : GetMusicServiceTrack(track.TrackId, service);
        }

        public EchoTrack LookupEchoTrack(string id)
        {
            string request = $"http://developer.echonest.com/api/v4/track/profile?api_key=B0SEV0FNKNEOHGFB0&format=json&id=spotify:track:{id}&bucket=audio_summary";
            dynamic results = GetMusicServiceResults(request);
            return EchoTrack.BuildEchoTrack(results);
        }

        private static IList<ServiceTrack> FilterKaraoke(IList<ServiceTrack> list)
        {
            return list.Where(track => !ContainsKaraoke(track.Name) && !ContainsKaraoke(track.Album)).ToList();
        }

        private static bool ContainsKaraoke(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var exclude = new[] { "karaoke", "in the style of", "a tribute to" };
            return exclude.Any(s => name.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) != -1);
        }

        private IList<ServiceTrack> DoFindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null, string region=null)
        {
            IList<ServiceTrack> tracks;
            switch (service.Id)
            {
                case ServiceType.Amazon:
                    tracks = FindMSSongAmazon(song, clean, title, artist);
                    break;
                default:
                    tracks = FindMSSongGeneral(song, service, clean, title, artist);
                    break;
            }

            if (tracks == null) return null;

            // Convoluted way of saying that we should coerce regions for spotify

            if (service.HasRegions && !string.IsNullOrWhiteSpace(region))
            {
                var dict = new Dictionary<string, ServiceTrack>();
                foreach (var track in tracks)
                {
                    if (dict.ContainsKey(track.TrackId)) continue;

                    ServiceTrack t = null;
                    if (!track.AvailableMarkets.Contains(region))
                    {
                        t = CoerceTrackRegion(track.TrackId, service, region);
                        if (t != null)
                        {
                            t.AvailableMarkets = PurchaseRegion.MergeRegions(t.AvailableMarkets, track.AvailableMarkets);
                        }
                    }

                    if (t == null) t = track;

                    dict[t.TrackId] = t;
                }

                tracks = dict.Values.ToList();
            }

            ComputeTrackPurchaseInfo(service,tracks);

            return tracks;
        }

        private void ComputeTrackPurchaseInfo(MusicService service, IEnumerable<ServiceTrack> tracks)
        {
            foreach (var track in tracks)
            {
                track.AlbumLink = service.GetPurchaseLink(PurchaseType.Album, track.CollectionId, track.TrackId);
                track.SongLink = service.GetPurchaseLink(PurchaseType.Song, track.CollectionId, track.TrackId);
                track.PurchaseInfo = AlbumDetails.BuildPurchaseInfo(service.Id, track.CollectionId, track.TrackId, track.AvailableMarkets);
            }
        }
        private IList<ServiceTrack> FindMSSongAmazon(SongDetails song, bool clean = false, string title = null, string artist = null)
        {
            var custom = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(artist);

            return custom ? AmazonFetcher.FetchTracks(title, artist) : AmazonFetcher.FetchTracks(song, clean);
        }

        private IList<ServiceTrack> FindMSSongGeneral(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null)
        {
            dynamic results = GetMusicServiceResults(service.BuildSearchRequest(artist, title), service);
            return service.ParseSearchResults(results);
        }

        private static int GetRateInfo(WebHeaderCollection headers, string type)
        {
            var s = headers.Get(type);
            if (s == null)
                return -1;

            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"{type}: {s}");
            int info;
            return int.TryParse(s, out info) ? info : -1;
        }

        private static dynamic GetMusicServiceResults(string request, MusicService service = null, IPrincipal principal = null)
        {
            while (true)
            {
                string responseString = null;

                if (request == null)
                {
                    return null;
                }

                var req = (HttpWebRequest)WebRequest.Create(request);
                req.Method = WebRequestMethods.Http.Get;
                req.Accept = "application/json";

                string auth = null;
                if (service != null)
                {
                    auth = AdmAuthentication.GetServiceAuthorization(service.Id,principal);
                }

                if (auth != null)
                {
                    req.Headers.Add("Authorization", auth);
                }

                try
                {
                    using (var response = (HttpWebResponse) req.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var stream = response.GetResponseStream();
                            if (stream != null)
                            {
                                using (var sr = new StreamReader(stream))
                                {
                                    responseString = sr.ReadToEnd();
                                }

                                var remaining = GetRateInfo(response.Headers, "X-RateLimit-Remaining");

                                if (remaining > 0 && remaining < 20)
                                {
                                    // TODO: Figure out a better way to sleep here (maybe keep track of the last time that RateLimit-Used == 0)
                                    // This may depend on what their algorithm is for trailing - if it's trailing minutewe would need to keep 
                                    // a queue of the last n and wait for (end of queue time + 1 minute - current time + some fudge factor).

                                    //var used = GetRateInfo(response.Headers, "X-RateLimit-Used");
                                    //var limit = GetRateInfo(response.Headers, "X-RateLimit-Limit");
                                    //if (used == -1 || limit == -1)
                                    //{
                                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                        $"Excedeed EchoNest Limits: Pre-emptive {remaining} - used = {GetRateInfo(response.Headers, "X-RateLimit-Used")} - limit = {GetRateInfo(response.Headers, "X-RateLimit-Limit")}");
                                    System.Threading.Thread.Sleep(3*1000);
                                    //}
                                }
                            }
                        }
                        else if ((int) response.StatusCode == 429 /*HttpStatusCode.TooManyRequests*/)
                        {
                            // Wait algorithm failed, paus for 15 seconds
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Excedeed EchoNest Limits: Caught");
                            System.Threading.Thread.Sleep(15*1000);
                            continue;
                        }
                        if (responseString == null)
                        {
                            throw new WebException(response.StatusDescription);
                        }
                    }
                }
                catch (WebException we)
                {
                    var r = we.Response as HttpWebResponse;
                    if (r == null || (int) r.StatusCode != 429) throw;

                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Excedeed EchoNest Limits: Caught");
                    System.Threading.Thread.Sleep(15 * 1000);
                    continue;
                }

                if (service != null)
                {
                    responseString = service.PreprocessResponse(responseString);
                }

                return Json.Decode(responseString);
            }
        }

        private static dynamic NextMusicServiceResults(dynamic last, MusicService service, IPrincipal principal = null)
        {
            var request = service.GetNextRequest(last);
            return request == null ? null : GetMusicServiceResults(request, service, principal);
        }

        #endregion

            #region IDanceMusicContext
        public ApplicationUser FindOrAddUser(string name, string role, object umanager)
        {
            var uman = umanager as ApplicationUserManager;

            var user = uman.FindByName(name);
            if (user == null)
            {
                user = new ApplicationUser { UserName = name, Email = name + "@music4dance.net", EmailConfirmed=true, StartDate=DateTime.Now };
                var res = uman.Create(user, "_This_Is_@_placeh0lder_");
                if (res.Succeeded)
                {
                    var user2 = uman.FindByName(name);
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{user2.UserName}:{user2.Id}");
                }
                
            }

            if (string.Equals(role, DanceMusicService.PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else if (!uman.IsInRole(user.Id, role))
            {
                uman.AddToRole(user.Id, role);
            }

            ApplicationUser ctxtUser = Users.Find(user.Id); //Users.FirstOrDefault(u => string.Equals(u.UserName, name, StringComparison.InvariantCultureIgnoreCase)); 
            Debug.Assert(ctxtUser != null);
            return ctxtUser;
        }

        public override int SaveChanges()
        {
            int ret;
            try
            {
                ret = base.SaveChanges();
            }
            catch (DbEntityValidationException e)
            {
                foreach (var err in e.EntityValidationErrors)
                {
                    foreach (var ve in err.ValidationErrors)
                    {
                        Trace.WriteLineIf(TraceLevels.General.TraceError, ve.ErrorMessage);
                    }
                }

                Debug.Assert(false);
                throw;
            }

            return ret;
        }

        public void CheckpointChanges()
        {
            if (Configuration.AutoDetectChangesEnabled)
            {
                throw new InvalidConstraintException("Attempting a checkpoint without having first disabled auto-detect");
            }

            TrackChanges(true);
            TrackChanges(false);
        }

        public void CheckpointSongs()
        {
            if (Configuration.AutoDetectChangesEnabled)
            {
                throw new InvalidConstraintException("Attempting a checkpoint without having first disabled auto-detect");
            }

            TrackChanges(true);
            TrackChanges(false);

            RemoveEntities<Song>();
            RemoveEntities<SongProperty>();
            RemoveEntities<DanceRating>();
            RemoveEntities<Tag>();
            RemoveEntities<ModifiedRecord>();
            RemoveEntities<SongLog>();
        }

        // TODO: Figure out if there is a type-safe way to do this...
        public void ClearEntities(IEnumerable<string> entities)
        {
            foreach (var s in entities)
            {
                switch (s)
                {
                    case "Song":
                        RemoveEntities<Song>();
                        break;
                    case "SongProperty":
                        RemoveEntities<SongProperty>();
                        break;
                    case "DanceRating":
                        RemoveEntities<DanceRating>();
                        break;
                    case "Tag":
                        RemoveEntities<Tag>();
                        break;
                    case "ModifiedRecord":
                        RemoveEntities<ModifiedRecord>();
                        break;
                    case "SongLog":
                        RemoveEntities<SongLog>();
                        break;
                    case "TopN":
                        RemoveEntities<TopN>();
                        break;
                }
            }
        }
        public void LoadDances()
        {
            Configuration.LazyLoadingEnabled = false;

            Dances.Include("DanceLinks").Include("TopSongs.Song.DanceRatings").Load();

            Configuration.LazyLoadingEnabled = true;
        }

        private void RemoveEntities<T>() where T : class
        {
            var list = Set<T>().Local.ToList();
            foreach (var p in list) 
                Entry(p).State = System.Data.Entity.EntityState.Detached;
        }

        public void TrackChanges(bool track)
        {
            if (track && Configuration.AutoDetectChangesEnabled == false)
            {
                // Turn change tracking back on and update what's been changed
                //  while it was off
                Configuration.AutoDetectChangesEnabled = true;
                ChangeTracker.DetectChanges();
                SaveChanges();
            }
            else if (!track && Configuration.AutoDetectChangesEnabled)
            {
                Configuration.AutoDetectChangesEnabled = false;
            }
        }

        public bool LazyLoadingEnabled
        {
            get { return Configuration.LazyLoadingEnabled; }
            set { Configuration.LazyLoadingEnabled = value; }
        }

        public bool ProxyCreationEnabled
        {
            get { return Configuration.ProxyCreationEnabled; }
            set { Configuration.ProxyCreationEnabled = value; }
        }

        #endregion

        private AWSFetcher AmazonFetcher => _awsFetcher ?? (_awsFetcher = new AWSFetcher());
        private AWSFetcher _awsFetcher;
    }
}