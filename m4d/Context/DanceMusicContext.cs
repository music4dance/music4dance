using System;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
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

        #endregion

        #region Properties
        public DbSet<Dance> Dances { get; set; }
        public DbSet<TagGroup> TagGroups { get; set; }
        public DbSet<Search> Searches { get; set; }
        public DbSet<PlayList> PlayLists { get; set; }
        #endregion

        #region Events
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Properties<DateTime>().Configure(c => c.HasColumnType("datetime2"));

            modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);

            //modelBuilder.Entity<TaggableObject>().HasKey(to => to.TagIdBase);
            modelBuilder.Entity<TagGroup>().HasKey(tt => tt.Key);
            modelBuilder.Entity<TagGroup>().Ignore(tt => tt.Count);
            modelBuilder.Entity<TagGroup>().Ignore(tt => tt.Value);
            modelBuilder.Entity<TagGroup>().Ignore(tt => tt.Category);
            modelBuilder.Entity<TagGroup>().Ignore(tt => tt.Children);
            modelBuilder.Entity<TagGroup>().HasOptional(x => x.Primary).WithMany().HasForeignKey(x => x.PrimaryId);

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

            if (Equals(role, DanceMusicService.PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else if (!uman.IsInRole(user.Id, role))
            {
                uman.AddToRole(user.Id, role);
            }

            var ctxtUser = Users.Find(user.Id); //Users.FirstOrDefault(u => string.Equals(u.UserName, name, StringComparison.InvariantCultureIgnoreCase)); 
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

        public void LoadDances()
        {
            Configuration.LazyLoadingEnabled = false;

            Dances.Include("DanceLinks").Load();

            Configuration.LazyLoadingEnabled = true;
        }

        #endregion
    }
}