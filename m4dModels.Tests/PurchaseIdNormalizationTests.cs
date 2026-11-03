namespace m4dModels.Tests;

// Purchase ids are stored in each service's canonical spelling (MusicService.NormalizeId): a log
// holding any other spelling loads as the canonical one, and re-saving it writes that spelling.
// ISRC is the service that actually varies in production data (lowercase and hyphenated codes);
// Spotify ids are case-sensitive base62 and must come through untouched.
[TestClass]
public class PurchaseIdNormalizationTests
{
    private static DanceMusicCoreService _dms;

    private const string Header = ".Create=\tUser=batch-s|P\tTime=11/01/2026 07:04:24\tTitle=Titanium\tArtist=David Guetta";

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
        _dms = await DanceMusicTester.CreateServiceWithUsers("PurchaseIdNormalization");
    }

    private static MusicService Isrc => MusicService.GetService(ServiceType.ISRC);

    [TestMethod]
    [DataRow("GB28K1100036")]
    [DataRow("gb28k1100036")]
    [DataRow("GB-28K-11-00036")]
    [DataRow(" gb-28k-11-00036 ")]
    public void IsrcNormalizeId_AnySpelling_IsUppercaseWithoutDelimiters(string isrc)
    {
        Assert.AreEqual("GB28K1100036", Isrc.NormalizeId(isrc));
    }

    [TestMethod]
    public void NormalizeId_SpotifyAndITunes_Unchanged()
    {
        Assert.AreEqual("0j7pTqhCfMmIgv2e4kf7ZP",
            MusicService.GetService(ServiceType.Spotify).NormalizeId("0j7pTqhCfMmIgv2e4kf7ZP"));
        Assert.AreEqual("693224822",
            MusicService.GetService(ServiceType.ITunes).NormalizeId("693224822"));
    }

    [TestMethod]
    public void SongProperty_LoadedPurchase_IsNormalized()
    {
        var props = SongProperty.Load(
            "Purchase:00:RS=gb-28k-11-00036\tPurchase-:01:RS=gb28k1100036\tPurchase:00:SS=0j7pTqhCfMmIgv2e4kf7ZP\tComment+=gb-28k")
            .ToList();

        Assert.AreEqual("GB28K1100036", props[0].Value);
        Assert.AreEqual("GB28K1100036", props[1].Value);
        Assert.AreEqual("0j7pTqhCfMmIgv2e4kf7ZP", props[2].Value);
        Assert.AreEqual("gb-28k", props[3].Value, "Only purchase properties are normalized");
    }

    [TestMethod]
    public void SongProperty_ObjectInitializer_IsNormalizedWhicheverIsAssignedFirst()
    {
        var nameFirst = new SongProperty { Name = "Purchase:00:RS", Value = "gb28k1100036" };
        var valueFirst = new SongProperty { Value = "gb28k1100036", Name = "Purchase:00:RS" };

        Assert.AreEqual("GB28K1100036", nameFirst.Value);
        Assert.AreEqual("GB28K1100036", valueFirst.Value);
    }

    [TestMethod]
    public async Task Song_TwoSpellingsOfOneIsrc_ReadAsOneAndSaveCanonical()
    {
        var song = await Song.Create(
            $"{Header}\tAlbum:00=Nothing but the Beat\tPurchase:00:RS=gb28k1100036" +
            "\tAlbum:01=Titanium - Single\tPurchase:01:RS=GB-28K-11-00036", _dms);

        CollectionAssert.AreEqual(new[] { "GB28K1100036" }, song.GetPurchaseIds(Isrc).Distinct().ToList());
        CollectionAssert.Contains(song.GetExtendedPurchaseIds().ToList(), "R:GB28K1100036");

        var serialized = song.Serialize([Song.NoSongId]);
        StringAssert.Contains(serialized, "Purchase:00:RS=GB28K1100036");
        StringAssert.Contains(serialized, "Purchase:01:RS=GB28K1100036");
        Assert.IsFalse(serialized.Contains("gb28k", StringComparison.Ordinal));
        Assert.IsFalse(serialized.Contains("GB-28K", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Song_RemoveWithOtherSpelling_RemovesTheId()
    {
        var song = await Song.Create(
            $"{Header}\tAlbum:00=Nothing but the Beat\tPurchase:00:RS=GB28K1100036" +
            "\t.Edit=\tUser=batch-s|P\tTime=11/02/2026 07:04:24\tPurchase-:00:RS=gb-28k-11-00036", _dms);

        Assert.AreEqual(0, song.GetPurchaseIds(Isrc).Count);
    }

    [TestMethod]
    public void AlbumDetails_AddPurchaseId_OtherSpellingOfStoredIsrc_NoOp()
    {
        var ad = new AlbumDetails();
        _ = ad.AddPurchaseId(PurchaseType.Song, ServiceType.ISRC, "gb28k1100036");

        var changed = ad.AddPurchaseId("RS", "GB-28K-11-00036");

        Assert.IsFalse(changed);
        Assert.AreEqual("GB28K1100036", ad.GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song));
    }
}
