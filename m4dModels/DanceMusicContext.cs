using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;

namespace m4dModels
{
    public class DanceMusicContext : IdentityDbContext<ApplicationUser>
    {
        public DanceMusicContext(DbContextOptions<DanceMusicContext> options)
            : base(options)
        {
            ConnectionString = options.FindExtension<SqlServerOptionsExtension>()?.ConnectionString;
        }

        public bool AutoDetectChangesEnabled
        {
            get => ChangeTracker.AutoDetectChangesEnabled;
            set => ChangeTracker.AutoDetectChangesEnabled = value;
        }

        private string ConnectionString { get; }

        public DanceMusicContext CreateTransientContext()
        {
            if (ConnectionString == null)
            {
                throw new Exception("Cannot create a new dbcontext from a test context");
            }

            var builder = new DbContextOptionsBuilder<DanceMusicContext>();
            builder.UseSqlServer(ConnectionString);
            builder.EnableSensitiveDataLogging();

            return new DanceMusicContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Dance>().ToTable("Dances");
            builder.Entity<DanceLink>().ToTable("DanceLink");
            builder.Entity<PlayList>().ToTable("PlayLists");
            builder.Entity<Search>().ToTable("Searches");
            builder.Entity<TagGroup>().ToTable("TagGroups");
            builder.Entity<ActivityLog>().ToTable("ActivityLog");
            builder.Entity<UsageLog>().ToTable("UsageLog");
            builder.Entity<UsageSummary>().HasNoKey();

            builder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
            builder.Entity<Dance>().Ignore(dance => dance.Info);

            builder.Entity<TagGroup>().HasKey(tt => tt.Key);
            builder.Entity<TagGroup>().Ignore(tt => tt.Count);
            builder.Entity<TagGroup>().Ignore(tt => tt.Value);
            builder.Entity<TagGroup>().Ignore(tt => tt.Category);
            builder.Entity<TagGroup>().Ignore(tt => tt.Children);

            builder.Entity<DanceLink>().HasKey(dl => dl.Id);
            builder.Entity<DanceLink>().Property(dl => dl.Id).ValueGeneratedNever();

            builder.Entity<ApplicationUser>().Property(u => u.Region).HasMaxLength(2);
            builder.Entity<ApplicationUser>().Property(u => u.ServicePreference).HasMaxLength(10);
            builder.Entity<ApplicationUser>().Property(u => u.LifetimePurchased).HasPrecision(18,2);
            builder.Entity<ApplicationUser>().Property(u => u.HitCount).HasDefaultValue(0);

            builder.Entity<Search>().Property(u => u.Query).IsRequired();
            builder.Entity<Search>().Ignore(u => u.Filter);

            builder.Entity<UsageLog>().Property(u => u.UsageId).IsRequired();
            builder.Entity<UsageLog>().Property(u => u.UsageId).HasMaxLength(40);
            builder.Entity<UsageLog>().Property(u => u.UserName);
            builder.Entity<UsageLog>().Property(u => u.Page).HasMaxLength(100);
            builder.Entity<UsageLog>().Property(u => u.Query).HasMaxLength(256);
            builder.Entity<UsageLog>().Property(u => u.Filter).HasMaxLength(256);
            builder.Entity<UsageLog>().Property(u => u.Referrer).HasMaxLength(1024);
            builder.Entity<UsageLog>().Property(u => u.UserAgent).HasMaxLength(256);

            builder.Entity<UsageLog>().HasIndex(u => u.UserName);
            builder.Entity<UsageLog>().HasIndex(u => u.UsageId);

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
        public DbSet<UsageSummary> UsageSummary { get; set; }
        #endregion
    }
}
