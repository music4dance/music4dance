using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;
using Newtonsoft.Json.Linq;

namespace m4d.Tests.Utilities;

[TestClass]
public class AppleMusicAuthenticationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000);

    private static (ECDsa Key, string Pem, string OneLine) NewKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = key.ExportPkcs8PrivateKeyPem();
        var oneLine = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        return (key, pem, oneLine);
    }

    private static JObject DecodePart(string part)
    {
        var padded = part.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
    }

    private static byte[] DecodeSignature(string part)
    {
        var padded = part.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static void AssertValidToken(string token, ECDsa key)
    {
        var parts = token.Split('.');
        Assert.HasCount(3, parts);

        var header = DecodePart(parts[0]);
        Assert.AreEqual("ES256", (string?)header["alg"]);
        Assert.AreEqual("KEYID12345", (string?)header["kid"]);

        var claims = DecodePart(parts[1]);
        Assert.AreEqual("TEAMID1234", (string?)claims["iss"]);
        Assert.AreEqual(Now.ToUnixTimeSeconds(), (long?)claims["iat"]);
        Assert.AreEqual(Now.ToUnixTimeSeconds() + 3600, (long?)claims["exp"]);

        // ES256 in a JWT is the raw 64-byte r||s signature, not DER
        var signature = DecodeSignature(parts[2]);
        Assert.HasCount(64, signature);
        Assert.IsTrue(key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256));
    }

    [TestMethod]
    public void CreateDeveloperToken_PemKey_SignsAVerifiableES256Token()
    {
        var (key, pem, _) = NewKey();
        using (key)
        {
            var token = AppleMusicAuthentication.CreateDeveloperToken(
                "TEAMID1234", "KEYID12345", pem, Now, TimeSpan.FromHours(1));
            AssertValidToken(token, key);
        }
    }

    [TestMethod]
    public void CreateDeveloperToken_OneLineKey_SignsAVerifiableES256Token()
    {
        var (key, _, oneLine) = NewKey();
        using (key)
        {
            // Password managers may wrap or pad the value; whitespace is ignored
            var wrapped = $" {oneLine[..60]}\n{oneLine[60..]} ";
            var token = AppleMusicAuthentication.CreateDeveloperToken(
                " TEAMID1234 ", "KEYID12345\n", wrapped, Now, TimeSpan.FromHours(1));
            AssertValidToken(token, key);
        }
    }

    [TestMethod]
    public void ImportPrivateKey_Garbage_ThrowsWithoutEchoingTheKey()
    {
        const string bad = "MIGTAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBHkwdwIBAQQgnot-a-real-key";
        var e = Assert.Throws<InvalidOperationException>(
            () => AppleMusicAuthentication.ImportPrivateKey(bad));
        Assert.DoesNotContain("MIG", e.Message);
    }

    [TestMethod]
    public void IsAppleMusicRequest_OnlyTheAppleMusicApiHost()
    {
        Assert.IsTrue(AppleMusicAuthentication.IsAppleMusicRequest(
            new Uri("https://api.music.apple.com/v1/catalog/us/songs?ids=1")));
        Assert.IsFalse(AppleMusicAuthentication.IsAppleMusicRequest(
            new Uri("https://itunes.apple.com/lookup?id=1&entity=song")));
    }

    [TestMethod]
    public void IsConfigured_RequiresAllThreeValues()
    {
        var partial = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AppleMusicAuthentication.TeamIdKey] = "TEAMID1234",
            [AppleMusicAuthentication.KeyIdKey] = "KEYID12345",
        }).Build();
        Assert.IsFalse(AppleMusicAuthentication.IsConfigured(partial));

        var full = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AppleMusicAuthentication.TeamIdKey] = "TEAMID1234",
            [AppleMusicAuthentication.KeyIdKey] = "KEYID12345",
            [AppleMusicAuthentication.PrivateKeyKey] = "x",
        }).Build();
        Assert.IsTrue(AppleMusicAuthentication.IsConfigured(full));
    }
}

