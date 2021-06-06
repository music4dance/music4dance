using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels
{
    public class AlbumViewModel : SongListModel
    {
        [Key] public string Title { get; set; }
        public string Artist { get; set; }

        public static AlbumViewModel Create(
            string title, string user,
            IMapper mapper,
            DanceMusicCoreService.CruftFilter cruft, DanceMusicService dms)
        {
            var songs = dms.FindAlbum(title, cruft);

            var map = new Dictionary<int, Song>();
            var max = 0;
            var floor = -1;

            string artist = null;
            var uniqueArtist = true;

            string albumTitle = null;

            foreach (var song in songs)
            {
                var album = song.AlbumFromTitle(title);
                if (album == null) continue;

                int track;
                if (!album.Track.HasValue || album.Track.Value == 0 ||
                    map.ContainsKey(album.Track.Value))
                {
                    track = floor;
                    floor -= 1;
                }
                else
                {
                    track = album.Track.Value;
                }

                map.Add(track, song);
                max = Math.Max(max, track);

                if (artist == null && !string.IsNullOrWhiteSpace(song.Artist))
                    artist = Song.CreateNormalForm(song.Artist);
                else if (uniqueArtist)
                    if (!string.Equals(Song.CreateNormalForm(song.Artist), artist,
                        StringComparison.InvariantCultureIgnoreCase))
                        uniqueArtist = false;

                if (albumTitle == null) albumTitle = album.Name;

                // Just keep the album that we're indexing on
                song.Albums = new List<AlbumDetails> {album};
            }

            var list = new List<Song>();
            // First add in the tracks that have valid #'s in order
            for (var i = 0; i <= max; i++)
                if (map.TryGetValue(i, out var song))
                    list.Add(song);
            // Then append the tracks that either don't have a number or are dups
            for (var i = -1; i > floor; i--)
                if (map.TryGetValue(i, out var song))
                    list.Add(song);

            return new AlbumViewModel
            {
                Title = albumTitle ?? title,
                UserName = user,
                Artist = uniqueArtist && list.Count > 0 ? list[0].Artist : string.Empty,
                Filter = mapper.Map<SongFilterSparse>(new SongFilter {Action = "Album"}),
                Songs = list.Select(mapper.Map<SongSparse>).ToList(),
                Histories = list.Select(s => s.GetHistory(mapper)).ToList()
            };
        }
    }
}