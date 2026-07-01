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

    #region AddPurchaseId(string key, string id) overload

    [TestMethod]
    public void AddPurchaseId_ByKey_FirstId_StoresIt()
    {
        var ad = new AlbumDetails();

        var changed = ad.AddPurchaseId("SS", "track123");

        Assert.IsTrue(changed);
        Assert.AreEqual("track123", ad.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void AddPurchaseId_ByKey_DifferentId_AccumulatesBoth()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId("SS", "oldTrack123");

        var changed = ad.AddPurchaseId("SS", "newTrack456");

        Assert.IsTrue(changed);
        CollectionAssert.AreEqual(
            new[] { "oldTrack123", "newTrack456" },
            (System.Collections.ICollection)ad.GetPurchaseIdentifiers(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void AddPurchaseId_ByKey_SameId_NoOp()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId("SS", "track123");

        var changed = ad.AddPurchaseId("SS", "track123");

        Assert.IsFalse(changed);
    }

    #endregion

    #region RemovePurchaseId

    [TestMethod]
    public void RemovePurchaseId_ExistingId_RemovesIt()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId("SS", "id1");
        _ = ad.AddPurchaseId("SS", "id2");

        var changed = ad.RemovePurchaseId("SS", "id1");

        Assert.IsTrue(changed);
        CollectionAssert.AreEqual(
            new[] { "id2" },
            (System.Collections.ICollection)ad.GetPurchaseIdentifiers(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void RemovePurchaseId_OnlyId_ClearsTheSlot()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId("SS", "track123");

        var changed = ad.RemovePurchaseId("SS", "track123");

        Assert.IsTrue(changed);
        Assert.IsNull(ad.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
        Assert.IsFalse(ad.HasPurchaseInfo);
    }

    [TestMethod]
    public void RemovePurchaseId_IdNotPresent_NoOp()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId("SS", "track123");

        var changed = ad.RemovePurchaseId("SS", "unknownId");

        Assert.IsFalse(changed);
        Assert.AreEqual("track123", ad.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void RemovePurchaseId_EmptyPurchase_NoOp()
    {
        var ad = new AlbumDetails();

        var changed = ad.RemovePurchaseId("SS", "track123");

        Assert.IsFalse(changed);
    }

    #endregion

    #region PurchaseDiff

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
    public void PurchaseDiff_NewIdAdded_EmitsOnePurchasePropertyForTheNewIdOnly()
    {
        // When a second id is accumulated on an existing slot, PurchaseDiff should emit
        // a single Purchase property carrying just the new id — not a comma-separated blob.
        var old = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = old.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");

        var edit = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = edit.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");
        _ = edit.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "newTrack456");

        var song = new Song();

        var modified = edit.PurchaseDiff(song, old);

        Assert.IsTrue(modified);
        var purchaseProps = song.SongProperties
            .Where(p => p.Name == SongProperty.FormatName(Song.PurchaseField, 0, "SS"))
            .ToList();
        Assert.AreEqual(1, purchaseProps.Count, "Expected exactly one Purchase property for the new id");
        Assert.AreEqual("newTrack456", purchaseProps[0].Value);
    }

    [TestMethod]
    public void PurchaseDiff_IdRemoved_EmitsSubtractiveProperty()
    {
        var old = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = old.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "id1");
        _ = old.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "id2");

        // new state has only id2 — id1 was removed
        var edit = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = edit.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "id2");

        var song = new Song();

        var modified = edit.PurchaseDiff(song, old);

        Assert.IsTrue(modified);
        var removedProps = song.SongProperties
            .Where(p => p.Name == SongProperty.FormatName(Song.RemovedPurchaseField, 0, "SS"))
            .ToList();
        Assert.AreEqual(1, removedProps.Count, "Expected one Purchase- property for the removed id");
        Assert.AreEqual("id1", removedProps[0].Value);
        Assert.AreEqual(0,
            song.SongProperties.Count(p => p.Name == SongProperty.FormatName(Song.PurchaseField, 0, "SS")),
            "Should not emit a Purchase+ for the unchanged id");
    }

    [TestMethod]
    public void PurchaseDiff_SlotCompletelyRemoved_EmitsSubtractivePropertyPerOldId()
    {
        var old = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };
        _ = old.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "id1");
        _ = old.AddPurchaseId(PurchaseType.Song, ServiceType.Spotify, "id2");

        // new state has no Spotify Song slot at all
        var edit = new AlbumDetails { Name = "Album", Track = 1, Index = 0 };

        var song = new Song();

        var modified = edit.PurchaseDiff(song, old);

        Assert.IsTrue(modified);
        var removedProps = song.SongProperties
            .Where(p => p.Name == SongProperty.FormatName(Song.RemovedPurchaseField, 0, "SS"))
            .Select(p => p.Value)
            .ToList();
        CollectionAssert.AreEquivalent(new[] { "id1", "id2" }, removedProps);
    }

    #endregion

    #region BuildAlbumInfo with Purchase / Purchase-

    [TestMethod]
    public void BuildAlbumInfo_TwoPurchasePropertiesForSameSlot_AccumulatesBothIds()
    {
        // Two edit blocks each adding a different id for the same service/album — both
        // should end up in the in-memory AlbumDetails without losing the first one.
        var props = new List<SongProperty>
        {
            new(SongProperty.FormatName(Song.AlbumField, 0, null), "My Album"),
            new(SongProperty.FormatName(Song.PurchaseField, 0, "SS"), "id1"),
            new(SongProperty.FormatName(Song.PurchaseField, 0, "SS"), "id2"),
        };

        var albums = Song.BuildAlbumInfo(props);

        Assert.AreEqual(1, albums.Count);
        CollectionAssert.AreEqual(
            new[] { "id1", "id2" },
            (System.Collections.ICollection)albums[0].GetPurchaseIdentifiers(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void BuildAlbumInfo_PurchaseMinusProperty_RemovesSpecificId()
    {
        // A Purchase- property should remove one specific id while leaving sibling ids intact.
        var props = new List<SongProperty>
        {
            new(SongProperty.FormatName(Song.AlbumField, 0, null), "My Album"),
            new(SongProperty.FormatName(Song.PurchaseField, 0, "SS"), "id1"),
            new(SongProperty.FormatName(Song.PurchaseField, 0, "SS"), "id2"),
            new(SongProperty.FormatName(Song.RemovedPurchaseField, 0, "SS"), "id1"),
        };

        var albums = Song.BuildAlbumInfo(props);

        Assert.AreEqual(1, albums.Count);
        CollectionAssert.AreEqual(
            new[] { "id2" },
            (System.Collections.ICollection)albums[0].GetPurchaseIdentifiers(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void BuildAlbumInfo_PurchaseMinusLastId_ClearsTheSlot()
    {
        var props = new List<SongProperty>
        {
            new(SongProperty.FormatName(Song.AlbumField, 0, null), "My Album"),
            new(SongProperty.FormatName(Song.PurchaseField, 0, "SS"), "id1"),
            new(SongProperty.FormatName(Song.RemovedPurchaseField, 0, "SS"), "id1"),
        };

        var albums = Song.BuildAlbumInfo(props);

        Assert.AreEqual(1, albums.Count);
        Assert.IsNull(albums[0].GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    #endregion
}
