using m4d.ViewModels;

using m4dModels;

namespace m4d.Tests.ViewModels;

/// <summary>What one cluster's row of the shared-id review form becomes.</summary>
[TestClass]
public class SharedIdChoiceFormTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");

    [TestMethod]
    public void ToChoice_CarriesEveryField()
    {
        var form = new SharedIdChoiceForm
        {
            Action = SharedIdAction.Merge,
            SongIds = [A, B],
            Groups = ["A", "B"],
            CandidateIds = [7],
            Title = B,
            Removals = [$"{A}|0", $"{B}|1|S:sp1"]
        };

        var choice = form.ToChoice();

        Assert.AreEqual(SharedIdAction.Merge, choice.Action);
        CollectionAssert.AreEqual(new[] { A, B }, choice.SongIds.ToList());
        CollectionAssert.AreEqual(new[] { "A", "B" }, choice.Groups.ToList());
        CollectionAssert.AreEqual(new[] { 7 }, choice.CandidateIds.ToList());
        Assert.AreEqual(new SharedIdNamePicks(Title: B), choice.Names);
        CollectionAssert.AreEqual(new[] { new SharedIdRemoval(A, 0), new SharedIdRemoval(B, 1, "S:sp1") },
            choice.Removals.ToList());
    }

    [TestMethod]
    public void ToChoice_NothingPosted_IsAnEmptyChoiceWithTheProposedNames()
    {
        var choice = new SharedIdChoiceForm().ToChoice();

        Assert.AreEqual(SharedIdAction.Leave, choice.Action);
        Assert.IsEmpty(choice.SongIds);
        Assert.IsEmpty(choice.CandidateIds);
        Assert.IsEmpty(choice.Removals);
        Assert.AreEqual(new SharedIdNamePicks(), choice.Names);
        Assert.IsEmpty(choice.MergeGroups);
    }

    [TestMethod]
    public void ToChoice_MalformedRemovals_AreDropped()
    {
        var choice = new SharedIdChoiceForm { SongIds = [A], Removals = ["0|1", $"{A}|x", $"{A}|2"] }.ToChoice();

        Assert.AreEqual(new SharedIdRemoval(A, 2), choice.Removals.Single());
    }
}
