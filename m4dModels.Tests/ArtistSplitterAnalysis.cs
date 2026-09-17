using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Manual-only analysis of ArtistSplitter against a production index backup
/// (/Admin/IndexBackup -> local/index-YYYY-MM-DD.txt). Skipped unless
/// M4D_ARTIST_ANALYSIS_INDEX points at a backup file. Writes TSV/Markdown reports to
/// M4D_ARTIST_ANALYSIS_OUT (default: an "artist-analysis" folder next to the backup).
/// See architecture/artist-index-plan.md §6.
/// </summary>
[TestClass]
public class ArtistSplitterAnalysis
{
    private const string IndexVariable = "M4D_ARTIST_ANALYSIS_INDEX";
    private const string OutputVariable = "M4D_ARTIST_ANALYSIS_OUT";
    private const string MinimumSongsVariable = "M4D_ARTIST_ANALYSIS_MIN_SONGS";

    private record SongCredit(string Title, string Artist);

    [TestMethod]
    [TestCategory("Manual")]
    public void AnalyzeIndexBackup()
    {
        var path = Environment.GetEnvironmentVariable(IndexVariable);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Assert.Inconclusive($"Set {IndexVariable} to an index backup file to run this analysis.");
            return;
        }

        var output = Environment.GetEnvironmentVariable(OutputVariable);
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.Combine(Path.GetDirectoryName(path)!, "artist-analysis");
        }
        _ = Directory.CreateDirectory(output);

        var minimumSongs = int.TryParse(
            Environment.GetEnvironmentVariable(MinimumSongsVariable), out var m) ? m : 1;

        var songs = File.ReadLines(path).Select(ParseLine).Where(s => s != null).ToList();

        var knowledge = BuildKnowledge(songs, minimumSongs);
        var results = songs
            .Select(s => (song: s, split: ArtistSplitter.Split(s.Artist, s.Title, knowledge)))
            .ToList();

        WriteSummary(output, songs, results, minimumSongs);
        WriteSplits(output, results);
        WriteUnresolved(output, results);
        WriteArtists(output, results);
        WriteCaseVariants(output, results);
    }

    private static ArtistKnowledge BuildKnowledge(List<SongCredit> songs, int minimumSongs) =>
        ArtistKnowledge.FromCredits(songs.Select(s => (s.Title, s.Artist)), minimumSongs);

    /// <summary>
    /// Effective Title/Artist from one backup line, applying the scalar rule that a pseudo-user
    /// write never overrides a real user's.
    /// </summary>
    private static SongCredit ParseLine(string line)
    {
        var ich = Song.TryParseId(line, out _);
        var properties = SongProperty.Load(ich > 0 ? line[ich..] : line);

        string title = null;
        string artist = null;
        var userTitle = false;
        var userArtist = false;
        var pseudo = false;

        foreach (var prop in properties)
        {
            switch (prop.BaseName)
            {
                case Song.UserField:
                case Song.UserProxy:
                    pseudo = prop.Value?.EndsWith("|P", StringComparison.OrdinalIgnoreCase) ?? false;
                    break;
                case Song.TitleField:
                    if (!pseudo || !userTitle)
                    {
                        title = prop.Value;
                        userTitle |= !pseudo;
                    }
                    break;
                case Song.ArtistField:
                    if (!pseudo || !userArtist)
                    {
                        artist = prop.Value;
                        userArtist |= !pseudo;
                    }
                    break;
                case Song.DeleteCommand:
                    if (string.IsNullOrEmpty(prop.Value) ||
                        string.Equals(prop.Value, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        title = null;
                    }
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(title) ? null : new SongCredit(title, artist);
    }

    private static string Tsv(params object[] values) =>
        string.Join('\t', values.Select(v => (v?.ToString() ?? "").Replace('\t', ' ')));

    private static void WriteSummary(string output, List<SongCredit> songs,
        List<(SongCredit song, ArtistSplit split)> results, int minimumSongs)
    {
        var split = results.Where(r => r.split.IsSplit(r.song.Artist)).ToList();
        var sb = new StringBuilder();
        _ = sb.AppendLine($"# Artist Splitter Analysis (v{ArtistSplitter.Version})");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        _ = sb.AppendLine($"- Evidence threshold (minimum songs): {minimumSongs}");
        _ = sb.AppendLine($"- Songs: {songs.Count:N0}");
        _ = sb.AppendLine($"- Songs with no artist: {songs.Count(s => string.IsNullOrWhiteSpace(s.Artist)):N0}");
        _ = sb.AppendLine($"- Distinct credits: {songs.Select(s => ArtistSplitter.CleanName(s.Artist)).Distinct().Count():N0}");
        _ = sb.AppendLine($"- Distinct individual artists: {results.SelectMany(r => r.split.Artists).Distinct().Count():N0}");
        _ = sb.AppendLine($"- Distinct individual artists (case-insensitive): {results.SelectMany(r => r.split.Artists).Distinct(StringComparer.OrdinalIgnoreCase).Count():N0}");
        _ = sb.AppendLine($"- **Songs split: {split.Count:N0} ({100.0 * split.Count / songs.Count:F1}%)**");
        _ = sb.AppendLine($"- Distinct credits split: {split.Select(r => r.song.Artist).Distinct().Count():N0}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("## Split songs by rule (a song can match several rules)");
        _ = sb.AppendLine();
        _ = sb.AppendLine("| Rule | Songs |");
        _ = sb.AppendLine("| ---- | ----- |");
        foreach (var g in split.SelectMany(r => r.split.Rules).GroupBy(r => r).OrderByDescending(g => g.Count()))
        {
            _ = sb.AppendLine($"| {g.Key} | {g.Count():N0} |");
        }
        _ = sb.AppendLine();
        _ = sb.AppendLine("## Unresolved separators (seen but not split)");
        _ = sb.AppendLine();
        _ = sb.AppendLine("| Separator | Songs | Distinct credits |");
        _ = sb.AppendLine("| --------- | ----- | ---------------- |");
        foreach (var g in results.SelectMany(r => r.split.Unresolved.Select(u => (u, r.song.Artist)))
                     .GroupBy(x => x.u).OrderByDescending(g => g.Count()))
        {
            _ = sb.AppendLine($"| `{g.Key}` | {g.Count():N0} | {g.Select(x => x.Artist).Distinct().Count():N0} |");
        }
        _ = sb.AppendLine();
        _ = sb.AppendLine("## Songs by number of individual artists");
        _ = sb.AppendLine();
        _ = sb.AppendLine("| Artists | Songs |");
        _ = sb.AppendLine("| ------- | ----- |");
        foreach (var g in results.GroupBy(r => r.split.Artists.Count).OrderBy(g => g.Key))
        {
            _ = sb.AppendLine($"| {g.Key} | {g.Count():N0} |");
        }

        File.WriteAllText(Path.Combine(output, "summary.md"), sb.ToString());
    }

    private static void WriteSplits(string output, List<(SongCredit song, ArtistSplit split)> results)
    {
        var lines = results
            .Where(r => r.split.IsSplit(r.song.Artist))
            .GroupBy(r => (artist: ArtistSplitter.CleanName(r.song.Artist), result: string.Join(" | ", r.split.Artists)))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.artist, StringComparer.OrdinalIgnoreCase)
            .Select(g => Tsv(g.Count(), g.Key.artist, g.Key.result,
                string.Join(",", g.SelectMany(r => r.split.Rules).Distinct()), g.First().song.Title));

        File.WriteAllLines(Path.Combine(output, "splits.tsv"),
            [Tsv("songs", "artist", "result", "rules", "sample title"), .. lines]);
    }

    private static void WriteUnresolved(string output, List<(SongCredit song, ArtistSplit split)> results)
    {
        var lines = results
            .Where(r => r.split.Unresolved.Count > 0)
            .GroupBy(r => (artist: ArtistSplitter.CleanName(r.song.Artist), result: string.Join(" | ", r.split.Artists)))
            .OrderByDescending(g => g.Count())
            .Select(g => Tsv(g.Count(), g.Key.artist,
                string.Join(",", g.SelectMany(r => r.split.Unresolved).Distinct()), g.Key.result,
                g.First().song.Title));

        File.WriteAllLines(Path.Combine(output, "unresolved.tsv"),
            [Tsv("songs", "artist", "unresolved", "result", "sample title"), .. lines]);
    }

    private static void WriteArtists(string output, List<(SongCredit song, ArtistSplit split)> results)
    {
        var lines = results
            .SelectMany(r => r.split.Artists)
            .GroupBy(a => a)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => Tsv(g.Count(), g.Key));

        File.WriteAllLines(Path.Combine(output, "artists.tsv"), [Tsv("songs", "artist"), .. lines]);
    }

    private static void WriteCaseVariants(string output, List<(SongCredit song, ArtistSplit split)> results)
    {
        var lines = results
            .SelectMany(r => r.split.Artists)
            .GroupBy(a => a)
            .Select(g => (name: g.Key, count: g.Count()))
            .GroupBy(x => ArtistSplitter.ArtistKey(x.name))
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Sum(x => x.count))
            .Select(g => Tsv(g.Sum(x => x.count),
                string.Join(" | ", g.OrderByDescending(x => x.count).Select(x => $"{x.name} ({x.count})"))));

        File.WriteAllLines(Path.Combine(output, "case-variants.tsv"), [Tsv("songs", "variants"), .. lines]);
    }
}
