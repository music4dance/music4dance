using DanceLibrary;
using EFTracingProvider;
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
using System.Data.Objects.DataClasses;

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

        public Song CreateSong(UserProfile user, string title, string artist, string album, string label, decimal? tempo, string purchase)
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
                song.Album = album;
                CreateSongProperty(song, "Album", album);
            }

            // Label
            if (!string.IsNullOrEmpty(label))
            {
                song.Publisher = label;
                CreateSongProperty(song, "Publisher", label);
            }

            // Tempo
            if (tempo != null)
            {
                song.Tempo = tempo;
                CreateSongProperty(song, "Tempo", tempo.ToString());
            }

            // Purchase Info
            if (!string.IsNullOrEmpty(purchase))
            {
                song.Purchase = purchase;
                CreateSongProperty(song, "Purchase", purchase);
            }

            song.TitleHash = CreateTitleHash(title);

            song = Songs.Add(song);

            return song;
        }

        public bool EditSong(UserProfile user, Song song)
        {
            var properties = from p in SongProperties 
                             where p.SongId == song.SongId
                             orderby p.Id descending
                             select p;

            bool modified = false;

            // Handle User association
            if (user != null)
            {
                //Dump();
                if (user.Songs.FirstOrDefault(s => s.SongId == song.SongId) == null)
                {
                    user.Songs.Add(song);
                }
                //Dump();
                CreateSongProperty(song, "User", user.UserName);
                //Dump();
            }

            // Handle Timestamps
            DateTime time = DateTime.Now;
            song.Modified = time;
            CreateSongProperty(song, "Time", time.ToString());

            modified |= UpdateSongProperty(song, "Title", song.Title, properties);
            modified |= UpdateSongProperty(song, "Artist", song.Artist, properties);
            modified |= UpdateSongProperty(song, "Album", song.Album, properties);
            modified |= UpdateSongProperty(song, "Publisher", song.Publisher, properties);
            modified |= UpdateSongProperty(song, "Tempo", song.Tempo, properties);
            modified |= UpdateSongProperty(song, "Length", song.Length, properties);
            modified |= UpdateSongProperty(song, "Track", song.Track, properties);
            modified |= UpdateSongProperty(song, "Purchase", song.Purchase, properties);
            modified |= UpdateSongProperty(song, "Genre", song.Genre, properties);

            if (modified)
            {
                song.TitleHash = CreateTitleHash(song.Title);

                // This seems totally non-optimal, but because of the relationship between users
                //  and songs the old song record is getting loaded underneath the new one
                var songs = Songs.Local.Where(s => s.SongId == song.SongId).ToArray();
                foreach (Song s in songs)
                {
                    if (s != song)
                    {
                        ((IObjectContextAdapter)this).ObjectContext.Detach(s);
                    }
                }

                Entry(song).State = EntityState.Modified;

                SaveChanges();
            }
            else
            {
                // TODO: figure out how to undo the top couple changes if no substantive changes were made... (may just be do nothing here)
            }

            return modified;
        }

        //static private void DuplicationTestGraph(IEntityWithChangeTracker rootEntity)
        //{
        //    const string primaryKey = "Id";
        //    var graphObjects = new Dictionary<string, IList<Object>>();

        //    using (ChangeTrackerIterator iterator = ChangeTrackerIterator.Create(rootEntity))
        //    {
        //        iterator.ToList().ForEach(u =>
        //        {
        //            if (!graphObjects.Keys.Contains(u.GetType().Name))
        //                graphObjects.Add(u.GetType().Name, new List<object>());
        //            IList<object> tmpList;
        //            graphObjects.TryGetValue(u.GetType().Name, out tmpList);
        //            tmpList.Add(u);
        //        });

        //        foreach (IList<object> objectsList in graphObjects.Values)
        //        {
        //            foreach (object obj in objectsList)
        //            {
        //                foreach (object obj1 in objectsList)
        //                {
        //                    if (!obj1.Equals(obj))
        //                    {
        //                        int primaryKey1 = (int)obj.GetType().GetProperty(primaryKey).GetValue(obj, null);
        //                        int primaryKey2 = (int)obj1.GetType().GetProperty(primaryKey).GetValue(obj1, null);

        //                        if (primaryKey1 == primaryKey2)
        //                            throw new Exception("There are at least two instances of the class " + obj.GetType().Name +
        //                                                " which have the same primary key = " + primaryKey1 + ". Make sure that the key values are unique before calling AcceptChanges.");
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        public static int CreateTitleHash(string title)
        {
            StringBuilder sb = new StringBuilder(title.Length);

            string norm = title.Normalize(NormalizationForm.FormD);

            bool paren = false;
            foreach (char c in norm)
            {
                if (paren)
                {
                    if (c == ')')
                    {
                        paren = false;
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        char cNew = char.ToUpper(c);
                        sb.Append(cNew);
                    }
                    else if (c == '(')
                    {
                        paren = true;
                    }
                }
            }

            string s = sb.ToString();
            int hash = s.GetHashCode();
            //Debug.WriteLine("{0}\t{1}",hash,s);

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

        public bool UpdateSongProperty(Song song, string name, decimal? value, IOrderedQueryable<SongProperty> properties)
        {
            bool modified = true;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = properties.FirstOrDefault(p => p.Name == name);

            if (prop != null) {
                decimal oldValue;
                if (decimal.TryParse(prop.Value, out oldValue) && oldValue == value)
                {
                    modified = false;
                }
            }
            else if (value == null)
            {
                modified = false;
            }

            if (modified)
            {
                SongProperty np = SongProperties.Create();
                np.Song = song;
                np.Name = name;
                np.Value = value.ToString();

                SongProperties.Add(np);
            }

            return modified;
        }

        public bool UpdateSongProperty(Song song, string name, int? value, IOrderedQueryable<SongProperty> properties)
        {
            bool modified = true;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = properties.FirstOrDefault(p => p.Name == name);

            if (prop != null)
            {
                int oldValue;
                if (int.TryParse(prop.Value, out oldValue) && oldValue == value)
                {
                    modified = false;
                }
            }
            else if (value == null)
            {
                modified = false;
            }

            if (modified)
            {
                SongProperty np = SongProperties.Create();
                np.Song = song;
                np.Name = name;
                np.Value = value.ToString();

                SongProperties.Add(np);
            }

            return modified;
        }

        public bool UpdateSongProperty(Song song, string name, string value, IOrderedQueryable<SongProperty> properties)
        {
            bool modified = false;

            //SongProperty prop = properties.FirstOrDefault(p => string.Equals(p.Name,name,StringComparison.InvariantCultureIgnoreCase));
            SongProperty prop = properties.FirstOrDefault(p => p.Name == name);

            // We are going to create a new property if there wasn't a property before and this property is non-empty OR
            //  if there was a property before and the value is different.
            modified = (prop == null && !string.IsNullOrWhiteSpace(value)) || (prop != null && !string.Equals(prop.Value,value,StringComparison.CurrentCulture));

            if (modified)
            {
                SongProperty np = SongProperties.Create();
                np.Song = song;
                np.Name = name;
                np.Value = value;

                SongProperties.Add(np);
            }

            return modified;
        }

        public void Dump()
        {
            // TODO: Create a dump routine to help dump the object graph - definitely need object id of some kind (address)

            Debug.WriteLine("------------------- songs ------------------");
            foreach (Song song in Songs.Local)
            {
                song.Dump();
            }

            Debug.WriteLine("------------------- properties ------------------");
            foreach (SongProperty prop in SongProperties.Local)
            {
                prop.Dump();
            }

            Debug.WriteLine("------------------- users ------------------");
            foreach (UserProfile user in UserProfiles.Local)
            {
                user.Dump();
            }
        }

        private static Dances _dances = new Dances();
    }
}
