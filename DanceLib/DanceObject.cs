using System.Collections.Generic;
using Newtonsoft.Json;

namespace DanceLibrary;

public class DanceObject
{
    public virtual string Id { get; set; }

    public virtual string Name { get; set; }

    public virtual Meter Meter { get; set; }

    public virtual TempoRange TempoRange { get; set; }

    public virtual string BlogTag { get; set; }

    public virtual List<string> Synonyms { get; set; }

    public virtual List<string> Searchonyms { get; set; }

    [JsonIgnore]
    public string CleanName => SeoFriendly(Name);

    public static string SeoFriendly(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? name : name.Replace(' ', '-').ToLower();
    }
}
