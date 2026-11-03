#nullable disable

using m4d.Configuration;

using Microsoft.Extensions.Configuration;

namespace m4d.Tests.Configuration;

[TestClass]
public class AppDataPathsTests
{
    private static IConfiguration Settings(string dataRoot) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string> { [AppDataPaths.RootSetting] = dataRoot })
        .Build();

    [TestMethod]
    public void Resolve_FallsBackToTheWebRootWhenUnset()
    {
        // Local runs, the sandbox and tests keep their files in wwwroot/AppData, as before
        var webRoot = Path.Combine(Path.GetTempPath(), "m4d-web-root");

        var paths = AppDataPaths.Resolve(Settings(null), webRoot);

        Assert.AreEqual(Path.GetFullPath(webRoot), paths.Root);
        Assert.AreEqual(Path.Combine(Path.GetFullPath(webRoot), "AppData"), paths.AppData);
    }

    [TestMethod]
    public void Resolve_TreatsABlankSettingAsUnset()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "m4d-web-root");

        Assert.AreEqual(Path.GetFullPath(webRoot), AppDataPaths.Resolve(Settings("  "), webRoot).Root);
    }

    [TestMethod]
    public void Resolve_UsesTheSettingWhenSet()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "m4d-data-root");

        var paths = AppDataPaths.Resolve(Settings(dataRoot), Path.Combine(Path.GetTempPath(), "m4d-web-root"));

        Assert.AreEqual(Path.GetFullPath(dataRoot), paths.Root);
        Assert.AreEqual(Path.Combine(Path.GetFullPath(dataRoot), "AppData"), paths.AppData);
    }

    [TestMethod]
    public void Resolve_FailsWithNoSettingAndNoWebRoot()
    {
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => AppDataPaths.Resolve(Settings(null), null));
    }

    [TestMethod]
    public void EnsureCreated_CreatesTheAppDataFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"m4d-data-root-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppDataPaths(root).EnsureCreated();

            Assert.IsTrue(Directory.Exists(paths.AppData));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
