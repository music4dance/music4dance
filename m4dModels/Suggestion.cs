using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace m4dModels
{
    public class Suggestion
    {
        [JsonProperty("value")] public string Value { get; set; }
        [JsonProperty("data")] public string Data { get; set; }
    }

    public class SuggestionList
    {
        [JsonProperty("query")] public string Query { get; set; }
        [JsonProperty("suggestions")] public IEnumerable<Suggestion> Suggestions { get; set; }
    }

    public class SuggestionComparer : IEqualityComparer<Suggestion>
    {
        // Suggestions are equal if their values are equal
        public bool Equals(Suggestion x, Suggestion y)
        {
            //Check whether the values are equal (case insensitive)
            return string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
        }

        // If Equals() returns true for a pair of objects 
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(Suggestion s)
        {
            return s.Value.ToLower().GetHashCode();
        }
    }
}