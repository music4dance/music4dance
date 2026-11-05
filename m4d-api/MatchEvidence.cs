using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace m4dApi;

public record Evidence(string TitleMatch, string ArtistMatch, int? LengthDeltaSeconds, string Confidence);

/// <summary>
/// Compares a recognized title and artist with a title/artist search hit. Pure strings in,
/// evidence out, so the rules can be tuned (or moved) without touching the search code.
/// </summary>
public static partial class MatchEvidence
{
    // Starting thresholds from the plan; tune against real recognitions
    public const int SupportingLengthDelta = 5;
    public const int ConflictingLengthDelta = 15;

    public static Evidence Build(
        string requestedTitle, string requestedArtist, int? requestedSeconds,
        string title, string artist, int? lengthSeconds)
    {
        var titleMatch = TextMatch(requestedTitle, title);
        var artistMatch = TextMatch(requestedArtist, artist);
        int? delta = requestedSeconds.HasValue && lengthSeconds.HasValue
            ? Math.Abs(requestedSeconds.Value - lengthSeconds.Value)
            : null;

        // A partial title is usually a different version ("Blue Bayou" vs "Blue Bayou (Remix)"),
        // and a large length difference usually a different recording, whatever the text says
        var conflicting = titleMatch is "partial" or "none" || artistMatch == "none" || delta > ConflictingLengthDelta;
        var strongText = titleMatch is "exact" or "normalized" && artistMatch is "exact" or "normalized";
        var confidence = conflicting
            ? "low"
            : strongText && (delta == null || delta <= SupportingLengthDelta) ? "high" : "medium";

        return new Evidence(titleMatch, artistMatch, delta, confidence);
    }

    /// <summary>
    /// exact (ignoring case and surrounding space), normalized (same letters and digits once
    /// accents and punctuation are dropped), partial (one contains the other), or none.
    /// </summary>
    public static string TextMatch(string requested, string found)
    {
        if (string.Equals(requested?.Trim(), found?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "exact";
        }
        var a = Normalize(requested);
        var b = Normalize(found);
        if (a.Length == 0 || b.Length == 0)
        {
            return "none";
        }
        return a == b ? "normalized" : a.Contains(b) || b.Contains(a) ? "partial" : "none";
    }

    private static string Normalize(string s)
    {
        var decomposed = (s ?? string.Empty).Normalize(NormalizationForm.FormD);
        var kept = decomposed
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ')
            .ToArray();
        return Whitespace().Replace(new string(kept), " ").Trim();
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();
}
