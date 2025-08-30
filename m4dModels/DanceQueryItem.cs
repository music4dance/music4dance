using System;
using System.Text.RegularExpressions;
using System.Linq;
using m4dModels; // For TagQuery

using DanceLibrary;

namespace m4dModels
{
    public partial class DanceQueryItem
    {
        public string Id { get; set; }
        public int Threshold { get; set; }
        public TagQuery TagQuery { get; set; }

        public static DanceQueryItem FromValue(string value)
        {
            var regex = ThresholdWithTagsRegex();
            var match = regex.Match(value);
            if (!match.Success)
            {
                throw new Exception($"Invalid value format: {value}");
            }

            var dance = Dances.Instance.DanceFromId(match.Groups[1].Value) ?? throw new Exception($"Couldn't find dance {match.Groups[1].Value}");
            var weight = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value)
                ? int.Parse(match.Groups[3].Value) : 1;

            var tags = match.Groups[4].Success ? match.Groups[4].Value : null;

            return new DanceQueryItem
            {
                Id = dance.Id,
                Threshold = match.Groups[2].Success && match.Groups[2].Value == "-" ? -weight : weight,
                TagQuery = new TagQuery(tags)
            };
        }

        public DanceObject Dance => Dances.Instance.DanceFromId(Id);

        public override string ToString()
        {
            var baseStr = $"{Id}{(Threshold != 1 ? (Threshold < 0 ? "-" : "+") + Math.Abs(Threshold) : "")}";
            if (TagQuery != null && TagQuery.TagList != null && TagQuery.TagList.Tags.Any())
            {
                return $"{baseStr}|{string.Join(",", TagQuery.TagList.Tags)}";
            }
            return baseStr;
        }

        public string Description
        {
            get
            {
                var desc = Threshold == 1
                    ? Dance.Name
                    : $"{Dance.Name} (with {(Threshold > 0 ? "at least" : "at most")} {Math.Abs(Threshold)} votes)";
                if (TagQuery != null && TagQuery.TagList != null && TagQuery.TagList.Tags.Any())
                {
                    return $"{desc} [tags: {string.Join(", ", TagQuery.TagList.Tags)}]";
                }
                return desc;
            }
        }

        public string ShortDescription
        {
            get
            {
                var desc = Threshold == 1
                    ? Dance.Name
                    : $"{Dance.Name} {(Threshold > 0 ? ">=" : "<=")} {Math.Abs(Threshold)}";
                if (TagQuery != null && TagQuery.TagList != null && TagQuery.TagList.Tags.Any())
                {
                    return $"{desc} [{string.Join(",", TagQuery.TagList.Tags)}]";
                }
                return desc;
            }
        }

        [GeneratedRegex(@"^([a-zA-Z0-9]+)([+-]?)(\d*)\|?(.*)?$")]
        private static partial Regex ThresholdWithTagsRegex();
    }
}
