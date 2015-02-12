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
using System.Web.Helpers;
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
        }

        private static DbConnection CreateConnection(string nameOrConnectionString)
        {
            ConnectionStringSettings connectionStringSetting =
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

        private static DbConnection CreateConnection(string connectionString, string providerInvariantName)
        {
            DbConnection connection;
            DbProviderFactory factory = DbProviderFactories.GetFactory(providerInvariantName);
            connection = factory.CreateConnection();
            connection.ConnectionString = connectionString;
            return connection;
        }

        #endregion

        #region Properties
        public DbSet<Song> Songs { get; set; }
        public DbSet<SongProperty> SongProperties { get; set; }
        public DbSet<Dance> Dances { get; set; }
        public DbSet<DanceRating> DanceRatings { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<TagType> TagTypes { get; set; }
        public DbSet<SongLog> Log { get; set; }
        public DbSet<ModifiedRecord> Modified { get; set; }
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
            modelBuilder.Entity<DanceRating>().HasKey(dr => new { dr.SongId, dr.DanceId });

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

            base.OnModelCreating(modelBuilder);
        }

        #endregion

        #region MusicService
        // Obviously not the clean abstraction, but Amazon is different enough that my abstraction
        //  between itunes and xbox doesn't work.   So I'm going to shoe-horn this in to get it working
        //  and refactor later.

        public IList<ServiceTrack> FindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null, string album = null)
        {
            IList<ServiceTrack> list;

            if (service != null)
            {
                list = DoFindMusicServiceSong(song, service, clean, title, artist);
            }
            else
            {
                List<ServiceTrack> acc = new List<ServiceTrack>();
                foreach (var servT in MusicService.GetServices())
                {
                    IList<ServiceTrack> t = DoFindMusicServiceSong(song, servT, clean, title, artist);
                    if (t != null)
                    {
                        acc.AddRange(t);
                    }
                }

                list = acc;
            }

            if (list != null)
            {
                list = FilterKaraoke(list);
                if (song != null)
                {
                    list = song.RankTracks(list);
                }
                else
                {
                    list = SongDetails.RankTracksByCluster(list, album);
                }
            }

            return list;
        }

        private static IList<ServiceTrack> FilterKaraoke(IList<ServiceTrack> list)
        {
            List<ServiceTrack> tracks = new List<ServiceTrack>();

            foreach (var track in list)
            {
                if (!ContainsKaraoke(track.Name) && !ContainsKaraoke(track.Album))
                {
                    tracks.Add(track);
                }
            }

            return tracks;
        }

        private static bool ContainsKaraoke(string name)
        {
            string[] exclude = new string[] { "karaoke", "in the style of", "a tribute to" };
            foreach (var s in exclude)
            {
                if (name.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        private IList<ServiceTrack> DoFindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null)
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

            if (tracks != null)
            {
                foreach (var track in tracks)
                {
                    track.AlbumLink = service.GetPurchaseLink(PurchaseType.Album, track.CollectionId, track.TrackId);
                    track.SongLink = service.GetPurchaseLink(PurchaseType.Song, track.CollectionId, track.TrackId);
                    track.PurchaseInfo = AlbumDetails.BuildPurchaseInfo(service.Id, track.CollectionId, track.TrackId);
                }
            }
            return tracks;
        }
        private IList<ServiceTrack> FindMSSongAmazon(SongDetails song, bool clean = false, string title = null, string artist = null)
        {
            if (_awsFetcher == null)
            {
                _awsFetcher = new AWSFetcher();
            }

            bool custom = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(artist);

            if (custom)
            {
                return _awsFetcher.FetchTracks(title, artist);
            }
            else
            {
                return _awsFetcher.FetchTracks(song, clean);
            }
        }

        private IList<ServiceTrack> FindMSSongGeneral(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null)
        {
            HttpWebResponse response;
            string responseString;

            // Make Music database request
            string req = service.BuildSearchRequest(artist, title);

            if (req == null)
            {
                return null;
            }

            var request = (HttpWebRequest)WebRequest.Create(req);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";

            var auth = AdmAuthentication.GetServiceAuthorization(service.Id);
            if (auth != null)
            {
                request.Headers.Add("Authorization",auth);
            }

            using (response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        responseString = sr.ReadToEnd();
                    }
                }
                else
                {
                    throw new WebException(response.StatusDescription);
                }
            }

            responseString = service.PreprocessSearchResponse(responseString);
            dynamic results = Json.Decode(responseString);
            return service.ParseSearchResults(results);
        }
        #endregion

        #region IDanceMusicContext
        public ApplicationUser FindOrAddUser(string name, string role, object umanager)
        {
            ApplicationUserManager uman = umanager as ApplicationUserManager;

            var user = uman.FindByName(name);
            if (user == null)
            {
                user = new ApplicationUser { UserName = name, Email = name + "@music4dance.net", EmailConfirmed=true, StartDate=DateTime.Now };
                IdentityResult res = uman.Create(user, "_This_Is_@_placeh0lder_");
                if (res.Succeeded)
                {
                    var user2 = uman.FindByName(name);
                    Trace.WriteLine(string.Format("{0}:{1}",user2.UserName,user2.Id));
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
                        Trace.WriteLine(ve.ErrorMessage);
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
            else
            {
                TrackChanges(true);
                TrackChanges(false);
            }
        }

        public void CheckpointSongs()
        {
            if (Configuration.AutoDetectChangesEnabled)
            {
                throw new InvalidConstraintException("Attempting a checkpoint without having first disabled auto-detect");
            }
            else
            {
                TrackChanges(true);
                TrackChanges(false);

                RemoveEntities<Song>();
                RemoveEntities<SongProperty>();
                RemoveEntities<DanceRating>();
                RemoveEntities<Tag>();
                RemoveEntities<ModifiedRecord>();
                RemoveEntities<SongLog>();
            }
        }

        private void RemoveEntities<T>() where T : class
        {
            List<T> list = new List<T>();
            foreach (var unknown in Set<T>().Local)
                list.Add(unknown);
            foreach (var p in list) 
                Entry(p).State = EntityState.Detached;
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
        #endregion

        AWSFetcher _awsFetcher;
    }
}