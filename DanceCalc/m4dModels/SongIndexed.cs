using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DanceLibrary;

namespace m4dModels
{
    public class SongIndexed
    {
        public SongIndexed(SongDetails song)
        {
            SongId = song.SongId;
            // First copy the scalar properties
            for (var i = 0; i < ScalarProperties.Length; i++)
            {
                var piDst = ScalarProperties[i];
                if (piDst == null) continue;

                var piSrc = SongBase.ScalarProperties[i];
                var v = piSrc.GetValue(song);
                piDst.SetValue(this, v);
            }

            Created = song.Created;
            Modified = song.Modified;

            // Then set up the purchase flags
            var purchase = string.IsNullOrWhiteSpace(song.Purchase) ? new List<string>() : song.Purchase.ToCharArray().Select(c => MusicService.GetService(c).Name).ToList();
            if (song.HasSample) purchase.Add("Sample");
            if (song.HasEchoNest) purchase.Add("EchoNest");
            Purchase = purchase.ToArray();

            // Next grab the albums
            Albums = song.Albums.Select(ad => ad.Name).ToArray();

            // Then the users
            Users = song.ModifiedBy.Select(m => m.UserName).ToArray();

            // And finally the tags
            var genre = song.TagSummary.GetTagSet("Music");
            var other = song.TagSummary.GetTagSet("Other");
            var tempo = song.TagSummary.GetTagSet("Tempo");
            var style = new HashSet<string>();

            var dance = song.TagSummary.GetTagSet("Dance");
            var inferred = new HashSet<string>();

            foreach (var dr in song.DanceRatings)
            {
                var d = Dances.Instance.DanceFromId(dr.DanceId).Name.ToLower();
                if (!dance.Contains(d))
                {
                    inferred.Add(d);
                }
                other.UnionWith(dr.TagSummary.GetTagSet("Other"));
                tempo.UnionWith(dr.TagSummary.GetTagSet("Tempo"));
                style.UnionWith(dr.TagSummary.GetTagSet("Style"));
            }

            DanceTags = dance.ToArray();
            DanceTagsInferred = inferred.ToArray();
            GenreTags = genre.ToArray();
            TempoTags = tempo.ToArray();
            StyleTags = style.ToArray();
        }

        public Guid SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public int? Length { get; set; }
        public string[] Purchase { get; set; }
        public float? Danceability { get; set; }
        public float? Energy { get; set; }
        public float? Valence { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public string[] Albums { get; set; }
        public string[] Users { get; set; }

        public string [] DanceTags { get; set; }
        public string [] DanceTagsInferred { get; set; }
        public string [] GenreTags { get; set; }
        public string [] StyleTags { get; set; }
        public string [] TempoTags { get; set; }
        public string [] OtherTags { get; set; }

        // TODO: We should seriously consider if we can abastract the taggable object stuff into
        // and interface + helper class so that we can share a base class between SongBase & SongIndexed
        public static readonly PropertyInfo[] ScalarProperties = {
            typeof(SongIndexed).GetProperty(SongBase.TitleField),
            typeof(SongIndexed).GetProperty(SongBase.ArtistField),
            typeof(SongIndexed).GetProperty(SongBase.TempoField),
            typeof(SongIndexed).GetProperty(SongBase.LengthField),
            null,
            typeof(SongIndexed).GetProperty(SongBase.DanceabilityField),
            typeof(SongIndexed).GetProperty(SongBase.EnergyField),
            typeof(SongIndexed).GetProperty(SongBase.ValenceFiled),
        };
    }
}
