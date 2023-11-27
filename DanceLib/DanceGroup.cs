using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace DanceLibrary;

public sealed class DanceGroup : DanceObject
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Microsoft.Usage",
        "CA2214:DoNotCallOverridableMethodsInConstructors")]
    [JsonConstructor]
    public DanceGroup(string name, string id, string[] danceIds)
    {
        Name = name;
        Id = id;

        Debug.Assert(danceIds != null);
        DanceIds = danceIds.ToList();
    }

    public override string Id { get; set; }

    public override string Name { get; set; }

    public override Meter Meter
    {
        get
        {
            Debug.Assert(Members.Count > 0);
            return Members[0].Meter;
        }
        set
        {
            //Debug.Assert(false);
        }
    }

    public override TempoRange TempoRange
    {
        get
        {
            Debug.Assert(Members != null && Members.Count > 0);

            var range = Members[0].TempoRange;

            for (var i = 1; i < Members.Count; i++)
            {
                range = range.Include(Members[i].TempoRange);
            }

            return range;
        }
        set
        {
            //Debug.Assert(false);
        }
    }

    public List<string> DanceIds { get; set; }

    [JsonIgnore]
    public IList<DanceObject> Members => Dances.Instance.FromIds(DanceIds);
}
