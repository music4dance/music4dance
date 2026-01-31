namespace DanceLibrary.Tests;

[TestClass]
public class DanceValidationTests
{
    // Helper method to create a properly configured DanceInstance for testing
    private static DanceInstance CreateTestDanceInstance(DanceValidation validation, string style = "Test")
    {
        // Create the parent DanceType first
        var danceType = new DanceType("Test Dance", new Meter(4, 4), []);
        danceType.Id = "TST";
        
        // Create the DanceInstance with all required parameters
        var instance = new DanceInstance(
            style: style,
            tempoRange: new TempoRange(100, 250),
            exceptions: [],
            organizations: ["Test"]);
        
        // Set the validation rules
        instance.Validation = validation;
        
        // Set the parent DanceType (this is what makes dance.Name work)
        instance.DanceType = danceType;
        
        return instance;
    }

    [TestMethod]
    public void DanceValidation_NoValidation_ReturnsNoCorrection()
    {
        // Arrange - DanceInstance without validation rules
        var instance = CreateTestDanceInstance(validation: null);
        var tempo = 120m;
        var meter = "4/4";

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 80m;
        var meter = "4/4";

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 280m;
        var meter = "4/4";

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 180m;
        var meter = "4/4";

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 180m;
        var meter = "3/4";

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 180m;
        string meter = null;

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 180m;
        var meter = "4/4";

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 80m;
        var meter = "3/4";

        // Act
        var result = instance.ValidateTempo(tempo, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var meter = "4/4";

        // Act
        var result = instance.ValidateTempo((decimal)input, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var meter = "4/4";

        // Act
        var result = instance.ValidateTempo((decimal)input, meter);

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
        var instance = CreateTestDanceInstance(validation);
        var tempo = 180m; // Valid tempo

        // Act
        var result = instance.ValidateTempo(tempo, invalidMeter);

        // Assert
        Assert.IsTrue(result.RequiresMeterFlag);
        Assert.IsTrue(result.MeterFlagReason.Contains(invalidMeter));
    }

    [TestMethod]
    public void DanceValidation_DanceTypeWithInstances_UsesSocialInstance()
    {
        // Arrange - Create a DanceType with multiple instances
        var danceType = new DanceType("Salsa", new Meter(4, 4), []);
        danceType.Id = "SLS";
        
        // Add Social instance with validation
        var socialInstance = new DanceInstance(
            style: "Social",
            tempoRange: new TempoRange(160, 220),
            exceptions: [],
            organizations: ["Social"]);
        socialInstance.Validation = new DanceValidation
        {
            DoubleTempoIfBelow = 120m,
            HalveTempoIfAbove = 250m
        };
        socialInstance.DanceType = danceType;
        danceType.Instances.Add(socialInstance);
        
        // Add Competition instance without validation
        var competitionInstance = new DanceInstance(
            style: "International",
            tempoRange: new TempoRange(180, 200),
            exceptions: [],
            organizations: ["DanceSport"]);
        competitionInstance.DanceType = danceType;
        danceType.Instances.Add(competitionInstance);
        
        var tempo = 80m;
        var meter = "4/4";

        // Act - Call ValidateTempo on the DanceType (it should use Social instance)
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual(160m, result.CorrectedTempo);
    }

    [TestMethod]
    public void DanceValidation_DanceTypeNoSocial_UsesFirstInstanceWithValidation()
    {
        // Arrange - Create a DanceType without Social instance
        var danceType = new DanceType("Cha Cha", new Meter(4, 4), []);
        danceType.Id = "CHA";
        
        // Add International instance with validation
        var internationalInstance = new DanceInstance(
            style: "International Latin",
            tempoRange: new TempoRange(120, 128),
            exceptions: [],
            organizations: ["DanceSport"]);
        internationalInstance.Validation = new DanceValidation
        {
            DoubleTempoIfBelow = 100m
        };
        internationalInstance.DanceType = danceType;
        danceType.Instances.Add(internationalInstance);
        
        var tempo = 80m;
        var meter = "4/4";

        // Act
        var result = danceType.ValidateTempo(tempo, meter);

        // Assert
        Assert.IsTrue(result.RequiresCorrection);
        Assert.AreEqual(160m, result.CorrectedTempo);
    }
}






