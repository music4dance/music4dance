using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Manual-only memory measurement of the <see cref="ArtistIndex"/> snapshot against a production
/// index backup (/Admin/IndexBackup -> local/index-YYYY-MM-DD.txt). Skipped unless
/// M4D_ARTIST_ANALYSIS_INDEX points at a backup file. The site runs on a small App Service
/// instance, so the numbers that matter are the snapshot it retains, the peak while building it,
/// and total allocation (GC pressure). See architecture/individual-artists.md §9.5.
/// </summary>
[TestClass]
public class ArtistIndexMemory
{
    private record Measurements(
        int Songs, int Artists, long Source, long Snapshot, long Uncollected, long Churn,
        long Json, long Chars, long NameChars);

    [TestMethod]
    [TestCategory("Manual")]
    public void MeasureFromIndexBackup()
    {
        var path = ArtistAnalysisCorpus.IndexPath;
        if (path == null)
        {
            Assert.Inconclusive(
                $"Set {ArtistAnalysisCorpus.IndexVariable} to an index backup file to run this measurement.");
            return;
        }

        var baseline = Settled();
        var m = BuildAndMeasure(path, baseline);
        var released = Settled();

        // Everything the grouping left live or uncollected, above the stand-in stream the harness
        // is holding. The live build streams from the search service and holds no such list.
        var peak = m.Uncollected - baseline - m.Source;

        var sb = new StringBuilder();
        _ = sb.AppendLine($"Songs streamed:            {m.Songs,12:N0}");
        _ = sb.AppendLine($"Distinct artists:          {m.Artists,12:N0}");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"Retained snapshot:         {Mb(m.Snapshot),12} ({m.Snapshot / (double)m.Artists:F0} bytes/artist)");
        _ = sb.AppendLine($"  characters within it:    {Mb(m.Chars * 2),12} (mean name {m.NameChars / (double)m.Artists:F1} chars)");
        _ = sb.AppendLine($"Peak over idle:            {Mb(peak),12}");
        _ = sb.AppendLine($"  transient above that:    {Mb(peak - m.Snapshot),12}");
        _ = sb.AppendLine($"Allocated while grouping:  {Mb(m.Churn),12}");
        _ = sb.AppendLine($"Left behind after release: {Mb(released - baseline),12}");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"Draining the stream first,");
        _ = sb.AppendLine($"  as BuildAsync once did,");
        _ = sb.AppendLine($"  would add to the peak:   {Mb(m.Source),12}");
        _ = sb.AppendLine();
        _ = sb.AppendLine($"Serialized as JSON:        {Mb(m.Json),12}");

        Console.WriteLine(sb.ToString());
        Assert.IsTrue(m.Artists > 0);
    }

    /// <summary>
    /// Each size is taken as the difference across the step that allocates it, inside one frame.
    /// Measuring by releasing instead would read wrong under Debug, which keeps a local rooted to
    /// the end of its method.
    /// </summary>
    private static Measurements BuildAndMeasure(string path, long baseline)
    {
        // Stands in for what the live stream yields: one already-split artist list per song,
        // because the index stores the Artists field. Reconstructing that from a backup needs the
        // splitter and its evidence, and that work is done and released before anything is
        // measured, so it doesn't land in the numbers.
        var source = ReconstructStream(path, out var songs);
        var withSource = Settled();

        var allocBefore = GC.GetTotalAllocatedBytes(true);
        var index = ArtistIndex.BuildAsync(AsAsync(source)).GetAwaiter().GetResult();
        var uncollected = GC.GetTotalMemory(false);
        var churn = GC.GetTotalAllocatedBytes(true) - allocBefore;
        var withSnapshot = Settled();

        var json = ArtistIndexJson(index);
        var (chars, names) = Payload(index);

        GC.KeepAlive(source);
        return new Measurements(
            songs, index.Count, withSource - baseline, withSnapshot - withSource, uncollected,
            churn, json, chars, names);
    }

    private static async IAsyncEnumerable<IReadOnlyList<string>> AsAsync(
        IEnumerable<IReadOnlyList<string>> source)
    {
        foreach (var artists in source)
        {
            yield return artists;
        }
        await Task.CompletedTask;
    }

    private static List<IReadOnlyList<string>> ReconstructStream(string path, out int songs)
    {
        var corpus = ArtistAnalysisCorpus.Load(path);
        songs = corpus.Count;

        var knowledge = ArtistAnalysisCorpus.Knowledge(corpus, 1);
        var result = new List<IReadOnlyList<string>>(corpus.Count);
        foreach (var song in corpus)
        {
            var artists = ArtistSplitter.Split(song.Artist, song.Title, knowledge).Artists;
            if (artists.Count > 0)
            {
                result.Add([.. artists]);
            }
        }
        return result;
    }

    private static string Mb(long bytes) => $"{bytes / 1024.0 / 1024.0:F1} MB";

    // Character payload the snapshot actually needs, against which the measured retention is
    // mostly per-object overhead: three string headers plus an entry object per artist.
    private static (long Chars, long NameChars) Payload(ArtistIndex index)
    {
        long chars = 0;
        long names = 0;
        foreach (var entry in index.Top(int.MaxValue))
        {
            names += entry.Name.Length;
            chars += entry.Name.Length + ArtistSplitter.ArtistKey(entry.Name).Length +
                ArtistIndex.SortKey(entry.Name).Length;
        }
        return (chars, names);
    }

    private static long ArtistIndexJson(ArtistIndex index) =>
        Encoding.UTF8.GetByteCount(index.SaveToJson());

    private static long Settled()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(true);
    }
}
