using System.Text;

using m4d.Configuration;
using m4d.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Services;

[TestClass]
public class RequestLogUploaderTests
{
    private const string Today = "requests-1a2b3c4d-20261116.0000.txt";
    private const string Yesterday = "requests-1a2b3c4d-20261115.0000.txt";

    private string _directory = "";
    private FakeStore _store = new();

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "m4d-request-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _store = new FakeStore();
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    /// <summary>Like an append blob with IfAppendPositionEqual: an append must start at the end.</summary>
    private sealed class FakeStore : IRequestLogStore
    {
        public Dictionary<string, List<byte>> Blobs { get; } = [];
        public List<(string Name, int Length)> Appends { get; } = [];
        public string FailOn { get; set; } = "";
        public List<Exception> ListFailures { get; } = [];
        public int Listings { get; private set; }

        public string Text(string name) => Encoding.UTF8.GetString(Blobs[name].ToArray());

        public Task<IReadOnlyDictionary<string, long>> GetLengthsAsync(string prefix, CancellationToken cancellationToken)
        {
            Listings += 1;
            if (ListFailures.Count > 0)
            {
                throw ListFailures[0];
            }
            IReadOnlyDictionary<string, long> lengths = Blobs
                .Where(b => b.Key.StartsWith(prefix, StringComparison.Ordinal))
                .ToDictionary(b => b.Key, b => (long)b.Value.Count);
            return Task.FromResult(lengths);
        }

        public Task AppendAsync(string name, long offset, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            if (name == FailOn)
            {
                throw new IOException("storage is down");
            }
            var blob = Blobs.TryGetValue(name, out var existing) ? existing : Blobs[name] = [];
            if (blob.Count != offset)
            {
                throw new InvalidOperationException($"append at {offset} but the blob is {blob.Count} bytes");
            }
            blob.AddRange(content.ToArray());
            Appends.Add((name, content.Length));
            return Task.CompletedTask;
        }
    }

