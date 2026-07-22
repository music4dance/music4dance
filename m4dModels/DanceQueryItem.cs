using DanceLibrary;

using System.Text.RegularExpressions;

namespace m4dModels;

public partial class DanceQueryItem
{
    private const char PrimaryMarker = '*';

    public string Id { get; set; }
    public int Threshold { get; set; }
    public TagQuery TagQuery { get; set; }

    // Marks this dance as the explicit target for dance-rating sort, tempo sort, and the
    // tempo range filter when more than one dance is selected - see DanceQuery.PrimaryDanceId.
    public bool IsPrimary { get; set; }

    // Only meaningful when Id refers to a DanceGroup: a group has no per-dance rating/tempo
    // fields of its own, so the marker must name which member dance to actually use. Null for
    // a marked plain dance (it's simply its own target). See DanceQuery.PrimaryDanceId.
    public string PrimaryTargetId { get; set; }

    public bool IsSimple => Threshold == 1 && !IsPrimary && TagQuery.TagList.IsEmpty;

    public static DanceQueryItem FromValue(string value)
    {
        var regex = ThresholdWithTagsRegex();
        var match = regex.Match(value);
        if (!match.Success)
        {
            throw new Exception($"Invalid value format: {value}");
        }

        var dance = Dances.Instance.DanceFromId(match.Groups[1].Value) ?? throw new Exception($"Couldn't find dance {match.Groups[1].Value}");
        var weight = match.Groups[5].Success && !string.IsNullOrEmpty(match.Groups[5].Value)
            ? int.Parse(match.Groups[5].Value) : 1;

        var tags = match.Groups[6].Success ? match.Groups[6].Value : null;

        return new DanceQueryItem
        {
            Id = dance.Id,
            IsPrimary = match.Groups[2].Success,
            PrimaryTargetId = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value)
                ? match.Groups[3].Value
                : null,
            Threshold = match.Groups[4].Success && match.Groups[4].Value == "-" ? -weight : weight,
            TagQuery = new TagQuery(tags)
        };
    }

    public DanceObject Dance => Dances.Instance.DanceFromId(Id);

    public override string ToString()
    {
        var marker = IsPrimary ? PrimaryMarker.ToString() + (PrimaryTargetId ?? "") : "";
        var baseStr =
            $"{Id}{marker}{(Threshold != 1 ? (Threshold < 0 ? "-" : "+") + Math.Abs(Threshold) : "")}";
        if (TagQuery?.TagList?.IsEmpty != true)
        {
            return $"{baseStr}|{TagQuery.TagList}";
        }
        return baseStr;
    }

    public string Description
    {
        get
        {
            var modifiers = new System.Collections.Generic.List<string>();
            if (Threshold != 1)
            {
                modifiers.Add(Threshold > 0
                    ? $"with at least {Threshold} votes"
                    : $"with at most {Math.Abs(Threshold)} votes");
            }
            if (TagQuery != null && !TagQuery.IsEmpty)
            {
                var separator = "";
                modifiers.Add(TagQuery.Description(ref separator));
            }
            var desc = Dance.Name;
            if (modifiers.Count > 0)
            {
                desc = $"{desc} ({string.Join(", ", modifiers)})";
            }
            return desc;
        }
    }

    public string ShortDescription
    {
        get
        {
            var modifiers = new System.Collections.Generic.List<string>();
            if (Threshold != 1)
            {
                modifiers.Add(Threshold > 0
                    ? $">={Threshold}"
                    : $"<={Math.Abs(Threshold)}");
            }
            if (TagQuery != null && !TagQuery.TagList.IsEmpty)
            {
                var separator = "";
                modifiers.Add(TagQuery.ShortDescription(ref separator));
            }
            var desc = Dance.Name;
            if (modifiers.Count > 0)
            {
                desc = $"{desc} ({string.Join(", ", modifiers)})";
            }
            return desc;
        }
    }

    [GeneratedRegex(@"^([a-zA-Z0-9]+)(\*)?([a-zA-Z0-9]+)?([+-]?)(\d*)\|?(.*)?$")]
    private static partial Regex ThresholdWithTagsRegex();
}
