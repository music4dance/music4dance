using m4d.Utilities;

using m4dModels;

namespace m4d.Tests.Utilities;

[TestClass]
public class UsageLogTsvTests
{
    private const string Header = "Id\tUsageId\tUserName\tDate\tPage\tQuery\tFilter\tReferrer\tUserAgent";

    private static List<UsageLog> ReadAll(params string[] lines)
    {
        using var reader = new StringReader(string.Join("\r\n", lines) + "\r\n");
        return [.. UsageLogTsv.Read(reader)];
    }

    [TestMethod]
    public void Read_MapsEveryColumnByHeaderName()
    {
        var records = ReadAll(
            Header,
            "42\tabc-123\tdwgray\t2025-03-04 05:06:07.1234567 -08:00\t/song/details\tq=foo\tfilter=bar\thttps://example.com/\tMozilla/5.0");

        Assert.HasCount(1, records);
        var r = records[0];
        Assert.AreEqual(42L, r.Id);
        Assert.AreEqual("abc-123", r.UsageId);
        Assert.AreEqual("dwgray", r.UserName);
        Assert.AreEqual(
            new DateTimeOffset(2025, 3, 4, 5, 6, 7, TimeSpan.FromHours(-8)).AddTicks(1234567),
            r.Date);
        Assert.AreEqual("/song/details", r.Page);
        Assert.AreEqual("q=foo", r.Query);
        Assert.AreEqual("filter=bar", r.Filter);
        Assert.AreEqual("https://example.com/", r.Referrer);
        Assert.AreEqual("Mozilla/5.0", r.UserAgent);
    }

    [TestMethod]
    public void Read_ParsesIsoUtcDate()
    {
        var records = ReadAll(Header, "1\tu\t\t2025-03-04T05:06:07Z\t/\t\t\t\t");

        Assert.AreEqual(new DateTimeOffset(2025, 3, 4, 5, 6, 7, TimeSpan.Zero), records[0].Date);
    }

    [TestMethod]
    public void Read_KeepsQuotesAndCommasLiterally()
    {
        // NoEscape: a quote is ordinary text, not the start of a quoted field.
        const string agent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) \"Chrome\", v=\"1\"";
        var records = ReadAll(Header, $"1\tu\t\t2025-03-04T05:06:07Z\t/\t\"q\"\t\t\t{agent}");

        Assert.AreEqual("\"q\"", records[0].Query);
        Assert.AreEqual(agent, records[0].UserAgent);
    }

    [TestMethod]
    public void Read_EmptyFieldsAreEmptyStrings()
    {
        var records = ReadAll(Header, "1\tu\t\t2025-03-04T05:06:07Z\t/\t\t\t\t");

        var r = records[0];
        Assert.AreEqual(string.Empty, r.UserName);
        Assert.AreEqual(string.Empty, r.Query);
        Assert.AreEqual(string.Empty, r.Filter);
        Assert.AreEqual(string.Empty, r.Referrer);
        Assert.AreEqual(string.Empty, r.UserAgent);
    }

    [TestMethod]
    public void Read_ColumnOrderComesFromHeader_AndUnknownColumnsAreIgnored()
    {
        var records = ReadAll(
            "UserAgent\tExtra\tPage\tDate\tUsageId\tId\tUserName\tQuery\tFilter\tReferrer",
            "ua\tignored\t/p\t2025-03-04T05:06:07Z\tu1\t7\tname\t\t\tref");

        var r = records[0];
        Assert.AreEqual("ua", r.UserAgent);
        Assert.AreEqual("/p", r.Page);
        Assert.AreEqual("u1", r.UsageId);
        Assert.AreEqual(7L, r.Id);
        Assert.AreEqual("name", r.UserName);
        Assert.AreEqual("ref", r.Referrer);
    }

    [TestMethod]
    public void Read_ReadsManyRowsInOrder()
    {
        var rows = Enumerable.Range(1, 250)
            .Select(i => $"{i}\tu{i}\t\t2025-03-04T05:06:07Z\t/p{i}\t\t\t\t")
            .Prepend(Header)
            .ToArray();

        var records = ReadAll(rows);

        Assert.HasCount(250, records);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 250).Select(i => $"u{i}").ToList(),
            records.Select(r => r.UsageId).ToList());
    }

    [TestMethod]
    public void Read_HeaderOnly_ReturnsNoRecords()
    {
        Assert.HasCount(0, ReadAll(Header));
    }

    [TestMethod]
    public void Read_MissingColumn_Throws()
    {
        Assert.Throws<Exception>(() => ReadAll(
            "Id\tUsageId\tUserName\tPage\tQuery\tFilter\tReferrer\tUserAgent",
            "1\tu\t\t/\t\t\t\t"));
    }

    [TestMethod]
    public void Read_BadDate_Throws()
    {
        Assert.Throws<Exception>(() => ReadAll(Header, "1\tu\t\tnot-a-date\t/\t\t\t\t"));
    }

    [TestMethod]
    public void Read_LeavesReaderOpen()
    {
        using var reader = new StringReader(Header + "\r\n");
        _ = UsageLogTsv.Read(reader).ToList();

        Assert.AreEqual(-1, reader.Peek());
    }
}
