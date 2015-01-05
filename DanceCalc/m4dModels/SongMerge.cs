using System;
using System.Collections.Generic;
using System.Linq;

namespace m4dModels
{
    public class SongMerge
    {
        public string Name {get;set;}
        public List<SongDetails> Songs {get; set;}
        public List<DanceMerge> Ratings { get; set; }
        public string Tags { get; set; }
        public List<SongPropertyMerge> Properties {get; set;}
        public string SongIds 
        {
            get {return string.Join(",",Songs.Select(s => s.SongId));}
        }

        public SongMerge(List<Song> songs)
        {
            Songs = songs.Select(s => new SongDetails(s)).ToList();

            Properties = new List<SongPropertyMerge>();

            // Consider trying to sort the song list by the number of defaults...

            // Create a merge table of basic properties

            foreach (string field in _mergeFields)
            {
                // Slightly kdlugy, but for now we're allowing alternates only for album so do a direct compare
                bool allowAlternates = field.EndsWith("List");

                SongPropertyMerge spm = new SongPropertyMerge() { Name = field, AllowAlternates = allowAlternates, Values = new List<object>() };

                int defaultIdx = -1;
                string fsCur = null;
                int cTotal = 0;
                int cMatch = 0;

                foreach (SongDetails song in Songs)
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
                {
                    if (allowAlternates)
                    {
                        spm.Selection = 0;
                    }
                    else
                    {
                        spm.Selection = -1;
                    }
                }
                else
                {
                    spm.Selection = defaultIdx;
                }
                    

                Properties.Add(spm);
            }

            // Create lists of dances and tags that can be merged
            Ratings = new List<DanceMerge>();
            Tags = string.Empty;

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

        static readonly string[] _mergeFields = { 
            SongBase.TitleField, 
            SongBase.ArtistField, 
            SongBase.AlbumListField, 
            SongBase.TempoField, 
            SongBase.LengthField 
        };

    }
}