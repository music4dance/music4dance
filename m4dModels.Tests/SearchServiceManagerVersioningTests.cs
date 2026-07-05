using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace m4dModels.Tests;

[TestClass]
public class SearchServiceManagerVersioningTests
{
    [TestMethod]
    public void HasNextFlags_AreFalse_WhenCodeVersionPlusOneIsNotConfigured()
    {
        var manager = CreateManager(includeVersion4: false, envVersion: 3);

        Assert.IsFalse(manager.HasNextIndex);
        Assert.IsFalse(manager.HasNextVersion);

        try
        {
            _ = manager.NextIndexName;
            Assert.Fail("Expected InvalidOperationException when next version is not configured.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "Index version 4 is not configured");
        }
    }

    [TestMethod]
    public void HasNextFlags_AreTrue_WhenCodeVersionPlusOneIsConfigured()
    {
        var manager = CreateManager(includeVersion4: true, envVersion: 3);

        Assert.IsTrue(manager.HasNextIndex);
        Assert.IsTrue(manager.HasNextVersion);
        Assert.AreEqual("songs-prod-4", manager.NextIndexName);
    }

    [TestMethod]
    public void EnvVersionBelowCodeVersion_IsClamped_AndDoesNotFakeNextVersion()
    {
        var manager = CreateManager(includeVersion4: false, envVersion: 2);

        Assert.AreEqual(3, manager.ConfigVersion);
        Assert.IsFalse(manager.NextVersion);
        Assert.IsFalse(manager.HasNextIndex);
        Assert.IsFalse(manager.HasNextVersion);
    }

    private static SearchServiceManager CreateManager(bool includeVersion4, int envVersion)
    {
        var values = new Dictionary<string, string>
        {
            ["SEARCHINDEX"] = "SongIndexProd",
            ["SEARCHINDEXVERSION"] = envVersion.ToString(),
            ["SongIndexProd-2:endpoint"] = "https://unit-test.search.windows.net",
            ["SongIndexProd-2:indexname"] = "songs-prod-2",
            ["SongIndexProd-3:endpoint"] = "https://unit-test.search.windows.net",
            ["SongIndexProd-3:indexname"] = "songs-prod-3",
        };

        if (includeVersion4)
        {
            values["SongIndexProd-4:endpoint"] = "https://unit-test.search.windows.net";
            values["SongIndexProd-4:indexname"] = "songs-prod-4";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var searchFactory = new Mock<IAzureClientFactory<SearchClient>>();
        var searchIndexFactory = new Mock<IAzureClientFactory<SearchIndexClient>>();

        return new SearchServiceManager(configuration, searchFactory.Object, searchIndexFactory.Object);
    }
}
