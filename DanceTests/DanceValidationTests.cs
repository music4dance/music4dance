namespace DanceLibrary.Tests;

[TestClass]
public class DanceValidationTests
{
    // Helper method to create a DanceType with validation rules attached directly.
    // Validation lives on DanceType (not per style/instance) - see DanceType.Validation for why.
    private static DanceType CreateTestDanceType(DanceValidation validation)
    {
        var danceType = new DanceType("Test Dance", new Meter(4, 4), [])
        {
            Id = "TST",
            Validation = validation
        };

        return danceType;
    }

    [TestMethod]
    public void DanceValidation_NoValidation_ReturnsNoCorrection()
    {
        // Arrange - DanceType without validation rules
        var danceType = CreateTestDanceType(validation: null);
        var tempo = 120m;
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsFalse(result.RequiresCorrection);
        Assert.IsFalse(result.RequiresMeterFlag);
        Assert.IsNull(result.CorrectedTempo);
    }

    [TestMethod]
    public void DanceValidation_TempoTooLow_DoublesTempo()
    {
        // Arrange
        var validation = new DanceValidation
        {
            DoubleTempoIfBelow = 120m
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 80m;
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual(160m, result.CorrectedTempo);
        Assert.IsTrue(result.CorrectionReason.Contains("doubled"));
        Assert.IsTrue(result.CorrectionReason.Contains("80"));
        Assert.IsTrue(result.CorrectionReason.Contains("160"));
    }

    [TestMethod]
    public void DanceValidation_TempoTooHigh_HalvesTempo()
    {
        // Arrange
        var validation = new DanceValidation
        {
            HalveTempoIfAbove = 250m
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 280m;
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual(140m, result.CorrectedTempo);
        Assert.IsTrue(result.CorrectionReason.Contains("halved"));
        Assert.IsTrue(result.CorrectionReason.Contains("280"));
        Assert.IsTrue(result.CorrectionReason.Contains("140"));
    }

    [TestMethod]
    public void DanceValidation_TempoValid_NoCorrection()
    {
        // Arrange
        var validation = new DanceValidation
        {
            DoubleTempoIfBelow = 120m,
            HalveTempoIfAbove = 250m
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 180m;
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsFalse(result.RequiresCorrection);
        Assert.IsNull(result.CorrectedTempo);
    }

    [TestMethod]
    public void DanceValidation_InvalidMeter_FlagsMeter()
    {
        // Arrange
        var validation = new DanceValidation
        {
            FlagInvalidMeters = new List<string> { "3/4", "6/8" }
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 180m;
        var meter = "3/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsFalse(result.RequiresCorrection);
        Assert.IsTrue(result.RequiresMeterFlag);
        Assert.IsTrue(result.MeterFlagReason.Contains("Invalid meter"));
        Assert.IsTrue(result.MeterFlagReason.Contains("3/4"));
    }

    [TestMethod]
    public void DanceValidation_NullMeter_NoMeterFlag()
    {
        // Arrange
        var validation = new DanceValidation
        {
            FlagInvalidMeters = new List<string> { "3/4" }
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 180m;
        string meter = null;

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsFalse(result.RequiresMeterFlag);
    }

    [TestMethod]
    public void DanceValidation_ValidMeter_NoMeterFlag()
    {
        // Arrange
        var validation = new DanceValidation
        {
            FlagInvalidMeters = new List<string> { "3/4", "6/8" }
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 180m;
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsFalse(result.RequiresMeterFlag);
    }

    [TestMethod]
    public void DanceValidation_BothTempoAndMeterInvalid_FlagsBoth()
    {
        // Arrange
        var validation = new DanceValidation
        {
            DoubleTempoIfBelow = 120m,
            FlagInvalidMeters = new List<string> { "3/4" }
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 80m;
        var meter = "3/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual(160m, result.CorrectedTempo);
        Assert.IsTrue(result.RequiresMeterFlag);
        Assert.IsTrue(result.MeterFlagReason.Contains("Invalid meter"));
    }

    [TestMethod]
    [DataRow(119, 238)] // Just below threshold
    [DataRow(60, 120)]  // Very low tempo
    [DataRow(100, 200)] // Common error case
    public void DanceValidation_VariousLowTempos_DoublesCorrectly(int input, int expected)
    {
        // Arrange
        var validation = new DanceValidation
        {
            DoubleTempoIfBelow = 120m
        };
        var danceType = CreateTestDanceType(validation);
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo((decimal)input, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual((decimal)expected, result.CorrectedTempo);
    }

    [TestMethod]
    [DataRow(251, 125.5)] // Just above threshold
    [DataRow(300, 150)]   // Very high tempo
    [DataRow(400, 200)]   // Extreme case
    public void DanceValidation_VariousHighTempos_HalvesCorrectly(double input, double expected)
    {
        // Arrange
        var validation = new DanceValidation
        {
            HalveTempoIfAbove = 250m
        };
        var danceType = CreateTestDanceType(validation);
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo((decimal)input, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual((decimal)expected, result.CorrectedTempo);
    }

    [TestMethod]
    [DataRow("2/4")]
    [DataRow("6/8")]
    [DataRow("5/4")]
    public void DanceValidation_InvalidMeters_FlagsAll(string invalidMeter)
    {
        // Arrange
        var validation = new DanceValidation
        {
            FlagInvalidMeters = new List<string> { "2/4", "6/8", "5/4" }
        };
        var danceType = CreateTestDanceType(validation);
        var tempo = 180m; // Valid tempo

        // Act
        var result = danceType.ValidateTempo(tempo, invalidMeter);

        // Assert
        Assert.IsTrue(result.RequiresMeterFlag);
        Assert.IsTrue(result.MeterFlagReason.Contains(invalidMeter));
    }

    [TestMethod]
    public void DanceValidation_AppliesRegardlessOfWhichInstancesExist()
    {
        // Arrange - validation lives on the DanceType, so it applies the same way whether the
        // dance has one style, several, or none loaded - style is irrelevant to resolution.
        var danceType = new DanceType("Salsa", new Meter(4, 4), [])
        {
            Id = "SLS",
            Validation = new DanceValidation
            {
                DoubleTempoIfBelow = 120m,
                HalveTempoIfAbove = 250m
            }
        };

        var socialInstance = new DanceInstance(
            style: "Social",
            tempoRange: new TempoRange(160, 220),
            exceptions: [],
            organizations: ["Social"]);
        socialInstance.DanceType = danceType;
        danceType.Instances.Add(socialInstance);

        var competitionInstance = new DanceInstance(
            style: "American Rhythm",
            tempoRange: new TempoRange(200, 200),
            exceptions: [],
            organizations: ["NDCA"]);
        competitionInstance.DanceType = danceType;
        danceType.Instances.Add(competitionInstance);

        var tempo = 80m;
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual(160m, result.CorrectedTempo);
    }

    [TestMethod]
    public void DanceValidation_DanceInstance_NeverHasOwnRules()
    {
        // Arrange - DanceInstance no longer carries Validation itself; calling ValidateTempo
        // directly on one (rather than its DanceType) is always a no-op.
        var danceType = new DanceType("Salsa", new Meter(4, 4), [])
        {
            Id = "SLS",
            Validation = new DanceValidation { DoubleTempoIfBelow = 120m }
        };

        var instance = new DanceInstance(
            style: "Social",
            tempoRange: new TempoRange(160, 220),
            exceptions: [],
            organizations: ["Social"]);
        instance.DanceType = danceType;
        danceType.Instances.Add(instance);

        // Act
        var result = instance.ValidateTempo(80m, "4/4");

        // Assert
        Assert.IsFalse(result.RequiresCorrection);
        Assert.IsFalse(result.RequiresMeterFlag);
    }
}
