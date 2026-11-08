using m4d.Security;

namespace m4d.Tests.Security;

[TestClass]
public class Http4xxCsvTests
{
    private static string Write(params Http4xxUrlStats[] urls)
    {
        using var writer = new StringWriter();
        Http4xxCsv.Write(writer, urls);
        return writer.ToString();
    }

    private static readonly DateTime Seen = new(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    [TestMethod]
    public void Write_Empty_WritesHeaderOnly()
    {
        Assert.AreEqual("Url,Status,Count,LastSeen\r\n", Write());
    }

    [TestMethod]
    public void Write_WritesOneRowPerUrl()
    {
        var csv = Write(
            new Http4xxUrlStats { Url = "/missing", StatusCode = 404, Count = 12, LastSeen = Seen },
            new Http4xxUrlStats { Url = "/gone", StatusCode = 410, Count = 1, LastSeen = Seen });

        Assert.AreEqual(
            "Url,Status,Count,LastSeen\r\n" +
            "/missing,404,12,2025-03-04T05:06:07.0000000Z\r\n" +
            "/gone,410,1,2025-03-04T05:06:07.0000000Z\r\n",
            csv);
    }

    [TestMethod]
    public void Write_QuotesUrlsContainingSeparatorsAndQuotes()
    {
        var csv = Write(
            new Http4xxUrlStats { Url = "/search?q=a,b&t=\"x\"", StatusCode = 404, Count = 3, LastSeen = Seen });

        StringAssert.Contains(csv, "\"/search?q=a,b&t=\"\"x\"\"\",404,3,");
    }

    [TestMethod]
    public void Write_DoesNotAlterLeadingFormulaCharacters()
    {
        var csv = Write(
            new Http4xxUrlStats { Url = "=cmd", StatusCode = 400, Count = 1, LastSeen = Seen });

        StringAssert.Contains(csv, "\r\n=cmd,400,1,");
    }

    [TestMethod]
    public void Write_LeavesWriterOpen()
    {
        using var writer = new StringWriter();
        Http4xxCsv.Write(writer, []);
        writer.Write("more");

        StringAssert.EndsWith(writer.ToString(), "more");
    }
}
