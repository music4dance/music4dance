using System.Collections.Generic;
using System.Linq;

namespace m4dModels;

public class DanceBuilderNext(DanceMusicCoreService dms, string source) : 
    DanceBuilder(dms, source)
{

    protected override IEnumerable<string> GlobalFacets =>
    [
        "GenreTags", "TempoTags", "OtherTags",
        "dance_ALL/StyleTags", "dance_ALL/TempoTags", "dance_ALL/OtherTags"
    ];

    protected override IEnumerable<string> GetDanceFacets(string danceId) =>
        GlobalFacets.Select(f => f.Replace(@"dance_ALL/", $"dance_{danceId}/"));
}
