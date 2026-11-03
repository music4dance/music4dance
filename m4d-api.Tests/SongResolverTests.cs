using static m4dApi.Tests.FakeSearch;

namespace m4dApi.Tests;

[TestClass]
public class SongResolverTests
{
    private const string Isrc = "USWB19902945";
    private const string SpotifyId = "4uLU6hMCjMI75M1A2tKUQC";

    private static ResolveRequest Request(
        string isrc = null, string appleMusicId = null, string spotifyId = null, string title = null,
        string artist = null, int? durationSeconds = null) =>
        new(isrc, appleMusicId, spotifyId, title, artist, durationSeconds);

    [TestMethod]
    [DataRow(null, null, null, null, null, "Supply at least one id, or both title and artist")]
    [DataRow(null, null, null, "Blue Bayou", null, "Supply at least one id, or both title and artist")]
    [DataRow("US-WB1-99-02945x", null, null, null, null, "isrc is not a valid ISRC")]
    [DataRow(null, "12a", null, null, null, "appleMusicId must be numeric")]
    [DataRow(null, null, "short", null, null, "spotifyId is not a valid Spotify track id")]
    [DataRow(null, null, "4uLU6hMCjMI75M1A2tKUQC'or'", null, null, "spotifyId is not a valid Spotify track id")]
    public void Validate_RejectsBadRequests(
        string isrc, string apple, string spotify, string title, string artist, string expected)
    {
        Assert.AreEqual(expected, SongResolver.Validate(Request(isrc, apple, spotify, title, artist)));
    }

    [TestMethod]
    public void Validate_RejectsMissingBodyAndOversizedText()
    {
        Assert.AreEqual("Request body is required", SongResolver.Validate(null));
        Assert.IsNotNull(SongResolver.Validate(Request(title: new string('a', 301), artist: "Linda Ronstadt")));
        Assert.IsNotNull(SongResolver.Validate(Request(isrc: Isrc, durationSeconds: -1)));
    }

    [TestMethod]
    [DataRow("us-wb1-99-02945", null, null, null, null)]
    [DataRow(null, "1440818839", null, null, null)]
    [DataRow(null, null, SpotifyId, null, null)]
    [DataRow(null, null, null, "Blue Bayou", "Linda Ronstadt")]
    public void Validate_AcceptsAnIdOrTitleAndArtist(
        string isrc, string apple, string spotify, string title, string artist)
    {
        Assert.IsNull(SongResolver.Validate(Request(isrc, apple, spotify, title, artist)));
    }

    [TestMethod]
    public async Task Resolve_SendsEveryIdInOneQuery()
    {
        var search = new FakeSearch().Returns(Song("a", serviceIds: [$"S:{SpotifyId}"], dances: ("WCS", 3, 120, null)));

        var result = await search.Resolver.Resolve(
            Request(isrc: "us-wb1-99-02945", appleMusicId: "1440818839", spotifyId: SpotifyId, title: "Blue Bayou",
                artist: "Linda Ronstadt"),
            CancellationToken.None);

        Assert.HasCount(1, search.Queries);
        Assert.AreEqual(
            $"ServiceIds/any(id: search.in(id, 'R:{Isrc},R:{Isrc.ToLowerInvariant()},I:1440818839,S:{SpotifyId}'))",
            search.Queries[0].Options.Filter);
        Assert.AreEqual("exact", result.Match);
        Assert.AreEqual("spotifyId", result.Method);
        Assert.HasCount(1, result.Candidates);
        CollectionAssert.AreEqual(new[] { "spotifyId" }, result.Candidates[0].MatchedBy);
        Assert.IsNull(result.Candidates[0].Evidence);
    }

    [TestMethod]
    public async Task Resolve_ReturnsAnIdMatchWithNoConfirmedDancesInsteadOfFallingBack()
    {
        var search = new FakeSearch().Returns(Song("a", serviceIds: [$"R:{Isrc}"], dances: ("WCS", -1, 120, null)));

        var result = await search.Resolver.Resolve(
            Request(isrc: Isrc, title: "Blue Bayou", artist: "Linda Ronstadt"), CancellationToken.None);

        Assert.HasCount(1, search.Queries);
        Assert.AreEqual("exact", result.Match);
        Assert.AreEqual("a", result.Candidates[0].SongId);
        Assert.IsEmpty(result.Candidates[0].DanceRatings);
    }

