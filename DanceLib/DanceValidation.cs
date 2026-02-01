using Newtonsoft.Json;

namespace DanceLibrary;

/// <summary>
/// Validation rules for tempo and meter from external sources (Spotify/EchoNest).
/// Used to detect and correct common tempo detection errors.
/// </summary>
public class DanceValidation
{
    /// <summary>
    /// If tempo is below this threshold, double it (half-time detection error).
    /// </summary>
    [JsonProperty("doubleTempoIfBelow", NullValueHandling = NullValueHandling.Ignore)]
    public decimal? DoubleTempoIfBelow { get; set; }

    /// <summary>
    /// If tempo is above this threshold, halve it (double-time detection error).
    /// </summary>
    [JsonProperty("halveTempoIfAbove", NullValueHandling = NullValueHandling.Ignore)]
    public decimal? HalveTempoIfAbove { get; set; }

    /// <summary>
    /// List of meter signatures (e.g., "3/4", "6/8") that should trigger a flag for manual review.
    /// </summary>
    [JsonProperty("flagInvalidMeters", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> FlagInvalidMeters { get; set; }
}

