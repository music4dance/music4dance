using DanceLibrary;
using EFTracingProvider;
using System;
using System.Configuration;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SongDatabase.Models
{
    public class DanceMusicContext : DbContext
    {
        // You can add custom code to this file. Changes will not be overwritten.
        // 
        // If you want Entity Framework to drop and regenerate your database
        // automatically whenever you change your model schema, add the following
        // code to the Application_Start method in your Global.asax file.
        // Note: this will destroy and re-create your database with every model change.
        // 
        // System.Data.Entity.Database.SetInitializer(new System.Data.Entity.DropCreateDatabaseIfModelChanges<SongDatabase.Models.DanceMusicContext>());

        #region Trace Mechanics

        public static bool TraceEnabled = false;

        public static void SetTrace()
        {
#if DEBUG
            TraceEnabled = true;
            EFTracingProviderConfiguration.LogToFile = @"d:\temp\log.txt";
#endif
        }

        public DanceMusicContext()
            : this("DefaultConnection")
        {
        }

        public DanceMusicContext(string nameOrConnectionString)
              : base(CreateConnection(nameOrConnectionString), true)
          {
              if (TraceEnabled)
              { 
                  ((IObjectContextAdapter)this).ObjectContext.EnableTracing();
              }
            }

        private static DbConnection CreateConnection(string nameOrConnectionString)
        {
            // does not support entity connection strings
            EFTracingProviderFactory.Register();
                
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
            if (TraceEnabled)
            {
                connection = CreateTracingConnection(connectionString, providerInvariantName);
            }
            else
            {
                DbProviderFactory factory = DbProviderFactories.GetFactory(providerInvariantName);
                connection = factory.CreateConnection();
                connection.ConnectionString = connectionString;
            }
            return connection;
        }


        private static EFTracingConnection CreateTracingConnection(string connectionString, string providerInvariantName)
        {

            string wrapperConnectionString =
                string.Format(@"wrappedProvider={0};{1}", providerInvariantName, connectionString);

            EFTracingConnection connection =
                new EFTracingConnection
                {
                    ConnectionString = wrapperConnectionString
                };

            return connection;
        }



        #endregion

        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<UserProfile> UserProfiles { get; set; }

        public DbSet<Dance> Dances { get; set; }

        protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
            modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);

            base.OnModelCreating(modelBuilder);
        }

        public static Dances DanceLibrary
        {
            get
            {
                return _dances;
            }
        }

        public Song CreateSong(UserProfile user, string title, string artist, string album, decimal? tempo)
        {
            DateTime time = DateTime.Now;

            Song song = Songs.Create();

            // Handle User association
            if (user != null)
            {
                user.Songs.Add(song);
                CreateSongProperty(song, "User", user.UserName);
            }

            // Handle Timestamps
            song.Created = time;
            song.Modified = time;
            CreateSongProperty(song, "Time", time.ToString());

            // Title
            Debug.Assert(!string.IsNullOrEmpty(title));
            song.Title = title;
            CreateSongProperty(song, "Title", title);

            // Artist
            if (!string.IsNullOrEmpty(artist))
            {
                song.Artist = artist;
                CreateSongProperty(song, "Artist", artist);
            }

            // Album
            if (!string.IsNullOrEmpty(album))
            {
                song.Album = title;
                CreateSongProperty(song, "Album", album);
            }

            // Tempo
            if (tempo != null)
            {
                song.Tempo = tempo;
                CreateSongProperty(song, "Tempo", tempo.ToString());
            }

            song.TitleHash = CreateTitleHash(title);

            song = Songs.Add(song);

            return song;
        }

        public static int CreateTitleHash(string title)
        {
            StringBuilder sb = new StringBuilder(title.Length);

            string norm = title.Normalize(NormalizationForm.FormD);
            foreach (char c in norm)
            {
                if (char.IsLetterOrDigit(c))
                {
                    char cNew = char.ToUpper(c);
                    sb.Append(cNew);
                }
            }

            string s = sb.ToString();
            int hash = s.GetHashCode();
            Debug.WriteLine("{0}\t{1}",hash,s);

            return hash;
        }

        public SongProperty CreateSongProperty(Song song, string name, string value)
        {
            SongProperty ret = SongProperties.Create();
            ret.Song = song;
            ret.Name = name;
            ret.Value = value;

            SongProperties.Add(ret);

            return ret;
        }

        private static Dances _dances = new Dances();
    }
}
