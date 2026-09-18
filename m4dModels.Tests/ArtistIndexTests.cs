namespace m4dModels.Tests;

[TestClass]
public class ArtistIndexTests
{
    private static ArtistIndex BuildSample() => ArtistIndex.Build(
    [
        ["The Beatles"],
        ["The Beatles", "Billy Preston"],
        ["Michael Bublé"],
        ["Michael Buble", "Meghan Trainor"],
        ["Michael Bublé"],
        ["Meghan Trainor"],
        ["Various Artists"],
        ["10,000 Maniacs"],
        ["Beyoncé", "beyoncé"],
    ], new DateTime(2026, 9, 16));

    [TestMethod]
    public void Build_GroupsVariantsAndUsesMostCommonSpelling()
    {
        var index = BuildSample();

        var bubles = index.Search("buble");
        Assert.AreEqual(1, bubles.Count);
        Assert.AreEqual(new ArtistIndexEntry("Michael Bublé", 3), bubles[0]);

        // A song listing the same artist twice counts once
        Assert.AreEqual(new ArtistIndexEntry("Beyoncé", 1), index.Search("beyonce").Single());
    }

    [TestMethod]
    public void Build_ExcludesNonArtists()
    {
        var index = BuildSample();
        Assert.AreEqual(0, index.Search("various").Count);
        Assert.AreEqual(6, index.Count);
    }

    [TestMethod]
    [DataRow("The Beatles", "beatles", "B")]
    [DataRow("Los Van Van", "van van", "V")]
    [DataRow("The The", "the", "T")]
    [DataRow("Ábrete", "abrete", "A")]
    [DataRow("10,000 Maniacs", "10,000 maniacs", "#")]
    public void SortKeyAndBucket(string name, string sortKey, string bucket)
    {
        Assert.AreEqual(sortKey, ArtistIndex.SortKey(name));
        Assert.AreEqual(bucket, ArtistIndex.BucketOf(name));
    }

    [TestMethod]
    public void InBucket_SortsIgnoringArticlesAndHonorsMinimumSongs()
    {
        var index = BuildSample();

        CollectionAssert.AreEqual(
            new[] { "The Beatles", "Beyoncé", "Billy Preston" },
            index.InBucket("b").Select(e => e.Name).ToList());
        CollectionAssert.AreEqual(
            new[] { "The Beatles" },
            index.InBucket("B", minSongs: 2).Select(e => e.Name).ToList());
        CollectionAssert.AreEqual(
            new[] { "10,000 Maniacs" },
            index.InBucket("#").Select(e => e.Name).ToList());
    }

    [TestMethod]
    public void Buckets_CountEveryLetter()
    {
        var buckets = BuildSample().Buckets(minSongs: 2);
        Assert.AreEqual(27, buckets.Count);
        Assert.AreEqual(1, buckets.Single(b => b.Bucket == "B").Artists);
        Assert.AreEqual(2, buckets.Single(b => b.Bucket == "M").Artists);
        Assert.AreEqual(0, buckets.Single(b => b.Bucket == "#").Artists);
    }

    [TestMethod]
    public void Search_PrefersPrefixMatches()
    {
        var index = ArtistIndex.Build([["Tony Evans"], ["Evanescence"], ["Evanescence"], ["Evan Taubenfeld"]]);
        CollectionAssert.AreEqual(
            new[] { "Evanescence", "Evan Taubenfeld", "Tony Evans" },
            index.Search("evan").Select(e => e.Name).ToList());
    }

    [TestMethod]
    public async Task SongIndexLocal_StreamsEffectiveArtists()
    {
        await DanceMusicTester.LoadDances();
        var dms = await DanceMusicTester.CreateService("ArtistIndexStream", useTestSongIndex: true);
        _ = await dms.AddPseudoUser(Song.ArtistBotUser, "artist-bot@music4dance.net");
        var index = (TestSongIndex)dms.SongIndex;
        await index.SaveSong(await Song.Create(
            ".Create=\tUser=dwgray\tTime=01/15/2024 14:30:00\tTitle=Hold My Heart (feat. ZZ Ward)\t" +
            "Artist=Lindsey Stirling\tTag+=Cha Cha:Dance\tDanceRating=CHA+1", dms));

        var artistIndex = await ArtistIndex.BuildAsync(index.StreamSongArtistsAsync(CruftFilter.AllCruft));

        CollectionAssert.AreEquivalent(new[] { "Lindsey Stirling", "ZZ Ward" },
            artistIndex.Top(10).Select(e => e.Name).ToList());
    }
}
