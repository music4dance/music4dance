using System.Text;
using System.Text.Json;

using m4d.Services.RequestLog;

using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Services;

[TestClass]
public class RequestLogTests
{
    private static RequestLogEntry Entry(DateTime time, string path = "/", string userAgent = "ua") =>
        new(time, "203.0.113.45", "172.70.1.20", "www.music4dance.net", "GET", path, "q=1",
            200, 14, 38457, 0, userAgent, "", "_ga=", "HTTP/2");

    private static readonly DateTime Ten = new(2026, 11, 15, 10, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime Eleven = new(2026, 11, 15, 11, 0, 1, DateTimeKind.Utc);

    private sealed class FakeSink : IRequestLogSink
    {
        public readonly Dictionary<string, List<string>> Lines = [];
        public bool Fail { get; set; }

        public Task AppendAsync(string blobName, IReadOnlyList<byte[]> blocks, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("storage down");
            }
            if (!Lines.TryGetValue(blobName, out var lines))
            {
                Lines[blobName] = lines = [];
            }
            lines.AddRange(blocks.SelectMany(b => Encoding.UTF8.GetString(b)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)));
            return Task.CompletedTask;
        }
    }

    private static RequestLogFlushService Service(RequestLogQueue queue, IRequestLogSink sink) =>
        new(queue, sink, new RequestLogOptions { Prefix = "msc4dnc" },
            NullLogger<RequestLogFlushService>.Instance);

    [TestMethod]
    public void BlobName_UsesTheHourlyLayout()
    {
        Assert.AreEqual("msc4dnc/y=2026/m=11/d=15/h=10/m=00/PT1H.json",
            RequestLogEntry.BlobName("msc4dnc", Ten));
    }

    [TestMethod]
    public void ToBlocks_WritesPlatformFieldNames()
    {
        var blocks = RequestLogEntry.ToBlocks([Entry(Ten, "/song/details/1")]);
        Assert.AreEqual(1, blocks.Count);
        var text = Encoding.UTF8.GetString(blocks[0]);
        Assert.IsTrue(text.EndsWith('\n'));

        using var doc = JsonDocument.Parse(text);
        var r = doc.RootElement;
        Assert.AreEqual("2026-11-15T10:59:59.000Z", r.GetProperty("time").GetString());
        Assert.AreEqual("203.0.113.45", r.GetProperty("CIp").GetString());
        Assert.AreEqual("172.70.1.20", r.GetProperty("RelayIp").GetString());
        Assert.AreEqual("/song/details/1", r.GetProperty("CsUriStem").GetString());
        Assert.AreEqual("q=1", r.GetProperty("CsUriQuery").GetString());
        Assert.AreEqual(200, r.GetProperty("ScStatus").GetInt32());
        Assert.AreEqual(14, r.GetProperty("TimeTaken").GetInt64());
        Assert.AreEqual(38457, r.GetProperty("ScBytes").GetInt64());
        Assert.AreEqual("_ga=", r.GetProperty("Cookie").GetString());
    }

    [TestMethod]
    public void ToBlocks_SplitsAtLineBoundaries()
    {
        var entries = Enumerable.Range(0, 50).Select(i => Entry(Ten, $"/p/{i}")).ToList();
        var oneLine = RequestLogEntry.ToBlocks([entries[0]])[0].Length;

        var blocks = RequestLogEntry.ToBlocks(entries, maxBlockBytes: oneLine * 7 + 3);

        Assert.IsTrue(blocks.Count > 1);
        Assert.IsTrue(blocks.All(b => b.Length <= oneLine * 7 + 3));
        Assert.IsTrue(blocks.All(b => b[^1] == (byte)'\n'));
        var lines = blocks.SelectMany(b => Encoding.UTF8.GetString(b).Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToList();
        Assert.AreEqual(50, lines.Count);
        Assert.IsTrue(lines.All(l => JsonDocument.Parse(l).RootElement.GetProperty("CsUriStem").GetString()!.StartsWith("/p/")));
    }

    [TestMethod]
    public void Queue_DropsAndCountsPastCapacity()
    {
        var queue = new RequestLogQueue(2);
        queue.Add(Entry(Ten));
        queue.Add(Entry(Ten));
        queue.Add(Entry(Ten));

        Assert.AreEqual(1, queue.TakeDropped());
        Assert.AreEqual(0, queue.TakeDropped());
        Assert.AreEqual(2, queue.Drain().Count);
        Assert.AreEqual(0, queue.Drain().Count);
    }

    [TestMethod]
    public async Task Flush_GroupsByHour()
    {
        var queue = new RequestLogQueue(10);
        var sink = new FakeSink();
        queue.Add(Entry(Ten, "/a"));
        queue.Add(Entry(Eleven, "/b"));
        queue.Add(Entry(Ten, "/c"));

        var written = await Service(queue, sink).FlushAsync(CancellationToken.None);

        Assert.AreEqual(3, written);
        Assert.AreEqual(2, sink.Lines["msc4dnc/y=2026/m=11/d=15/h=10/m=00/PT1H.json"].Count);
        Assert.AreEqual(1, sink.Lines["msc4dnc/y=2026/m=11/d=15/h=11/m=00/PT1H.json"].Count);
        Assert.AreEqual(0, queue.Drain().Count);
    }

    [TestMethod]
    public async Task Flush_DropsAFailedBatchAndCarriesOn()
    {
        var queue = new RequestLogQueue(10);
        var sink = new FakeSink { Fail = true };
        var service = Service(queue, sink);
        queue.Add(Entry(Ten));

        Assert.AreEqual(0, await service.FlushAsync(CancellationToken.None));
        Assert.AreEqual(0, queue.Drain().Count);

        sink.Fail = false;
        queue.Add(Entry(Eleven));
        Assert.AreEqual(1, await service.FlushAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task Flush_EmptyQueueWritesNothing()
    {
        var sink = new FakeSink();
        Assert.AreEqual(0, await Service(new RequestLogQueue(10), sink).FlushAsync(CancellationToken.None));
        Assert.AreEqual(0, sink.Lines.Count);
    }

    [TestMethod]
    public void EffectivePrefix_PrefersConfiguredValue()
    {
        Assert.AreEqual("custom", new RequestLogOptions { Prefix = "/custom/" }.EffectivePrefix);
    }
}
