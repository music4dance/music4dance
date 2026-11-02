using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using DanceLibrary;

namespace m4dApi;

public record ResolveRequest(
    string Isrc, string AppleMusicId, string SpotifyId, string Title, string Artist, int? DurationSeconds);

// Tempo is beats per minute everywhere in the API (never measures or bars per minute), spelled
// out in the field name because "BPM" can mean bars per minute

public record DanceRating(string DanceId, string DanceName, int Weight, double? BeatsPerMinute, string[] Styles);

public record Candidate(
    string SongId, string SongUrl, string Title, string Artist, int? LengthSeconds, double? BeatsPerMinute,
    string Meter, DanceRating[] DanceRatings,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Evidence Evidence);

public record QueryTiming(string Kind, long Ms, int Results);

public record ResolveResult(
    string Match, string Method, Candidate[] Candidates, [property: JsonIgnore] IReadOnlyList<QueryTiming> Queries);

/// <summary>
/// Resolves a recognized track to music4dance songs straight from the search index: no SQL, no
/// Song materialization, no call into the web app. Only the fields in IndexFields leave Search,
/// so the property log, user names and comments never do.
/// </summary>
public partial class SongResolver
{
    public const int MaxTextLength = 300;
    public const int MaxCandidates = 3;
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
        _danceIds = [.. dances.AllDanceTypes.Select(t => t.Id).Where(id => id != IndexFields.AllDances)];
        _select =
        [
            .. IndexFields.Selected,
            IndexFields.Dance(IndexFields.AllDances),
            .. _danceIds.Select(IndexFields.Dance),
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
        if (request.Title?.Length > MaxTextLength || request.Artist?.Length > MaxTextLength)
        {
            return $"title and artist are limited to {MaxTextLength} characters";
        }
        if (request.DurationSeconds is < 0 or > 86400)
        {
            return "durationSeconds is out of range";
        }
        var hasId = !string.IsNullOrWhiteSpace(request.Isrc)
            || !string.IsNullOrWhiteSpace(request.AppleMusicId)
            || !string.IsNullOrWhiteSpace(request.SpotifyId);
        var hasText = !string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(request.Artist);
        return hasId || hasText ? null : "Supply at least one id, or both title and artist";
    }

    /// <summary>
    /// One query for every supplied id; only when none of them matches, one title/artist query.
    /// Expects a request that passed Validate.
    /// </summary>
    public async Task<ResolveResult> Resolve(ResolveRequest request, CancellationToken cancellationToken)
    {
        var queries = new List<QueryTiming>();

        var ids = RequestedIds(request);
        if (ids.Count > 0)
        {
            // Ids are validated to [A-Za-z0-9], so they can't break out of the search.in literal
            var options = new SearchOptions
            {
                Filter = $"{IndexFields.ServiceIds}/any(id: search.in(id, '{string.Join(",", ids.Select(i => i.Id))}'))"
                    + $" and {IndexFields.ConfirmedOnly}",
                Size = 5,
            };
            var docs = await Query("*", options, "id", queries, cancellationToken);
            if (docs.Count > 0)
            {
                // Duplicates awaiting merge can share an id: prefer the most confirmed votes
                var best = docs.MaxBy(d => Votes(d, IndexFields.AllDances) ?? 0);
                var serviceIds = Strings(Field(best, IndexFields.ServiceIds)).ToHashSet();
                var method = ids.First(i => serviceIds.Contains(i.Id)).Method;
                return new ResolveResult("exact", method, [ToCandidate(best, null)], queries);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(request.Artist))
        {
            var options = new SearchOptions { Filter = IndexFields.ConfirmedOnly, Size = MaxCandidates };
            foreach (var field in IndexFields.Searched)
            {
                options.SearchFields.Add(field);
            }
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
        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
        {
            docs.Add(result.Document);
        }
        queries.Add(new QueryTiming(kind, stopwatch.ElapsedMilliseconds, docs.Count));
        return docs;
    }

    private Candidate ToCandidate(SearchDocument doc, ResolveRequest fuzzyRequest)
    {
        var songId = Field(doc, IndexFields.SongId)?.ToString();
        var title = Field(doc, IndexFields.Title)?.ToString();
        var artist = Field(doc, IndexFields.Artist)?.ToString();
        var length = AsInt(Field(doc, IndexFields.Length));
        var meter = Strings(Field(doc, IndexFields.TempoTags)).FirstOrDefault(Meters.Contains);

        var ratings = _danceIds
            .Select(id => (id, field: Field(doc, IndexFields.Dance(id)) as SearchDocument))
            .Where(d => d.field != null)
            .Select(d => (d.id, d.field, votes: AsInt(Field(d.field, IndexFields.Votes)) ?? 0))
            // -1 is the unconfirmed-only sentinel written by SongIndex.DocumentFromSong
            .Where(d => d.votes > 0)
            .OrderByDescending(d => d.votes)
            .Select(d => ToRating(d.id, d.votes, d.field))
            .ToArray();

        var evidence = fuzzyRequest == null
            ? null
            : MatchEvidence.Build(fuzzyRequest.Title, fuzzyRequest.Artist, fuzzyRequest.DurationSeconds, title, artist, length);

        // The index stores tempo as beats per minute, so it passes through unconverted
        return new Candidate(
            songId, SongUrlBase + songId, title, artist, length, AsDouble(Field(doc, IndexFields.Tempo)), meter,
            ratings, evidence);
    }

    // dance_{ID}/Tempo already falls back to the song's tempo at index time
    private DanceRating ToRating(string danceId, int votes, SearchDocument field) =>
        new(
            danceId, _dances.DanceFromId(danceId)?.Name ?? danceId, votes,
            AsDouble(Field(field, IndexFields.DanceTempo)), [.. Strings(Field(field, IndexFields.StyleTags))]);

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

    private static object Field(SearchDocument doc, string name) => doc.TryGetValue(name, out var value) ? value : null;

    private static int? Votes(SearchDocument doc, string danceId) =>
        Field(doc, IndexFields.Dance(danceId)) is SearchDocument field ? AsInt(Field(field, IndexFields.Votes)) : null;

    private static IEnumerable<string> Strings(object value) =>
        value is IEnumerable<object> items ? items.Select(i => i?.ToString()).Where(i => i != null) : [];

    private static int? AsInt(object value) =>
        value == null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);

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
}
