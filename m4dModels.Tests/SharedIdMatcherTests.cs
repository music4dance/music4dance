using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// The shared-id review's match tiers: how closely the titles and artists of songs that share an
/// id agree.
/// </summary>
[TestClass]
public class SharedIdMatcherTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        // The dance names that keep a qualified title apart
        await DanceMusicTester.LoadDances();
    }

    private static Song S(string title, string artist) => new() { Title = title, Artist = artist };

    [TestMethod]
    [DataRow("Shape of You", "Ed Sheeran", "Shape Of You (Remastered)", "ed sheeran", SharedIdMatch.Exact)]
    [DataRow("Shape of You", "Ed Sheeran", "Shape of You!", "Ed Sheeran", SharedIdMatch.Exact)]
    [DataRow("Shape of You", "Ed Sheeran feat. Khalid", "Shape of You", "Ed Sheeran", SharedIdMatch.WithoutFeaturing)]
    [DataRow("Shape of You feat. Khalid", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.WithoutFeaturing)]
    [DataRow("Shape of You - 2017 Remaster", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.Exact)]
    [DataRow("Shape of You - Radio Edit", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.Exact)]
    [DataRow("Shape of You - Salsa Version", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.Close)]
    [DataRow("Shape of You feat. Khalid - Remastered", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.WithoutFeaturing)]
    [DataRow("Shape of You", "Ed Sheeran", "Shape of You", "Ed Sheeran & Khalid", SharedIdMatch.FewerArtists)]
    [DataRow("Perfect", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.Different)]
    [DataRow("Shape of You", "Ed Sheeran", "Shape of You", "Taylor Swift", SharedIdMatch.Different)]
    [DataRow("Shape of You", "", "Shape of You", "", SharedIdMatch.Different)]
    public void Match_ClassifiesTheTiers(string titleA, string artistA, string titleB, string artistB,
        SharedIdMatch expected)
    {
        Assert.AreEqual(expected, SharedIdMatcher.Match([S(titleA, artistA), S(titleB, artistB)]));
    }

    [TestMethod]
    [DataRow("Ghostbusters (from \"Ghostbusters\")", "Ghostbusters - From \"Ghostbusters\"")]
    [DataRow("Don't Stop Me Now", "Dont Stop Me Now")]
    [DataRow("Song (Remix)", "Song - Remix")]
    public void Match_TitlesWithTheSameLettersAndDigits_AreExact(string a, string b)
    {
        Assert.AreEqual(SharedIdMatch.Exact, SharedIdMatcher.Match([S(a, "Ray Parker Jr."), S(b, "Ray Parker Jr.")]));
    }

    [TestMethod]
    public void Match_SquashedTitles_DifferOnlyByFeat()
    {
        Assert.AreEqual(SharedIdMatch.WithoutFeaturing, SharedIdMatcher.Match(
            [S("Song - From \"Film\" feat. Khalid", "Ed Sheeran"), S("Song (from \"Film\")", "Ed Sheeran")]));
    }

    [TestMethod]
    [DataRow("Too Good At Goodbyes - Edit", "Too Good At Goodbyes")]
    [DataRow("Scooby Doo Pa Pa - Remix", "Scooby Doo Pa Pa")]
    [DataRow("A Nightmare on My Street - Edit", "A Nightmare on My Street")]
    [DataRow("Don't Let Me Get Me - Radio Edit", "Don't Let Me Get Me")]
    [DataRow("Come on over Baby (All I Want Is You) - Radio Version", "Come on over Baby (All I Want Is You)")]
    [DataRow("A Little Less Conversation - JXL Radio Edit Remix", "A Little Less Conversation")]
    [DataRow("Seven Nation Army - Remasteris\u00e9e", "Seven Nation Army - Remastered")]
    [DataRow("Miss Kiss Kiss Bang - Big Band Version", "Miss Kiss Kiss Bang - Radio Version")]
    [DataRow("After All The Bars Are Closed - Last Call Version", "After All The Bars Are Closed")]
    [DataRow("Stay - Rerecorded", "Stay")]
    [DataRow("Song (Radio Edit)", "Song")]
    [DataRow("Song \u2013 Extended Mix", "Song [Club Mix]")]
    public void Match_AQualifierThatDoesntChangeTheDance_IsExact(string a, string b)
    {
        Assert.AreEqual(SharedIdMatch.Exact, SharedIdMatcher.Match([S(a, "Artist"), S(b, "Artist")]));
    }

    [TestMethod]
    [DataRow("Song - Salsa Version", "Song")]
    [DataRow("Song - Bachata Remix", "Song - Radio Edit")]
    [DataRow("Song (Cha Cha Mix)", "Song")]
    [DataRow("Song - 128 BPM", "Song")]
    [DataRow("Song - West Coast Swing Edit", "Song - Edit")]
    public void Match_AQualifierNamingADanceOrTempo_IsNotExact(string a, string b)
    {
        Assert.AreNotEqual(SharedIdMatch.Exact, SharedIdMatcher.Match([S(a, "Artist"), S(b, "Artist")]));
    }

    [TestMethod]
    [DataRow("Song - Radio Edit", "Song")]
    [DataRow("Song - Part 1 - Remix", "Song")]
    [DataRow("Song (Live) [Remix]", "Song")]
    [DataRow("Song - Salsa Version", "Song - Salsa Version")]
    [DataRow("Song - Salsa Version - Edit", "Song - Salsa Version")]
    [DataRow("Song-Name", "Song-Name")]
    [DataRow("(Intro)", "(Intro)")]
    [DataRow("Edit - Remix", "Edit")]
    public void WithoutQualifiers_DropsTheQualifiersThatDontNameADance(string title, string expected)
    {
        Assert.AreEqual(expected, SharedIdMatcher.WithoutQualifiers(title));
    }

    [TestMethod]
    public void SameVersionWithoutFeaturing_KeepsTheQualifiers()
    {
        Assert.IsTrue(SharedIdMatcher.SameTitleWithoutFeaturing("Song - Acoustic", "Song"));
        Assert.IsFalse(SharedIdMatcher.SameVersionWithoutFeaturing("Song - Acoustic", "Song"));
        Assert.IsTrue(SharedIdMatcher.SameVersionWithoutFeaturing("Song (feat. X) - Remastered", "Song"));
    }

    [TestMethod]
    [DataRow("Pink", "P!nk")]
    [DataRow("Lady Antebellum", "Lady A")]
    [DataRow("The New 101 Strings Orchestra", "101 Strings Orchestra")]
    [DataRow("New 101 String Orchestra", "101 Strings")]
    [DataRow("Andy Fortuna Productions", "Andy Fortuna")]
    [DataRow("Yambao Orquesta", "Orquesta Yambao")]
    [DataRow("El Gran Combo De Puerto Rico", "El Gran Combo")]
    [DataRow("Michael P. Kramer", "Michael Paul Kramer")]
    [DataRow("Juan Luis Guerra", "Juan Luis Guerra 4.40")]
    [DataRow("Sasha Sloan", "Sasha Alex Sloan")]
    public void Match_AnArtistsAliases_AreExact(string a, string b)
    {
        Assert.AreEqual(SharedIdMatch.Exact, SharedIdMatcher.Match([S("Song", a), S("Song", b)]));
    }

    [TestMethod]
    public void Match_AnAliasWithAFeaturedArtist_DiffersOnlyByFeat()
    {
        Assert.AreEqual(SharedIdMatch.WithoutFeaturing,
            SharedIdMatcher.Match([S("Just Give Me a Reason", "Pink feat. Nate Ruess"), S("Just Give Me a Reason", "P!nk")]));
    }

    [TestMethod]
    public void Match_EveryPairMustMatch()
    {
        // The first two are a close match, but the third is a different song
        Assert.AreEqual(SharedIdMatch.Different, SharedIdMatcher.Match(
            [S("Shape of You", "Ed Sheeran"), S("Shape of You - Salsa Version", "Ed Sheeran"), S("Perfect", "Ed Sheeran")]));
    }

    [TestMethod]
    [DataRow("Song - Remastered", "Song")]
    [DataRow("Song - Remastered 2011", "Song")]
    [DataRow("Song - 2011 Remaster", "Song")]
    [DataRow("Song (2011 Remastered Version)", "Song")]
    [DataRow("Song [Digitally Remastered]", "Song")]
    [DataRow("Song - Remastered Version 2009", "Song")]
    [DataRow("Song \u2013 Single Version", "Song")]
    [DataRow("Song (Single Version)", "Song")]
    [DataRow("Song: Single Version", "Song")]
    [DataRow("Song (Single Version) - Remastered 2011", "Song")]
    [DataRow("Song - Single", "Song - Single")]
    [DataRow("Remastered", "Remastered")]
    [DataRow("Remastered Love", "Remastered Love")]
    [DataRow("Song - Remix", "Song - Remix")]
    public void WithoutVersionNotes_DropsTheRemasterAndSingleVersionNotes(string title, string expected)
    {
        Assert.AreEqual(expected, SharedIdMatcher.WithoutVersionNotes(title));
    }

    [TestMethod]
    public void Match_FewerThanTwoSongs_IsDifferent()
    {
        Assert.AreEqual(SharedIdMatch.Different, SharedIdMatcher.Match([S("Shape of You", "Ed Sheeran")]));
        Assert.AreEqual(SharedIdMatch.Different, SharedIdMatcher.Match([]));
    }

    [TestMethod]
    public void TitleArtistMatch_RequiresAnArtist()
    {
        Assert.IsFalse(SharedIdMatcher.TitleArtistMatch([S("Shared Song", ""), S("Shared Song", "")]));
        Assert.IsFalse(SharedIdMatcher.CloseMatch([S("Shared Song", ""), S("Shared Song", "")]));
    }

    private static Song L(int? length) => new() { Title = "Song", Artist = "Artist", Length = length };

    [TestMethod]
    public void LengthSpread_IsTheLongestMinusTheShortest_IgnoringSongsWithoutALength()
    {
        Assert.AreEqual(42, SharedIdMatcher.LengthSpread([L(200), L(null), L(242), L(210)]));
        Assert.AreEqual(0, SharedIdMatcher.LengthSpread([L(200), L(null)]));
        Assert.AreEqual(0, SharedIdMatcher.LengthSpread([L(null), L(null)]));
    }

    private static Song T(decimal? tempo) => new() { Title = "Song", Artist = "Artist", Tempo = tempo };

    [TestMethod]
    public void TemposAgree_EverySongHasATempo_WithinTwoBpm()
    {
        Assert.IsTrue(SharedIdMatcher.TemposAgree([T(120), T(122), T(121.5m)]));
        Assert.IsFalse(SharedIdMatcher.TemposAgree([T(120), T(122.5m)]), "More than 2 BPM apart");
        Assert.IsFalse(SharedIdMatcher.TemposAgree([T(120), T(null)]), "A song without a tempo");
        Assert.IsFalse(SharedIdMatcher.TemposAgree([T(120)]), "One song");
    }

    [TestMethod]
    [DataRow("Benny Goodman and His Orchestra", "Benny Goodman")]
    [DataRow("Benny Goodman & His Orchestra", "Benny Goodman")]
    [DataRow("Benny Goodman Orchestra", "Benny Goodman and His Orchestra")]
    [DataRow("Charlie Parker Quartet", "Charlie Parker")]
    [DataRow("The Oscar Peterson Trio", "Oscar Peterson")]
    [DataRow("Benny Goodman Sextet", "Benny Goodman")]
    [DataRow("Stan Getz Duo", "Stan Getz")]
    [DataRow("Count Basie Big Band", "Count Basie")]
    public void Match_ALeaderWithOrWithoutTheirGroup_IsExact(string first, string second)
    {
        Assert.AreEqual(SharedIdMatch.Exact, SharedIdMatcher.Match([S("Song", first), S("Song", second)]));
    }

    [TestMethod]
    public void Match_ALeadersGroup_DiffersOnlyByFeat()
    {
        Assert.AreEqual(SharedIdMatch.WithoutFeaturing,
            SharedIdMatcher.Match([S("Song feat. Chet Baker", "Charlie Parker Quartet"), S("Song", "Charlie Parker")]));
    }

    [TestMethod]
    public void Match_TheSameArtistsCreditedDifferently_IsSameArtists()
    {
        Assert.AreEqual(SharedIdMatch.SameArtists, SharedIdMatcher.Match(
            [S("Song", "Teddy Wilson And His Orchestra With Billie Holliday"), S("Song", "Teddy Wilson; Billie Holliday")]));
    }

    [TestMethod]
    [DataRow("Modern Jazz Quartet", "Modern Jazz Five")]
    [DataRow("Kingston Trio", "Kingston Quartet")]
    public void Match_ActsNamedForTheirGroups_AreNotTheSameArtist(string first, string second)
    {
        // Neither credit is a leader another song credits on their own
        Assert.AreNotEqual(SharedIdMatch.Exact, SharedIdMatcher.Match([S("Song", first), S("Song", second)]));
    }

    [TestMethod]
    public void Match_ACreditThatLeavesAnArtistOut_IsFewerArtists()
    {
        Assert.AreEqual(SharedIdMatch.FewerArtists,
            SharedIdMatcher.Match([S("Song", "Tony Bennett"), S("Song (Remastered)", "Tony Bennett & Lady Gaga")]));
        Assert.AreEqual(SharedIdMatch.FewerArtists,
            SharedIdMatcher.Match([S("Song", "Tony Bennett"), S("Song (feat. Lady Gaga)", "Tony Bennett & Diana Krall")]),
            "Featured artists count");
    }

    [TestMethod]
    public void Match_DifferentLeadArtists_OrDifferentTitles_AreNotArtistMatches()
    {
        Assert.IsNull(SharedIdMatcher.ArtistsMatch([S("Song", "Lady Gaga & Tony Bennett"), S("Song", "Tony Bennett & Diana Krall")]));
        Assert.IsNull(SharedIdMatcher.ArtistsMatch([S("Song", "Tony Bennett"), S("Other Song", "Tony Bennett & Lady Gaga")]));
    }
}
