#nullable disable

using System.Collections.Concurrent;
using System.Net;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;
using Newtonsoft.Json;

namespace m4d.Tests.Utilities;

// Stage 1 of the Apple Music API migration: an iTunes id lookup goes to the Apple Music API when
// the developer token is configured, and to the legacy iTunes lookup otherwise
[TestClass]
[DoNotParallelize] // Tests share the process-wide Apple Music suspension and AdmAuthentication caches
public class AppleMusicTrackLookupTests
{
    // A song as the Apple Music API returns it (trimmed from a live response, 2026-11-26)
    private const string AppleSong = """
        {"data":[{"id":"1440858507","type":"songs","attributes":{
          "albumName":"Beauty Behind the Madness","artistName":"The Weeknd",
          "artwork":{"width":3000,"height":3000,"url":"https://is1-ssl.mzstatic.com/image/thumb/Music124/x.jpg/{w}x{h}bb.jpg"},
          "durationInMillis":235605,"genreNames":["R&B/Soul","Music"],"isrc":"USUG11500952",
          "name":"In the Night","previews":[{"url":"https://audio-ssl.itunes.apple.com/preview.m4a"}],
          "releaseDate":"2015-08-28","trackNumber":10,
          "url":"https://music.apple.com/us/album/in-the-night/1440858094?i=1440858507"},
          "relationships":{"albums":{"data":[{"id":"1440858094","type":"albums"}]}}}]}
        """;

    // The same song from the legacy lookup (trimmed)
    private const string LegacySong = """
        {"resultCount":1,"results":[{"wrapperType":"track","kind":"song","collectionId":1440858094,
          "trackId":1440858507,"artistName":"The Weeknd","collectionName":"Beauty Behind the Madness",
          "trackName":"In the Night","previewUrl":"https://audio-ssl.itunes.apple.com/preview.m4a",
          "artworkUrl30":"https://is1-ssl.mzstatic.com/image/thumb/Music124/x.jpg/30x30bb.jpg",
          "releaseDate":"2015-08-28T12:00:00Z","trackNumber":10,"trackTimeMillis":235605,
          "primaryGenreName":"R&B/Soul"}]}
        """;

    private const string AppleNoSong = """{"data":[]}""";

    private static readonly MusicService ITunes = MusicService.GetService(ServiceType.ITunes);

    private static IConfiguration Configuration(bool appleMusic, string trackLookup = null)
    {
        var values = new Dictionary<string, string>();
        if (appleMusic)
        {
            values[AppleMusicAuthentication.TeamIdKey] = "TEAMID1234";
            values[AppleMusicAuthentication.KeyIdKey] = "KEYID12345";
            values[AppleMusicAuthentication.PrivateKeyKey] = "x";
        }

        if (trackLookup != null)
        {
            values[MusicServiceManager.AppleMusicTrackLookupKey] = trackLookup;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // Answers Apple Music and legacy iTunes requests separately and records each request's url
    private sealed class ITunesHandler(HttpStatusCode appleStatus, string appleBody,
        HttpStatusCode legacyStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public int AppleRequests => Requests.Count(r => r.Host == "api.music.apple.com");
        public int LegacyRequests => Requests.Count(r => r.Host == "itunes.apple.com");

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri);
            var apple = request.RequestUri.Host == "api.music.apple.com";
            return Task.FromResult(new HttpResponseMessage(apple ? appleStatus : legacyStatus)
            {
                Content = new StringContent(apple ? appleBody : LegacySong, Encoding.UTF8, "application/json"),
            });
        }
    }

    // A stub token, so these tests don't depend on signing, and a cache of its own
    private sealed class StubTokenManager(IConfiguration configuration) : MusicServiceManager(configuration)
    {
        protected override Task<string> ServiceAuthorization(MusicService service, IPrincipal principal) =>
            Task.FromResult(service.Id == ServiceType.ITunes ? "Bearer stub-token" : null);
    }

    private static (StubTokenManager Manager, ITunesHandler Handler) Manager(
        IConfiguration configuration, HttpStatusCode appleStatus = HttpStatusCode.OK, string appleBody = AppleSong,
        HttpStatusCode legacyStatus = HttpStatusCode.OK)
    {
        var handler = new ITunesHandler(appleStatus, appleBody, legacyStatus);
        return (new StubTokenManager(configuration)
        {
            Http = new HttpClient(handler),
            TrackCache = new ConcurrentDictionary<string, Lazy<Task<ServiceTrack>>>(),
            ThrottleDelay = TimeSpan.Zero,
        }, handler);
    }

    [TestInitialize]
    [TestCleanup]
    public void Reset()
    {
        AdmAuthentication.Clear();
        MusicServiceManager.ResumeAppleMusicLookups();
        MusicServiceManager.ResumeITunes();
    }

