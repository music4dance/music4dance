using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 

namespace m4dModels
{
    public interface IDanceMusicContext: IUserMap, IDisposable
    {
        DbSet<Song> Songs { get;  }

        DbSet<SongProperty> SongProperties { get;  }

        DbSet<Dance> Dances { get; }

        DbSet<DanceRating> DanceRatings { get; }

        DbSet<Tag> Tags { get; }

        DbSet<TagType> TagTypes { get; }

        DbSet<SongLog> Log { get; }

        DbSet<ModifiedRecord> Modified { get; }

        int SaveChanges();
    }
}
