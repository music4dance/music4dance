using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Splitting sources back out of a simple merge (SongIndex.UnmergeSong / BuildUnmerge) and the
/// .Unmerge bookkeeping on Song (GetAltids, GetUnmergeableSources).
/// </summary>
[TestClass]
public class UnmergeTests
{
    private const string Header = "User=dwgray\tTime=01/01/2020 10:00:00 AM";

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<(DanceMusicService, ApplicationUser)> CreateService(string name)
    {
        var dms = await DanceMusicTester.CreateService(name, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        return (dms, await dms.FindUser("dwgray"));
    }

    private static async Task<Song> SaveSong(DanceMusicService dms, string data)
    {
        var song = await Song.Create(data, dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    // An edit made after the merge, by dwgray
    private static async Task<Song> AppendEdit(DanceMusicService dms, Song song, string content)
    {
        var edited = await Song.Create(song.SongId,
            SongProperty.Serialize(song.SongProperties, null) +
            $"\t.Edit=\tUser=dwgray\tTime=01/01/2030 10:00:00 AM\t{content}",
            dms);
        await dms.SongIndex.SaveSong(edited);
        return edited;
    }

    private static string Serialize(Song song) => SongProperty.Serialize(song.SongProperties, null);

    private static string AlbumIds(AlbumDetails album) =>
        string.Join(",", album.GetExtendedPurchaseIds(PurchaseType.Song).Order());

    [TestMethod]
    public async Task Unmerge_RestoresTheSourceAndKeepsTheRest()
    {
        var (dms, user) = await CreateService("Unmerge_Restores");
        var songA = await SaveSong(dms,
            $".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Song\tArtist=Artist\tTempo=120.0\tAlbum:00=One\tPurchase:00:SS=aaa\tDanceRating=SLS+1");
        var songB = await SaveSong(dms,
            ".Create=\tUser=dwgray\tTime=01/02/2020 10:00:00 AM\tTitle=Song\tArtist=Other Artist\tTempo=125.0\tAlbum:00=Two\tPurchase:00:SS=bbb\tDanceRating=BCH+1" +
            "\t.Edit=\tUser=dwgray\tTime=01/03/2020 10:00:00 AM\tTempo=130.0");
        var originalB = Serialize(songB);

        var merged = await dms.SongIndex.SimpleMergeSongs(user, [songA, songB]);
        merged = await AppendEdit(dms, merged, "Tempo=140.0");

        var result = await dms.SongIndex.UnmergeSong(user, merged.SongId, [songB.SongId], noMerge: false);

        var restored = result.Restored.Single();
        Assert.AreEqual(songB.SongId, restored.SongId);
        Assert.AreEqual(originalB, Serialize(restored));
        Assert.AreEqual("Other Artist", restored.Artist);
        Assert.AreEqual(130.0M, restored.Tempo);

        var remainder = result.Remainder;
        Assert.AreEqual(merged.SongId, remainder.SongId);
        Assert.AreEqual("Artist", remainder.Artist);
        Assert.AreEqual(140.0M, remainder.Tempo);
        Assert.AreEqual("One", remainder.Albums.Single().Name);
        Assert.IsFalse(remainder.SongProperties.Any(p => p.IsEdit && p.Value == songB.SongId.ToString()));
        Assert.AreEqual(songB.SongId.ToString(),
            remainder.SongProperties.Single(p => p.Name == Song.UnmergeCommand).Value);
        CollectionAssert.AreEquivalent(new[] { songA.SongId.ToString() }, remainder.GetAltids().ToList());
        Assert.IsEmpty(remainder.GetUnmergeableSources());
        Assert.IsEmpty(result.Shared);

        // Both are in the index under their ids
        Assert.AreEqual("Other Artist", (await dms.SongIndex.FindSong(songB.SongId)).Artist);
        Assert.AreEqual(140.0M, (await dms.SongIndex.FindSong(merged.SongId)).Tempo);
    }

    [TestMethod]
    public async Task Unmerge_TwoOfThree_LeavesOneAndMarksNoMerge()
    {
        var (dms, user) = await CreateService("Unmerge_TwoOfThree");
        var songs = new List<Song>();
        for (var i = 0; i < 3; i++)
        {
            songs.Add(await SaveSong(dms,
                $".Create=\tUser=dwgray\tTime=01/0{i + 1}/2020 10:00:00 AM\tTitle=Song\tArtist=Artist\tAlbum:00=Album {i}\tPurchase:00:SS=id{i}"));
        }

        var merged = await dms.SongIndex.SimpleMergeSongs(user, songs);
        CollectionAssert.AreEquivalent(songs.Select(s => s.SongId).ToList(),
            merged.GetUnmergeableSources().ToList());

        var result = await dms.SongIndex.UnmergeSong(user, merged.SongId,
            [songs[1].SongId, songs[2].SongId], noMerge: true);

        Assert.HasCount(2, result.Restored);
        foreach (var restored in result.Restored)
        {
            Assert.AreEqual(Song.NoMergeCommand,
                restored.SongProperties.Last(p => p.IsAction).Name);
            // The merge numbered these albums 1 and 2; restored, each is album 0 again
            Assert.AreEqual(0, restored.Albums.Single().Index);
        }
        Assert.AreEqual("Album 0", result.Remainder.Albums.Single().Name);
        Assert.IsFalse(result.Remainder.SongProperties.Any(p => p.BaseName == Song.NoMergeCommand));
    }

    [TestMethod]
    public async Task Unmerge_PostMergeAlbumEdit_MovesWithTheAlbumsSource()
    {
        var (dms, user) = await CreateService("Unmerge_PostMergeAlbums");
        var songA = await SaveSong(dms,
            ".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Song\tArtist=Artist\tAlbum:00=Shared\tAlbum:01=Only A");
        var songB = await SaveSong(dms,
            ".Create=\tUser=dwgray\tTime=01/02/2020 10:00:00 AM\tTitle=Song\tArtist=Artist\tAlbum:00=Only B\tAlbum:01=shared");

        var merged = await dms.SongIndex.SimpleMergeSongs(user, [songA, songB]);
        var onlyB = merged.Albums.Single(a => a.Name == "Only B").Index;
        var shared = merged.Albums.Single(a => a.Name == "shared").Index;
        merged = await AppendEdit(dms, merged,
            $"Tempo=140.0\tPurchase:{onlyB:D2}:SS=bbb\tPurchase:{shared:D2}:SS=sss");

        var (remainder, restoredList) = await dms.SongIndex.BuildUnmerge(
            user, merged, [songB.SongId], noMerge: false);

        var restored = restoredList.Single();
        Assert.AreEqual("S:bbb", AlbumIds(restored.Albums.Single(a => a.Name == "Only B")));
        Assert.AreEqual(string.Empty, AlbumIds(restored.Albums.Single(a => a.Name == "shared")));
        Assert.IsNull(restored.Tempo);

        Assert.AreEqual(140.0M, remainder.Tempo);
        Assert.AreEqual("S:sss", AlbumIds(remainder.Albums.Single(a => a.Name == "Shared")));
        Assert.IsFalse(remainder.Albums.Any(a => a.Name == "Only B"));
        Assert.IsFalse(remainder.GetExtendedPurchaseIds().Contains("S:bbb"));
    }

    [TestMethod]
    public async Task Unmerge_PostMergeEditOfOnlyMovedAlbums_DropsTheEmptyBlock()
    {
        var (dms, user) = await CreateService("Unmerge_EmptyBlock");
        var songA = await SaveSong(dms, $".Create=\t{Header}\tTitle=Song\tArtist=Artist\tAlbum:00=A");
        var songB = await SaveSong(dms, ".Create=\tUser=dwgray\tTime=01/02/2020 10:00:00 AM\tTitle=Song\tArtist=Artist\tAlbum:00=B");

        var merged = await dms.SongIndex.SimpleMergeSongs(user, [songA, songB]);
        var albumB = merged.Albums.Single(a => a.Name == "B").Index;
        merged = await AppendEdit(dms, merged, $"Purchase:{albumB:D2}:SS=bbb");

        var (remainder, restored) = await dms.SongIndex.BuildUnmerge(user, merged, [songB.SongId], false);

        // .Create(A), .Merge, .Unmerge: the post-merge edit went to B whole
        Assert.HasCount(3, remainder.SongProperties.Where(p => p.IsAction).ToList());
        Assert.HasCount(2, restored.Single().SongProperties.Where(p => p.IsAction).ToList());
    }

    [TestMethod]
    public async Task Unmerge_SharedId_RecordsThePairAsRejected()
    {
        var (dms, user) = await CreateService("Unmerge_SharedId");
        var songA = await SaveSong(dms, $".Create=\t{Header}\tTitle=Song\tArtist=Artist\tAlbum:00=A\tPurchase:00:SS=same");
        var songB = await SaveSong(dms, ".Create=\tUser=dwgray\tTime=01/02/2020 10:00:00 AM\tTitle=Song\tArtist=Artist\tAlbum:00=B\tPurchase:00:SS=same");

        var merged = await dms.SongIndex.SimpleMergeSongs(user, [songA, songB]);
        var result = await dms.SongIndex.UnmergeSong(user, merged.SongId, [songB.SongId], false);

        var pair = result.Shared.Single();
        CollectionAssert.AreEquivalent(new[] { merged.SongId, songB.SongId }, new[] { pair.SongA, pair.SongB });
        CollectionAssert.AreEqual(new[] { "S:same" }, pair.Ids.ToList());

        var row = await dms.Context.SharedIdCandidates.SingleAsync();
        Assert.AreEqual(SharedIdStatus.Rejected, row.Status);
        Assert.AreEqual("S:same", row.ServiceIds);
        Assert.AreEqual("dwgray", row.ResolvedBy);
    }

    [TestMethod]
    public async Task Unmerge_NestedMerge_PeelsTheOuterLayer()
    {
        var (dms, user) = await CreateService("Unmerge_Nested");
        var songA = await SaveSong(dms, $".Create=\t{Header}\tTitle=Song\tArtist=Artist");
        var songB = await SaveSong(dms, ".Create=\tUser=dwgray\tTime=01/02/2020 10:00:00 AM\tTitle=Song\tArtist=Artist");
        var songC = await SaveSong(dms, ".Create=\tUser=dwgray\tTime=01/03/2020 10:00:00 AM\tTitle=Song\tArtist=Artist C");

        var inner = await dms.SongIndex.SimpleMergeSongs(user, [songA, songB]);
        var outer = await dms.SongIndex.SimpleMergeSongs(user, [inner, songC]);
        CollectionAssert.AreEquivalent(new[] { inner.SongId, songC.SongId }, outer.GetUnmergeableSources().ToList());

        var result = await dms.SongIndex.UnmergeSong(user, outer.SongId, [songC.SongId], false);

        Assert.AreEqual("Artist C", result.Restored.Single().Artist);
        CollectionAssert.AreEquivalent(
            new[] { songA.SongId, songB.SongId, inner.SongId }.Select(g => g.ToString()).ToList(),
            result.Remainder.GetAltids().ToList());
    }

    [TestMethod]
    public async Task BuildUnmerge_InvalidChoices_Throw()
    {
        var (dms, user) = await CreateService("Unmerge_Invalid");
        var songA = await SaveSong(dms, $".Create=\t{Header}\tTitle=Song\tArtist=Artist");
        var songB = await SaveSong(dms, ".Create=\tUser=dwgray\tTime=01/02/2020 10:00:00 AM\tTitle=Song\tArtist=Artist");
        var merged = await dms.SongIndex.SimpleMergeSongs(user, [songA, songB]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            dms.SongIndex.BuildUnmerge(user, merged, [songA.SongId, songB.SongId], false));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            dms.SongIndex.BuildUnmerge(user, merged, [], false));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            dms.SongIndex.BuildUnmerge(user, merged, [Guid.NewGuid()], false));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            dms.SongIndex.BuildUnmerge(user, songA, [songB.SongId], false));
    }

    [TestMethod]
    public async Task GetUnmergeableSources_UnstampedMerge_IsEmpty()
    {
        var (dms, _) = await CreateService("Unmerge_Legacy");
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var song = await Song.Create(
            $".Create=\t{Header}\tTitle=Song\tArtist=Artist\t.Merge={a};{b}\tUser=dwgray\tTime=01/02/2020 10:00:00 AM",
            dms);

        Assert.IsEmpty(song.GetUnmergeableSources());
    }

    [TestMethod]
    public async Task GetAltids_AppliesMergesAndUnmergesInOrder()
    {
        var (dms, _) = await CreateService("Unmerge_AltIds");
        var song = await Song.Create(
            $".Create=\t{Header}\tTitle=Song\tArtist=Artist" +
            "\t.Merge=a;b;c\tUser=dwgray\tTime=01/02/2020 10:00:00 AM" +
            "\t.Unmerge=b;c\tUser=dwgray\tTime=01/03/2020 10:00:00 AM" +
            "\t.Merge=x;c\tUser=dwgray\tTime=01/04/2020 10:00:00 AM",
            dms);

        CollectionAssert.AreEquivalent(new[] { "a", "x", "c" }, song.GetAltids().ToList());
    }
}
