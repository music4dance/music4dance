using m4dModels.Utilities;

namespace m4dModels.Tests;

[TestClass]
public class StringHelpersTests
{
    // ─── UnquoteCsvCell ───────────────────────────────────────────────────────

    [TestMethod]
    public void UnquoteCsvCell_PlainValue_Unchanged()
    {
        Assert.AreEqual("hello", "hello".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_SimpleWrappedValue_StripsOuterQuotes()
    {
        Assert.AreEqual("hello", "\"hello\"".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_InternalDoubledQuotes_Unescaped()
    {
        // RFC 4180: "" inside a quoted field → single "
        // Export-Csv encodes  "Ring" by Darren  as  """Ring"" by Darren"
        Assert.AreEqual("\"Ring\" by Darren", "\"\"\"Ring\"\" by Darren\"".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_EmptyQuotedString_ReturnsEmpty()
    {
        Assert.AreEqual("", "\"\"".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_EmptyString_Unchanged()
    {
        Assert.AreEqual("", "".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_SingleQuoteChar_Unchanged()
    {
        // A lone " is not a valid RFC 4180 quoted field — return as-is
        Assert.AreEqual("\"", "\"".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_StartsWithQuoteNoClosing_Unchanged()
    {
        Assert.AreEqual("\"no close", "\"no close".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_EndsWithQuoteNoOpening_Unchanged()
    {
        Assert.AreEqual("no open\"", "no open\"".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_MultipleInternalDoubledQuotes_AllUnescaped()
    {
        // "say ""hello"" and ""bye""" → say "hello" and "bye"
        Assert.AreEqual("say \"hello\" and \"bye\"",
            "\"say \"\"hello\"\" and \"\"bye\"\"\"".UnquoteCsvCell());
    }

    [TestMethod]
    public void UnquoteCsvCell_ValueWithNoInternalQuotes_StripsOnlyOuter()
    {
        Assert.AreEqual("Sherry McClure (USA)", "\"Sherry McClure (USA)\"".UnquoteCsvCell());
    }
}
