using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace m4dModels
{

    public class KeywordQuery(string s = "")
    {
        private readonly string data = s ?? "";

        public static KeywordQuery FromParts(Dictionary<string, string> parts)
        {
            var simple = true;
            var segments = new List<string>();
            var everywhere = parts.TryGetValue("Everywhere", out var value) ? value : "";

            foreach (var part in parts)
            {
                if (!string.IsNullOrEmpty(part.Value) && part.Key != "Everywhere")
                {
                    segments.Add($"{part.Key}:({part.Value})");
                    simple = false;
                }
            }

            if (!string.IsNullOrEmpty(everywhere))
            {
                segments.Add(everywhere);
            }

            return new KeywordQuery(simple ? everywhere : "`" + string.Join(" ", segments));
        }

        public bool IsLucene => data.StartsWith('`');

        public string Keywords
        {
            get { return IsLucene ? data[1..] : data; }
        }

        public string Query
        {
            get { return data; }
        }

        public KeywordQuery Update(string part, string value)
        {
            var parts = Fields;
            if (!string.IsNullOrEmpty(value))
            {
                parts[part] = value;
            }
            else
            {
                parts.Remove(part);
            }
            return FromParts(parts);
        }

        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(data))
                {
                    return "";
                }

                if (!IsLucene)
                {
                    return $"containing the text \"{data}\"";
                }

                var fields = Fields;
                var all = fields.TryGetValue("Everywhere", out var value) ? value : "";
                fields.Remove("Everywhere");

                if (!string.IsNullOrEmpty(all) && fields.Count == 0)
                {
                    return $"containing the text \"{all}\"";
                }

                var result = "where";
                var first = true;
                if (!string.IsNullOrEmpty(all))
                {
                    result = $"containing the text \"{all}\" anywhere";
                    first = false;
                }

                foreach (var @field in fields)
                {
                    if (!first)
                    {
                        result += " and";
                    }
                    result += $" {field.Key.ToLower()} contains \"{field.Value}\"";
                    first = false;
                }

                return result;
            }
        }

        public string ShortDescription
        {
            get
            {
                if (string.IsNullOrEmpty(data))
                {
                    return "";
                }

                if (!IsLucene)
                {
                    return $"\"{data}\"";
                }

                var fields = Fields;
                var all = fields.TryGetValue("Everywhere", out var value) ? value : "";
                fields.Remove("Everywhere");

                if (!string.IsNullOrEmpty(all) && fields.Count == 0)
                {
                    return $"\"{all}\"";
                }

                var result = "";
                var first = true;
                if (!string.IsNullOrEmpty(all))
                {
                    result = $"\"{all}\" anywhere";
                    first = false;
                }

                foreach (var @field in fields)
                {
                    if (!first)
                    {
                        result += " and ";
                    }
                    result += $"\"{field.Value}\" in {field.Key.ToLower()}";
                    first = false;
                }

                return result;
            }
        }

        public string GetField(string field)
        {
            return Fields.TryGetValue(field, out var value) ? value : "";
        }

        private Dictionary<string, string> Fields
        {
            get
            {
                var search = Keywords;
                var matches = _regex.Matches(search);
                var result = new Dictionary<string, string>();
                foreach (Match match in matches)
                {
                    result[match.Groups["field"].Value] = match.Groups["search"].Value;
                }
                var all = _regex.Replace(search, "").Trim(); // replace "regex" with your actual regex pattern
                if (!string.IsNullOrEmpty(all))
                {
                    result["Everywhere"] = all;
                }
                return result;
            }
        }

        private static readonly Regex _regex = new(@"(?<field>Artist|Title|Albums):\((?<search>[^)]*)\)", RegexOptions.Compiled);
    }
}
