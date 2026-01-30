namespace DanceLibrary.Tests;

[TestClass]
public class DanceValidationTests
{
    // Helper method to test validation logic without needing full dance setup
    private static TempoValidationResult TestValidation(DanceValidation validation, decimal tempo, string meter)
    {
        // Create a mock dance instance with validation
        var instance = new DanceInstance(
            style: "Test",
            tempoRange: new TempoRange(100, 250),
            exceptions: [],
            organizations: ["Test"]);
        
        instance.Validation = validation;
        
        return instance.ValidateTempo(tempo, meter);
    }

    [TestMethod]
    public void DanceValidation_NoValidation_ReturnsNoCorrection()
    {
        // Arrange - use a dance from the system without validation
        var waltz = Dances.Instance.DanceFromId("WLZ");
        var tempo = 120m;
        var meter = "3/4";

        // Act
        var result = waltz.ValidateTempo(tempo, meter);

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
        var tempo = 80m;
        var meter = "4/4";

        // Act
        var result = TestValidation(validation, tempo, meter);

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
        var tempo = 280m;
        var meter = "4/4";

        // Act
        var result = TestValidation(validation, tempo, meter);

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
        var tempo = 180m;
        var meter = "4/4";

        // Act
        var result = TestValidation(validation, tempo, meter);

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
        var tempo = 180m;
        var meter = "3/4";

        // Act
        var result = TestValidation(validation, tempo, meter);

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
        var tempo = 180m;
        string meter = null;

        // Act
        var result = TestValidation(validation, tempo, meter);

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
        var tempo = 180m;
        var meter = "4/4";

        // Act
        var result = TestValidation(validation, tempo, meter);

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
        var tempo = 80m;
        var meter = "3/4";

        // Act
        var result = TestValidation(validation, tempo, meter);

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
        var meter = "4/4";

        // Act
        var result = TestValidation(validation, (decimal)input, meter);

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
        var meter = "4/4";

        // Act
        var result = TestValidation(validation, (decimal)input, meter);

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
        var tempo = 180m; // Valid tempo

        // Act
        var result = TestValidation(validation, tempo, invalidMeter);

        // Assert
        Assert.IsTrue(result.RequiresMeterFlag);
        Assert.IsTrue(result.MeterFlagReason.Contains(invalidMeter));
    }
}





