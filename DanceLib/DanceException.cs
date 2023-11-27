using System.ComponentModel;
using Newtonsoft.Json;

namespace DanceLibrary;

public class DanceException
{
    public string Organization { get; set; }

    public TempoRange TempoRange { get; set; }

    [DefaultValue("All")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public string Competitor { get; set; }

    [DefaultValue("All")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public string Level { get; set; }
}
