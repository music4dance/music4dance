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
        public bool SawCancelledToken { get; private set; }

        public Task AppendAsync(string blobName, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            SawCancelledToken |= cancellationToken.IsCancellationRequested;
            if (Fail)
            {
                throw new InvalidOperationException("storage down");
            }
            if (!Lines.TryGetValue(blobName, out var lines))
            {
                Lines[blobName] = lines = [];
            }
            // The caller reuses the buffer, so decode it now.
            lines.AddRange(Encoding.UTF8.GetString(buffer, 0, count)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries));
            return Task.CompletedTask;
        }
    }

    private static RequestLogFlushService Service(RequestLogQueue queue, IRequestLogSink sink,
        int flushIntervalSeconds = 60) =>
        new(queue, sink, new RequestLogOptions { Prefix = "msc4dnc", FlushIntervalSeconds = flushIntervalSeconds },
            NullLogger<RequestLogFlushService>.Instance);

    /// <summary>The blocks WriteBlocksAsync hands out, copied (it reuses the buffer).</summary>
    private static async Task<(List<byte[]> Blocks, int Written)> Blocks(
        IEnumerable<RequestLogEntry> entries, int bufferSize = RequestLogEntry.MaxBlockBytes)
    {
        var blocks = new List<byte[]>();
        var written = await RequestLogEntry.WriteBlocksAsync(entries, new byte[bufferSize], (buffer, count) =>
        {
            blocks.Add(buffer.AsSpan(0, count).ToArray());
            return Task.CompletedTask;
        });
        return (blocks, written);
    }

    [TestMethod]
    public void BlobName_UsesTheHourlyLayout()
    {
        Assert.AreEqual("msc4dnc/y=2026/m=11/d=15/h=10/m=00/PT1H.json",
            RequestLogEntry.BlobName("msc4dnc", Ten));
    }

    [TestMethod]
    public async Task WriteBlocks_WritesPlatformFieldNames()
    {
        var (blocks, written) = await Blocks([Entry(Ten, "/song/details/1")]);
        Assert.AreEqual(1, written);
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
    public async Task WriteBlocks_SplitsAtLineBoundaries()
    {
        var entries = Enumerable.Range(0, 50).Select(i => Entry(Ten, $"/p/{i:D2}")).ToList();
        var oneLine = (await Blocks([entries[0]])).Blocks[0].Length;

        var (blocks, written) = await Blocks(entries, bufferSize: oneLine * 7 + 3);

        Assert.AreEqual(50, written);
        Assert.AreEqual(8, blocks.Count);
        Assert.IsTrue(blocks.All(b => b.Length <= oneLine * 7 + 3));
        Assert.IsTrue(blocks.All(b => b[^1] == (byte)'\n'));
        var lines = blocks.SelectMany(b => Encoding.UTF8.GetString(b).Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToList();
        CollectionAssert.AreEqual(entries.Select(e => e.Path).ToList(),
            lines.Select(l => JsonDocument.Parse(l).RootElement.GetProperty("CsUriStem").GetString()).ToList());
    }

    [TestMethod]
    public async Task WriteBlocks_SkipsALineLongerThanTheBuffer()
    {
        var small = Entry(Ten, "/a");
        var oneLine = (await Blocks([small])).Blocks[0].Length;
        var huge = Entry(Ten, "/" + new string('x', oneLine * 3));

        var (blocks, written) = await Blocks([small, huge, small], bufferSize: oneLine * 2);

        Assert.AreEqual(2, written);
        Assert.AreEqual(1, blocks.Count);
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
    public async Task Stop_FlushesWhatsQueuedWithALiveToken()
    {
        var queue = new RequestLogQueue(10);
        var sink = new FakeSink();
        var service = Service(queue, sink, flushIntervalSeconds: 3600);

        await service.StartAsync(CancellationToken.None);
        queue.Add(Entry(Ten, "/a"));
        queue.Add(Entry(Ten, "/b"));
        await service.StopAsync(CancellationToken.None);

        Assert.AreEqual(2, sink.Lines["msc4dnc/y=2026/m=11/d=15/h=10/m=00/PT1H.json"].Count);
        Assert.IsFalse(sink.SawCancelledToken);
    }

    [TestMethod]
    [DataRow("https://m4dlogs.blob.core.windows.net", "app-request-logs", "https://m4dlogs.blob.core.windows.net/app-request-logs")]
    [DataRow("https://m4dlogs.blob.core.windows.net/", "/app-request-logs/", "https://m4dlogs.blob.core.windows.net/app-request-logs")]
    public void ContainerUri_BuildsFromServiceAndContainer(string service, string container, string expected)
    {
        var uri = new RequestLogOptions { ServiceUri = service, Container = container }.ContainerUri(out var error);
        Assert.AreEqual(expected, uri.ToString());
        Assert.IsNull(error);
    }

    [TestMethod]
    [DataRow("", "app-request-logs")]
    [DataRow("m4dlogs.blob.core.windows.net", "app-request-logs")]
    [DataRow("ftp://m4dlogs.blob.core.windows.net", "app-request-logs")]
    [DataRow("https://m4dlogs.blob.core.windows.net", "")]
    [DataRow("https://m4dlogs.blob.core.windows.net", "  ")]
    public void ContainerUri_RejectsBadSettings(string service, string container)
    {
        var uri = new RequestLogOptions { ServiceUri = service, Container = container }.ContainerUri(out var error);
        Assert.IsNull(uri);
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }

    [TestMethod]
    public void EffectivePrefix_PrefersConfiguredValue()
    {
        Assert.AreEqual("custom", new RequestLogOptions { Prefix = "/custom/" }.EffectivePrefix);
    }
}
