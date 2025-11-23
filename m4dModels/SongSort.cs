using System.Diagnostics;

namespace m4dModels;

public class SongSort
{
    public const string Dances = SongIndex.DancesField;
    public const string Modified = SongIndex.ModifiedField;
    public const string Edited = SongIndex.EditedField;
    public const string Created = SongIndex.CreatedField;
    public const string Closest = "Closest";
    public const string Comments = SongIndex.CommentsField;
    public const string Tempo = Song.TempoField;
    public const string Length = Song.LengthField;
    public const string Beat = SongIndex.BeatField;
    public const string Mood = SongIndex.MoodField;
    public const string Energy = Song.EnergyField;

    private static readonly string[] s_directional =
    [
        Song.TitleField, Song.ArtistField, Song.TempoField, Song.LengthField,
        SongIndex.ModifiedField, SongIndex.CreatedField, SongIndex.EditedField,
        Song.EnergyField, SongIndex.MoodField, SongIndex.BeatField, SongIndex.CommentsField
    ];

    private static readonly string[] s_numerical =
        [Song.TempoField, SongIndex.MoodField, SongIndex.CreatedField];

    private static readonly string[] s_intrinsic =
        [Song.EnergyField, SongIndex.MoodField, SongIndex.BeatField];

    public SongSort(string sort, bool hasQuery = false)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            HasQuery = hasQuery;
            return;
        }

        var list = sort.Split('_').ToList();

        _id = list[0];
        list.RemoveAt(0);

        if (!string.IsNullOrEmpty(Id))
        {
            _id = $"{char.ToUpper(Id[0])}{Id[1..].ToLower()}";

            if (!(s_directional.Contains(Id) || s_intrinsic.Contains(Id) ||
                s_numerical.Contains(Id) || string.Equals(Id, Dances)))
            {
                _id = null;
                return;
            }
        }

        if (list.Count > 0)
        {
            if (string.Equals(list[0], "desc", StringComparison.OrdinalIgnoreCase))
            {
                Descending = true;
                list.RemoveAt(0);
            }
            else if (string.Equals(list[0], "asc", StringComparison.OrdinalIgnoreCase))
            {
                list.RemoveAt(0);
            }
        }

        if (list.Count > 0)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Bad Sort: {sort}");
        }
    }

    public string Id => string.IsNullOrEmpty(_id) && !HasQuery ? Dances : _id;
    public bool Descending { get; private set; }
    public bool HasQuery { get; private set; }

    private string _id { get; set; }

    public bool Numeric => s_intrinsic.Contains(Id) || Id == "Tempo";
    public bool Directional => s_directional.Contains(Id);

    public string FriendlyName
    {
        get
        {
            const string dr = "Dance Rating";
            const string cm = "Closest Match";

            return Id switch
            {
                Dances => dr,
                Modified => "Last Modified",
                Edited => "Last Edited",
                Created => "When Added",
                Closest => cm,
                null or "" => HasQuery ? cm : dr,
                _ => Id,
            };
        }
    }

    public IList<string> OData
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Id) || Id == Dances)
            {
                return null;
            }

            var desc = Descending;
            if (Id is Modified or Created or Edited)
            {
                desc = !desc;
            }

            var order = desc ? "desc" : "asc";
            return [$"{Id} {order}"];
        }
    }

    public string Description
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                return string.Empty;
            }

            var ret = new System.Text.StringBuilder();
            _ = ret.AppendFormat(" Sorted by {0} from ", FriendlyName);

            if (!Descending)
            {
                _ = Id switch
                {
                    Tempo => ret.Append("slowest to fastest"),
                    Length => ret.Append("shortest to longest"),
                    Modified or Created or Comments => ret.Append("newest to oldest"),
                    Dances => ret.Append("most popular to least popular"),
                    Beat => ret.Append("weakest to strongest"),
                    Mood => ret.Append("saddest to happiest"),
                    Energy => ret.Append("lowest to highest"),
                    _ => ret.Append("A to Z"),
                };
            }
            else
            {
                _ = Id switch
                {
                    Tempo => ret.Append("fastest to slowest"),
                    Length => ret.Append("longest to shortest"),
                    Modified or Created or Comments => ret.Append("oldest to newest"),
                    Dances => ret.Append("most popular to least popular"),
                    Beat => ret.Append("strongest to weakest"),
                    Mood => ret.Append("happiest to saddest"),
                    Energy => ret.Append("highest to lowest"),
                    _ => ret.Append("Z to A"),
                };
            }

            _ = ret.Append('.');
            return ret.ToString();
        }
    }

    public override string ToString()
    {
        return $"{Id}{(Descending ? "_desc" : string.Empty)}";
    }
}
