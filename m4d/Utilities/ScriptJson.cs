using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4d.Utilities;

/// <summary>
/// Builds JSON that is safe to emit through Html.Raw inside an inline &lt;script&gt; block.
/// A string value containing "&lt;/script&gt;" would otherwise close the block and let the rest
/// of the value run as markup, so &lt;, &gt;, &amp; and quotes are written as \uXXXX escapes.
/// The resulting JavaScript values are unchanged.
/// </summary>
public static class ScriptJson
{
    // Legal in JSON strings but line terminators in older JavaScript engines.
    private const string LineSeparator = "\u2028";
    private const string ParagraphSeparator = "\u2029";

    public static string Serialize(object value, bool camelCase = false, bool indented = false)
    {
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            StringEscapeHandling = StringEscapeHandling.EscapeHtml,
        };
        if (camelCase)
        {
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        }

        return JsonConvert.SerializeObject(
            value, indented ? Formatting.Indented : Formatting.None, settings);
    }

    /// <summary>
    /// Makes JSON that was serialized elsewhere safe for a script block. In valid JSON these
    /// characters can only appear inside string literals, where the \uXXXX form decodes to the
    /// same character.
    /// </summary>
    public static string Escape(string json)
    {
        return json?
            .Replace("<", "\\u003c")
            .Replace(">", "\\u003e")
            .Replace("&", "\\u0026")
            .Replace(LineSeparator, "\\u2028")
            .Replace(ParagraphSeparator, "\\u2029");
    }
}
