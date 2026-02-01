namespace DanceLibrary;

/// <summary>
/// Result of tempo validation containing any corrections or flags needed.
/// </summary>
public class TempoValidationResult
{
    public bool RequiresCorrection { get; set; }
    public decimal? CorrectedTempo { get; set; }
    public string CorrectionReason { get; set; }
    
    public bool RequiresMeterFlag { get; set; }
    public string MeterFlagReason { get; set; }
}

/// <summary>
/// Extension methods for validating tempo and meter data from external sources.
/// </summary>
public static class DanceValidationExtensions
{
    /// <summary>
    /// Validates tempo and meter against this dance's validation rules.
    /// Returns a result indicating if corrections or flags are needed.
    /// </summary>
    /// <param name="dance">The dance object to validate against</param>
    /// <param name="tempo">The tempo in BPM from external source (Spotify/EchoNest)</param>
    /// <param name="meter">The meter string (e.g., "4/4", "3/4") from external source</param>
    /// <returns>Validation result with any corrections or flags</returns>
    public static TempoValidationResult ValidateTempo(
        this DanceObject dance, 
        decimal tempo, 
        string meter)
    {
        var result = new TempoValidationResult();
        
        // Get validation rules - try DanceInstance first, then fall back to DanceType
        DanceValidation validation = null;
        if (dance is DanceInstance instance)
        {
            validation = instance.Validation;
        }
        else if (dance is DanceType danceType && danceType.Instances.Count > 0)
        {
            // For DanceType, check if any instance has validation rules
            // Prefer Social style, or use first instance with validation
            var socialInstance = danceType.Instances.FirstOrDefault(i => i.Style == "Social");
            validation = socialInstance?.Validation ?? 
                         danceType.Instances.FirstOrDefault(i => i.Validation != null)?.Validation;
        }
        
        if (validation == null)
        {
            return result; // No validation rules for this dance
        }
        
        // Check tempo corrections
        if (validation.DoubleTempoIfBelow.HasValue &&
            tempo < validation.DoubleTempoIfBelow.Value)
        {
            result.RequiresCorrection = true;
            result.CorrectedTempo = tempo * 2;
            result.CorrectionReason = 
                $"Tempo {tempo} BPM below {validation.DoubleTempoIfBelow} threshold for {dance.Name} - doubled to {result.CorrectedTempo}";
        }
        else if (validation.HalveTempoIfAbove.HasValue &&
            tempo > validation.HalveTempoIfAbove.Value)
        {
            result.RequiresCorrection = true;
            result.CorrectedTempo = tempo / 2;
            result.CorrectionReason = 
                $"Tempo {tempo} BPM above {validation.HalveTempoIfAbove} threshold for {dance.Name} - halved to {result.CorrectedTempo}";
        }
        
        // Check meter validation
        if (!string.IsNullOrEmpty(meter) && 
            validation.FlagInvalidMeters != null && validation.FlagInvalidMeters.Contains(meter))
        {
            result.RequiresMeterFlag = true;
            var danceName = dance?.Name ?? "this dance";
            var expectedMeter = dance?.Meter?.ToString() ?? "4/4";
            result.MeterFlagReason = 
                $"Invalid meter {meter} for {danceName} (expected {expectedMeter})";
        }
        
        return result;
    }
}

