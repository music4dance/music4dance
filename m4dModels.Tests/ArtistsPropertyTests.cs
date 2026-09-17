using Newtonsoft.Json.Linq;

namespace m4dModels.Tests;

[TestClass]
public class ArtistsPropertyTests
{
    private static DanceMusicCoreService _dms;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
        _dms = await DanceMusicTester.CreateServiceWithUsers("ArtistsProperty");
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }

    private static Task<Song> CreateSong(IEnumerable<string> log) =>
        Song.Create(string.Join("\t", log), _dms);

    [TestMethod]
    public async Task SharedReplayCases()
    {
        var path = FindRepoFile(Path.Combine("m4d", "ClientApp", "src", "models", "__tests__", "artists-replay-cases.json"));
        var cases = (JArray)JObject.Parse(await File.ReadAllTextAsync(path))["cases"];
        Assert.IsTrue(cases.Count > 0);

        foreach (var c in cases)
        {
            var name = (string)c["name"];
            var song = await CreateSong(c["log"].Values<string>());

            var expectedArtists = c["artists"]?.Type == JTokenType.Null ? null : c["artists"].Values<string>().ToList();
            if (expectedArtists == null)
            {
                Assert.IsNull(song.Artists, name);
            }
            else
            {
                Assert.IsNotNull(song.Artists, name);
                CollectionAssert.AreEqual(expectedArtists, song.Artists.ToList(), name);
            }

            CollectionAssert.AreEqual(c["effective"].Values<string>().ToList(), song.EffectiveArtists.ToList(), name);
            Assert.AreEqual(Enum.Parse<ArtistsSource>((string)c["source"]), song.ArtistsSource, name);
        }
    }

    [TestMethod]
    public async Task SharedCleanNameCases()
    {
        var path = FindRepoFile(Path.Combine("m4d", "ClientApp", "src", "models", "__tests__", "artists-replay-cases.json"));
        var cases = (JArray)JObject.Parse(await File.ReadAllTextAsync(path))["cleanName"];
        Assert.IsTrue(cases.Count > 0);

        foreach (var c in cases)
        {
            var input = (string)c[0];
            Assert.AreEqual((string)c[1], ArtistSplitter.CleanName(input), $"input: '{input}'");
        }
    }

    [TestMethod]
    public async Task SharedArtistKeyCases()
    {
        var path = FindRepoFile(Path.Combine("m4d", "ClientApp", "src", "models", "__tests__", "artists-replay-cases.json"));
        var cases = (JArray)JObject.Parse(await File.ReadAllTextAsync(path))["artistKey"];
        Assert.IsTrue(cases.Count > 0);

        foreach (var c in cases)
        {
            var input = (string)c[0];
            Assert.AreEqual((string)c[1], ArtistSplitter.ArtistKey(input), $"input: '{input}'");
        }
    }

    [TestMethod]
    public async Task UpdateArtists_AppendsBotBlockWhenSplit()
    {
        var song = await CreateSong([".Create=", "User=dwgray", "Time=01/15/2024 14:30:00",
            "Title=Hold My Heart (feat. ZZ Ward)", "Artist=Lindsey Stirling"]);
        var count = song.SongProperties.Count;

        Assert.IsTrue(song.UpdateArtists(null, new DateTime(2024, 1, 16)));

        CollectionAssert.AreEqual(new[] { "Lindsey Stirling", "ZZ Ward" }, song.Artists.ToList());
        Assert.AreEqual(ArtistsSource.Heuristic, song.ArtistsSource);
        var added = song.SongProperties.Skip(count).Select(p => $"{p.Name}={p.Value}").ToList();
        Assert.AreEqual(".Edit=", added[0]);
        Assert.AreEqual($"User={Song.ArtistBotUser}|P", added[1]);
        Assert.AreEqual("Artists=Lindsey Stirling|ZZ Ward", added[^1]);

        // Idempotent
        Assert.IsFalse(song.UpdateArtists(null));

        // In-memory state matches what replaying the log produces
        var reloaded = await Song.Create(song.Serialize(null), _dms);
        CollectionAssert.AreEqual(song.Artists.ToList(), reloaded.Artists.ToList());
        Assert.AreEqual(song.ArtistsSource, reloaded.ArtistsSource);
    }

    [TestMethod]
    public async Task UpdateArtists_NoChangeForSingleArtist()
    {
        var song = await CreateSong([".Create=", "User=dwgray", "Time=01/15/2024 14:30:00",
            "Title=Come Away With Me", "Artist=Norah Jones"]);
        var count = song.SongProperties.Count;

        Assert.IsFalse(song.UpdateArtists(null));
        Assert.AreEqual(count, song.SongProperties.Count);
        Assert.IsNull(song.Artists);
    }

    [TestMethod]
    public async Task UpdateArtists_ClearsStaleBotList()
    {
        var song = await CreateSong([".Create=", "User=dwgray", "Time=01/15/2024 14:30:00",
            "Title=Rolex", "Artist=Ayo & Teo",
            ".Edit=", "User=artist-bot|P", "Time=01/16/2024 14:30:00", "Artists=Ayo|Teo"]);

        Assert.IsTrue(song.UpdateArtists(null));
        Assert.IsNull(song.Artists);
        Assert.AreEqual("Artists=", $"{song.SongProperties[^1].Name}={song.SongProperties[^1].Value}");
    }

    [TestMethod]
    public async Task UpdateArtists_LeavesHumanAndServiceListsAlone()
    {
        var human = await CreateSong([".Create=", "User=dwgray", "Time=01/15/2024 14:30:00",
            "Title=Hold My Heart (feat. ZZ Ward)", "Artist=Lindsey Stirling", "Artists=Lindsey Stirling"]);
        Assert.IsFalse(human.UpdateArtists(null));

        var service = await CreateSong([".Create=", "User=dwgray", "Time=01/15/2024 14:30:00",
            "Title=Hold My Heart (feat. ZZ Ward)", "Artist=Lindsey Stirling",
            ".Edit=", "User=batch-s|P", "Time=01/16/2024 14:30:00", "Artists=Lindsey Stirling|Zz Ward"]);
        Assert.IsFalse(service.UpdateArtists(null));
    }

    /// <summary>
    /// "Undo My Changes" strips the user's own blocks and replays what is left, so an artist-bot
    /// split written before the user touched it should come back.
    /// </summary>
    [TestMethod]
    public async Task UndoUserChanges_RestoresTheBotSplit()
    {
        var song = await CreateSong([".Create=", "User=dwgray", "Time=01/15/2024 14:30:00",
            "Title=Fireball", "Artist=Pitbull feat. John Ryan",
            ".Edit=", "User=artist-bot|P", "Time=01/16/2024 14:30:00",
            "Artists=Pitbull|John Ryan"]);
        CollectionAssert.AreEqual(new[] { "Pitbull", "John Ryan" }, song.Artists.ToList());

        var editor = await _dms.FindUser("Charlie");
        song.SongProperties.AddRange(SongProperty.Load(
            ".Edit=	User=Charlie	Time=01/17/2024 14:30:00	Artists=John Ryan"));
        await song.Reload([.. song.SongProperties], _dms);
        CollectionAssert.AreEqual(new[] { "John Ryan" }, song.Artists.ToList());
        Assert.AreEqual(ArtistsSource.User, song.ArtistsSource);

        Assert.IsTrue(await song.UndoUserChanges(editor, _dms));
        Assert.IsNotNull(song.Artists, "log after undo: " + song.Serialize(null).Replace("	", " | "));

        CollectionAssert.AreEqual(new[] { "Pitbull", "John Ryan" }, song.Artists.ToList());
        Assert.AreEqual(ArtistsSource.Heuristic, song.ArtistsSource);
    }

    [TestMethod]
    public async Task ArtistsSurviveSerializationRoundTrip()
    {
        var song = await CreateSong([".Create=", "User=dwgray", "Time=01/15/2024 14:30:00",
            "Title=Islands in the Stream", "Artist=Dolly Parton & Kenny Rogers",
            "Artists=Dolly Parton|Kenny Rogers"]);

        var copy = await Song.Create(song.Serialize(null), _dms);
        CollectionAssert.AreEqual(song.Artists.ToList(), copy.Artists.ToList());
        Assert.AreEqual(ArtistsSource.User, copy.ArtistsSource);
    }
}
