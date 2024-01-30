using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace DanceLibrary;

public class DanceType : DanceObject
{
    public DanceType()
    {
        Groups = [];
        Organizations = [];
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
        Organizations = other.Organizations;
        Groups = other.Groups;
    }

    [JsonConstructor]
    public DanceType(string name, Meter meter, string[] organizations,
        DanceInstance[] instances) : this()
    {
        Name = name;
        Meter = meter;
        Instances = new List<DanceInstance>(instances);

        Organizations = organizations == null ? [] :new List<string>(organizations);

        foreach (var instance in instances)
        {
            instance.DanceType = this;
        }
    }

    public DanceType Reduce(IEnumerable<DanceInstance> instances) {
        var other = MemberwiseClone() as DanceType;
        other.Instances = new List<DanceInstance>(instances);
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

    public List<string> Organizations { get; set; }

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
