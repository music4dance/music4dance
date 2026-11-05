using Azure.Search.Documents.Indexes.Models;
using m4dApi;

namespace m4dModels.Tests;

/// <summary>
/// The partner API (m4d-api) reads the song index directly with its own copy of the field names
/// (m4d-api/IndexFields.cs, linked into this project). These tests fail an index change that
/// would break it here, in the web app's PR, instead of in production.
/// </summary>
[TestClass]
public class PartnerApiIndexContractTests
{
    private static Dictionary<string, SearchField> s_fields;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
        s_fields = new TestSongIndex().BuildIndexFields().ToDictionary(f => f.Name);
    }

    [TestMethod]
    public void Index_HasEveryTopLevelFieldTheApiSelects()
    {
        foreach (var name in IndexFields.Selected)
        {
            Assert.IsTrue(s_fields.ContainsKey(name), $"The partner API selects {name}");
        }
    }

    [TestMethod]
    public void Index_CanSearchAndFilterTheFieldsTheApiQueries()
    {
        foreach (var name in IndexFields.Searched)
        {
            Assert.IsTrue(s_fields[name].IsSearchable, $"The partner API searches {name}");
        }
        foreach (var name in IndexFields.Filtered)
        {
            Assert.IsTrue(s_fields[name].IsFilterable, $"The partner API filters on {name}");
        }
        var allVotes = s_fields[IndexFields.Dance(IndexFields.AllDances)].Fields.Single(f => f.Name == IndexFields.Votes);
        Assert.IsTrue(allVotes.IsFilterable, "The partner API filters on dance_ALL/Votes");
    }

    [TestMethod]
    public void Index_HasTheDanceSubfieldsForEveryDance()
    {
        var danceIds = DanceLibrary.Dances.Instance.AllDanceTypes.Select(t => t.Id).Append(IndexFields.AllDances);
        foreach (var id in danceIds)
        {
            var name = IndexFields.Dance(id);
            Assert.IsTrue(s_fields.TryGetValue(name, out var field), $"The partner API selects {name}");
            var subfields = field.Fields.Select(f => f.Name).ToHashSet();
            foreach (var subfield in IndexFields.DanceSubfields)
            {
                Assert.Contains(subfield, subfields, $"The partner API reads {name}/{subfield}");
            }
        }
    }
}
