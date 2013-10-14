using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using SongDatabase.Models;

namespace music4dance.ViewModels
{
    public class SongMerge
    {
        public string Name {get;set;}
        public List<Song> Songs {get; set;}
        public List<DanceMerge> Ratings { get; set; }
        public List<SongPropertyMerge> Properties {get; set;}
        public string SongIds 
        {
            get {return string.Join(",",Songs.Select(s => s.SongId));}
        }

        public SongMerge(List<Song> songs)
        {
            Songs = songs;
            Properties = new List<SongPropertyMerge>();

            // Consider trying to sort the song list by the number of defaults...

            // Create a merge table of basic properties
            foreach (string field in _mergeFields)
            {
                SongPropertyMerge spm = new SongPropertyMerge() { Name = field, Values = new List<object>() };

                int defaultIdx = -1;
                string fsCur = null;
                int cTotal = 0;
                int cMatch = 0;

                foreach (Song song in songs)
                {
                    object fo = song.GetType().GetProperty(field).GetValue(song, null);

                    spm.Values.Add(fo);

                    string fs = null;
                    if (fo != null)
                    {
                        fs = fo.ToString();
                        if (string.IsNullOrWhiteSpace(fs))
                            fs = null;
                    }

                    if (fsCur == null && fs != null)
                    {
                        fsCur = fs;
                        cMatch = 1;
                        defaultIdx = cTotal;
                    }
                    else if (string.Equals(fsCur, fs, StringComparison.Ordinal))
                    {
                        cMatch += 1;
                    }

                    cTotal += 1;
                }

                if (cTotal == cMatch)
                    spm.Selection = -1;
                else
                    spm.Selection = defaultIdx;

                Properties.Add(spm);
            }

            // Create list of dances that can be merged
            Ratings = new List<DanceMerge>();

            int idx = 0;
            foreach (Song song in songs)
            {
                foreach (DanceRating dr in song.DanceRatings)
                {
                    DanceMerge dm = new DanceMerge()
                    {
                        DanceId = dr.DanceId,
                        DanceName = dr.Dance.Info.Name,
                        SongIdx = idx,
                        Weight = dr.Weight,
                        Keep = true
                    };

                    Ratings.Add(dm);
                }
                idx += 1;
            }
        }

        static string[] _mergeFields = { 
            DanceMusicContext.TitleField, 
            DanceMusicContext.ArtistField, 
            DanceMusicContext.AlbumField, 
            DanceMusicContext.TempoField, 
            DanceMusicContext.PublisherField, 
            DanceMusicContext.GenreField, 
            DanceMusicContext.TrackField, 
            DanceMusicContext.LengthField };
    }

    public class SongPropertyMerge
    {
        public string Name { get; set; }
        public int Selection { get; set; }
        public List<object> Values { get; set; }
    }

    public class DanceMerge
    {
        public string DanceId { get; set; }
        public string DanceName { get; set; }
        public int SongIdx { get; set; }
        public int Weight { get; set; }
        public bool Keep { get; set; }
    }
}