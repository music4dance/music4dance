namespace m4dModels.Tests;

/// <summary>
/// Tests that per-user dance rating contributions are capped at ±1 during song loading.
/// This prevents pseudo users (playlist imports) from accumulating multiple votes for the
/// same song/dance when the same playlist is imported more than once or a song appears in
/// multiple playlists from the same source.
///
/// Batch/service accounts (usernames starting with "batch" or equal to "tempo-bot") are
/// exempt from this cap and may use any delta value.
/// </summary>
[TestClass]
public class DanceRatingCapTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<DanceMusicCoreService> GetService(string suffix) =>
        await DanceMusicTester.CreateServiceWithUsers($"DanceRatingCap_{suffix}");

    [TestMethod]
    public async Task PseudoUser_TwoPositiveVotesSameDance_CappedAtOne()
    {
        var dms = await GetService("Pseudo2x");
        var song = await Song.Create(
            ".Create=\tUser=ArthurMurrays|P\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=ArthurMurrays|P\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist after first vote");
        Assert.AreEqual(1, rating.Weight, "Pseudo user second +1 vote should be ignored (capped at 1)");
    }

    [TestMethod]
    public async Task PseudoUser_ThreePositiveVotesSameDance_CappedAtOne()
    {
        var dms = await GetService("Pseudo3x");
        var song = await Song.Create(
            ".Create=\tUser=Studio|P\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=Studio|P\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA+1\t" +
            ".Edit=\tUser=Studio|P\tTime=03/01/2024 10:00:00 AM\tDanceRating=CHA+1",
            dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(1, rating.Weight, "Three votes from same pseudo user should still be capped at 1");
    }

    [TestMethod]
    public async Task RealUser_TwoPositiveVotesSameDance_CappedAtOne()
    {
        var dms = await GetService("Real2x");
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=alice\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA+1",
            dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(1, rating.Weight, "Real user second +1 vote should be ignored (capped at 1)");
    }

    [TestMethod]
    public async Task TwoDifferentUsers_OneVoteEach_TotalIsTwo()
    {
        var dms = await GetService("TwoUsers");
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=bob\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA+1",
            dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(2, rating.Weight, "Two different users each voting once should sum to 2");
    }

    [TestMethod]
    public async Task User_UpvoteThenDownvote_DanceRemoved()
    {
        var dms = await GetService("UpDown");
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=alice\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA-1",
            dms);

        var rating = song.FindRating("CHA");
        Assert.IsNull(rating, "Net zero rating should remove the dance");
    }

    [TestMethod]
    public async Task User_UpvoteDownvoteUpvote_NetOne()
    {
        var dms = await GetService("UpDownUp");
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=alice\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA-1\t" +
            ".Edit=\tUser=alice\tTime=03/01/2024 10:00:00 AM\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist after up-down-up sequence");
        Assert.AreEqual(1, rating.Weight, "Up-down-up sequence should result in net +1");
    }

    [TestMethod]
    public async Task BatchUser_HighDeltaVote_NotCapped()
    {
        var dms = await GetService("Batch");
        var song = await Song.Create(
            ".Create=\tUser=batch\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=SFT+5\tTag+=Slow Foxtrot:Dance",
            dms);

        var rating = song.FindRating("SFT");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(5, rating.Weight, "Batch user high-delta vote should not be capped");
    }

    [TestMethod]
    public async Task PseudoUser_CapAppliesPerDance_NotCrossContaminated()
    {
        // Verify the cap is per-dance: capping CHA does not affect SLS
        var dms = await GetService("PerDance");
        var song = await Song.Create(
            ".Create=\tUser=Studio|P\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tDanceRating=SLS+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=Studio|P\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA+1\tDanceRating=SLS+1",
            dms);

        var chaRating = song.FindRating("CHA");
        var slsRating = song.FindRating("SLS");
        Assert.IsNotNull(chaRating, "CHA rating should exist");
        Assert.IsNotNull(slsRating, "SLS rating should exist");
        Assert.AreEqual(1, chaRating.Weight, "CHA should be capped at 1");
        Assert.AreEqual(1, slsRating.Weight, "SLS should be capped at 1");
    }

    [TestMethod]
    public async Task TempoBot_HighDeltaVote_NotCapped()
    {
        // tempo-bot is a service account and must be exempt from the ±1 cap
        var dms = await GetService("TempoBot");
        var song = await Song.Create(
            ".Create=\tUser=tempo-bot\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=FXT+5\tTag+=Slow Foxtrot:Dance",
            dms);

        var rating = song.FindRating("FXT");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(5, rating.Weight, "tempo-bot high-delta vote should not be capped");
    }

    [TestMethod]
    public async Task TempoBot_RepeatedVotesSameDance_NotCapped()
    {
        // tempo-bot voting the same dance twice should accumulate freely
        var dms = await GetService("TempoBotRepeat");
        var song = await Song.Create(
            ".Create=\tUser=tempo-bot\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=FXT+3\tTag+=Slow Foxtrot:Dance\t" +
            ".Edit=\tUser=tempo-bot\tTime=02/01/2024 10:00:00 AM\tDanceRating=FXT+2",
            dms);

        var rating = song.FindRating("FXT");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(5, rating.Weight, "tempo-bot repeated votes should accumulate without cap");
    }

    [TestMethod]
    public async Task SetRatingsFromProperties_EnforcesSameCap()
    {
        // SetRatingsFromProperties should apply the same ±1 cap as LoadProperties
        var dms = await GetService("SetRatings");
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=alice\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA+1",
            dms);

        // Simulate a recalculation (e.g. the dbAdmin UpdateRatings action)
        song.SetRatingsFromProperties();

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist after recalculation");
        Assert.AreEqual(1, rating.Weight, "SetRatingsFromProperties should also cap at 1");
    }
}
