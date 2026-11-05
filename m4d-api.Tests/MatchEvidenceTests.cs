namespace m4dApi.Tests;

[TestClass]
public class MatchEvidenceTests
{
    [TestMethod]
    [DataRow("Blue Bayou", "blue bayou ", "exact")]
    [DataRow("Beyoncé", "Beyonce", "normalized")]
    [DataRow("Rock-a-Bye Baby", "Rock a Bye, Baby!", "normalized")]
    [DataRow("Blue Bayou", "Blue Bayou (Remix)", "partial")]
    [DataRow("Blue Bayou - Live", "Blue Bayou", "partial")]
    [DataRow("Blue Bayou", "Desperado", "none")]
    [DataRow("Blue Bayou", "", "none")]
    [DataRow("!!!", "???", "none")]
    public void TextMatch_ClassifiesTheComparison(string requested, string found, string expected)
    {
        Assert.AreEqual(expected, MatchEvidence.TextMatch(requested, found));
    }

    [TestMethod]
    [DataRow("Blue Bayou", "Linda Ronstadt", 231, 233, "high")]
    [DataRow("Blue Bayou", "Linda Ronstadt", null, 233, "high")]
    [DataRow("Blue Bayou", "Linda Ronstadt", 231, null, "high")]
    [DataRow("Blue Bayou", "Linda Ronstadt", 231, 241, "medium")]
    [DataRow("Blue Bayou", "Linda Ronstadt", 231, 247, "low")]
    [DataRow("Blue Bayou", "Linda Ronstadt & Friends", 231, 231, "medium")]
    [DataRow("Blue Bayou", "Roy Orbison", 231, 231, "low")]
    [DataRow("Blue Bayou (Remix)", "Linda Ronstadt", 231, 231, "low")]
    public void Build_SuggestsAConfidence(
        string title, string artist, int? requestedSeconds, int? lengthSeconds, string expected)
    {
        var evidence = MatchEvidence.Build(title, artist, requestedSeconds, "Blue Bayou", "Linda Ronstadt", lengthSeconds);

        Assert.AreEqual(expected, evidence.Confidence);
    }

    [TestMethod]
    public void Build_ReportsTheLengthDifference()
    {
        Assert.AreEqual(7, MatchEvidence.Build("a", "b", 240, "a", "b", 233).LengthDeltaSeconds);
        Assert.AreEqual(7, MatchEvidence.Build("a", "b", 226, "a", "b", 233).LengthDeltaSeconds);
        Assert.IsNull(MatchEvidence.Build("a", "b", null, "a", "b", 233).LengthDeltaSeconds);
    }
}
