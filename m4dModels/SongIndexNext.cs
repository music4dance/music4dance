using Azure.Search.Documents.Indexes.Models;

namespace m4dModels;

// SongIndexNext: implements the v3 index schema with per-dance Tempo sub-field.
public class SongIndexNext : SongIndex
{
    public const string DanceTempoSubField = "Tempo";

    public SongIndexNext()
    {
    }

    internal SongIndexNext(DanceMusicCoreService dms, string id) : base(dms, id)
    {
    }

    public override bool IsNext => true;

    // Override to add Tempo sub-field to each dance_{id} complex field.
    public override SearchIndex BuildIndex()
    {
        var baseIndex = base.BuildIndex();

        // Add Tempo sub-field to every dance_{id} complex field.
        foreach (var field in baseIndex.Fields.Where(
            f => f.Name.StartsWith("dance_") && f.Type == SearchFieldDataType.Complex))
        {
            field.Fields.Add(new SearchField(DanceTempoSubField, SearchFieldDataType.Double)
            {
                IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true
            });
        }

        return baseIndex;
    }

    // Override to populate dance_{id}/Tempo with the per-dance override or the song-level tempo.
    protected override object DocumentFromSong(Song song)
    {
        var docObj = base.DocumentFromSong(song);
        if (docObj is not Azure.Search.Documents.Models.SearchDocument doc)
        {
            return docObj;
        }

        foreach (var dr in song.DanceRatings)
        {
            var fieldName = BuildDanceFieldName(dr.DanceId);
            if (doc[fieldName] is not Dictionary<string, object> danceDoc)
            {
                continue;
            }

            // Use per-dance tempo override when set; fall back to song-level tempo.
            var effectiveTempo = dr.Tempo ?? song.Tempo;
            danceDoc[DanceTempoSubField] = CleanNumber((float?)effectiveTempo);
        }


        // Note: dance_ALL/Votes is used for aggregate vote sorting, but Tempo is not
        // needed here — non-single-dance queries use the top-level song Tempo field.
        return doc;
    }
}
