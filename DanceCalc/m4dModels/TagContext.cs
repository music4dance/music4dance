using System;
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

        public DbSet<SongProperty> SongProperties { get { return null; }}

        public DbSet<Dance> Dances
        {
            get { return _context.Dances; }
        }

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
            throw new NotImplementedException();
        }

        public void TrackChanges(bool track)
        {
            throw new NotImplementedException();
        }

        public void CheckpointSongs()
        {
            throw new NotImplementedException();
        }

        public void LoadDances()
        {
            throw new NotImplementedException();
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
