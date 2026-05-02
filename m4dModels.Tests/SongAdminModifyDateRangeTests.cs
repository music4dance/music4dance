namespace m4dModels.Tests;

/// <summary>
/// Integration tests for Song.AdminModify with date-range filtering (FromDate/ToDate).
/// Verifies that property modifications are scoped to edit blocks whose timestamp falls
/// within the specified date range, leaving other blocks unchanged.
/// </summary>
[TestClass]
public class SongAdminModifyDateRangeTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<DanceMusicCoreService> GetService() =>
        await DanceMusicTester.CreateServiceWithUsers("AdminModifyDateRange");

    // A song with two .Edit blocks — both attributed to "dwgray" but in different years.
    // Create:  dwgray  2014-11-01
    // Edit 1:  dwgray  2015-04-15   (tempo-bot batch edits, year we want to re-attribute)
    // Edit 2:  dwgray  2016-09-20   (a later personal edit — should NOT be touched)
    private const string TwoEditsTemplate =
        ".Create=\tUser=dwgray\tTime=11/01/2014 10:00:00 AM\tTitle=Test Song\tArtist=Artist\tTempo=120.0\tDanceRating=SLS+1\t" +
        ".Edit=\tUser=dwgray\tTime=04/15/2015 12:00:00 PM\tTempo=130.0\t" +
        ".Edit=\tUser=dwgray\tTime=09/20/2016 14:00:00\tDanceRating=SLS+2";

    [TestMethod]
    public async Task AdminModify_DateRange_OnlyModifiesBlocksInRange()
    {
        var service = await GetService();
        var song = new Song();
        await song.Load(TwoEditsTemplate, service);

        var modifierJson =
            """
            {
              "fromDate": "2015-01-01T00:00:00",
              "toDate": "2015-12-31T23:59:59",
              "properties": [
                { "action": "ReplaceValue", "name": "User", "value": "dwgray", "replace": "tempo-bot" }
              ]
            }
            """;

        var changed = await song.AdminModify(modifierJson, service);

        Assert.IsTrue(changed, "AdminModify should return true when blocks are modified");

        var blocks = SongPropertyBlockParser.ParseBlocks(song.SongProperties,
            a => a == Song.EditCommand || a == Song.CreateCommand);

        // Block 0: create 2014 — untouched
        var create = blocks.First(b => b.Timestamp.HasValue && b.Timestamp.Value.Year == 2014);
        Assert.AreEqual("dwgray", create.User, "Create block (2014) should still be dwgray");

        // Block 1: edit 2015 — should be re-attributed
        var edit2015 = blocks.First(b => b.Timestamp.HasValue && b.Timestamp.Value.Year == 2015);
        Assert.AreEqual("tempo-bot", edit2015.User, "Edit block (2015) should be tempo-bot");

        // Block 2: edit 2016 — untouched
        var edit2016 = blocks.First(b => b.Timestamp.HasValue && b.Timestamp.Value.Year == 2016);
        Assert.AreEqual("dwgray", edit2016.User, "Edit block (2016) should still be dwgray");
    }

    [TestMethod]
    public async Task AdminModify_DateRange_NoBlocksInRange_ReturnsFalse()
    {
        var service = await GetService();
        var song = new Song();
        await song.Load(TwoEditsTemplate, service);

        var modifierJson =
            """
            {
              "fromDate": "2000-01-01T00:00:00",
              "toDate": "2000-12-31T23:59:59",
              "properties": [
                { "action": "ReplaceValue", "name": "User", "value": "dwgray", "replace": "tempo-bot" }
              ]
            }
            """;

        var changed = await song.AdminModify(modifierJson, service);

        Assert.IsFalse(changed, "AdminModify should return false when no blocks match the range");

        // Verify none of the blocks were modified
        var blocks = SongPropertyBlockParser.ParseBlocks(song.SongProperties,
            a => a == Song.EditCommand || a == Song.CreateCommand);
        Assert.IsTrue(blocks.All(b => b.User == "dwgray"),
            "All blocks should still be dwgray");
    }

    [TestMethod]
    public async Task AdminModify_DateRange_ExcludeUsersIsHonored()
    {
        var service = await GetService();

        // Two 2015 blocks: one dwgray (should match), one batch-a (excluded)
        const string mixedSong =
            ".Create=\tUser=dwgray\tTime=03/01/2015 10:00:00 AM\tTitle=Mixed\tArtist=X\tTempo=100.0\t" +
            ".Edit=\tUser=batch-a\tTime=03/05/2015 11:00:00 AM\tTag+=4/4:Tempo";

        var song = new Song();
        await song.Load(mixedSong, service);

        var modifierJson =
            """
            {
              "fromDate": "2015-01-01T00:00:00",
              "toDate": "2015-12-31T23:59:59",
              "excludeUsers": ["batch-a"],
              "properties": [
                { "action": "ReplaceValue", "name": "User", "value": "dwgray", "replace": "tempo-bot" }
              ]
            }
            """;

        var changed = await song.AdminModify(modifierJson, service);
        Assert.IsTrue(changed, "Should be changed (dwgray block is in range and not excluded)");

        var blocks = SongPropertyBlockParser.ParseBlocks(song.SongProperties,
            a => a == Song.EditCommand || a == Song.CreateCommand);

        var createBlock = blocks.First(b => b.Timestamp.HasValue && b.Timestamp.Value.Month == 3 && b.Timestamp.Value.Day == 1);
        Assert.AreEqual("tempo-bot", createBlock.User, "dwgray create block should be re-attributed");

        var editBlock = blocks.First(b => b.Timestamp.HasValue && b.Timestamp.Value.Day == 5);
        Assert.AreEqual("batch-a", editBlock.User, "batch-a edit block should be unchanged (excluded)");
    }

    [TestMethod]
    public async Task AdminModify_NoDateRange_BehavesAsOriginal()
    {
        var service = await GetService();
        var song = new Song();
        await song.Load(TwoEditsTemplate, service);

        // No date range — should modify ALL dwgray blocks
        var modifierJson =
            """
            {
              "properties": [
                { "action": "ReplaceValue", "name": "User", "value": "dwgray", "replace": "tempo-bot" }
              ]
            }
            """;

        var changed = await song.AdminModify(modifierJson, service);
        Assert.IsTrue(changed);

        var blocks = SongPropertyBlockParser.ParseBlocks(song.SongProperties,
            a => a == Song.EditCommand || a == Song.CreateCommand);

        Assert.IsTrue(blocks.All(b => b.User == "tempo-bot"),
            "All blocks should be re-attributed when no date range is specified");
    }
}
