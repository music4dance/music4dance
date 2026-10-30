using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using DanceLibrary;

namespace m4dApi;

public record ResolveRequest(
    string Isrc, string AppleMusicId, string SpotifyId, string Title, string Artist, int? DurationSeconds);

public record DanceRating(string DanceId, string Name, int Weight, double? Tempo, string[] StyleTags);

public record Evidence(string TitleMatch, string ArtistMatch, int? LengthDeltaSeconds, string Confidence);

public record Candidate(
    string SongId, string SongUrl, string Title, string Artist, int? LengthSeconds, double? Tempo,
    string Meter, DanceRating[] DanceRatings, Evidence Evidence);

public record QueryTiming(string Kind, long Ms, int Results);

public record ResolveResult(string Match, string Method, Candidate[] Candidates, List<QueryTiming> Queries);

/// <summary>
/// Resolves a recognized track to music4dance songs straight from the search index: no SQL, no
/// Song materialization, no call into the web app. Field names mirror m4dModels/SongIndex.cs.
/// </summary>
public partial class SongResolver
{
    // The confirmed-content filter from SongIndex.AddCruftInfo
    private const string NoCruft = "DanceTags/any() and (not DanceTags/any() or dance_ALL/Votes ne null)";
    private const string SongUrlBase = "https://www.music4dance.net/song/details?id=";
    private static readonly string[] Meters = ["4/4", "3/4", "2/4"];

    private readonly SearchClient _client;
    private readonly Dances _dances;
    private readonly string[] _danceIds;
    private readonly string[] _select;

    public SongResolver(SearchClient client, Dances dances)
    {
        _client = client;
        _dances = dances;
        _danceIds = [.. dances.AllDanceTypes.Select(t => t.Id).Where(id => id != "ALL")];
        _select =
        [
            "SongId", "Title", "Artist", "Length", "Tempo", "TempoTags", "ServiceIds", "dance_ALL",
            .. _danceIds.Select(id => $"dance_{id}"),
        ];
    }

    public static string Validate(ResolveRequest request)
    {
        if (request == null)
        {
            return "Request body is required";
        }
        if (!string.IsNullOrWhiteSpace(request.Isrc) && !IsrcPattern().IsMatch(NormalizeIsrc(request.Isrc)))
        {
            return "isrc is not a valid ISRC";
        }
        if (!string.IsNullOrWhiteSpace(request.AppleMusicId) && !ApplePattern().IsMatch(request.AppleMusicId))
        {
            return "appleMusicId must be numeric";
        }
        if (!string.IsNullOrWhiteSpace(request.SpotifyId) && !SpotifyPattern().IsMatch(request.SpotifyId))
        {
            return "spotifyId is not a valid Spotify track id";
        }
        var hasId = !string.IsNullOrWhiteSpace(request.Isrc)
            || !string.IsNullOrWhiteSpace(request.AppleMusicId)
            || !string.IsNullOrWhiteSpace(request.SpotifyId);
        var hasText = !string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(request.Artist);
        return hasId || hasText ? null : "Supply at least one id, or both title and artist";
    }

