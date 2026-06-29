using Microsoft.VisualStudio.TestTools.UnitTesting;
using m4d.Controllers;
using m4dModels;

namespace m4d.Tests.Controllers;

[TestClass]
public class SongControllerTests
{
    #region RemoveDeadPurchaseId Tests

    [TestMethod]
    public void RemoveDeadPurchaseId_SingleId_RemovesTheWholeProperty()
    {
        var props = new List<SongProperty> { new("Purchase:00:SS", "deadTrack123") };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "deadTrack123");

        Assert.IsTrue(changed);
        Assert.AreEqual(1, del.Count);
        Assert.AreEqual("deadTrack123", del[0].Value);
    }

    [TestMethod]
    public void RemoveDeadPurchaseId_MultipleIds_StripsOnlyTheDeadOnePreservingSiblings()
    {
        // A property may hold more than one id for the same service/type (see
        // AlbumDetails.AddPurchaseId) - removing a dead one should not lose a sibling
        // that's still good.
        var prop = new SongProperty("Purchase:00:SS", "deadTrack123,liveTrack456");
        var props = new List<SongProperty> { prop };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "deadTrack123");

        Assert.IsTrue(changed);
        Assert.AreEqual(0, del.Count);
        Assert.AreEqual("liveTrack456", prop.Value);
    }

    [TestMethod]
    public void RemoveDeadPurchaseId_IdNotPresent_NoOp()
    {
        var props = new List<SongProperty> { new("Purchase:00:SS", "liveTrack456") };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "deadTrack123");

        Assert.IsFalse(changed);
        Assert.AreEqual(0, del.Count);
    }

    [TestMethod]
    public void RemoveDeadPurchaseId_EmptyDeadId_NoOp()
    {
        var props = new List<SongProperty> { new("Purchase:00:SS", "liveTrack456") };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "");

        Assert.IsFalse(changed);
        Assert.AreEqual(0, del.Count);
    }

    #endregion
}
