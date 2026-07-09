using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Regression coverage for a bug where SongIndex.AdminEditSong(string) - used by the
/// admin "update" bulk-edit tool (DanceMusicService.AdminUpdate) - was fed the full
/// serialized song (as produced by Song.Serialize/ToString, which starts with
/// "SongId={guid}\t") and parsed it as-is, without stripping the id header the way
/// Song.Create(string, ...) does. That left a bogus "SongId" SongProperty at the front
/// of the song's property log, which then reappeared at the front of every future
/// Properties-field write (m4dModels/SongIndex.cs's DocumentFromSong).
/// </summary>
[TestClass]
public class AdminEditSongTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    [TestMethod]
    public async Task AdminEditSong_WithFullSerializedInput_DoesNotLeaveStraySongIdProperty()
    {
        var dms = await DanceMusicTester.CreateService(
            "TestDb_AdminEditSong_StripsSongId", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);

        var song = await Song.Create(
            ".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tTempo=120.0",
            dms);
        await dms.SongIndex.SaveSong(song);

        // Mimic what an admin submits via the bulk "update" tool: the full serialized song
        // (SongId prefix included), with an extra edit block tacked on the end.
        var adminEditInput =
            $"{song.Serialize(null)}\t.Edit=\tUser=dwgray\tTime=01/02/2020 11:00:00 AM\tTempo=125.0";

        Assert.IsTrue(await dms.SongIndex.AdminEditSong(adminEditInput));

        var reloaded = await dms.SongIndex.FindSong(song.SongId);

        Assert.IsFalse(
            reloaded.SongProperties.Any(p => p.Name == SongIndex.SongIdField),
            "AdminEdit should strip the leading SongId header instead of storing it as a property");
        Assert.AreEqual(125.0m, reloaded.Tempo);

        // This is exactly what DocumentFromSong writes to the Properties field - it must not
        // start with a stray "SongId=" token, or reads will mis-detect it as compressed Base64.
        var reserialized = SongProperty.Serialize(reloaded.SongProperties, null);
        Assert.IsFalse(reserialized.StartsWith(SongIndex.SongIdField + "=", StringComparison.Ordinal));
    }
}
