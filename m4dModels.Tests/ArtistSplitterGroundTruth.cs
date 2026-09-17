using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Manual-only accuracy sample for ArtistSplitter, measured against Spotify's structured
/// <c>track.artists[]</c> lists (architecture/individual-artists.md §4.2). Needs the network and
/// dev Spotify credentials, so it is skipped unless both an index backup and credentials are
/// configured:
/// <list type="bullet">
/// <item><c>M4D_ARTIST_ANALYSIS_INDEX</c> — an /Admin/IndexBackup file.</item>
/// <item><c>Authentication:Spotify:ClientId</c> / <c>:ClientSecret</c> — from m4d's user secrets
/// (read directly, so `dotnet user-secrets --project m4d` is the one place to set them), or
/// <c>M4D_SPOTIFY_CLIENT_ID</c> / <c>M4D_SPOTIFY_CLIENT_SECRET</c> in the environment.</item>
/// </list>
/// Samples distinct credits per rule rather than songs so popular credits can't dominate, and
/// draws a second unbiased sample for recall, since the rule buckets over-represent exactly the
/// credits recall is asking about. Spotify is a strong signal, not an oracle, so the report
/// separates genuine splitting mistakes from the cases where it simply names an act at a different
/// length or knows a collaborator our `Artist` string never mentioned.
/// </summary>
[TestClass]
public class ArtistSplitterGroundTruth
{
    private const string SampleVariable = "M4D_ARTIST_TRUTH_SAMPLE";
    private const string OverallVariable = "M4D_ARTIST_TRUTH_OVERALL";
    private const string SeedVariable = "M4D_ARTIST_TRUTH_SEED";
    private const string OutputVariable = "M4D_ARTIST_ANALYSIS_OUT";
    private const string MinimumSongsVariable = "M4D_ARTIST_ANALYSIS_MIN_SONGS";

    /// <summary>m4d's UserSecretsId: the same dev credentials the site itself runs on.</summary>
    private const string SecretsId = "60050f39-d7c1-4b33-8b65-1e6cbb538661";

    private const string NoSplitBucket = "(not split)";
    private const string UnresolvedBucket = "(unresolved)";

    /// <summary>One sampled credit: what we produced and which bucket it was drawn for.</summary>
    private record Candidate(
        string Credit, string Title, string TrackId, IReadOnlyList<string> Artists,
        IReadOnlyList<string> Rules, bool Split, bool Unresolved, int Songs);

    /// <summary>What Spotify says about a sampled credit's track.</summary>
    private record Truth(string TrackName, List<string> Artists);

    [TestMethod]
    [TestCategory("Manual")]
    public async Task CompareToSpotify()
    {
        var path = ArtistAnalysisCorpus.IndexPath;
        if (path == null)
        {
            Assert.Inconclusive(
                $"Set {ArtistAnalysisCorpus.IndexVariable} to an index backup file to run this sample.");
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(SecretsId)
            .Build();
        var clientId = configuration["Authentication:Spotify:ClientId"] ??
            Environment.GetEnvironmentVariable("M4D_SPOTIFY_CLIENT_ID");
        var clientSecret = configuration["Authentication:Spotify:ClientSecret"] ??
            Environment.GetEnvironmentVariable("M4D_SPOTIFY_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            Assert.Inconclusive(
                "Set Authentication:Spotify:ClientId and :ClientSecret in user secrets, " +
                "or M4D_SPOTIFY_CLIENT_ID and M4D_SPOTIFY_CLIENT_SECRET in the environment.");
            return;
        }

        var output = Environment.GetEnvironmentVariable(OutputVariable);
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.Combine(Path.GetDirectoryName(path)!, "artist-analysis");
        }
        _ = Directory.CreateDirectory(output);

        var perBucket = int.TryParse(Environment.GetEnvironmentVariable(SampleVariable), out var n)
            ? n
            : 150;
        var seed = int.TryParse(Environment.GetEnvironmentVariable(SeedVariable), out var s)
            ? s
            : 20260917;
        var minimumSongs = int.TryParse(
            Environment.GetEnvironmentVariable(MinimumSongsVariable), out var m) ? m : 1;

