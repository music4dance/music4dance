using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using m4dModels;

namespace m4d.ViewModels
{
    public class AlbumViewModel
    {
        [Key]
        public string Title {get;set;}
        public string Artist {get;set;}
        public IList<SongDetails> Songs {get;set;}

        static public AlbumViewModel Create(string title, DanceMusicService dms)
        {
            // TODO: if we really don't have distinct in linq syntax should probably use function syntax for the whole thing...
            var ids = (from sp in dms.SongProperties
                where sp.Name.StartsWith(SongBase.AlbumField) && sp.Value == title
                select sp.SongId).Distinct();
            var songs = from s in dms.Songs
                where ids.Contains(s.SongId)
                select s;

            var map = new Dictionary<int,SongDetails>();
            var max = 0;
            var floor = -1;

            string artist = null;
            var uniqueArtist = true;

            string albumTitle = null;

            foreach (var song in songs)
            {
                var sd = new SongDetails(song);
                var album = sd.AlbumFromTitle(title);
                if (album == null) continue;

                int track;
                if (!album.Track.HasValue || album.Track.Value == 0 || map.ContainsKey(album.Track.Value))
                {
                    track = floor;
                    floor -= 1;
                }
                else
                {
                    track = album.Track.Value;
                }

                map.Add(track, sd);
                max = Math.Max(max, track);

                if (artist == null && !string.IsNullOrWhiteSpace(sd.Artist))
                {
                    artist = SongBase.CreateNormalForm(sd.Artist);
                }
                else if (uniqueArtist)
                {
                    if (!string.Equals(SongBase.CreateNormalForm(sd.Artist), artist, StringComparison.InvariantCultureIgnoreCase))
                    {
                        uniqueArtist = false;
                    }
                }

                if (albumTitle == null)
                {
                    albumTitle = album.Name;
                }
            }

            var list = new List<SongDetails>();
            // First add in the tracks that have valid #'s in order
            for (var i = 0; i <= max; i++)
            {
                SongDetails sd;
                if (map.TryGetValue(i,out sd))
                {
                    list.Add(sd);
                }
            }
            // Then append the tracks that either don't have a number or are dups
            for (var i = -1; i > floor; i--)
            {
                SongDetails sd;
                if (map.TryGetValue(i, out sd))
                {
                    list.Add(sd);
                }
            }

            if (list.Count > 0)
            {
                var viewModel = new AlbumViewModel { Title = albumTitle, Artist = uniqueArtist ? list[0].Artist : string.Empty, Songs = list };
                return viewModel;
            }
            return null;
        }
    }
}