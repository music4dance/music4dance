using System;
using System.Collections.Generic;
using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace m4dModels
{
    public interface IDanceMusicFactory
    {
        DanceMusicService CreateDisconnectedService();
    }

    public interface IDanceMusicContext: IDisposable
    {
        DbSet<Dance> Dances { get; }
        DbSet<TagType> TagTypes { get; }
        DbSet<Search> Searches { get; }
        IDbSet<ApplicationUser> Users { get; }
        IDbSet<IdentityRole> Roles { get; }
        Database Database { get; }
        int SaveChanges();
        void TrackChanges(bool track);

        void LoadDances(bool inclueRatings=true);

        bool LazyLoadingEnabled { get; set; }
        bool ProxyCreationEnabled { get; set; }
    }

    //public static class DanceMusicContextHelpers
    //{
    //    public static void ModelCreating(this IDanceMusicContext dmc, System.Data.Entity.DbModelBuilder modelBuilder)
    //    {
    //        modelBuilder.Properties<DateTime>().Configure(c => c.HasColumnType("datetime2"));

    //        modelBuilder.Entity<Song>().Property(song => song.Tempo).HasPrecision(6, 2);
    //        modelBuilder.Entity<Song>().Ignore(song => song.CurrentLog);
    //        modelBuilder.Entity<Song>().Ignore(song => song.AlbumName);
    //        modelBuilder.Entity<Dance>().Property(dance => dance.Id).HasMaxLength(5);
    //        modelBuilder.Entity<Dance>().Ignore(dance => dance.Info);
    //        modelBuilder.Entity<DanceRating>().HasKey(dr => new { dr.SongId, dr.DanceId });

    //        modelBuilder.Entity<TagType>().HasKey(tt => tt.Key);
    //        modelBuilder.Entity<TagType>().Ignore(tt => tt.Value);
    //        modelBuilder.Entity<TagType>().Ignore(tt => tt.Category);


    //        modelBuilder.Entity<ModifiedRecord>().HasKey(t => new { t.ApplicationUserId, t.SongId });
    //        modelBuilder.Entity<ModifiedRecord>().Ignore(mr => mr.UserName);
    //        modelBuilder.Entity<DanceLink>().HasKey(dl => dl.Id);
    //    }
    //}
}
