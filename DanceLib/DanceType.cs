using Newtonsoft.Json;

using System.Diagnostics;

namespace DanceLibrary;

public class DanceType : DanceObject
{
    public DanceType()
    {
        Groups = [];
        Instances = [];
    }

    public DanceType(DanceType other)
    {
        Id = other.Id;
        Name = other.Name;
        Meter = other.Meter;
        Instances = other.Instances;
        BlogTag = other.BlogTag;
        Synonyms = other.Synonyms;
        Searchonyms = other.Searchonyms;
        Groups = other.Groups;
    }

    [JsonConstructor]
    public DanceType(string name, Meter meter, DanceInstance[] instances) : this()
    {
        Name = name;
        Meter = meter;
        Instances = [.. instances];

        foreach (var instance in instances)
        {
            instance.DanceType = this;
        }
    }

    public DanceType Reduce(IEnumerable<DanceInstance> instances)
    {
        var other = MemberwiseClone() as DanceType;
        other.Instances = [.. instances];
        foreach (var instance in other.Instances)
        {
            instance.DanceType = other;
        }

        return other;
    }

    public sealed override string Id { get; set; }

    public sealed override string Name { get; set; }

    public sealed override Meter Meter { get; set; }

    public sealed override string BlogTag { get; set; }

    public sealed override List<string> Synonyms { get; set; }

    public sealed override List<string> Searchonyms { get; set; }

    public override TempoRange TempoRange
    {
        get
        {
            Debug.Assert(Instances.Count > 0);
            var tr = Instances[0].TempoRange;
            for (var i = 1; i < Instances.Count; i++)
            {
                tr = tr.Include(Instances[i].TempoRange);
            }

            return tr;
        }
        set
        {
            //Debug.Assert(false);
        }
    }

    [JsonIgnore]
    public List<string> Organizations
    {
        get
        {
            var orgs = new HashSet<string>();
            foreach (var instance in Instances)
            {
                orgs.UnionWith(instance.Organizations);
            }
            return orgs.Count > 0 ? [.. orgs] : ["Unaffiliated"];
        }
    }

    public List<DanceInstance> Instances { get; set; }

    // Virtual for Moq
    [JsonIgnore]
    public virtual IList<DanceGroup> Groups { get; }

    public Uri Link { get; set; }

    public override bool Equals(object obj)
    {
        return obj is DanceType other && Name == other.Name;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public bool ShouldSerializeTempoRange() => false;
}
