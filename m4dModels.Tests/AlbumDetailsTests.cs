namespace m4dModels.Tests;

[TestClass]
public class AlbumDetailsTests
{
    [TestMethod]
    public void AddPurchaseId_FirstId_StoresIt()
    {
        var ad = new AlbumDetails();

        var changed = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "track123");

        Assert.IsTrue(changed);
        Assert.AreEqual(
            "track123", ad.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void AddPurchaseId_DifferentId_AccumulatesBothWithoutLosingTheFirst()
    {
        // Spotify (and other services) periodically reissue a different id for what is
        // otherwise the same recording/album - both should stay registered.
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");

        var changed = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "newTrack456");

        Assert.IsTrue(changed);
        CollectionAssert.AreEqual(
            new[] { "oldTrack123", "newTrack456" },
            (System.Collections.ICollection)ad.GetPurchaseIdentifiers(
                ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void AddPurchaseId_SameIdAgain_NoOp()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "track123");

        var changed = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "track123");

        Assert.IsFalse(changed);
        CollectionAssert.AreEqual(
            new[] { "track123" },
            (System.Collections.ICollection)ad.GetPurchaseIdentifiers(
                ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void GetPurchaseIdentifier_NoIds_ReturnsNull()
    {
        var ad = new AlbumDetails();

        Assert.IsNull(ad.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void GetPurchaseIdentifier_MultipleIds_ReturnsThePrimaryOnly()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "newTrack456");

        Assert.AreEqual(
            "oldTrack123", ad.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void GetPurchaseLink_MultipleIds_LinksToThePrimaryIdOnly()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "newTrack456");

        var link = ad.GetPurchaseLink(ServiceType.Spotify, PurchaseType.Song);

        Assert.IsNotNull(link);
        Assert.AreEqual("https://open.spotify.com/track/oldTrack123", link.Link);
    }

    [TestMethod]
    public void GetExtendedPurchaseIds_MultipleIds_ReturnsOneEntryPerId()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "newTrack456");

        var ids = ad.GetExtendedPurchaseIds(PurchaseType.Song);

        CollectionAssert.AreEqual(
            new[] { "S:oldTrack123", "S:newTrack456" }, (System.Collections.ICollection)ids);
    }

    [TestMethod]
    public void PurchaseDiff_SlotUnchanged_ReturnsFalse()
    {
        var old = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = old.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "track123");

        var edit = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = edit.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "track123");

        var song = new Song();

        var modified = edit.PurchaseDiff(song, old);

        Assert.IsFalse(modified);
        Assert.AreEqual(0, song.SongProperties.Count);
    }

    [TestMethod]
    public void PurchaseDiff_AccumulatedIdOnExistingSlot_EmitsChangeAndReturnsTrue()
    {
        // Regression test for the bug where !string.Equals(value, value) was always false,
        // so adding a second ID via AddPurchaseId never produced a SongProperty update —
        // meaning the new ID never reached the Azure ServiceIds field.
        var old = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = old.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");

        var edit = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = edit.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");
        _ = edit.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "newTrack456");

        var song = new Song();

        var modified = edit.PurchaseDiff(song, old);

        Assert.IsTrue(modified);
        var expectedName = SongProperty.FormatName(Song.PurchaseField, 0, "SS");
        var prop = song.SongProperties.FirstOrDefault(p => p.Name == expectedName);
        Assert.IsNotNull(prop, $"Expected a {expectedName} property to be written");
        Assert.AreEqual("oldTrack123,newTrack456", prop.Value);
    }
}
