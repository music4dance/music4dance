using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace DanceLibrary;

public class DanceInstance : DanceObject
{
    [JsonConstructor]
    public DanceInstance(string style, TempoRange tempoRange, DanceException[] exceptions, string[] organizations = null)
    {
        Style = style;
        TempoRange = tempoRange;
        Exceptions = exceptions == null ? [] : [.. exceptions];
        Organizations = organizations == null ? [] : [.. organizations];
        foreach (var de in Exceptions)
        {
            de.DanceInstance = this;
        }
    }

    public DanceInstance ReduceExceptions(List<string> orgs)
    {
        var other = MemberwiseClone() as DanceInstance;
        if (orgs != null)
        {
            var exceptions = Exceptions.Where(de => orgs.Contains(de.Organization)).ToList();
            if (exceptions.Count > 0)
            {
                other.TempoRange = exceptions.Aggregate(
                    null as TempoRange,
                    (current, de) => de.TempoRange.Include(current));
            }
        }
        other.Exceptions = [];
        return other;
    }

    [JsonIgnore]
    public DanceType DanceType { get; internal set; }

    public sealed override TempoRange TempoRange { get; set; }

    public override string Id => DanceType.Id + StyleId;

    public override Meter Meter => DanceType.Meter;

    public override string Name => ShortStyle + ' ' + DanceType.Name;

    [JsonProperty(Order = int.MinValue)]
    public string Style { get; set; }

    public string CompetitionGroup { get; set; }

    [DefaultValue(0)]
    public int CompetitionOrder { get; set; }

    public List<DanceException> Exceptions { get; set; }

    public List<string> Organizations { get; set; }

    [JsonIgnore]
    public string ShortStyle
    {
        get
        {
            var words = Style.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            Debug.Assert(words.Length > 0);
            return words[0];
        }
    }

    [JsonIgnore]
    public char StyleId
    {
        get
        {
            var ss = ShortStyle;
            Debug.Assert(!string.IsNullOrEmpty(ss));
            return ShortStyle[0];
        }
    }

    public override string ToString()
    {
        return $"{Style} ({TempoRange}BPM)";
    }

    public bool ShouldSerializeId() => false;
    public bool ShouldSerializeMeter() => false;
    public bool ShouldSerializeName() => false;

    public bool ShouldSerializeExceptions()
    {
        return Exceptions.Count > 0;
    }

    public bool ShouldSerializeOrganizations()
    {
        return Organizations != null && Organizations.Count > 0;
    }
}
