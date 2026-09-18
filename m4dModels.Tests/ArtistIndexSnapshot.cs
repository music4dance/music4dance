using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Manual-only regeneration of the artist index snapshot that ships in source control
/// (m4d/ClientApp/src/assets/content/artist-index-fallback.json, copied to wwwroot/content by the
/// m4d build). A freshly deployed instance reads it when it has no runtime snapshot yet, so the
/// artist pages work on the first request after a deploy and keep working if the search service
/// is unreachable. Skipped unless M4D_ARTIST_ANALYSIS_INDEX points at an index backup.
/// Worth re-running after a backfill that materially changes the artist list.
/// See architecture/individual-artists.md §9.6.
/// </summary>
[TestClass]
public class ArtistIndexSnapshot
{
    internal static string FallbackPath => Path.Combine(RepositoryRoot(),
        "m4d", "ClientApp", "src", "assets", "content", "artist-index-fallback.json");

    [TestMethod]
    [TestCategory("Manual")]
    public void WriteFallbackSnapshot()
    {
        var path = ArtistAnalysisCorpus.IndexPath;
        if (path == null)
        {
            Assert.Inconclusive(
                $"Set {ArtistAnalysisCorpus.IndexVariable} to an index backup file to regenerate the snapshot.");
            return;
        }

        var corpus = ArtistAnalysisCorpus.Load(path);
        var knowledge = ArtistAnalysisCorpus.Knowledge(corpus, 1);

        // Dated from the backup rather than from now: the snapshot is as old as the data in it,
        // and the cache decides whether to rebuild from that date. Stamping it "now" would buy a
        // deploy six hours of serving a months-old artist list before it refreshed.
        var built = File.GetLastWriteTimeUtc(path);

        var index = ArtistIndex.Build(
            corpus.Select(s => (IReadOnlyList<string>)
                [.. ArtistSplitter.Split(s.Artist, s.Title, knowledge).Artists]),
            built);

        var destination = FallbackPath;

        File.WriteAllText(destination, index.SaveToJson());

        Console.WriteLine(
            $"Wrote {index.Count:N0} artists ({new FileInfo(destination).Length / 1024.0 / 1024.0:F1} MB), " +
            $"built {built:u}, to {destination}" + Environment.NewLine +
            "Run 'yarn format' in m4d/ClientApp before committing - this writes compact JSON and " +
            "prettier owns the checked-in formatting.");
        Assert.IsTrue(index.Count > 0);
    }

    // Found by walking up rather than counted in "..", which depends on whether the build went to
    // bin/ or to the BaseOutputPath redirect used while a dev server holds the normal output.
    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "music4dance.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory);
    }
}
