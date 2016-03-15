using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DanceLibrary;
using Microsoft.Azure.Search.Models;

namespace m4dModels
{
    public class SongIndexed
    {
        public SongIndexed()
        {
            
        }

        public SongIndexed(SongDetails song)
        {
            // First copy the scalar properties

            SongId = song.SongId;
            Title = song.Title;
            Artist = song.Artist;
            Length = song.Length;
            Beat = song.Danceability;
            Energy = song.Energy;
            Mood = song.Valence;

            Created = song.Created;
            Modified = song.Modified;

            Sample = song.Sample;

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
            OtherTags = other.ToArray();
        }

        public Guid SongId { get; set; }
        public double? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public int? Length { get; set; }
        public string[] Purchase { get; set; }
        public double? Beat { get; set; }
        public double? Energy { get; set; }
        public double? Mood { get; set; }
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
        public string Sample { get; set; }

        public static Index Index => new Index
        {
            Name = "songs",
            Fields = new []
            {
                new Field("SongId", DataType.String) {IsKey = true},
                new Field("Title", DataType.String) {IsSearchable = true, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Artist", DataType.String) {IsSearchable = true, IsSortable = true, IsFilterable = false, IsFacetable = false},
                new Field("Albums", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("Users", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("Created", DataType.DateTimeOffset) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Modified", DataType.DateTimeOffset) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Tempo", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Length", DataType.Int32) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Beat", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Energy", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Mood", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Purchase", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("DanceTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("DanceTagsInferred", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("GenreTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("StyleTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("TempoTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("OtherTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("Sample", DataType.String) {IsSearchable = false, IsSortable = false, IsFilterable = false, IsFacetable = false},
            },
            Suggesters = new[]
            {
                new Suggester("songs",SuggesterSearchMode.AnalyzingInfixMatching, "Title", "Artist", "Albums", "DanceTags", "Purchase", "GenreTags", "TempoTags", "StyleTags", "OtherTags")
            }
        };
    }
}
