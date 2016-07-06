using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using m4d.Controllers;
using m4dModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using EntityState = System.Data.Entity.EntityState;

// Let's see if we can mock up a recoverable log file by spitting out
// something resembling a tab-separated flat list of songs items with a
// command associated with each line.  Might add a checkpoint command
// into the songproperties table as well...

// COMMAND  User    Title   Artist  Album   Publisher   Tempo   Length  Track   Genre   Purchase    DanceRating Custom

// Kill Publisher Track Purchase -> do these move to custom


namespace m4d.Context
{
    public class DanceMusicFactory : IDanceMusicFactory
    {
        public DanceMusicService CreateDisconnectedService()
        {
            var context = DanceMusicContext.Create();
            return new DanceMusicService(context, ApplicationUserManager.Create(null, context));
        }
    }

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

        //private static DbConnection CreateConnection(string nameOrConnectionString)
        //{
        //    var connectionStringSetting =
        //        ConfigurationManager.ConnectionStrings[nameOrConnectionString];
        //    string connectionString;
        //    string providerName;

        //    if (connectionStringSetting != null)
        //    {
        //        connectionString = connectionStringSetting.ConnectionString;
        //        providerName = connectionStringSetting.ProviderName;
        //    }
        //    else
        //    {
        //        providerName = "System.Data.SqlClient";
        //        connectionString = nameOrConnectionString;
        //    }

        //    return CreateConnection(connectionString, providerName);
        //}

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

            if (String.Equals(role, DanceMusicService.PseudoRole))
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
                    case "TopN":
                        RemoveEntities<TopN>();
                        break;
                }
            }
        }
        public void LoadDances(bool includeRatings=false)
        {
            Configuration.LazyLoadingEnabled = false;

            if (includeRatings)
            {
                Dances.Include("DanceLinks").Include("TopSongs.Song.DanceRatings").Load();
            }
            else
            {
                Dances.Include("DanceLinks").Load();
            }

            Configuration.LazyLoadingEnabled = true;
        }

        private void RemoveEntities<T>() where T : class
        {
            var list = Set<T>().Local.ToList();
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
    }
}