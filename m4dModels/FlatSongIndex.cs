using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

using DanceLibrary;

namespace m4dModels
{
    public class FlatSongIndex : SongIndex
    {
        // For Moq
        public FlatSongIndex()
        {
        }

        internal FlatSongIndex(DanceMusicCoreService dms, string id) : base(dms, id)
        {
        }

        #region Index Management

        public override SearchIndex BuildIndex()
        {
            var fields = new List<SearchField>
            {
                new(SongIdField, SearchFieldDataType.String) { IsKey = true },
                new(
                    AltIdField, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = false, IsSortable = false, IsFilterable = true,
                    IsFacetable = false
                },
                new(Song.TitleField, SearchFieldDataType.String)
                {
                    IsSearchable = true, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(Song.TitleHashField, SearchFieldDataType.Int32)
                {
                    IsSearchable = false, IsSortable = false, IsFilterable = true,
                    IsFacetable = false
                },
                new(Song.ArtistField, SearchFieldDataType.String)
                {
                    IsSearchable = true, IsSortable = true, IsFilterable = false,
                    IsFacetable = false
                },
                new(
                    AlbumsField, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(
                    UsersField, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(CreatedField, SearchFieldDataType.DateTimeOffset)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(ModifiedField, SearchFieldDataType.DateTimeOffset)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(EditedField, SearchFieldDataType.DateTimeOffset)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(Song.TempoField, SearchFieldDataType.Double)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(Song.LengthField, SearchFieldDataType.Int32)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(BeatField, SearchFieldDataType.Double)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(Song.EnergyField, SearchFieldDataType.Double)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(MoodField, SearchFieldDataType.Double)
                {
                    IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
                },
                new(
                    Song.PurchaseField, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(LookupStatus, SearchFieldDataType.Boolean)
                {
                    IsSearchable = false, IsSortable = false, IsFilterable = true,
                    IsFacetable = false
                },
                new(
                    Song.DanceTags, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(
                    DanceTagsInferred, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(
                    GenreTags, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(
                    StyleTags, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(
                    TempoTags, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(
                    OtherTags, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true
                },
                new(Song.SampleField, SearchFieldDataType.String)
                {
                    IsSearchable = false, IsSortable = false, IsFilterable = true,
                    IsFacetable = false
                },
                new(
                    ServiceIds, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true,
                    IsFacetable = false
                },
                new(
                    CommentsField, SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true, IsSortable = false, IsFilterable = true,
                    IsFacetable = false
                },
                new(PropertiesField, SearchFieldDataType.String)
                {
                    IsSearchable = false, IsSortable = false, IsFilterable = false,
                    IsFacetable = false
                },
                IndexFieldFromDanceId("ALL")
            };

            var ids = Dances.Instance.AllDanceTypes.Where(t => t.Id != "ALL").Select(t => IndexFieldFromDanceId(t.Id));
            fields.AddRange(ids);

            return Info.BuildIndex(
                fields,
                suggesters: searchSuggesters,
                scoringProfiles: searchScoringProfiles,
                defaultScoringProfile : "Default"
            );
        }

        protected virtual IList<SearchSuggester> searchSuggesters => [
            new SearchSuggester("songs",
                Song.TitleField, Song.ArtistField, AlbumsField, Song.DanceTags,
                Song.PurchaseField, GenreTags, TempoTags, StyleTags, OtherTags)
        ];

        protected virtual IList<ScoringProfile> searchScoringProfiles => [
            new ScoringProfile("Default")
            {
                TextWeights = new TextWeights(
                    new Dictionary<string, double>
                    {
                        {Song.TitleField, 10},
                        {Song.ArtistField, 10},
                        {CommentsField, 5},
                        {AlbumsField, 2},
                    })
            }
        ];

        private static SearchField IndexFieldFromDanceId(string id)
        {
            return new SearchField(BuildDanceFieldName(id), SearchFieldDataType.Int32)
            {
                IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = false
            };
        }

        protected static string BuildDanceFieldName(string id)
        {
            return $"dance_{id}";
        }

        public override async Task<bool> UpdateIndex(IEnumerable<string> dances)
        {
            var index = await GetSearchIndex();
            foreach (var dance in dances)
            {
                var field = IndexFieldFromDanceId(dance);
                if (index.Fields.All(f => f.Name != field.Name))
                {
                    index.Fields.Add(field);
                }
            }

            var response = await Info.CreateOrUpdateIndexAsync(index, IsNext);
            return response.Value != null;
        }

        protected override object DocumentFromSong(Song song)
        {
            var tagMap = DanceMusicService.DanceStats.TagManager.TagMap;

            // Set up the purchase flags
            var purchase = string.IsNullOrWhiteSpace(song.Purchase)
                ? []
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

            var purchaseIds = song.GetExtendedPurchaseIds();

            // And the tags
            var ts = new TagSummary(song.TagSummary, tagMap);
            var genre = ts.GetTagSet("Music");
            var other = ts.GetTagSet("Other");
            var tempo = ts.GetTagSet("Tempo");
            var style = new HashSet<string>();

            var dance = song.TagSummary.GetTagSet("Dance");

            var comments = new List<string>();
            AccumulateComments(song.Comments, comments);

            foreach (var dr in song.DanceRatings)
            {
                var dobj = Dances.Instance.DanceFromId(dr.DanceId);
                if (dobj is DanceGroup)
                {
                    continue;
                }
                var d = dobj.Name.ToLower();
                ts = new TagSummary(dr.TagSummary, tagMap);
                other.UnionWith(ts.GetTagSet("Other"));
                tempo.UnionWith(ts.GetTagSet("Tempo"));
                style.UnionWith(ts.GetTagSet("Style"));
                AccumulateComments(dr.Comments, comments);
            }

            var users = song.ModifiedBy.Select(
                m =>
                    m.UserName.ToLower() +
                    (m.Like.HasValue ? m.Like.Value ? "|l" : "|h" : string.Empty)).ToArray();

            var altIds = song.GetAltids().ToArray();

            var doc = new SearchDocument
            {
                [SongIdField] = song.SongId.ToString(),
                [AltIdField] = altIds,
                [Song.TitleField] = song.Title,
                [Song.TitleHashField] = song.TitleHash,
                [Song.ArtistField] = song.Artist,
                [Song.LengthField] = song.Length,
                [BeatField] = CleanNumber(song.Danceability),
                [Song.EnergyField] = CleanNumber(song.Energy),
                [MoodField] = CleanNumber(song.Valence),
                [Song.TempoField] = CleanNumber((float?)song.Tempo),
                [CreatedField] = song.Created,
                [ModifiedField] = song.Modified,
                [EditedField] = song.Edited,
                [Song.SampleField] = song.Sample,
                [Song.PurchaseField] = purchase.ToArray(),
                [ServiceIds] = purchaseIds.ToArray(),
                [LookupStatus] = song.LookupTried(),
                [AlbumsField] = song.Albums.Select(ad => ad.Name).ToArray(),
                [UsersField] = users,
                [Song.DanceTags] = dance.ToArray(),
                [GenreTags] = genre.ToArray(),
                [TempoTags] = tempo.ToArray(),
                [StyleTags] = style.ToArray(),
                [OtherTags] = other.ToArray(),
                [CommentsField] = comments.ToArray(),
                [PropertiesField] = SongProperty.Serialize(song.SongProperties, null)
            };

            // Set the dance ratings
            foreach (var dr in song.DanceRatings)
            {
                var dobj = Dances.Instance.DanceFromId(dr.DanceId);
                if (dobj is DanceGroup)
                {
                    Trace.WriteLine($"Invalid use of group {dobj.Name} in song {song.SongId}");
                    continue;
                }
                doc[BuildDanceFieldName(dr.DanceId)] = dr.Weight;
            }

            var all = song.DanceRatings.Sum(dr => dr.Weight);
            doc["dance_ALL"] = all == 0 ? null : all;

            return doc;
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
            return (doc as SearchDocument)?.GetString(SongIdField);
        }

        protected async Task<SearchIndex> GetSearchIndex()
        {
            try
            {
                return await Info.GetIndexAsync(IsNext);
            }
            catch (Azure.RequestFailedException ex)
            {
                Debug.WriteLine(ex.Message);
                return await ResetIndex();
            }
        }
    }
}
