using System;
using System.Collections.Generic;
using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace m4dModels
{
    public class TagContext : IDanceMusicContext
    {

        public DbSet<Song> Songs
        {
            get { throw new NotImplementedException(); }
        }

        public DbSet<SongProperty> SongProperties => null;

        public DbSet<Dance> Dances => _context.Dances;

        public DbSet<DanceRating> DanceRatings
        {
            get { throw new NotImplementedException(); }
        }

        public DbSet<Tag> Tags { get; set; }

        public DbSet<TagType> TagTypes { get; set; }

        public DbSet<SongLog> Log
        {
            get { throw new NotImplementedException(); }
        }

        public DbSet<ModifiedRecord> Modified
        {
            get { throw new NotImplementedException(); }
        }
        public DbSet<Search> Searches { get {throw new NotImplementedException();} }

        public IDbSet<ApplicationUser> Users
        {
            get { throw new NotImplementedException(); }
        }

        public IDbSet<IdentityRole> Roles
        {
            get { throw new NotImplementedException(); }
        }

        public int SaveChanges()
        {
            return 0;
        }

        public void TrackChanges(bool track)
        {
        }

        public void CheckpointSongs()
        {
        }

        public void ClearEntities(IEnumerable<string> entities)
        {
            
        }
        public void LoadDances()
        {
            throw new NotImplementedException();
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

        public static DanceMusicService CreateService(DanceMusicService realService)
        {
            var context = new TagContext(realService.Context);
            var service = new DanceMusicService(context, realService.UserManager);
            return service;
        }

        public TagContext(IDanceMusicContext realContext)
        {
            _context = realContext;

            //SongProperties = new TestDbSet<SongProperty>();
            Tags = new TestDbSet<Tag>();
            TagTypes = new TagTypeSet();
        }

        private readonly IDanceMusicContext _context;

        public void Dispose()
        {
        }

    }
}
