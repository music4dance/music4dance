#nullable disable

using System.Text.Json;

using m4d.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Services;

[TestClass]
[DoNotParallelize] // Tests set UpdateMessageService's process-wide RefreshInterval (restored after each).
public class UpdateMessageServiceTests
{
    private class FakeFiles : IUpdateMessageFileManager
    {
        public string Json { get; set; }
        public int Reads { get; private set; }
        public bool FailWrites { get; set; }
        public bool FailReads { get; set; }

        public string Read()
        {
            Reads++;
            return FailReads ? throw new IOException("share unavailable") : Json;
        }

        public void Write(string json)
        {
            if (FailWrites)
            {
                throw new IOException("share unavailable");
            }
            Json = json;
        }

        public void Delete()
        {
            if (FailWrites)
            {
                throw new IOException("share unavailable");
            }
            Json = null;
        }
    }

    private static readonly DateTime Started = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private TimeSpan _savedInterval;
    private string _directory;

    [TestInitialize]
    public void Initialize()
    {
        _savedInterval = UpdateMessageService.RefreshInterval;
        UpdateMessageService.RefreshInterval = TimeSpan.Zero;
        _directory = Path.Combine(Path.GetTempPath(), $"m4d-update-message-{Guid.NewGuid():N}");
    }

    [TestCleanup]
    public void Cleanup()
    {
        UpdateMessageService.RefreshInterval = _savedInterval;
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static UpdateMessageService Service(IUpdateMessageFileManager files, DateTime? started = null) =>
        new(NullLogger<UpdateMessageService>.Instance, files, started ?? Started);

    private static string Stored(string message, bool persist, DateTime setAt) =>
        JsonSerializer.Serialize(new UpdateMessageState(message, persist, "dwgray", setAt));

    [TestMethod]
    public void Startup_KeepsAPersistedMessage()
    {
        var files = new FakeFiles { Json = Stored("Upgrading", persist: true, Started.AddHours(-1)) };

        var service = Service(files);

        Assert.AreEqual("Upgrading", service.Message);
        Assert.IsTrue(service.Current.Persist);
        Assert.AreEqual("dwgray", service.Current.SetBy);
    }

    [TestMethod]
    public void Startup_DropsAndDeletesANonPersistedMessageFromBeforeTheRestart()
    {
        var files = new FakeFiles { Json = Stored("Upgrading", persist: false, Started.AddHours(-1)) };

        var service = Service(files);

        Assert.IsNull(service.Message);
        Assert.IsNull(files.Json);
    }

    [TestMethod]
    public void Set_PersistedWritesTheFile_ClearDeletesIt()
    {
        var files = new FakeFiles();
        var service = Service(files);

        Assert.IsTrue(service.Set("Upgrading", persist: true, "dwgray"));
        Assert.IsNotNull(files.Json);
        Assert.AreEqual("Upgrading", service.Message);

        Assert.IsTrue(service.Clear("dwgray"));
        Assert.IsNull(files.Json);
        Assert.IsNull(service.Message);
    }

    [TestMethod]
    public void Set_WhitespaceClears()
    {
        var files = new FakeFiles();
        var service = Service(files);
        _ = service.Set("Upgrading", persist: true, "dwgray");

        _ = service.Set("   ", persist: true, "dwgray");

        Assert.IsNull(service.Message);
        Assert.IsNull(files.Json);
    }

    [TestMethod]
    public void Set_NonPersistedIsSharedUntilTheNextStartup()
    {
        var files = new FakeFiles();
        var started = DateTime.UtcNow.AddMinutes(-1);
        _ = Service(files, started).Set("Rebuilding", persist: false, "UpdateSearchIdx");

        // A peer that was already running sees it...
        Assert.AreEqual("Rebuilding", Service(files, started).Message);

        // ...and the next instance to start drops it
        Assert.IsNull(Service(files, DateTime.UtcNow.AddMinutes(1)).Message);
        Assert.IsNull(files.Json);
    }

    [TestMethod]
    public void Current_RereadsTheFileOnlyAfterTheRefreshInterval()
    {
        UpdateMessageService.RefreshInterval = TimeSpan.FromHours(1);
        var files = new FakeFiles();
        var service = Service(files);
        var reads = files.Reads;

        files.Json = Stored("Set elsewhere", persist: true, DateTime.UtcNow);

        Assert.IsNull(service.Message);
        Assert.AreEqual(reads, files.Reads);

        UpdateMessageService.RefreshInterval = TimeSpan.Zero;
        Assert.AreEqual("Set elsewhere", service.Message);
    }

    [TestMethod]
    public void Current_KeepsTheLastMessageWhenTheFileCannotBeRead()
    {
        var files = new FakeFiles();
        var service = Service(files);
        _ = service.Set("Upgrading", persist: true, "dwgray");

        files.FailReads = true;

        Assert.AreEqual("Upgrading", service.Message);
    }

    [TestMethod]
    public void Set_FailedWriteShowsLocallyAndIsNotUndoneByARefresh()
    {
        var files = new FakeFiles { FailWrites = true };
        var service = Service(files);

        Assert.IsFalse(service.Set("Upgrading", persist: true, "dwgray"));
        Assert.AreEqual("Upgrading", service.Message);

        files.FailWrites = false;
        Assert.IsTrue(service.Clear("dwgray"));
        Assert.IsNull(service.Message);
    }

    [TestMethod]
    public void Startup_CorruptFileMeansNoMessage()
    {
        var files = new FakeFiles { Json = "{ not json" };

        var service = Service(files);

        Assert.IsNull(service.Message);
    }

    [TestMethod]
    public void WithoutAFileManager_LivesInMemory()
    {
        var service = Service(null);

        Assert.IsTrue(service.Set("Sandbox", persist: true, "dwgray"));

        Assert.AreEqual("Sandbox", service.Message);
    }

    [TestMethod]
    public void FileManager_TwoInstancesShareTheFileAndARestartKeepsAPersistedMessage()
    {
        var started = DateTime.UtcNow.AddMinutes(-1);
        var first = Service(new UpdateMessageFileManager(_directory), started);
        var second = Service(new UpdateMessageFileManager(_directory), started);

        Assert.IsTrue(first.Set("Upgrading", persist: true, "dwgray"));
        Assert.AreEqual("Upgrading", second.Message);
        Assert.IsTrue(File.Exists(Path.Combine(_directory, "AppData", "update-message.json")));

        var restarted = Service(new UpdateMessageFileManager(_directory), DateTime.UtcNow.AddMinutes(1));
        Assert.AreEqual("Upgrading", restarted.Message);

        Assert.IsTrue(second.Clear("dwgray"));
        Assert.IsNull(first.Message);
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "AppData", "update-message.json")));
    }
}
