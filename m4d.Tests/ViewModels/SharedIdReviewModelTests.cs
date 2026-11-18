using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;

using m4dModels;
using m4dModels.Utilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace m4d.Tests.ViewModels;

/// <summary>
/// The shared-id review's JSON contract: the request the page app posts and what it becomes, and
/// the page model it's sent.
/// </summary>
[TestClass]
public class SharedIdReviewModelTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    [TestMethod]
    public void ToChoice_CarriesEveryField()
    {
        var request = new SharedIdChoiceRequest
        {
            Action = SharedIdAction.Merge,
            CandidateIds = [7],
            Songs = [new SharedIdSongGroup { SongId = A, Group = "A" }, new SharedIdSongGroup { SongId = B, Group = "B" }],
            TitleFrom = B,
            Removals = [new SharedIdRemoval(A, 0), new SharedIdRemoval(B, 1, "S:sp1")],
            Deletions = [B]
        };

        var choice = request.ToChoice();

        Assert.AreEqual(SharedIdAction.Merge, choice.Action);
        CollectionAssert.AreEqual(new[] { A, B }, choice.SongIds.ToList());
        CollectionAssert.AreEqual(new[] { "A", "B" }, choice.Groups.ToList());
        CollectionAssert.AreEqual(new[] { 7 }, choice.CandidateIds.ToList());
        Assert.AreEqual(new SharedIdNamePicks(Title: B), choice.Names);
        CollectionAssert.AreEqual(new[] { new SharedIdRemoval(A, 0), new SharedIdRemoval(B, 1, "S:sp1") },
            choice.Removals.ToList());
        CollectionAssert.AreEqual(new[] { B }, choice.Deletions.ToList());
    }

    [TestMethod]
    public void ToChoice_MissingArrays_IsAnEmptyChoiceWithTheProposedNames()
    {
        var choice = new SharedIdChoiceRequest
            { Songs = null, CandidateIds = null, Removals = null, Deletions = null }.ToChoice();

        Assert.AreEqual(SharedIdAction.Leave, choice.Action);
        Assert.IsEmpty(choice.SongIds);
        Assert.IsEmpty(choice.CandidateIds);
        Assert.IsEmpty(choice.Removals);
        Assert.IsEmpty(choice.Deletions);
        Assert.AreEqual(new SharedIdNamePicks(), choice.Names);
        Assert.IsEmpty(choice.MergeGroups);
    }

    [TestMethod]
    public void ToChoice_TypedNames_AreCarried()
    {
        var choice = new SharedIdChoiceRequest
            { Title = "Typed", Artists = ["X", "Y"] }.ToChoice();

        Assert.AreEqual("Typed", choice.Names.Typed.Title);
        Assert.IsNull(choice.Names.Typed.Artist);
        CollectionAssert.AreEqual(new[] { "X", "Y" }, choice.Names.Typed.Artists.ToList());
        Assert.IsNull(new SharedIdChoiceRequest().ToChoice().Names.Typed);
        var tempo = new SharedIdChoiceRequest { Tempo = 121.5m, TempoFrom = B }.ToChoice().Names;
        Assert.AreEqual((121.5m, (Guid?)B), (tempo.Typed.Tempo, tempo.Tempo));
    }

    [TestMethod]
    public void ToChoice_ASongWithNoGroup_IsLeftOut()
    {
        var choice = new SharedIdChoiceRequest
        {
            Action = SharedIdAction.Merge,
            Songs = [new SharedIdSongGroup { SongId = A, Group = "A" }, new SharedIdSongGroup { SongId = B }]
        }.ToChoice();

        CollectionAssert.AreEqual(new[] { A }, choice.MergeGroups.Single().ToList());
    }

    // The body the page app sends (ClusterChoice.toRequest), through the serializer settings the
    // API uses: enum names, camel case, null picks and an album removal without an id
    [TestMethod]
    public void TheClientsRequest_Deserializes()
    {
        var json = $$"""
            {
              "page": 3,
              "choices": [{
                "action": "Reject",
                "candidateIds": [101, 102],
                "songs": [{ "songId": "{{A}}", "group": "A" }, { "songId": "{{B}}", "group": "" }],
                "titleFrom": "{{B}}",
                "artistFrom": null,
                "artistsFrom": null,
                "tempoFrom": "{{A}}",
                "tempo": null,
                "removals": [{ "songId": "{{A}}", "album": 0, "id": "I:111" }, { "songId": "{{B}}", "album": 1 }]
              }]
            }
            """;

        var request = JsonConvert.DeserializeObject<SharedIdApplyRequest>(json)!;
        var choice = request.Choices.Single().ToChoice();

        Assert.AreEqual(3, request.Page);
        Assert.AreEqual(SharedIdAction.Reject, choice.Action);
        CollectionAssert.AreEqual(new[] { "A", "" }, choice.Groups.ToList());
        Assert.AreEqual(new SharedIdNamePicks(Title: B, Tempo: A), choice.Names);
        CollectionAssert.AreEqual(new[] { new SharedIdRemoval(A, 0, "I:111"), new SharedIdRemoval(B, 1) },
            choice.Removals.ToList());
    }

    [TestMethod]
    public async Task Build_FlattensAClusterForThePage()
    {
        var dms = await DanceMusicTester.CreateServiceWithUsers("SharedIdReviewModel");
        var song = await Song.Create(
            ".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Probe\tArtist=X\tLength=200\tTempo=120.0" +
            "\tAlbum:00=First\tTrack:00=3\tPurchase:00:SS=sp1\tPurchase:00:RS=US111\tDanceRating=SLS+2\tDanceRating=CHA+1",
            dms);
        var cluster = new SharedIdPageCluster(
            new SharedIdCluster
            {
                Candidates = [new SharedIdCandidate { Id = 4, SongA = song.SongId, SongB = B, ServiceIds = "R:US111", Source = "Seed" }],
                SongIds = [song.SongId, B]
            }, song, new SongNames("Probe", "X", ["X", "Y"]));

        var preview = new SharedIdPreview(DateTimeOffset.UnixEpoch,
        [
            new SharedIdMergePlan(new SharedIdCluster { SongIds = [A] }, null),
            new SharedIdMergePlan(new SharedIdCluster { SongIds = [B] }, SharedIdMergePlan.WithoutFeaturing),
            new SharedIdMergePlan(new SharedIdCluster { SongIds = [A, B] }, SharedIdMergePlan.WithoutFeaturing),
            new SharedIdMergePlan(new SharedIdCluster { SongIds = [B, A] }, SharedIdMergePlan.Close)
        ]);

        var model = SharedIdReviewModel.Build(
            new SharedIdPage { Page = 2, ClusterCount = 45, PendingCandidates = 50, Clusters = [cluster] },
            preview, "Done");
        var entry = model.Clusters.Single();
        var merged = entry.Merged;

        Assert.AreEqual(2, model.Page);
        Assert.AreEqual(3, model.PageCount);
        Assert.AreEqual("Done", model.Message);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, model.SortedBy);
        Assert.AreEqual(1, model.WouldMergeCount);
        Assert.AreEqual(2, model.WouldMergeFeaturingCount);
        Assert.AreEqual(SharedIdAction.Merge, entry.DefaultAction);
        Assert.IsTrue(entry.CleansFeaturing);
        CollectionAssert.AreEqual(new[] { "R:US111" }, entry.ServiceIds.ToList());
        Assert.AreEqual(4, entry.Candidates.Single().Id);
        CollectionAssert.AreEqual(new[] { "X", "Y" }, entry.Proposal.Artists.ToList());
        CollectionAssert.AreEqual(new[] { "SLS", "CHA" }, merged.Dances.Select(d => d.DanceId).ToList(),
            "Heaviest first");
        var album = merged.Albums.Single();
        Assert.AreEqual(0, album.Index);
        Assert.AreEqual(3, album.Track);
        var ids = album.Ids.ToDictionary(i => i.Id);
        Assert.IsTrue(ids["R:US111"].Shared);
        Assert.IsNull(ids["R:US111"].Link);
        Assert.IsFalse(ids["S:sp1"].Shared);
        Assert.IsNotNull(ids["S:sp1"].Link);
    }

    // The page app's TypedJSON classes read enums as their names and camel-cased members
    [TestMethod]
    public void Serialized_UsesEnumNamesAndCamelCase()
    {
        var model = new SharedIdReviewModel
        {
            Clusters = [new SharedIdReviewCluster { Match = SharedIdMatch.WithoutFeaturing, DefaultAction = SharedIdAction.Leave }]
        };

        var json = JObject.Parse(ScriptJson.Serialize(model, camelCase: true));
        var cluster = json["clusters"]![0]!;

        Assert.AreEqual("WithoutFeaturing", cluster["match"]!.ToString());
        Assert.AreEqual("Leave", cluster["defaultAction"]!.ToString());
        Assert.IsNull(cluster["merged"], "Nulls are left out, so the client's optional members are undefined");
    }

    [TestMethod]
    public async Task LookupTracks_LooksUpEachSpotifyAndITunesIdOnce()
    {
        var calls = new List<(string Id, ServiceType Service)>();
        Task<ServiceTrack> Fetch(string id, MusicService service)
        {
            calls.Add((id, service.Id));
            return Task.FromResult(id switch
            {
                "sp1" => new ServiceTrack
                {
                    Name = "Probe", Artist = "Probe Artist", Album = "First Album", Duration = 185,
                    ISRC = "USRC1", ReleaseDate = "2020-01-02"
                },
                "boom" => throw new InvalidOperationException("Service down"),
                _ => null!
            });
        }

        var tracks = await SharedIdServiceTrack.Lookup(
            ["S:sp1", "R:USRC1", "I:111", "S:sp1", "A:amz", "I:boom", "X", null], Fetch);

        CollectionAssert.AreEqual(
            new[] { ("sp1", ServiceType.Spotify), ("111", ServiceType.ITunes), ("boom", ServiceType.ITunes) },
            calls);
        Assert.AreEqual(
            new SharedIdServiceTrack("S:sp1", true, "Probe", "Probe Artist", "First Album", 185, "USRC1", "2020-01-02"),
            tracks[0]);
        Assert.AreEqual(new SharedIdServiceTrack("I:111", false), tracks[1]);
        Assert.AreEqual(new SharedIdServiceTrack("I:boom", false), tracks[2]);
        Assert.HasCount(3, tracks);
    }

    [TestMethod]
    public async Task LookupTracks_NoIds_LooksUpNothing()
    {
        Assert.IsEmpty(await SharedIdServiceTrack.Lookup(null!, (_, _) => Task.FromResult<ServiceTrack>(null!)));
    }

    [TestMethod]
    public void OwnersResponse_SpellsOutTheStatus_AndGivesTheTracksAsTheLookupDoes()
    {
        var song = Guid.NewGuid();
        var ownership = new SharedIdOwnership(
            [new SharedIdOwnerProposal("S:sp1", SharedIdOwnerStatus.Owned, "The song is it", song)],
            [new SharedIdRemoval(Guid.NewGuid(), 0)],
            new Dictionary<string, ServiceTrack> { ["S:sp1"] = new() { Name = "Probe", Artist = "Probe Artist" }, ["I:1"] = null! });

        var json = JObject.Parse(JsonConvert.SerializeObject(SharedIdOwnersResponse.Build(ownership),
            JsonHelpers.CamelCaseSerializer));

        Assert.AreEqual("Owned", (string)json["ids"]![0]!["status"]!);
        Assert.AreEqual(song.ToString(), (string)json["ids"]![0]!["song"]!);
        Assert.AreEqual(0, (int)json["removals"]![0]!["album"]!);
        Assert.IsTrue((bool)json["tracks"]![0]!["found"]!);
        Assert.IsFalse((bool)json["tracks"]![1]!["found"]!);
        Assert.IsEmpty(SharedIdOwnersResponse.Build(null!).Ids);
    }
}
