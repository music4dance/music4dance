using System.Text.RegularExpressions;

namespace m4dModels;


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
                segments.Add($"{part.Key}:({EscapeGrouping(part.Value)})");
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

    /// <summary>
    /// The search text to send to Azure Search. A Lucene (field) query is rebuilt from its parts
    /// with every value escaped, so user text is always searched literally: an artist like
    /// "IAMSU!", "AC/DC" or "OR" otherwise makes Azure reject the whole query with a 400. The
    /// field syntax itself is the only Lucene the user gets. Simple queries pass through.
    /// </summary>
    public string AzureSearch
    {
        get
        {
            if (!IsLucene)
            {
                return data;
            }

            var fields = Fields;
            var everywhere = fields.TryGetValue("Everywhere", out var value) ? value : "";
            var segments = fields
                .Where(f => f.Key != "Everywhere")
                .Select(f => $"{f.Key}:({EscapeLucene(f.Value)})")
                .ToList();
            if (!string.IsNullOrEmpty(everywhere))
            {
                segments.Add(EscapeLucene(everywhere));
            }

            return string.Join(" ", segments);
        }
    }

    /// <summary>
    /// Escapes every Lucene special character and lowercases the operator words AND, OR and NOT
    /// (escaping one turns it into a term that matches nothing).
    /// </summary>
    public static string EscapeLucene(string value) =>
        _operatorWords.Replace(
            _luceneSpecial.Replace(value, @"\$0"),
            m => m.Value.ToLowerInvariant());

    // Inside a field's parentheses only \, ( and ) are structural; escaping them lets a value
    // like "Earth, Wind & Fire (Remix)" round-trip through the query string.
    private static string EscapeGrouping(string value) => _grouping.Replace(value, @"\$0");

    private static string Unescape(string value) => _escaped.Replace(value, "$1");

    public KeywordQuery Update(string part, string value)
    {
        var parts = Fields;
        if (!string.IsNullOrEmpty(value))
        {
            parts[part] = value;
        }
        else
        {
            _ = parts.Remove(part);
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
            _ = fields.Remove("Everywhere");

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
                result += $" {@field.Key.ToLower()} contains \"{@field.Value}\"";
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
            _ = fields.Remove("Everywhere");

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
                result += $"\"{@field.Value}\" in {@field.Key.ToLower()}";
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
                result[match.Groups["field"].Value] = Unescape(match.Groups["search"].Value);
            }
            var all = Unescape(_regex.Replace(search, "").Trim());
            if (!string.IsNullOrEmpty(all))
            {
                result["Everywhere"] = all;
            }
            return result;
        }
    }

    // A field's value runs to the first unescaped ")"
    private static readonly Regex _regex = new(
        @"(?<field>Artist|Title|Albums):\((?<search>(?:\\.|[^\\)])*)\)", RegexOptions.Compiled);

    private static readonly Regex _escaped = new(@"\\(.)", RegexOptions.Compiled);
    private static readonly Regex _grouping = new(@"[\\()]", RegexOptions.Compiled);
    private static readonly Regex _luceneSpecial = new(@"[+\-&|!(){}\[\]^""~*?:\\/]", RegexOptions.Compiled);
    private static readonly Regex _operatorWords = new(@"\b(?:AND|OR|NOT)\b", RegexOptions.Compiled);
}
