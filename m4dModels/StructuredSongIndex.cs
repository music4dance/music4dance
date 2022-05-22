using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using DanceLibrary;

namespace m4dModels
{
    public class StructuredSongIndex : SongIndex
    {
        internal StructuredSongIndex(DanceMusicCoreService dms, string id) : base(dms, id)
        {
        }

        #region Index Management

        public override SearchIndex GetIndex()
        {
            var fieldBuilder = new FieldBuilder();
            var fields = fieldBuilder.Build(typeof(SongDocument));

            var index = new SearchIndex(Info.Name, fields.ToArray());
            index.Suggesters.Add(
                new SearchSuggester(
                    "songs",
                    Song.TitleField, Song.ArtistField, AlbumsField, Song.DanceTags,
                    Song.PurchaseField, GenreTags, TempoTags, StyleTags, OtherTags));

            return index;
        }

        public override Task<bool> UpdateIndex(IEnumerable<string> dances)
        {
            return Task.FromResult(true);
        }

        protected override object DocumentFromSong(Song song)
        {
            var tagMap = DanceMusicService.DanceStats.TagManager.TagMap;

            // Set up the purchase flags
            var purchase = string.IsNullOrWhiteSpace(song.Purchase)
                ? new List<string>()
                : song.Purchase.ToCharArray().Where(c => MusicService.GetService(c) != null)
                    .Select(c => MusicService.GetService(c).Name).ToList();
            if (song.HasSample)
            {
                purchase.Add("Sample");
            }

            if (song.HasEchoNest)
            {
                purchase.Add("EchoNest");
            }

            if (song.BatchProcessed)
            {
                purchase.Add("---");
            }

            if (song.Purchase != null && song.Purchase.Contains('x', StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"SongId = {song.SongId}, Purchase = {song.Purchase}");
            }

            // And the tags
            var ts = new TagSummary(song.TagSummary, tagMap);
            var genre = ts.GetTagSet("Music");
            var other = ts.GetTagSet("Other");
            var tempo = ts.GetTagSet("Tempo");

            var dance = song.TagSummary.GetTagSet("Dance");

            var dances = new List<DanceDocument>();

            foreach (var dr in song.DanceRatings)
            {
                var dobj = Dances.Instance.DanceFromId(dr.DanceId);
                if (dobj is DanceGroup)
                {
                    continue;
                }
                ts = new TagSummary(dr.TagSummary, tagMap);

                dances.Add(new DanceDocument
                {
                    Name = dobj.Name.ToLower(),
                    Votes = dr.Weight,
                    StyleTags = ts.GetTagSet("Style").ToList(),
                    TempoTags = ts.GetTagSet("Tempo").ToList(),
                    OtherTags = ts.GetTagSet("Other").ToList(),
                    Comments = dr.Comments.Select(c => c.Comment).ToList()

                });
            }

            var users = song.ModifiedBy.Select(
                m =>
                    m.UserName.ToLower() +
                    (m.Like.HasValue ? m.Like.Value ? "|l" : "|h" : string.Empty)).ToList();

            var altIds = song.GetAltids().ToArray();

            return new SongDocument
            {
                Id = song.SongId.ToString(),
                Properties = SongProperty.Serialize(song.SongProperties, null),
                AlternateIds = song.GetAltids().ToList(),
                TitleHash = song.TitleHash,
                ServiceIds = song.GetExtendedPurchaseIds().ToList(),
                Title = song.Title,
                Artist = song.Artist,
                Album = song.Albums.Select(ad => ad.Name).ToList(),
                Tempo = (double?)song.Tempo,
                Length = song.Length,

                Created = song.Created,
                Modified = song.Modified,
                Edited = song.Edited,

                Users = users,
                Beat = CleanNumber(song.Danceability),
                Energy = CleanNumber(song.Energy),
                Mood = CleanNumber(song.Valence),
                Purchase = purchase,

                GenreTags = genre.ToList(),
                TempoTags = tempo.ToList(),
                OtherTags = other.ToList(),

                Sample = song.Sample,
                
                Dances = dances,
                Comments = song.Comments.Select(c => c.Comment).ToList()
            };
        }
        #endregion

        protected override Task<Song> CreateSong(SearchDocument document)
        {
            if (document[PropertiesField] is not string properties || document[SongIdField] is not string sid)
            {
                throw new ArgumentOutOfRangeException(nameof(document));
            }

            if (!Guid.TryParse(sid, out var id))
            {
                throw new ArgumentOutOfRangeException(nameof(document));
            }

            return Song.Create(id, properties, DanceMusicService);
        }

        protected override string GetSongId(object doc)
        {
            return (doc as SongDocument)?.Id;
        }

    }
}