    [TestMethod]
    public async Task ParseTrackResults_AppleMusicSong_FillsTheLegacyFieldsAndTheISRC()
    {
        ServiceTrack track = await ITunes.ParseTrackResults(JsonConvert.DeserializeObject(AppleSong), null);

        Assert.AreEqual(ServiceType.ITunes, track.Service);
        Assert.AreEqual("1440858507", track.TrackId);
        Assert.AreEqual("1440858094", track.CollectionId);
        Assert.AreEqual("In the Night", track.Name);
        Assert.AreEqual("The Weeknd", track.Artist);
        Assert.AreEqual("Beauty Behind the Madness", track.Album);
        Assert.AreEqual(236, track.Duration);
        Assert.AreEqual(10, track.TrackNumber);
        Assert.AreEqual("2015-08-28", track.ReleaseDate);
        CollectionAssert.AreEqual(new[] { "R&B/Soul" }, track.Genres, "Without the catch-all Music");
        Assert.AreEqual("https://is1-ssl.mzstatic.com/image/thumb/Music124/x.jpg/30x30bb.jpg", track.ImageUrl);
        Assert.AreEqual("https://audio-ssl.itunes.apple.com/preview.m4a", track.SampleUrl);
        Assert.AreEqual("USUG11500952", track.ISRC);
    }

    [TestMethod]
    public async Task ParseTrackResults_AppleMusicNoSong_ReturnsNull()
    {
        ServiceTrack track = await ITunes.ParseTrackResults(JsonConvert.DeserializeObject(AppleNoSong), null);
        Assert.IsNull(track);
    }

    [TestMethod]
    public async Task ParseTrackResults_LegacySong_FillsTheSample()
    {
        ServiceTrack track = await ITunes.ParseTrackResults(JsonConvert.DeserializeObject(LegacySong), null);

        Assert.AreEqual("1440858507", track.TrackId);
        Assert.AreEqual("1440858094", track.CollectionId);
        Assert.AreEqual("https://audio-ssl.itunes.apple.com/preview.m4a", track.SampleUrl);
        Assert.IsNull(track.ISRC);
    }

    [TestMethod]
    public async Task ParseTrackResults_LegacyNoResults_ReturnsNull()
    {
        ServiceTrack track = await ITunes.ParseTrackResults(
            JsonConvert.DeserializeObject("""{"resultCount":0,"results":[]}"""), null);
        Assert.IsNull(track);
    }

    [TestMethod]
    public async Task ParseSearchResults_LegacyNonSongItem_IsSkipped()
    {
        var tracks = await ITunes.ParseSearchResults(
            JsonConvert.DeserializeObject("""{"results":[{"kind":"music-video","trackId":1}]}"""), null, []);
        Assert.IsEmpty(tracks);
    }

    [TestMethod]
    public void ParseAppleMusicSong_ArtworkWithCropAndFormat_FillsEveryPlaceholder()
    {
        var song = JsonConvert.DeserializeObject(
            """{"id":"1","attributes":{"artwork":{"url":"https://example.com/a/{w}x{h}{c}.{f}"}}}""");
        ServiceTrack track = ITunesService.ParseAppleMusicSong(song);
        Assert.AreEqual("https://example.com/a/30x30bb.jpg", track.ImageUrl);
    }