    private RequestLogUploader Uploader(int chunkBytes = 1024 * 1024) => new(
        new RequestLogSetup.Settings(_directory, "requests-1a2b3c4d-", 30, 100,
            new Uri("https://m4dlogs.blob.core.windows.net/c")),
        _store, NullLogger<RequestLogUploader>.Instance)
    { ChunkBytes = chunkBytes };

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_directory, name), text);

    private void Append(string name, string text) => File.AppendAllText(Path.Combine(_directory, name), text);

    [TestMethod]
    public async Task Upload_CopiesEachFile_ThenNothingUntilItGrows()
    {
        Write(Yesterday, "#Fields: date\n2026-11-15\n");
        Write(Today, "#Fields: date\n2026-11-16\n");
        var uploader = Uploader();

        await uploader.UploadAsync(CancellationToken.None);
        var second = await uploader.UploadAsync(CancellationToken.None);

        Assert.AreEqual("#Fields: date\n2026-11-15\n", _store.Text(Yesterday));
        Assert.AreEqual("#Fields: date\n2026-11-16\n", _store.Text(Today));
        Assert.AreEqual(0, second);
        Assert.AreEqual(2, _store.Appends.Count);
    }

    [TestMethod]
    public async Task Upload_AppendsOnlyWhatTheFileGained()
    {
        Write(Today, "line 1\n");
        var uploader = Uploader();
        await uploader.UploadAsync(CancellationToken.None);

        Append(Today, "line 2\nline 3\n");
        var uploaded = await uploader.UploadAsync(CancellationToken.None);

        Assert.AreEqual("line 2\nline 3\n".Length, uploaded);
        Assert.AreEqual("line 1\nline 2\nline 3\n", _store.Text(Today));
    }

    [TestMethod]
    public async Task Upload_HoldsBackAPartialLastLine()
    {
        Write(Today, "line 1\nline");
        var uploader = Uploader();

        await uploader.UploadAsync(CancellationToken.None);
        Assert.AreEqual("line 1\n", _store.Text(Today));

        Append(Today, " 2\n");
        await uploader.UploadAsync(CancellationToken.None);
        Assert.AreEqual("line 1\nline 2\n", _store.Text(Today));
    }

    [TestMethod]
    public async Task Upload_ResumesFromTheBlobLength()
    {
        // As after a restart: the blob already has the first line.
        Write(Today, "line 1\nline 2\n");
        _store.Blobs[Today] = [.. Encoding.UTF8.GetBytes("line 1\n")];

        var uploaded = await Uploader().UploadAsync(CancellationToken.None);

        Assert.AreEqual("line 2\n".Length, uploaded);
        Assert.AreEqual("line 1\nline 2\n", _store.Text(Today));
    }

    [TestMethod]
    public async Task Upload_SplitsALargeFileIntoChunksOfWholeLines()
    {
        var text = string.Concat(Enumerable.Range(1, 50).Select(i => $"line {i}\n"));
        Write(Today, text);

        await Uploader(chunkBytes: 32).UploadAsync(CancellationToken.None);

        Assert.AreEqual(text, _store.Text(Today));
        Assert.IsTrue(_store.Appends.Count > 10);
        Assert.IsTrue(_store.Appends.All(a => a.Length <= 32));
    }

    [TestMethod]
    public async Task Upload_ALineLongerThanAChunk_StillGoes()
    {
        var text = new string('x', 100) + "\n";
        Write(Today, text);

        await Uploader(chunkBytes: 32).UploadAsync(CancellationToken.None);

        Assert.AreEqual(text, _store.Text(Today));
    }

    [TestMethod]
    public async Task Upload_AFailedFile_DoesNotStopTheOthers()
    {
        Write(Yesterday, "old\n");
        Write(Today, "new\n");
        _store.FailOn = Yesterday;

        await Uploader().UploadAsync(CancellationToken.None);

        Assert.IsFalse(_store.Blobs.ContainsKey(Yesterday));
        Assert.AreEqual("new\n", _store.Text(Today));
    }

    [TestMethod]
    public async Task Upload_ReadsAFileTheLoggerHasOpen()
    {
        // The W3C logger appends through File.AppendText, which shares the file for reading.
        await using (var writer = new StreamWriter(new FileStream(Path.Combine(_directory, Today),
            FileMode.Append, FileAccess.Write, FileShare.Read)))
        {
            await writer.WriteAsync("line 1\n");
            await writer.FlushAsync();

            await Uploader().UploadAsync(CancellationToken.None);
        }

        Assert.AreEqual("line 1\n", _store.Text(Today));
    }

    [TestMethod]
    public async Task Upload_ListsTheBlobsOncePerPass()
    {
        Write(Yesterday, "old\n");
        Write(Today, "new\n");

        await Uploader().UploadAsync(CancellationToken.None);

        Assert.AreEqual(1, _store.Listings);
    }

    [TestMethod]
    public async Task Upload_CopiesFilesAPreviousInstanceLeftBehind()
    {
        const string other = "requests-99887766-20261114.0000.txt";
        Write(other, "from another instance\n");

        await Uploader().UploadAsync(CancellationToken.None);

        Assert.AreEqual("from another instance\n", _store.Text(other));
    }

    [TestMethod]
    public async Task Upload_DeletesCopiedFilesOnceTheyAreOld()
    {
        Write(Yesterday, "old\n");
        Write(Today, "new\n");
        var uploader = Uploader();
        await uploader.UploadAsync(CancellationToken.None);
        File.SetLastWriteTimeUtc(Path.Combine(_directory, Yesterday),
            DateTime.UtcNow - RequestLogUploader.KeepAfterUpload - TimeSpan.FromHours(1));

        await uploader.UploadAsync(CancellationToken.None);

        Assert.IsFalse(File.Exists(Path.Combine(_directory, Yesterday)));
        Assert.IsTrue(File.Exists(Path.Combine(_directory, Today)), "a recent file stays");
        Assert.AreEqual("old\n", _store.Text(Yesterday));
    }

    [TestMethod]
    public async Task Upload_KeepsOldFilesThatWereNotCopied()
    {
        Write(Yesterday, "old\n");
        File.SetLastWriteTimeUtc(Path.Combine(_directory, Yesterday), DateTime.UtcNow - TimeSpan.FromDays(10));
        _store.FailOn = Yesterday;

        await Uploader().UploadAsync(CancellationToken.None);

        Assert.IsTrue(File.Exists(Path.Combine(_directory, Yesterday)));
    }

    [TestMethod]
    public async Task TryUpload_AFailedPass_DoesNotThrow()
    {
        Write(Today, "line 1\n");
        _store.ListFailures.Add(new IOException("network share hiccup"));

        // Before the fix this escaped ExecuteAsync, which stops the host by default.
        await Uploader().TryUploadAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task TryUpload_ATimeoutThatIsNotShutdown_DoesNotThrow()
    {
        // An SDK or HttpClient timeout surfaces as a TaskCanceledException.
        Write(Today, "line 1\n");
        _store.ListFailures.Add(new TaskCanceledException("timed out"));

        // Before the fix this escaped ExecuteAsync, which stops the host by default.
        await Uploader().TryUploadAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task TryUpload_Shutdown_StillCancels()
    {
        Write(Today, "line 1\n");
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();
        _store.ListFailures.Add(new OperationCanceledException(shutdown.Token));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Uploader().TryUploadAsync(shutdown.Token));
    }

    [TestMethod]
    public async Task Upload_IgnoresOtherFilesAndAMissingDirectory()
    {
        Write("notes.txt", "not a log\n");
        Assert.AreEqual(0, await Uploader().UploadAsync(CancellationToken.None));

        Directory.Delete(_directory, recursive: true);
        Assert.AreEqual(0, await Uploader().UploadAsync(CancellationToken.None));
        Directory.CreateDirectory(_directory);
    }
}
