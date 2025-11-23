using System.Diagnostics;
using System.Runtime.Serialization;

namespace m4dModels;

// TagCount is a helper class to covert between a Tag+Count structure and a string of the form Tag[:Count]
[DataContract]
public class TagCount
{
    private bool Parse(string s)
    {
        var ret = true;
        var list = s.Split([':'], StringSplitOptions.RemoveEmptyEntries).ToList();
        var c = 1;

        if (list.Count is < 1 or > 3)
        {
            return false;
        }

        if (list.Count > 1)
        {
            ret = int.TryParse(list[^1], out c);
        }

        Count = c;
        Value = list[0].Trim();
        if (list.Count > 2 || ret == false)
        {
            Value += ":" + list[1];
        }

        return true;
    }

    public string Serialize()
    {
        return $"{Value}:{Count}";
    }

    private static string ClassDisplayName(string tagClass)
    {
        return s_classNames.TryGetValue(tagClass, out var name) ? name : "unknown";
    }

    private static readonly Dictionary<string, string> s_classNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "style", "style" },
        { "tempo", "tempo" },
        { "music", "musical genre" },
        { "other", "other" },
        { "dance", "dance" },
    };

    #region Properties

    [DataMember]
    public string Value { get; set; }

    [DataMember]
    public int Count { get; set; }

    public string TagValue => Value.Split(':')[0];

    public string TagClass =>
        Value.Contains(':') ? Value[(Value.LastIndexOf(':') + 1)..] : null;

    public string Description =>
        $"{TagValue} ({ClassDisplayName(TagClass)})";
    #endregion

    #region Constructors

    public TagCount(string value, int count)
    {
        Value = value;
        Count = count;
    }

    public TagCount(string serialized, int? count = null)
    {
        if (Parse(serialized))
        {
            if (count.HasValue)
            {
                Count = count.Value;
            }

            return;
        }

        Trace.WriteLineIf(TraceLevels.General.TraceError, $"Invalid TagCount: {serialized}");
        throw new ArgumentOutOfRangeException();
    }

    #endregion

    #region Operators

    public override string ToString()
    {
        return Serialize();
    }

    public override bool Equals(object obj)
    {
        var tc = obj as TagCount;
        return tc != null && Value == tc.Value && Count == tc.Count;
    }

    public override int GetHashCode()
    {
        return (Value.GetHashCode() * 1023) ^ Count;
    }

    public static bool operator ==(TagCount a, TagCount b)
    {
        // If both are null, or both are same instance, return true.
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        // Handle a is null case.
        return a?.Equals(b) ?? false;
    }

    public static bool operator !=(TagCount a, TagCount b)
    {
        return !(a == b);
    }

    #endregion
}
