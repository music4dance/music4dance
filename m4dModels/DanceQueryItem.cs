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
        var weight = match.Groups[4].Success && !string.IsNullOrEmpty(match.Groups[4].Value)
            ? int.Parse(match.Groups[4].Value) : 1;

        var tags = match.Groups[5].Success ? match.Groups[5].Value : null;

        return new DanceQueryItem
        {
            Id = dance.Id,
            IsPrimary = match.Groups[2].Success && match.Groups[2].Value == PrimaryMarker.ToString(),
            Threshold = match.Groups[3].Success && match.Groups[3].Value == "-" ? -weight : weight,
            TagQuery = new TagQuery(tags)
        };
    }

    public DanceObject Dance => Dances.Instance.DanceFromId(Id);

    public override string ToString()
    {
        var baseStr =
            $"{Id}{(IsPrimary ? PrimaryMarker.ToString() : "")}{(Threshold != 1 ? (Threshold < 0 ? "-" : "+") + Math.Abs(Threshold) : "")}";
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

    [GeneratedRegex(@"^([a-zA-Z0-9]+)(\*?)([+-]?)(\d*)\|?(.*)?$")]
    private static partial Regex ThresholdWithTagsRegex();
}
