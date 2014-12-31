using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels.Tests
{
    class MockContext : IDanceMusicContext
    {
        class DanceSet : TestDbSet<Dance>
        {
            public override Dance Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                if (id == null)
                {
                    return null;
                }
                else
                {
                    return this.SingleOrDefault(d => d.Id == id);
                }
            }
        }

        class SongSet : TestDbSet<Song>
        {
            public override Song Find(params object[] keyValues)
            {
                var id = keyValues.Single() as Guid?;
                return id == null ? null : this.SingleOrDefault(s => s.SongId == id);
            }
        }
        class TagTypeSet : TestDbSet<TagType>
        {
            public override TagType Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                if (id == null)
                {
                    return null;
                }
                else
                {
                    return this.SingleOrDefault(tt => tt.Key == id);
                }
            }
        }

        class ApplicationUserSet : TestDbSet<ApplicationUser>
        {
            public override ApplicationUser Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                if (id == null)
                {
                    return null;
                }
                else
                {
                    return this.SingleOrDefault(u => u.Id == id);
                }
            }
        }

        public MockContext(bool seedUsers=true)
        {
            this.Songs = new SongSet();
            this.SongProperties = new TestDbSet<SongProperty>();
            this.Dances = new DanceSet();
            this.DanceRatings = new TestDbSet<DanceRating>();
            this.Tags = new TestDbSet<Tag>();
            this.TagTypes = new TagTypeSet();
            this.Log = new TestDbSet<SongLog>();
            this.Modified = new TestDbSet<ModifiedRecord>();
            this.Users = new ApplicationUserSet();
            this.Roles = new TestDbSet<IdentityRole>();

            if (seedUsers)
            {
                Users.Add(new ApplicationUser() {UserName="dwgray", Id="05849D25-0292-44CF-A3E6-74D07D94855C"});
                Users.Add(new ApplicationUser() {UserName="batch", Id="DE3752CA-42CD-46FB-BEE9-F7163CFB091B"});
            }
        }

        #region Events
        //protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        //{
        //    DanceMusicContextHelpers.ModelCreating(modelBuilder);
        //}
        #endregion

        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        public DbSet<Tag> Tags { get; set; }

        public DbSet<TagType> TagTypes { get; set; }

        public DbSet<SongLog> Log { get; set; }

        public DbSet<ModifiedRecord> Modified { get; set; }

        public IDbSet<ApplicationUser> Users { get; set; }

        public IDbSet<IdentityRole> Roles { get; set; }

        public int SaveChangesCount { get; private set; } 

        public int SaveChanges()
        {
            this.SaveChangesCount++;
            return 1;
        }
        public void TrackChanges(bool track)
        {
            // NOOP?
        }

        public ApplicationUser FindOrAddUser(string name, string role, object umanager)
        {
            ApplicationUser user = Users.FirstOrDefault(u => string.Equals(u.UserName, name, StringComparison.InvariantCultureIgnoreCase)); 

            if (user == null)
            {
                user = new ApplicationUser() { UserName = name, Id = Guid.NewGuid().ToString("D") };
                Users.Add(user);
            }

            // TODO: Should we add a concept of roles into the mock????

            return user;
        }

        public void Dispose()
        {
        }
    }
}
