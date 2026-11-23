namespace m4dModels.Tests;

[TestClass]
public class KeywordQueryTests
{
    [TestMethod]
    public void ShouldCorrectlyDetectLuceneTrue()
    {
        var searchQuery = new KeywordQuery("`test");
        Assert.IsTrue(searchQuery.IsLucene);
    }

    [TestMethod]
    public void ShouldCorrectlyDetectLuceneFalse()
    {
        var searchQuery = new KeywordQuery("test");
        Assert.IsFalse(searchQuery.IsLucene);
    }

    [TestMethod]
    public void ShouldReturnIsLuceneFalseOnEmptyString()
    {
        var searchQuery = new KeywordQuery("");
        Assert.IsFalse(searchQuery.IsLucene);
    }

    [TestMethod]
    public void ShouldReturnSearchTestForSimpleSyntax()
    {
        var searchQuery = new KeywordQuery("test");
        Assert.AreEqual("test", searchQuery.Keywords);
    }

    [TestMethod]
    public void ShouldReturnSearchTestForLuceneSyntax()
    {
        var searchQuery = new KeywordQuery("`test");
        Assert.AreEqual("test", searchQuery.Keywords);
    }

    [TestMethod]
    public void DescriptionIsAccurateForSimpleSearch()
    {
        var searchQuery = new KeywordQuery("test");
        Assert.AreEqual("containing the text \"test\"", searchQuery.Description);
    }

    [TestMethod]
    public void DescriptionIsAccurateForLuceneSearchWithoutFields()
    {
        var searchQuery = new KeywordQuery("`test");
        Assert.AreEqual("containing the text \"test\"", searchQuery.Description);
    }

    [TestMethod]
    public void DescriptionIsAccurateForSingleField()
    {
        var searchQuery = new KeywordQuery("`Title:(test)");
        Assert.AreEqual("where title contains \"test\"", searchQuery.Description);
    }

    [TestMethod]
    public void DescriptionIsAccurateForMultipleFields()
    {
        var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo)");
        Assert.AreEqual("where title contains \"test\" and artist contains \"foo\"", searchQuery.Description);
    }

    [TestMethod]
    public void DescriptionIsAccurateForMultipleFieldsAndAll()
    {
        var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo) bar");
        Assert.AreEqual("containing the text \"bar\" anywhere and title contains \"test\" and artist contains \"foo\"", searchQuery.Description);
    }

    [TestMethod]
    public void ShortDescriptionIsAccurateForSingleField()
    {
        var searchQuery = new KeywordQuery("`Title:(test)");
        Assert.AreEqual(@"""test"" in title", searchQuery.ShortDescription);
    }

    [TestMethod]
    public void ShortDescriptionIsAccurateForMultipleFields()
    {
        var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo)");
        Assert.AreEqual(@"""test"" in title and ""foo"" in artist", searchQuery.ShortDescription);
    }

    [TestMethod]
    public void ShortDescriptionIsAccurateForMultipleFieldsAndAll()
    {
        var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo) bar");
        Assert.AreEqual(@"""bar"" anywhere and ""test"" in title and ""foo"" in artist", searchQuery.ShortDescription);
    }

    [TestMethod]
    public void ShouldAddPartWhenValueIsProvided()
    {
        var searchQuery = new KeywordQuery("something");
        var updatedQuery = searchQuery.Update("Title", "test");
        Assert.AreEqual("`Title:(test) something", updatedQuery.Query);
    }

    [TestMethod]
    public void ShouldDeletePartWhenValueIsNotProvided()
    {
        var searchQuery = new KeywordQuery("Title:(test) something");
        var updatedQuery = searchQuery.Update("Title", "");
        Assert.AreEqual("something", updatedQuery.Query);
    }

    // Each failed with a 400 from Azure Search in production (2026-11-17 to 11-18)
    [TestMethod]
    [DataRow(@"`Artist:(IAMSU!)", @"Artist:(IAMSU\!)")]
    [DataRow(@"`Artist:(AC/DC)", @"Artist:(AC\/DC)")]
    [DataRow(@"`Artist:(Trin-I-Tee 5:7)", @"Artist:(Trin\-I\-Tee 5\:7)")]
    [DataRow(@"`Artist:(OR)", @"Artist:(or)")]
    [DataRow(@"`Artist:(Simon AND Garfunkel)", @"Artist:(Simon and Garfunkel)")]
    [DataRow(@"`Artist:(Fred And Victor\) Ken And B.J.)", @"Artist:(Fred And Victor\) Ken And B.J.)")]
    [DataRow(@"`Artist:(Coco", @"Artist\:\(Coco")]
    [DataRow(@"`Artist:(Ricky Martin) Title:(Vente Pa' Ca) vente", @"Artist:(Ricky Martin) Title:(Vente Pa' Ca) vente")]
    [DataRow(@"`Artist:(Ricky Martin) wild*", @"Artist:(Ricky Martin) wild\*")]
    public void AzureSearchEscapesTheUsersText(string query, string expected)
    {
        Assert.AreEqual(expected, new KeywordQuery(query).AzureSearch);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("plain text, with: (anything) OR *")]
    public void AzureSearchPassesSimpleQueriesThrough(string query)
    {
        Assert.AreEqual(query, new KeywordQuery(query).AzureSearch);
    }

    [TestMethod]
    public void FieldValuesWithParenthesesRoundTrip()
    {
        var query = new KeywordQuery().Update("Artist", @"Earth, Wind & Fire (Remix) \o/");
        Assert.AreEqual(@"`Artist:(Earth, Wind & Fire \(Remix\) \\o/)", query.Query);
        Assert.AreEqual(@"Earth, Wind & Fire (Remix) \o/", query.GetField("Artist"));
        Assert.AreEqual(
            @"Artist:(Earth, Wind \& Fire \(Remix\) \\o\/)", query.AzureSearch);
    }

    // An empty group is invalid Lucene: Azure rejects the whole query
    [TestMethod]
    [DataRow("`Artist:( )", "")]
    [DataRow("`Artist:() Title:(Hey)", "Title:(Hey)")]
    [DataRow("`Artist:(  Ricky  ) x", "Artist:(Ricky) x")]
    public void AzureSearchDropsBlankFields(string query, string expected)
    {
        Assert.AreEqual(expected, new KeywordQuery(query).AzureSearch);
    }

    [TestMethod]
    public void UpdateWithWhitespaceRemovesTheField()
    {
        Assert.AreEqual("something", new KeywordQuery("something").Update("Artist", "  ").Query);
    }

    // Saved searches and SpotifyFromSearch playlists can hold these
    [TestMethod]
    public void AzureSearchKeepsRepeatedFields()
    {
        Assert.AreEqual(
            "Artist:(Ricky) Artist:(Martin)",
            new KeywordQuery("`Artist:(Ricky) Artist:(Martin)").AzureSearch);
    }

    // Only field values are escaped by FromParts, so the rest mustn't be unescaped
    [TestMethod]
    public void EverywhereTextKeepsBackslashesAcrossEdits()
    {
        var query = new KeywordQuery(@"`Artist:(x) AC\DC").Update("Title", "y");
        Assert.AreEqual(@"AC\DC", query.GetField("Everywhere"));
        Assert.AreEqual(@"`Artist:(x) Title:(y) AC\DC", query.Query);
        Assert.AreEqual(@"Artist:(x) Title:(y) AC\\DC", query.AzureSearch);
    }
}