    [TestMethod]
    public void BuildAppleMusicSongsRequest_EscapesTheIds()
    {
        Assert.AreEqual(
            "https://api.music.apple.com/v1/catalog/us/songs?ids=1,2%263",
            ITunesService.BuildAppleMusicSongsRequest("us", ["1", "2&3"]));
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_Configured_UsesAppleMusic()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true));

        var track = await manager.GetMusicServiceTrack("1440858507", ITunes);

        Assert.AreEqual("USUG11500952", track.ISRC);
        Assert.AreEqual("1440858094", track.CollectionId);
        Assert.HasCount(1, handler.Requests);
        Assert.AreEqual(
            "https://api.music.apple.com/v1/catalog/us/songs?ids=1440858507",
            handler.Requests[0].ToString());
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_NotInTheStorefront_ReturnsNullWithoutTheLegacyLookup()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true), appleBody: AppleNoSong);

        Assert.IsNull(await manager.GetMusicServiceTrack("1072024229", ITunes));
        Assert.AreEqual(0, handler.LegacyRequests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_NotConfigured_UsesTheLegacyLookup()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: false));

        var track = await manager.GetMusicServiceTrack("1440858507", ITunes);

        Assert.AreEqual("1440858507", track.TrackId);
        Assert.IsNull(track.ISRC);
        Assert.AreEqual(0, handler.AppleRequests);
        Assert.AreEqual(1, handler.LegacyRequests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_TurnedOff_UsesTheLegacyLookup()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true, trackLookup: "false"));

        _ = await manager.GetMusicServiceTrack("1440858507", ITunes);

        Assert.AreEqual(0, handler.AppleRequests);
        Assert.AreEqual(1, handler.LegacyRequests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_SwitchMistyped_StaysOn()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true, trackLookup: "off"));

        var track = await manager.GetMusicServiceTrack("1440858507", ITunes);

        Assert.AreEqual("USUG11500952", track.ISRC);
        Assert.AreEqual(0, handler.LegacyRequests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_NonNumericId_UsesTheLegacyLookup()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true));

        _ = await manager.GetMusicServiceTrack("abc", ITunes);

        Assert.AreEqual(0, handler.AppleRequests);
        Assert.AreEqual(1, handler.LegacyRequests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_Throttled_FallsBackWithoutWaiting()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true), (HttpStatusCode)429);

        var first = await manager.GetMusicServiceTrack("1440858507", ITunes);
        _ = await manager.GetMusicServiceTrack("1017804211", ITunes);

        Assert.AreEqual("1440858507", first.TrackId, "The legacy lookup answers");
        Assert.AreEqual(1, handler.AppleRequests, "No 429 retries, then a short suspension");
        Assert.AreEqual(2, handler.LegacyRequests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_LegacyFallback_IsNotCached()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true), HttpStatusCode.InternalServerError);

        _ = await manager.GetMusicServiceTrack("1440858507", ITunes);
        _ = await manager.GetMusicServiceTrack("1440858507", ITunes);

        Assert.AreEqual(2, handler.AppleRequests, "The ISRC-less fallback track isn't kept");
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_AppleMusicTrack_IsCached()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true));

        _ = await manager.GetMusicServiceTrack("1440858507", ITunes);
        _ = await manager.GetMusicServiceTrack("1440858507", ITunes);

        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_Legacy403_PausesTheLegacyLookup()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: false), legacyStatus: HttpStatusCode.Forbidden);

        Assert.IsNull(await manager.GetMusicServiceTrack("1440858507", ITunes));
        Assert.IsTrue(MusicServiceManager.Paused);
        Assert.IsNull(await manager.GetMusicServiceTrack("1017804211", ITunes));

        Assert.AreEqual(1, handler.LegacyRequests, "Paused after the first 403");
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_TokenRejected_FallsBackAndStopsAskingApple()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true), HttpStatusCode.Unauthorized);

        var first = await manager.GetMusicServiceTrack("1440858507", ITunes);
        var second = await manager.GetMusicServiceTrack("1017804211", ITunes);

        Assert.AreEqual("1440858507", first.TrackId, "The legacy lookup answers");
        Assert.IsNotNull(second);
        Assert.AreEqual(3, handler.AppleRequests, "One lookup's try and 2 retries, then none");
        Assert.AreEqual(2, handler.LegacyRequests);
    }

    [TestMethod]
    public async Task GetMusicServiceTrack_AppleServerError_FallsBackForThatLookupOnly()
    {
        var (manager, handler) = Manager(Configuration(appleMusic: true), HttpStatusCode.InternalServerError);

        _ = await manager.GetMusicServiceTrack("1440858507", ITunes);
        _ = await manager.GetMusicServiceTrack("1017804211", ITunes);

        Assert.AreEqual(2, handler.AppleRequests, "Each lookup still tries Apple first");
        Assert.AreEqual(2, handler.LegacyRequests);
    }
}

// GetISRCData over a song's iTunes ids, the path UpdateAudioData takes once iTunes lookups come
// from the Apple Music API
[TestClass]
public class GetISRCDataITunesTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private sealed class FakeMusicServiceManager(Dictionary<string, string> isrcById)
        : MusicServiceManager(new Mock<IConfiguration>().Object)
    {
        public override Task<ServiceTrack> GetMusicServiceTrack(string id, MusicService service) =>
            Task.FromResult(isrcById.TryGetValue(id, out var isrc)
                ? new ServiceTrack { Service = service.Id, TrackId = id, ISRC = isrc }
                : null);
    }

    [TestMethod]
    public async Task GetISRCData_ITunes_AttachesTheISRCToTheAlbumCarryingTheId()
    {
        var dms = await DanceMusicTester.CreateService("GetISRC_ITunes", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "batch-i", true);
        var testIndex = (TestSongIndex)dms.SongIndex;
        var song = await Song.Create(
            ".Create=\tUser=batch-i\tTitle=Test Song\tArtist=Test Artist\t" +
            "Album:00=Album A\tTrack:00=1\tPurchase:00:SS=spotify1\t" +
            "Album:01=Album B\tTrack:01=2\tPurchase:01:IS=111", dms);
        var manager = new FakeMusicServiceManager(new() { ["111"] = "USAAA0000001", ["spotify1"] = "USBBB0000001" });

        Assert.IsTrue(await manager.GetISRCData(dms, song, MusicService.GetService(ServiceType.ITunes)));

        Assert.HasCount(1, testIndex.EditCalls);
        var edit = testIndex.EditCalls[0].Edit;
        Assert.IsNull(edit.Albums[0].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song),
            "Only the iTunes ids are looked up");
        Assert.AreEqual("USAAA0000001", edit.Albums[1].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song));
    }
}