[TestClass]
public class GetAppleISRCDataTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    // Replaces the Apple Music call with a dictionary of catalog id -> ISRC; ids not in it are
    // "not in the storefront", as Apple leaves them out of the response.
    private class FakeMusicServiceManager(Dictionary<string, string> isrcById)
        : MusicServiceManager(new Mock<IConfiguration>().Object)
    {
        public List<IReadOnlyCollection<string>> Lookups { get; } = [];

        public override Task<IList<ServiceTrack>> LookupAppleMusicSongs(IReadOnlyCollection<string> ids)
        {
            Lookups.Add(ids);
            IList<ServiceTrack> tracks = [.. ids
                .Where(isrcById.ContainsKey)
                .Select(id => new ServiceTrack
                {
                    Service = ServiceType.ITunes, TrackId = id, ISRC = isrcById[id],
                    Name = "Test Song", Artist = "Test Artist", Album = "Album",
                })];
            return Task.FromResult(tracks);
        }
    }

    private static async Task<(DanceMusicService dms, TestSongIndex testIndex)> CreateTestEnv(string dbName)
    {
        var dms = await DanceMusicTester.CreateService(dbName, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "batch-i", true);
        return (dms, (TestSongIndex)dms.SongIndex);
    }

    private static Task<Song> CreateSong(DanceMusicService dms, string purchases) =>
        Song.Create(
            $".Create=\tUser=batch-i\tTitle=Test Song\tArtist=Test Artist\t{purchases}", dms);

    [TestMethod]
    public async Task GetAppleISRCData_TwoITunesIds_AttachesEachISRCToItsAlbum()
    {
        var (dms, testIndex) = await CreateTestEnv("AppleISRC_TwoIds");
        var song = await CreateSong(dms,
            "Album:00=Album A\tTrack:00=1\tPurchase:00:IS=111\t" +
            "Album:01=Album B\tTrack:01=2\tPurchase:01:IS=222");
        var manager = new FakeMusicServiceManager(new() { ["111"] = "USAAA0000001", ["222"] = "USAAA0000002" });

        Assert.IsTrue(await manager.GetAppleISRCData(dms, song));

        Assert.HasCount(1, manager.Lookups, "One batched call for all of a song's ids");
        Assert.HasCount(1, testIndex.EditCalls);
        var edit = testIndex.EditCalls[0].Edit;
        Assert.AreEqual("USAAA0000001", edit.Albums[0].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song));
        Assert.AreEqual("USAAA0000002", edit.Albums[1].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song));
    }

    [TestMethod]
    public async Task GetAppleISRCData_SameISRCOnTwoIds_AttachesItOnce()
    {
        var (dms, testIndex) = await CreateTestEnv("AppleISRC_SameIsrc");
        var song = await CreateSong(dms,
            "Album:00=Album A\tTrack:00=1\tPurchase:00:IS=111\t" +
            "Album:01=Album B\tTrack:01=2\tPurchase:01:IS=222");
        var manager = new FakeMusicServiceManager(new() { ["111"] = "USAAA0000001", ["222"] = "USAAA0000001" });

        Assert.IsTrue(await manager.GetAppleISRCData(dms, song));

        var edit = testIndex.EditCalls[0].Edit;
        Assert.AreEqual("USAAA0000001", edit.Albums[0].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song));
        Assert.IsNull(edit.Albums[1].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song));
    }

    [TestMethod]
    public async Task GetAppleISRCData_IdNotInStorefront_NoEdit()
    {
        var (dms, testIndex) = await CreateTestEnv("AppleISRC_NotFound");
        var song = await CreateSong(dms, "Album:00=Album A\tTrack:00=1\tPurchase:00:IS=111");
        var manager = new FakeMusicServiceManager([]);

        Assert.IsFalse(await manager.GetAppleISRCData(dms, song));
        Assert.IsEmpty(testIndex.EditCalls);
    }

    [TestMethod]
    public async Task GetAppleISRCData_NoITunesId_DoesNotCallApple()
    {
        var (dms, testIndex) = await CreateTestEnv("AppleISRC_NoId");
        var song = await CreateSong(dms, "Album:00=Album A\tTrack:00=1\tPurchase:00:SS=spotify1");
        var manager = new FakeMusicServiceManager(new() { ["111"] = "USAAA0000001" });

        Assert.IsFalse(await manager.GetAppleISRCData(dms, song));
        Assert.IsEmpty(manager.Lookups);
        Assert.IsEmpty(testIndex.EditCalls);
    }

    [TestMethod]
    public async Task LookupAppleMusicSongs_NotConfigured_AbortsTheBatch()
    {
        var manager = new MusicServiceManager(new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<AbortBatchException>(() => manager.LookupAppleMusicSongs(["111"]));
    }
}

// Calls the real Apple Music API with the developer key from m4d's user secrets. Opt-in, so the
// regular suite never depends on Apple: set M4D_APPLE_MUSIC_LIVE=1 to run it.
[TestClass]
public class AppleMusicLiveTests
{
    private const string SecretsId = "60050f39-d7c1-4b33-8b65-1e6cbb538661";

    [TestMethod]
    [TestCategory("Manual")]
    public async Task LookupAppleMusicSongs_StoredITunesIds_ReturnsISRCs()
    {
        if (Environment.GetEnvironmentVariable("M4D_APPLE_MUSIC_LIVE") != "1")
        {
            Assert.Inconclusive("Set M4D_APPLE_MUSIC_LIVE=1 to call the Apple Music API.");
            return;
        }

        var configuration = new ConfigurationBuilder().AddUserSecrets(SecretsId).Build();
        if (!AppleMusicAuthentication.IsConfigured(configuration))
        {
            Assert.Inconclusive("Set Authentication:AppleMusic:TeamId, :KeyId and :PrivateKey in m4d's user secrets.");
            return;
        }

        AdmAuthentication.Clear();
        var manager = new MusicServiceManager(configuration);

        // 1072024229 isn't in the us storefront (checked 2026-11-25)
        var tracks = await manager.LookupAppleMusicSongs(["1017804211", "1072024229"]);

        Assert.HasCount(1, tracks);
        Assert.AreEqual("1017804211", tracks[0].TrackId);
        Assert.AreEqual("USUG11500952", tracks[0].ISRC);
    }
}
