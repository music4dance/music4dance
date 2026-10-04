using m4d.Utilities;

using Newtonsoft.Json.Linq;

namespace m4d.Tests.Utilities;

[TestClass]
public class ScriptJsonTests
{
    private const string Attack = "</script><script>alert(1)</script>";

    [TestMethod]
    public void Serialize_StringValue_CannotCloseScriptBlock()
    {
        var json = ScriptJson.Serialize(new { Title = Attack, Artist = "Tom & Jerry's \"Band\"" });

        Assert.IsFalse(json.Contains('<'), json);
        Assert.IsFalse(json.Contains('>'), json);
        Assert.IsFalse(json.Contains('&'), json);

        var parsed = JObject.Parse(json);
        Assert.AreEqual(Attack, parsed["Title"]!.Value<string>());
        Assert.AreEqual("Tom & Jerry's \"Band\"", parsed["Artist"]!.Value<string>());
    }

    [TestMethod]
    public void Serialize_CamelCase_IgnoresNulls()
    {
        var json = ScriptJson.Serialize(new { SongTitle = "x", Missing = (int?)null }, camelCase: true);

        Assert.AreEqual("{\"songTitle\":\"x\"}", json);
    }

    [TestMethod]
    public void Serialize_PlainString_IsAQuotedStringLiteral()
    {
        // Vue3() string models used to be wrapped as '...' with only ' escaped.
        var json = ScriptJson.Serialize("it's a \\ " + Attack);

        Assert.IsTrue(json.StartsWith('"') && json.EndsWith('"'), json);
        Assert.IsFalse(json.Contains('<'), json);
        Assert.AreEqual("it's a \\ " + Attack, JToken.Parse(json).Value<string>());
    }

    [TestMethod]
    public void Escape_PreSerializedJson_KeepsValueAndRemovesMarkup()
    {
        var original = new JArray(new JObject(new JProperty("key", Attack + "\u2028"), new JProperty("count", 3)))
            .ToString();

        var escaped = ScriptJson.Escape(original);

        Assert.IsFalse(escaped.Contains('<'), escaped);
        Assert.IsFalse(escaped.Contains('\u2028'), escaped);
        Assert.IsTrue(JToken.DeepEquals(JToken.Parse(original), JToken.Parse(escaped)));
    }

    [TestMethod]
    public void Escape_Null_ReturnsNull()
    {
        Assert.IsNull(ScriptJson.Escape(null));
    }
}
