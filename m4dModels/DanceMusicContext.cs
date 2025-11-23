using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;

using System.Diagnostics;

namespace m4dModels
{
    public class DanceMusicContext(DbContextOptions<DanceMusicContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public bool AutoDetectChangesEnabled
        {
            get => ChangeTracker.AutoDetectChangesEnabled;
            set => ChangeTracker.AutoDetectChangesEnabled = value;
        }

        private string ConnectionString { get; } = options.FindExtension<SqlServerOptionsExtension>()?.ConnectionString;

        public DanceMusicContext CreateTransientContext()
        {
            if (ConnectionString == null)
            {
                throw new Exception("Cannot create a new dbcontext from a test context");
            }

            var builder = new DbContextOptionsBuilder<DanceMusicContext>();
            _ = builder.UseSqlServer(ConnectionString);
            _ = builder.EnableSensitiveDataLogging();

            return new DanceMusicContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            _ = builder.Entity<Dance>().ToTable("Dances");
            _ = builder.Entity<DanceLink>().ToTable("DanceLink");
            _ = builder.Entity<PlayList>().ToTable("PlayLists");
            _ = builder.Entity<Search>().ToTable("Searches");
            _ = builder.Entity<TagGroup>().ToTable("TagGroups");
            _ = builder.Entity<ActivityLog>().ToTable("ActivityLog");
            _ = builder.Entity<UsageLog>().ToTable("UsageLog");

            _ = builder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            _ = builder.Entity<Dance>().Ignore(dance => dance.Info);

            _ = builder.Entity<TagGroup>().HasKey(tt => tt.Key);
            _ = builder.Entity<TagGroup>().Ignore(tt => tt.Count);
            _ = builder.Entity<TagGroup>().Ignore(tt => tt.Value);
            _ = builder.Entity<TagGroup>().Ignore(tt => tt.Category);
            _ = builder.Entity<TagGroup>().Ignore(tt => tt.Children);

            _ = builder.Entity<DanceLink>().HasKey(dl => dl.Id);
            _ = builder.Entity<DanceLink>().Property(dl => dl.Id).ValueGeneratedNever();

            _ = builder.Entity<ApplicationUser>().Property(u => u.Region).HasMaxLength(2);
            _ = builder.Entity<ApplicationUser>().Property(u => u.ServicePreference).HasMaxLength(10);
            _ = builder.Entity<ApplicationUser>().Property(u => u.LifetimePurchased).HasPrecision(18, 2);
            _ = builder.Entity<ApplicationUser>().Property(u => u.HitCount).HasDefaultValue(0);

            _ = builder.Entity<Search>().Property(u => u.Query).IsRequired();
            _ = builder.Entity<Search>().Ignore(u => u.Filter);

            _ = builder.Entity<UsageLog>().Property(u => u.UsageId).IsRequired();
            _ = builder.Entity<UsageLog>().Property(u => u.UsageId).HasMaxLength(40);
            _ = builder.Entity<UsageLog>().Property(u => u.UserName);
            _ = builder.Entity<UsageLog>().Property(u => u.Page).HasMaxLength(100);
            _ = builder.Entity<UsageLog>().Property(u => u.Query).HasMaxLength(256);
            _ = builder.Entity<UsageLog>().Property(u => u.Filter).HasMaxLength(256);
            _ = builder.Entity<UsageLog>().Property(u => u.Referrer).HasMaxLength(1024);
            _ = builder.Entity<UsageLog>().Property(u => u.UserAgent).HasMaxLength(256);

            _ = builder.Entity<UsageLog>().HasIndex(u => u.UserName);
            _ = builder.Entity<UsageLog>().HasIndex(u => u.UsageId);

            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);
        }


        public override async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            int ret;
            try
            {
                ret = await base.SaveChangesAsync(cancellationToken);
            }
            catch (Exception e) /* DbEntityValidationException */
            {
                // CORETODO: Figure out if we can get to better error handling
                Trace.WriteLineIf(
                    TraceLevels.General.TraceError,
                    $"Failed on SaveChanges {e.Message}");

                //foreach (var err in e.EntityValidationErrors)
                //{
                //    foreach (var ve in err.ValidationErrors)
                //    {
                //        Trace.WriteLineIf(TraceLevels.General.TraceError, ve.ErrorMessage);
                //    }
                //}

                Debug.Assert(false);
                throw;
            }

            return ret;
        }

        public async Task<List<Dance>> LoadDances()
        {
            return await Dances.Include(d => d.DanceLinks).ToListAsync();
        }

        #region Properties

        public DbSet<Dance> Dances { get; set; }
        public DbSet<DanceLink> DanceLinks { get; set; }
        public DbSet<TagGroup> TagGroups { get; set; }
        public DbSet<Search> Searches { get; set; }
        public DbSet<PlayList> PlayLists { get; set; }
        public DbSet<ActivityLog> ActivityLog { get; set; }

        public DbSet<UsageLog> UsageLog { get; set; }
        #endregion
    }
}
