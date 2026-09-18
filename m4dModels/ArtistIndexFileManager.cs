using System.Text;

namespace m4dModels;

public interface IArtistIndexFileManager
{
    Task<string> Read();
    Task Write(string json);
    void Delete();
}

/// <summary>
/// Persists the <see cref="ArtistIndex"/> snapshot beside dance-environment.json, so a restarted
/// instance can answer the first visitor from the last good snapshot instead of making them wait
/// for a fresh streaming pass - and can keep answering when the search service is unreachable.
/// Mirrors <see cref="DanceStatsFileManager"/>: a runtime cache under AppData, falling back to a
/// snapshot in source control under content. See architecture/individual-artists.md §9.6.
/// </summary>
public class ArtistIndexFileManager(string appRoot, string fileName = "artist-index")
    : IArtistIndexFileManager
{
    private string AppData => Path.Combine(appRoot, "AppData");
    private string Content => Path.Combine(appRoot, "content");
    private string RuntimePath => Path.Combine(AppData, $"{fileName}.json");
    private string FallbackPath => Path.Combine(Content, $"{fileName}-fallback.json");

    public async Task<string> Read()
    {
        if (File.Exists(RuntimePath))
        {
            return await File.ReadAllTextAsync(RuntimePath);
        }

        return File.Exists(FallbackPath) ? await File.ReadAllTextAsync(FallbackPath) : null;
    }

    public async Task Write(string json)
    {
        _ = Directory.CreateDirectory(AppData);

        // Written aside and moved into place: on a multi-instance app this file lives on a shared
        // Azure Files volume, so two instances can finish builds at once. A move replaces it in
        // one step, where a direct write can be read half-finished by the instance next to it.
        var temporary = $"{RuntimePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json, Encoding.UTF8);
            File.Move(temporary, RuntimePath, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>
    /// Drops the runtime snapshot, leaving the source-control fallback alone. Used when a backfill
    /// makes the persisted snapshot wrong rather than merely old - an instance restarting after
    /// that shouldn't read pre-backfill artists back out of the shared volume.
    /// </summary>
    public void Delete() => TryDelete(RuntimePath);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover temp file is harmless; the original failure is the one worth reporting.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
