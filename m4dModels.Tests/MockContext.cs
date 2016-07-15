using System;
using System.Data.Entity;
using System.Linq;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace m4dModels.Tests
{
    internal class MockContext : IDanceMusicContext
    {
        public static DanceMusicService CreateService(bool seedUsers)
        {
            // TODO: Hopefully we can load from json at some point
            var context = new MockContext(seedUsers);
            var umanager = new UserManager<ApplicationUser>(new MockUserStore(context));
            var service = new DanceMusicService(context, umanager);
            service.SeedDances();
            return service;
        }

        private class DanceSet : TestDbSet<Dance>
        {
            public override Dance Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                return id == null ? null : this.SingleOrDefault(d => d.Id == id);
            }
        }

        private class ApplicationUserSet : TestDbSet<ApplicationUser>
        {
            public override ApplicationUser Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                return id == null ? null : this.SingleOrDefault(u => u.Id == id);
            }
        }

        public MockContext(bool seedUsers = true)
        {
            Dances = new DanceSet();
            TagTypes = new TagTypeSet();
            Searches = new SearchSet();
            Users = new ApplicationUserSet();
            Roles = new TestDbSet<IdentityRole>();

            if (!seedUsers) return;

            Users.Add(new ApplicationUser {UserName = "dwgray", Id = "05849D25-0292-44CF-A3E6-74D07D94855C"});
            Users.Add(new ApplicationUser {UserName = "batch", Id = "DE3752CA-42CD-46FB-BEE9-F7163CFB091B"});
            Users.Add(new ApplicationUser { UserName = "batch-a", Id = "09bae0b2-e15f-4e2e-8131-cf72138244c3"});
            Users.Add(new ApplicationUser { UserName = "batch-e", Id = "42d8928c-82da-4937-a6f1-b67dddd6d7b3" });
            Users.Add(new ApplicationUser { UserName = "batch-i", Id = "11be8632-e6ce-4bc9-9a5a-f334a14d55ef" });
            Users.Add(new ApplicationUser { UserName = "batch-s", Id = "ae8ead37-25d9-441f-9bb6-d48a1e8fb7f8"});
            Users.Add(new ApplicationUser { UserName = "batch-x", Id = "64903fbf-9fd1-4ea4-9586-3c465ab463da"});
            Users.Add(new ApplicationUser { UserName = "DWTS", Id = "bdac9b0a-085f-40ff-a510-a578300fd663"});
            Users.Add(new ApplicationUser { UserName = "Charlie", Id = "ef3c470d-a282-4e8e-918c-9a9d123752ee" });
            Users.Add(new ApplicationUser { UserName = "ohdwg", Id = "e86a1e4e-b9ae-4201-acce-ce4f3642bb80" });
        }

        #region Events

        //protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        //{
        //    DanceMusicContextHelpers.ModelCreating(modelBuilder);
        //}

        #endregion


        public DbSet<Dance> Dances { get; set; }


        public DbSet<TagType> TagTypes { get; set; }


        public DbSet<Search> Searches { get; set; }

        public IDbSet<ApplicationUser> Users { get; set; }

        public IDbSet<IdentityRole> Roles { get; set; }
        public Database Database { get { throw new NotImplementedException(); } }

        public int SaveChangesCount { get; private set; }

        public int SaveChanges()
        {
            SaveChangesCount++;
            return 1;
        }

        public void TrackChanges(bool track)
        {
            // NOOP?
        }

        public bool LazyLoadingEnabled
        {
            get { return true; }
            set { var t = value; }
        }

        public bool ProxyCreationEnabled
        {
            get { return true; }
            set { var t = value; }
        }

        public void LoadDances(bool includeRatings=true) { }

        public void Dispose()
        {
        }
    }
}
