using DanceLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using System.Text;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Reflection;

using m4d.ViewModels;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNet.Identity;
using System.Data.Entity.Validation;
using System.Net;
using System.IO;

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
            DbConnection connection = null;
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
        protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        {
            modelBuilder.Properties<DateTime>().Configure(c => c.HasColumnType("datetime2"));
            modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
            modelBuilder.Entity<Song>().Ignore(song => song.CurrentLog);
            modelBuilder.Entity<Song>().Ignore(song => song.AlbumName);
            modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);
            modelBuilder.Entity<DanceRating>().HasKey(dr => new { dr.SongId, dr.DanceId });
            modelBuilder.Entity<Tag>().HasKey(tag => new { tag.SongId, tag.Value });
            modelBuilder.Entity<TagType>().HasKey(tt => tt.Value);
            modelBuilder.Entity<TagType>().Ignore(tt => tt.CategoryList);
            modelBuilder.Entity<ModifiedRecord>().HasKey(t => new { t.ApplicationUserId, t.SongId });
            modelBuilder.Entity<ModifiedRecord>().Ignore(mr => mr.UserName);

            base.OnModelCreating(modelBuilder);
        }

        #endregion

        public override int SaveChanges()
        {
            int ret = 0;
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


        #region MusicService
        // Obviously not the clean abstraction, but Amazon is different enough that my abstraction
        //  between itunes and xbox doesn't work.   So I'm going to shoe-horn this in to get it working
        //  and refactor later.

        public IList<ServiceTrack> FindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null, string album = null)
        {
            IList<ServiceTrack> list = null;

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
            IList<ServiceTrack> tracks = null;
            switch (service.ID)
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
                    track.PurchaseInfo = AlbumDetails.BuildPurchaseInfo(service.ID, track.CollectionId, track.TrackId);
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
            HttpWebRequest request = null;
            HttpWebResponse response = null;

            string responseString = null;

            // Make Music database request

            string req = service.BuildSearchRequest(artist, title);

            if (req == null)
            {
                return null;
            }

            request = (HttpWebRequest)WebRequest.Create(req);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";
            if (service.RequiresKey)
            {
                request.Headers.Add("Authorization", XboxAuthorization);
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

            if (responseString != null)
            {
                responseString = service.PreprocessSearchResponse(responseString);
                dynamic results = System.Web.Helpers.Json.Decode(responseString);
                return service.ParseSearchResults(results);
            }
            else
            {
                return new List<ServiceTrack>();
            }
        }

        private static string XboxAuthorization
        {
            get
            {
                if (s_admAuth == null)
                {
                    string clientId = "music4dance";
                    string clientSecret = "iGvYm97JA+qYV1K2lvh8sAnL8Pebp5cN2KjvGnOD4gI=";

                    s_admAuth = new AdmAuthentication(clientId, clientSecret);

                }

                return "Bearer " + s_admAuth.GetAccessToken().access_token;
            }
        }

        private static AdmAuthentication s_admAuth = null;

        #endregion

        public ModifiedRecord CreateMapping(Guid songId, string applicationId)
        {
            ModifiedRecord us = Modified.Create();
            us.ApplicationUserId = applicationId;
            us.SongId = songId;
            return us;
        }

        #region IUserMap
        public ApplicationUser FindUser(string name)
        {
            return Users.FirstOrDefault(u => u.UserName.ToLower() == name.ToLower());
        }

        public ApplicationUser FindOrAddUser(string name, string role)
        {
            var ustore = new UserStore<ApplicationUser>(this);
            var umanager = new UserManager<ApplicationUser>(ustore);

            var user = FindUser(name);
            if (user == null)
            {
                user = new ApplicationUser { UserName = name };
                umanager.Create(user, "_this_is_a_placeholder_");
            }

            if (string.Equals(role, DanceMusicService.PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else if (!umanager.IsInRole(user.Id, role))
            {
                umanager.AddToRole(user.Id, role);
            }

            return user;
        }


        #endregion

        public IDictionary<string, IdentityRole> RoleDictionary
        {
            get
            {
                if (_roles == null)
                {
                    _roles = new Dictionary<string, IdentityRole>();

                    foreach (var role in Roles)
                    {
                        _roles.Add(role.Id, role);
                    }
                }
                return _roles;
            }
        }
        private IDictionary<string, IdentityRole> _roles = null;

        AWSFetcher _awsFetcher;

    }
}