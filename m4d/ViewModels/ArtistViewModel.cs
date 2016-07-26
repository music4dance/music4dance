using System;
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

        public static ArtistViewModel Create(string name, DanceMusicService.CruftFilter cruft, DanceMusicService dms)
        {
            var songs = dms.FindArtist(name,cruft);

            return new ArtistViewModel {Name = name, Songs = songs.Take(100).ToList() };
        }
    }
}