    [TestMethod]
    public async Task Resolve_ReportsIdsThatPointAtDifferentSongsAsAmbiguous()
    {
        var search = new FakeSearch().Returns(
            Song("apple", serviceIds: ["I:1440818839"], dances: ("WCS", 9, 120, null)),
            Song("isrc", serviceIds: [$"R:{Isrc}", "S:other"], dances: ("WCS", 2, 120, null)));

        var result = await search.Resolver.Resolve(
            Request(isrc: Isrc, appleMusicId: "1440818839", title: "Blue Bayou", artist: "Linda Ronstadt"),
            CancellationToken.None);

        Assert.HasCount(1, search.Queries);
        Assert.AreEqual("ambiguous", result.Match);
        Assert.IsNull(result.Method);
        // The ISRC identifies the recording, so its song comes first whatever the votes
        CollectionAssert.AreEqual(new[] { "isrc", "apple" }, result.Candidates.Select(c => c.SongId).ToArray());
        CollectionAssert.AreEqual(new[] { "isrc" }, result.Candidates[0].MatchedBy);
        CollectionAssert.AreEqual(new[] { "appleMusicId" }, result.Candidates[1].MatchedBy);
        Assert.IsTrue(result.Candidates.All(c => c.Evidence == null));
    }

    [TestMethod]
    public async Task Resolve_TreatsSongsLinkedByASharedIdAsOneRecording()
    {
        // "both" carries the ISRC and the Apple id, so all three are duplicates of one recording
        var search = new FakeSearch().Returns(
            Song("apple", serviceIds: ["I:1440818839"], dances: ("WCS", 2, 120, null)),
            Song("isrc", serviceIds: [$"R:{Isrc}"], dances: ("WCS", 7, 120, null)),
            Song("both", serviceIds: [$"R:{Isrc}", "I:1440818839"], dances: ("WCS", 3, 120, null)));

        var result = await search.Resolver.Resolve(
            Request(isrc: Isrc, appleMusicId: "1440818839"), CancellationToken.None);

        Assert.AreEqual("exact", result.Match);
        Assert.AreEqual("isrc", result.Method);
        Assert.HasCount(1, result.Candidates);
        Assert.AreEqual("isrc", result.Candidates[0].SongId);
        CollectionAssert.AreEqual(new[] { "isrc", "appleMusicId" }, result.Candidates[0].MatchedBy);
    }

    [TestMethod]
    public async Task Resolve_SelectsOnlyTheContractFields()
    {
        var search = new FakeSearch();

        _ = await search.Resolver.Resolve(Request(isrc: Isrc), CancellationToken.None);

        var select = search.Queries[0].Options.Select;
        CollectionAssert.IsSubsetOf(IndexFields.Selected, select.ToArray());
        CollectionAssert.Contains(select.ToArray(), "dance_WCS");
        CollectionAssert.Contains(select.ToArray(), "dance_ALL");
        Assert.IsFalse(select.Any(f => f is "Properties" or "Users" or "Comments"));
    }

    [TestMethod]
    public async Task Resolve_MatchesALowercaseStoredIsrc()
    {
        var search = new FakeSearch().Returns(
            Song("a", serviceIds: [$"R:{Isrc.ToLowerInvariant()}"], dances: ("WCS", 3, 120, null)));

        var result = await search.Resolver.Resolve(Request(isrc: Isrc), CancellationToken.None);

        Assert.AreEqual("exact", result.Match);
        Assert.AreEqual("isrc", result.Method);
    }

    [TestMethod]
    public async Task Resolve_PrefersTheDuplicateWithTheMostConfirmedVotes()
    {
        var search = new FakeSearch().Returns(
            Song("few", serviceIds: [$"R:{Isrc}"], dances: ("WCS", 2, 120, null)),
            Song("many", serviceIds: [$"R:{Isrc}"], dances: [("WCS", 5, 120, null), ("RMB", 4, 120, null)]),
            Song("some", serviceIds: [$"R:{Isrc}"], dances: ("WCS", 6, 120, null)));

        var result = await search.Resolver.Resolve(Request(isrc: Isrc), CancellationToken.None);

        Assert.AreEqual("many", result.Candidates[0].SongId);
    }