    public async Task<ResolveResult> Resolve(ResolveRequest request, CancellationToken cancellationToken)
    {
        var queries = new List<QueryTiming>();

        var ids = RequestedIds(request);
        if (ids.Count > 0)
        {
            // Ids are validated to [A-Za-z0-9], so they can't break out of the search.in literal
            var options = new SearchOptions
            {
                Filter = $"ServiceIds/any(id: search.in(id, '{string.Join(",", ids.Select(i => i.Id))}')) and {NoCruft}",
                Size = 5,
            };
            var docs = await Query("*", options, "id", queries, cancellationToken);
            if (docs.Count > 0)
            {
                // Duplicates awaiting merge can share an id: prefer the most confirmed votes
                var best = docs.MaxBy(d => DanceVotes(d, "ALL") ?? 0);
                var serviceIds = Strings(best["ServiceIds"]).ToHashSet();
                var method = ids.First(i => serviceIds.Contains(i.Id)).Method;
                return new ResolveResult("exact", method, [ToCandidate(best, null)], queries);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(request.Artist))
        {
            var options = new SearchOptions
            {
                Filter = NoCruft,
                Size = 3,
                SearchFields = { "Title", "Artist" },
            };
            var docs = await Query(
                $"{StripOperators(request.Title)} {StripOperators(request.Artist)}", options, "titleArtist",
                queries, cancellationToken);
            if (docs.Count > 0)
            {
                var candidates = docs.Select(d => ToCandidate(d, request)).ToArray();
                return new ResolveResult("fuzzy", "titleArtist", candidates, queries);
            }
        }

        return new ResolveResult("none", null, [], queries);
    }

    public async Task<bool> Ping(CancellationToken cancellationToken)
    {
        var options = new SearchOptions { Size = 1, Select = { "SongId" } };
        var response = await _client.SearchAsync<SearchDocument>("*", options, cancellationToken);
        return response.Value != null;
    }

    private async Task<List<SearchDocument>> Query(
        string search, SearchOptions options, string kind, List<QueryTiming> queries,
        CancellationToken cancellationToken)
    {
        foreach (var field in _select)
        {
            options.Select.Add(field);
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.SearchAsync<SearchDocument>(search, options, cancellationToken);
        var docs = new List<SearchDocument>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            docs.Add(result.Document);
        }
        queries.Add(new QueryTiming(kind, stopwatch.ElapsedMilliseconds, docs.Count));
        return docs;
    }

    private Candidate ToCandidate(SearchDocument doc, ResolveRequest fuzzyRequest)
    {
        var songId = doc.GetString("SongId");
        var title = doc.GetString("Title");
        var artist = doc.GetString("Artist");
        var length = AsInt(doc["Length"]);
        var meter = Strings(doc["TempoTags"]).FirstOrDefault(Meters.Contains);

        var ratings = _danceIds
            .Select(id => (id, field: doc.TryGetValue($"dance_{id}", out var value) ? value as SearchDocument : null))
            .Where(d => d.field != null)
            .Select(d => (d.id, d.field, votes: AsInt(d.field["Votes"]) ?? 0))
            // -1 is the unconfirmed-only sentinel written by SongIndex.DocumentFromSong
            .Where(d => d.votes > 0)
            .OrderByDescending(d => d.votes)
            .Select(d => new DanceRating(
                d.id, _dances.DanceFromId(d.id)?.Name ?? d.id, d.votes, AsDouble(d.field["Tempo"]),
                [.. Strings(d.field["StyleTags"])]))
            .ToArray();

        var evidence = fuzzyRequest == null ? null : BuildEvidence(fuzzyRequest, title, artist, length);

        return new Candidate(
            songId, SongUrlBase + songId, title, artist, length, AsDouble(doc["Tempo"]), meter, ratings, evidence);
    }

    private static Evidence BuildEvidence(ResolveRequest request, string title, string artist, int? length)
    {
        var titleMatch = TextMatch(request.Title, title);
        var artistMatch = TextMatch(request.Artist, artist);
        int? delta = request.DurationSeconds.HasValue && length.HasValue
            ? Math.Abs(request.DurationSeconds.Value - length.Value)
            : null;

        // Starting thresholds from the plan; tune against real recognitions
        var strongText = titleMatch is "exact" or "normalized" && artistMatch is "exact" or "normalized";
        var confidence = delta > 15 || titleMatch is "partial" or "none"
            ? "low"
            : strongText && (delta == null || delta <= 5) ? "high" : "medium";

        return new Evidence(titleMatch, artistMatch, delta, confidence);
    }

    private static string TextMatch(string requested, string found)
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

    private static List<(string Method, string Id)> RequestedIds(ResolveRequest request)
    {
        // Order is the priority for reporting which id matched
        var ids = new List<(string Method, string Id)>();
        if (!string.IsNullOrWhiteSpace(request.Isrc))
        {
            // Some ISRCs are stored lowercase in the index, and ServiceIds matching is case-sensitive
            var isrc = NormalizeIsrc(request.Isrc);
            ids.Add(("isrc", $"R:{isrc}"));
            ids.Add(("isrc", $"R:{isrc.ToLowerInvariant()}"));
        }
        if (!string.IsNullOrWhiteSpace(request.AppleMusicId))
        {
            ids.Add(("appleMusicId", $"I:{request.AppleMusicId}"));
        }
        if (!string.IsNullOrWhiteSpace(request.SpotifyId))
        {
            ids.Add(("spotifyId", $"S:{request.SpotifyId}"));
        }
        return ids;
    }

    private static string NormalizeIsrc(string isrc) => isrc.Replace("-", string.Empty).Trim().ToUpperInvariant();

    private static string StripOperators(string s) => SimpleQueryOperators().Replace(s, " ");

    private static int? DanceVotes(SearchDocument doc, string danceId) =>
        doc.TryGetValue($"dance_{danceId}", out var value) && value is SearchDocument field
            ? AsInt(field["Votes"])
            : null;

    private static IEnumerable<string> Strings(object value) =>
        value is IEnumerable<object> items ? items.Select(i => i?.ToString()).Where(i => i != null) : [];

    private static int? AsInt(object value) => value == null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    private static double? AsDouble(object value) =>
        value == null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    [GeneratedRegex("^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$")]
    private static partial Regex IsrcPattern();

    [GeneratedRegex("^[0-9]{1,15}$")]
    private static partial Regex ApplePattern();

    [GeneratedRegex("^[0-9A-Za-z]{22}$")]
    private static partial Regex SpotifyPattern();

    [GeneratedRegex("[+|\\-\"*()~\\\\&!^:\\[\\]{}/?]")]
    private static partial Regex SimpleQueryOperators();

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();
}