        var overallSize = int.TryParse(Environment.GetEnvironmentVariable(OverallVariable), out var o)
            ? o
            : 400;

        var songs = ArtistAnalysisCorpus.Load(path);
        var knowledge = ArtistAnalysisCorpus.Knowledge(songs, minimumSongs);
        var candidates = Candidates(songs, knowledge);
        var buckets = Buckets(candidates);
        var sample = Sample(buckets, perBucket, seed);

        // Recall needs a sample that isn't drawn per rule: the rule buckets over-represent the
        // credits we split, which is exactly the thing recall is trying to measure
        var shuffle = new Random(seed + 1);
        var overall = candidates.OrderBy(_ => shuffle.Next()).Take(overallSize).ToList();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await GetToken(client, clientId, clientSecret));
        var truth = await FetchTracks(
            client, sample.Concat(overall).Select(c => c.TrackId).Distinct().ToList());

        WriteReports(output, buckets, sample, overall, truth, perBucket, overallSize, seed);
    }

    /// <summary>
    /// Every distinct credit that carries a Spotify track id, with the splitter's verdict on it.
    /// </summary>
    private static List<Candidate> Candidates(List<CorpusSong> songs, ArtistKnowledge knowledge)
    {
        var candidates = new List<Candidate>();

        var credits = songs
            .Where(song => !string.IsNullOrWhiteSpace(song.Artist))
            .GroupBy(song => ArtistSplitter.CleanName(song.Artist), StringComparer.Ordinal);

        foreach (var credit in credits)
        {
            // Any song of this credit will do, as long as it has something to look up
            var song = credit.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.SpotifyTrackId));
            if (song == null)
            {
                continue;
            }

            var split = ArtistSplitter.Split(song.Artist, song.Title, knowledge);
            candidates.Add(new Candidate(
                credit.Key, song.Title, song.SpotifyTrackId, split.Artists, split.Rules,
                split.IsSplit(song.Artist), split.Unresolved.Count > 0, credit.Count()));
        }

        return candidates;
    }

    /// <summary>
    /// One bucket per rule, plus buckets for unresolved and untouched credits. A credit appears in
    /// every rule bucket it matched, so the buckets overlap; that is fine for per-rule precision.
    /// </summary>
    private static Dictionary<string, List<Candidate>> Buckets(List<Candidate> candidates)
    {
        var buckets = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            foreach (var bucket in BucketsOf(candidate))
            {
                if (!buckets.TryGetValue(bucket, out var list))
                {
                    buckets[bucket] = list = [];
                }
                list.Add(candidate);
            }
        }

        return buckets;
    }

    private static IEnumerable<string> BucketsOf(Candidate candidate)
    {
        if (candidate.Split)
        {
            foreach (var rule in candidate.Rules.Distinct())
            {
                yield return rule;
            }
        }
        else
        {
            yield return candidate.Unresolved ? UnresolvedBucket : NoSplitBucket;
        }
    }

    /// <summary>
    /// Up to <paramref name="perBucket"/> credits from each bucket, drawn with a fixed seed so a
    /// re-run of the same splitter version reproduces the same sample.
    /// </summary>
    private static List<Candidate> Sample(
        Dictionary<string, List<Candidate>> buckets, int perBucket, int seed)
    {
        var random = new Random(seed);
        return [.. buckets
            .OrderBy(b => b.Key, StringComparer.Ordinal)
            .SelectMany(b => b.Value.OrderBy(_ => random.Next()).Take(perBucket))
            .DistinctBy(c => c.TrackId)];
    }

    private static async Task<string> GetToken(HttpClient client, string id, string secret)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new StringContent(
                "grant_type=client_credentials", Encoding.UTF8,
                "application/x-www-form-urlencoded"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}")));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(response.IsSuccessStatusCode, $"Spotify token request failed: {response.StatusCode}");

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString();
    }

    /// <summary>
    /// GET /v1/tracks in batches of 50. Missing or unavailable tracks are simply absent from the
    /// result.
    /// </summary>
    private static async Task<Dictionary<string, Truth>> FetchTracks(
        HttpClient client, List<string> trackIds)
    {
        var truth = new Dictionary<string, Truth>(StringComparer.Ordinal);

        for (var i = 0; i < trackIds.Count; i += 50)
        {
            var batch = trackIds.Skip(i).Take(50).ToList();
            var body = await Get(client, $"https://api.spotify.com/v1/tracks?ids={string.Join(',', batch)}");
            if (body == null)
            {
                continue;
            }

            using var json = JsonDocument.Parse(body);
            var tracks = json.RootElement.GetProperty("tracks");
            for (var t = 0; t < tracks.GetArrayLength(); t++)
            {
                var track = tracks[t];
                if (track.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                var artists = track.GetProperty("artists").EnumerateArray()
                    .Select(a => a.GetProperty("name").GetString())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList();
                truth[batch[t]] = new Truth(track.GetProperty("name").GetString(), artists);
            }
        }

        return truth;
    }

    private static async Task<string> Get(HttpClient client, string url)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                Assert.Fail($"Spotify request failed: {response.StatusCode} for {url}");
            }

            var retry = response.Headers.RetryAfter?.Delta ??
                TimeSpan.FromSeconds(response.Headers.RetryAfter?.Date == null ? 5 : 30);
            await Task.Delay(retry + TimeSpan.FromSeconds(1));
        }

        return null;
    }

    /// <summary>
    /// Deliberately looser than <see cref="ArtistSplitter.ArtistKey"/>, which is what the index
    /// matches on: Spotify spells the same act with different punctuation ("Earth, Wind &amp; Fire"),
    /// a different conjunction ("Kenny Vance and the Planotones") and with or without a leading
    /// article ("Glenn Miller Orchestra"). Those are not splitting mistakes, so they should not
    /// count as disagreement here. Name variants are §11.4's problem, not the splitter's.
    /// </summary>
    private static string CompareKey(string name) =>
        string.Join(' ', ArtistSplitter.ArtistKey(name)
            .Split(PunctuationAndSpace, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !IgnoredInComparison.Contains(w)));

    private static readonly char[] PunctuationAndSpace =
        [.. " ,.&+'\"-()[]/\\;:!?"];

    private static readonly HashSet<string> IgnoredInComparison = new(StringComparer.Ordinal)
    {
        "the", "and", "y", "und", "et", "e", "la", "el", "los", "las", "les", "der", "die", "das",
    };

    /// <summary>
    /// How our split compares to Spotify's artist list. Two names count as the same act when one is
    /// a whole run of words inside the other, which is how Spotify's "Duke Ellington &amp; His
    /// Orchestra" lines up with our "Duke Ellington" and how "Pistol Annies" lines up with our
    /// "Pistol Annies &amp; Friends".
    /// </summary>
    private static string Verdict(Candidate candidate, Truth truth)
    {
        var ours = candidate.Artists.Select(CompareKey).ToHashSet(StringComparer.Ordinal);
        var theirs = truth.Artists.Select(CompareKey).ToHashSet(StringComparer.Ordinal);

        if (ours.SetEquals(theirs))
        {
            return "match";
        }

        var unmatchedOurs = ours.Where(o => !theirs.Any(t => SameAct(o, t))).ToList();
        var unmatchedTheirs = theirs.Where(t => !ours.Any(o => SameAct(o, t))).ToList();

        if (unmatchedOurs.Count == 0 && unmatchedTheirs.Count == 0)
        {
            return "contained";
        }

        if (unmatchedOurs.Count > 0 && unmatchedTheirs.Count > 0)
        {
            return "differ";
        }

        if (unmatchedOurs.Count > 0)
        {
            return "extra";
        }

        // A collaborator our Artist string never mentions is a limit of the data, not a splitting
        // mistake: only Spotify capture (§9.3) can find those. One we did mention and failed to
        // pull out is a real miss.
        var credit = CompareKey(candidate.Credit + " " + candidate.Title);
        return unmatchedTheirs.Any(t => ContainsName(credit, t)) ? "missed" : "incomplete";
    }

    private static bool SameAct(string ours, string theirs) =>
        ContainsName(theirs, ours) || ContainsName(ours, theirs);

    /// <summary>Whether <paramref name="name"/> appears as a whole run of words in the credit.</summary>
    private static bool ContainsName(string credit, string name)
    {
        var index = credit.IndexOf(name, StringComparison.Ordinal);
        return index >= 0 &&
            (index == 0 || credit[index - 1] == ' ') &&
            (index + name.Length == credit.Length || credit[index + name.Length] == ' ');
    }

    private static string Tsv(params object[] values) =>
        string.Join('\t', values.Select(v => (v?.ToString() ?? "").Replace('\t', ' ')));

    private static void WriteReports(
        string output, Dictionary<string, List<Candidate>> buckets, List<Candidate> sample,
        List<Candidate> overall, Dictionary<string, Truth> truth, int perBucket, int overallSize,
        int seed)
    {
        var sampled = sample.Select(c => c.TrackId).ToHashSet(StringComparer.Ordinal);

        var sb = new StringBuilder();
        _ = sb.AppendLine($"# Spotify Accuracy Sample (splitter v{ArtistSplitter.Version})");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        _ = sb.AppendLine($"- Precision sample: up to {perBucket:N0} distinct credits per rule, seed {seed}");
        _ = sb.AppendLine($"- Recall sample: {overallSize:N0} credits drawn across all credits");
        _ = sb.AppendLine($"- Credits sampled: {sample.Concat(overall).DistinctBy(c => c.TrackId).Count():N0}");
        _ = sb.AppendLine($"- Tracks Spotify returned: {truth.Count:N0}");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "Spotify is a strong signal, not an oracle, and most disagreement is not a splitting " +
            "mistake. Verdicts:");
        _ = sb.AppendLine();
        _ = sb.AppendLine("| Verdict | Meaning |");
        _ = sb.AppendLine("| ------- | ------- |");
        _ = sb.AppendLine("| `match` | Same names. |");
        _ = sb.AppendLine(
            "| `contained` | Same acts, named at different lengths: our \"Duke Ellington\" against " +
            "their \"Duke Ellington & His Orchestra\", or our \"Pistol Annies & Friends\" against " +
            "their \"Pistol Annies\". Counts as agreement. |");
        _ = sb.AppendLine(
            "| `incomplete` | Spotify names a collaborator our `Artist` string never mentions. Only " +
            "Spotify capture (§9.3) can reach these, so they are left out of precision. |");
        _ = sb.AppendLine(
            "| `missed` | The collaborator **was** in our credit or title and we failed to pull it " +
            "out. A real miss. |");
        _ = sb.AppendLine(
            "| `extra` | We list a name Spotify does not. Usually Spotify crediting only the lead " +
            "artist on a `feat.` track, where our split is the better answer. |");
        _ = sb.AppendLine("| `differ` | Names disagree both ways. Read these first. |");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "Names are compared loosely (punctuation, conjunctions and leading articles folded), so " +
            "\"Earth, Wind & Fire\" and \"Earth Wind And Fire\" agree. Reconciling those spellings is " +
            "a separate job (§11.4); counting them here would hide real splitting mistakes.");
        _ = sb.AppendLine();
        _ = sb.AppendLine("## Precision by rule");
        _ = sb.AppendLine();
        _ = sb.AppendLine(
            "| Rule | Credits | Judged | Match | Contained | Extra | Missed | Differ | Agree % | " +
            "Agree % (song-weighted) | Incomplete credits |");
        _ = sb.AppendLine(
            "| ---- | ------- | ------ | ----- | --------- | ----- | ------ | ------ | ------- | " +
            "---------------------- | ------------------ |");

        foreach (var bucket in buckets.OrderByDescending(b => b.Value.Sum(c => c.Songs)))
        {
            var checked_ = bucket.Value
                .Where(c => sampled.Contains(c.TrackId) && truth.ContainsKey(c.TrackId))
                .Select(c => (candidate: c, verdict: Verdict(c, truth[c.TrackId])))
                .ToList();
            if (checked_.Count == 0)
            {
                continue;
            }

            // A credit whose Artist string was simply incomplete says nothing about the rule
            var judged = checked_.Where(x => x.verdict != "incomplete").ToList();
            if (judged.Count == 0)
            {
                continue;
            }

            var counts = judged.GroupBy(x => x.verdict)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            int Count(string key) => counts.GetValueOrDefault(key);
            var agree = judged.Where(x => x.verdict is "match" or "contained").ToList();

            _ = sb.AppendLine(Row(
                $"`{bucket.Key}`", $"{bucket.Value.Count:N0}", $"{judged.Count:N0}",
                $"{Count("match")}", $"{Count("contained")}", $"{Count("extra")}",
                $"{Count("missed")}", $"{Count("differ")}",
                $"{100.0 * agree.Count / judged.Count:F1}%",
                $"{100.0 * agree.Sum(x => x.candidate.Songs) / judged.Sum(x => x.candidate.Songs):F1}%",
                $"{checked_.Count - judged.Count:N0}"));
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine("## Recall (from the unbiased sample)");
        _ = sb.AppendLine();
        var checkedOverall = overall.Where(c => truth.ContainsKey(c.TrackId)).ToList();
        var multi = checkedOverall.Where(c => truth[c.TrackId].Artists.Count > 1).ToList();
        if (multi.Count == 0)
        {
            _ = sb.AppendLine("No credit in the recall sample had 2+ Spotify artists.");
        }
        else
        {
            var caught = multi.Where(c => c.Split).ToList();
            _ = sb.AppendLine($"- Credits checked: {checkedOverall.Count:N0}");
            _ = sb.AppendLine(
                $"- Spotify lists 2+ artists: {multi.Count:N0} " +
                $"({100.0 * multi.Count / checkedOverall.Count:F1}% of credits, " +
                $"{100.0 * multi.Sum(c => c.Songs) / checkedOverall.Sum(c => c.Songs):F1}% of songs)");
            _ = sb.AppendLine(
                $"- **Split by the heuristic: {caught.Count:N0} ({100.0 * caught.Count / multi.Count:F1}% of credits, " +
                $"{100.0 * caught.Sum(c => c.Songs) / multi.Sum(c => c.Songs):F1}% of songs)**");
            _ = sb.AppendLine(
                $"- Left whole with a separator we saw: {multi.Count(c => !c.Split && c.Unresolved):N0}");
            _ = sb.AppendLine(
                $"- Left whole with no separator at all: {multi.Count(c => !c.Split && !c.Unresolved):N0}");
            _ = sb.AppendLine();
            _ = sb.AppendLine(
                "The last line is the ceiling on a separator-based heuristic: Spotify knows about a " +
                "collaborator our `Artist` string never mentions.");
        }

        File.WriteAllText(Path.Combine(output, "spotify-accuracy.md"), sb.ToString());

        var rows = sample.Concat(overall)
            .DistinctBy(c => c.TrackId)
            .Where(c => truth.ContainsKey(c.TrackId))
            .Select(c => (candidate: c, verdict: Verdict(c, truth[c.TrackId])))
            .Where(x => x.verdict != "match")
            .OrderBy(x => x.verdict, StringComparer.Ordinal)
            .ThenByDescending(x => x.candidate.Songs)
            .Select(x => Tsv(
                x.verdict, x.candidate.Songs, x.candidate.Credit,
                string.Join(" | ", x.candidate.Artists),
                string.Join(" | ", truth[x.candidate.TrackId].Artists),
                string.Join(",", x.candidate.Unresolved ? [.. x.candidate.Rules, "unresolved"] : x.candidate.Rules),
                x.candidate.Title, truth[x.candidate.TrackId].TrackName, x.candidate.TrackId));

        File.WriteAllLines(Path.Combine(output, "spotify-mismatches.tsv"),
            [Tsv("verdict", "songs", "credit", "ours", "spotify", "rules", "our title",
                "spotify title", "track"), .. rows]);
    }

    private static string Row(params string[] cells) => $"| {string.Join(" | ", cells)} |";
}
