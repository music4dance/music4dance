using System.Runtime.CompilerServices;

namespace DanceLibrary.Tests;

/// <summary>
/// Loads the real dances.json/dancegroups.json shipped to production - not the DanceTests or
/// m4dModels.Tests fixture copies used by other tests - to catch drift between the two. A
/// schema migration (e.g. moving DanceValidation from DanceInstance to DanceType) can be applied
/// correctly to every hand-maintained fixture while production data or code falls out of sync,
/// and fixture-only coverage would never notice.
/// </summary>
[TestClass]
public class ProductionDanceDataTests
{
    private static Dances s_dances;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        var typesJson = await File.ReadAllTextAsync(ContentPath("dances.json"));
        var groupsJson = await File.ReadAllTextAsync(ContentPath("dancegroups.json"));
        s_dances = Dances.Load(typesJson, groupsJson);
    }

    // Locates the repo's actual content files relative to this source file rather than the test
    // binary's output directory, so it works the same from `dotnet test`, the unlocked-output
    // build tasks, and the IDE test runner.
    private static string ContentPath(string fileName, [CallerFilePath] string testFilePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, ".."));
        return Path.Combine(repoRoot, "m4d", "ClientApp", "src", "assets", "content", fileName);
    }

    [TestMethod]
    public void Salsa_HasValidationRules()
    {
        var dance = s_dances.DanceFromId("SLS") as DanceType;
        Assert.IsNotNull(dance, "Salsa (SLS) should exist in production dances.json as a DanceType");
        Assert.IsNotNull(dance.Validation, "Salsa should have a validation block");
        Assert.AreEqual(120.0m, dance.Validation.DoubleTempoIfBelow);
        Assert.AreEqual(250.0m, dance.Validation.HalveTempoIfAbove);
        CollectionAssert.AreEquivalent(new[] { "3/4", "6/8" }, dance.Validation.FlagInvalidMeters);
    }

    [TestMethod]
    public void Quickstep_HasValidationRules()
    {
        var dance = s_dances.DanceFromId("QST") as DanceType;
        Assert.IsNotNull(dance, "Quickstep (QST) should exist in production dances.json as a DanceType");
        Assert.IsNotNull(dance.Validation, "Quickstep should have a validation block");
        Assert.AreEqual(120.0m, dance.Validation.DoubleTempoIfBelow);
        Assert.AreEqual(250.0m, dance.Validation.HalveTempoIfAbove);
        CollectionAssert.AreEquivalent(new[] { "3/4", "6/8" }, dance.Validation.FlagInvalidMeters);
    }

    [TestMethod]
    public void Quickstep_LowTempo_ActuallyCorrects()
    {
        // Regression test for a real incident: hundreds of catalog Quickstep songs with a
        // too-low tempo produced zero corrections when run through "Validate Tempo" because the
        // production data/code combination wasn't actually exercised by any existing test - only
        // hand-maintained fixtures were. This drives the real dances.json end-to-end.
        var dance = s_dances.DanceFromId("QST");
        var result = dance.ValidateTempo(100m, null);
        Assert.IsTrue(result.RequiresCorrection, "A Quickstep song at 100 BPM should be flagged for correction");
        Assert.AreEqual(200m, result.CorrectedTempo);
    }

    [TestMethod]
    public void Salsa_LowTempo_ActuallyCorrects()
    {
        var dance = s_dances.DanceFromId("SLS");
        var result = dance.ValidateTempo(80m, null);
        Assert.IsTrue(result.RequiresCorrection, "A Salsa song at 80 BPM should be flagged for correction");
        Assert.AreEqual(160m, result.CorrectedTempo);
    }

    [TestMethod]
    [DataRow("PBD", 145.0, 500.0)]
    [DataRow("CST", 150.0, 500.0)]
    [DataRow("BOL", 50.0, 150.0)]
    [DataRow("JSW", 125.0, 300.0)]
    [DataRow("BBA", 140.0, 500.0)]
    [DataRow("JIV", 100.0, 230.0)]
    [DataRow("NC2", 25.0, 140.0)]
    [DataRow("BCH", 100.0, 190.0)]
    [DataRow("ECS", 80.0, 175.0)]
    [DataRow("SSW", 110.0, 240.0)]
    public void NewlyAddedDance_HasValidationRules(string danceId, double doubleBelow, double halveAbove)
    {
        var dance = s_dances.DanceFromId(danceId) as DanceType;
        Assert.IsNotNull(dance, $"{danceId} should exist in production dances.json as a DanceType");
        Assert.IsNotNull(dance.Validation, $"{danceId} should have a validation block");
        Assert.AreEqual((decimal)doubleBelow, dance.Validation.DoubleTempoIfBelow);
        Assert.AreEqual((decimal)halveAbove, dance.Validation.HalveTempoIfAbove);
        CollectionAssert.AreEquivalent(new[] { "3/4", "6/8" }, dance.Validation.FlagInvalidMeters);
    }

    [TestMethod]
    public void SlowWaltz_HasNoValidationRules()
    {
        // Sanity check against the opposite mistake: a copy-paste that gives every dance a
        // validation block instead of just the ones that have actually been tuned for one.
        var dance = s_dances.DanceFromId("SWZ") as DanceType;
        Assert.IsNotNull(dance, "Slow Waltz (SWZ) should exist in production dances.json as a DanceType");
        Assert.IsNull(dance.Validation);
    }
}