    [TestMethod]
    public async Task Resolve_FallsBackToTitleAndArtistWhenNoIdMatches()
    {
        var search = new FakeSearch()
            .Returns()
            .Returns(
                Song("a", length: 233, dances: ("WCS", 3, 120, null)),
                Song("b", title: "Blue Bayou (Live)", length: 260, dances: ("WCS", 1, 120, null)));

        var result = await search.Resolver.Resolve(
            Request(isrc: Isrc, title: "Blue Bayou", artist: "Linda Ronstadt (feat. Roy Orbison)", durationSeconds: 231),
            CancellationToken.None);

        Assert.HasCount(2, search.Queries);
        var (text, options) = search.Queries[1];
        Assert.AreEqual("Blue Bayou Linda Ronstadt  feat. Roy Orbison ", text);
        CollectionAssert.AreEqual(IndexFields.Searched, options.SearchFields.ToArray());
        Assert.AreEqual(IndexFields.ConfirmedOnly, options.Filter);
        Assert.AreEqual(SongResolver.MaxCandidates, options.Size);

        Assert.AreEqual("fuzzy", result.Match);
        Assert.AreEqual("titleArtist", result.Method);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Candidates.Select(c => c.SongId).ToArray());
        Assert.AreEqual(new Evidence("exact", "partial", 2, "medium"), result.Candidates[0].Evidence);
        Assert.AreEqual("low", result.Candidates[1].Evidence.Confidence);
    }

    [TestMethod]
    public async Task Resolve_StripsSearchOperatorsFromTitleAndArtist()
    {
        var search = new FakeSearch();

        _ = await search.Resolver.Resolve(Request(title: "Rock-a-Bye (Baby)*", artist: "A|B+C"), CancellationToken.None);

        Assert.HasCount(1, search.Queries);
        Assert.AreEqual("Rock a Bye  Baby   A B C", search.Queries[0].Search);
    }

    [TestMethod]
    public async Task Resolve_ReturnsNoneWhenNothingMatches()
    {
        var search = new FakeSearch();

        var result = await search.Resolver.Resolve(Request(isrc: Isrc), CancellationToken.None);

        Assert.AreEqual("none", result.Match);
        Assert.IsNull(result.Method);
        Assert.IsEmpty(result.Candidates);
        Assert.HasCount(1, result.Queries);
    }

    [TestMethod]
    public async Task Resolve_ProjectsIndexFields()
    {
        var search = new FakeSearch().Returns(Song(
            "a", bpm: 90, serviceIds: [$"R:{Isrc}"], tempoTags: ["3/4", "Fast"],
            dances: [("WCS", -1, 90, null), ("SWZ", 4, 87, ["International"]), ("VWZ", 7, null, null)]));

        var candidate = (await search.Resolver.Resolve(Request(isrc: Isrc), CancellationToken.None)).Candidates[0];

        Assert.AreEqual("a", candidate.SongId);
        Assert.AreEqual("https://www.music4dance.net/song/details?id=a", candidate.SongUrl);
        Assert.AreEqual("Blue Bayou", candidate.Title);
        Assert.AreEqual("Linda Ronstadt", candidate.Artist);
        Assert.AreEqual(231, candidate.LengthSeconds);
        Assert.AreEqual("3/4", candidate.Meter);
        Assert.AreEqual(90, candidate.BeatsPerMinute);

        // Unconfirmed-only (-1) ratings are dropped; the rest are ordered by votes
        CollectionAssert.AreEqual(new[] { "VWZ", "SWZ" }, candidate.DanceRatings.Select(r => r.DanceId).ToArray());
        var slowWaltz = candidate.DanceRatings[1];
        Assert.AreEqual(Dances.DanceFromId("SWZ").Name, slowWaltz.DanceName);
        Assert.AreEqual(4, slowWaltz.Weight);
        Assert.AreEqual(87, slowWaltz.BeatsPerMinute);
        CollectionAssert.AreEqual(new[] { "International" }, slowWaltz.Styles);
        Assert.IsNull(candidate.DanceRatings[0].BeatsPerMinute);
    }

    [TestMethod]
    public async Task Resolve_ReportsTempoWithoutAMeter()
    {
        var search = new FakeSearch().Returns(
            Song("a", bpm: 120, serviceIds: [$"R:{Isrc}"], tempoTags: [], dances: ("WCS", 3, 118, null)));

        var candidate = (await search.Resolver.Resolve(Request(isrc: Isrc), CancellationToken.None)).Candidates[0];

        Assert.IsNull(candidate.Meter);
        Assert.AreEqual(120, candidate.BeatsPerMinute);
        Assert.AreEqual(118, candidate.DanceRatings[0].BeatsPerMinute);
    }
}
