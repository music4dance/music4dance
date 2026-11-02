using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

[TestClass]
public class DanceStatsFileManagerTests
{
    [TestMethod]
    public async Task GetStats_ReadsOnlyTheRuntimeCache()
    {
        // DanceStatsManager falls back itself, so it knows to rebuild what came from the fallback
        var root = TempRoot();
        try
        {
            var files = new DanceStatsFileManager(root);
            WriteFallback(root, "fallback");

            Assert.IsNull(await files.GetStats());
            Assert.AreEqual("fallback", await files.GetFallbackStats());

            await files.WriteStats("runtime");
            Assert.AreEqual("runtime", await files.GetStats());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetFallbackStats_ReadsNullWithoutASnapshot()
    {
        var root = TempRoot();
        try
        {
            Assert.IsNull(await new DanceStatsFileManager(root).GetFallbackStats());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteStats_GoesUnderTheDataRoot()
    {
        // With M4D_DATA_ROOT set, the packaged content stays where the package is mounted
        // (read-only) and only the runtime cache moves to the persistent data root.
        var root = TempRoot();
        var dataRoot = TempRoot();
        try
        {
            var files = new DanceStatsFileManager(root, dataRoot: dataRoot);
            WriteFallback(root, "fallback");

            await files.WriteStats("runtime");

            Assert.IsTrue(File.Exists(Path.Combine(dataRoot, "AppData", "dance-environment.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "AppData")));
            Assert.AreEqual("runtime", await files.GetStats());
            Assert.AreEqual("fallback", await files.GetFallbackStats());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"m4d-dance-stats-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFallback(string root, string json)
    {
        var content = Path.Combine(root, "content");
        _ = Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(content, "dance-environment-fallback.json"), json);
    }
}
