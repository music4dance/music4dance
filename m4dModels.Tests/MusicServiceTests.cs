using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

[TestClass]
public class MusicServiceTests
{
    [TestMethod]
    public void TestFilter()
    {
        Assert.IsNull(MusicService.FormatPurchaseFilter(""));
        Assert.IsNull(MusicService.FormatPurchaseFilter("BZY"));
        Assert.AreEqual("Amazon, Spotify", MusicService.FormatPurchaseFilter("AS"));
        Assert.AreEqual("ITunes", MusicService.FormatPurchaseFilter("I", "; "));
    }
}
