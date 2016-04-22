using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using m4dModels;

namespace m4d.ViewModels
{
    public class ArtistViewModel
    {
        [Key]
        public string Name { get; set; }
        public IList<Song> Songs { get; set; }

        static public ArtistViewModel Create(string name, DanceMusicService.CruftFilter cruft, DanceMusicService dms)
        {
            var songs = dms.Songs.Where(s => s.Artist == name);
            if ((cruft & DanceMusicService.CruftFilter.NoDances) != DanceMusicService.CruftFilter.NoDances)
            {
                songs = songs.Where(s => s.DanceRatings.Any());
            }
            if ((cruft & DanceMusicService.CruftFilter.NoPublishers) != DanceMusicService.CruftFilter.NoPublishers)
            {
                songs = songs.Where(s => s.Purchase != null);
            }

            return new ArtistViewModel {Name = name, Songs = songs.Take(100).ToList() };
        }
    }
}