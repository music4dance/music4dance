using System.Collections.Generic;
using Newtonsoft.Json;

namespace m4dModels
{
    public class Suggestion
    {
        [JsonProperty("value")]
        public string Value { get; set; }
        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public class SuggestionList
    {
        [JsonProperty("query")]
        public string Query { get; set; }
        [JsonProperty("suggestions")]

        public IEnumerable<Suggestion> Suggestions { get; set; }
    }
}
