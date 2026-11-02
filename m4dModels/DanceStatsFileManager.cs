using System.Text;

namespace m4dModels;

public interface IDanceStatsFileManager
{
    Task<string> GetDances();
    Task<string> GetGroups();
    Task<string> GetStats();
    Task WriteStats(string stats);

}

/// <summary>
/// Reads the packaged dance files from <paramref name="appRoot"/>/content and keeps the runtime
/// cache in <paramref name="dataRoot"/>/AppData. The two differ when the package is mounted
/// read-only and runtime files live on the /home volume (M4D_DATA_ROOT); dataRoot defaults to
/// appRoot.
/// </summary>
public class DanceStatsFileManager(string appRoot, string fileName = "dance-environment", string dataRoot = null)
    : IDanceStatsFileManager
{
    private string AppRoot { get; } = appRoot;
    private string AppData => Path.Combine(dataRoot ?? AppRoot, "AppData");
    private string Content => Path.Combine(AppRoot, "content");
    private readonly string FileName = fileName;

    public Task<string> GetDances()
    {
        return File.ReadAllTextAsync(Path.Combine(Content, "dances.json"));
    }

    public Task<string> GetGroups()
    {
        return File.ReadAllTextAsync(Path.Combine(Content, "dancegroups.json"));
    }

    public async Task<string> GetStats()
    {
        // Try runtime cache first (AppData/dance-environment.json)
        var path = Path.Combine(AppData, $"{FileName}.json");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path);
        }

        // Fall back to static cache in source control (content/dance-environment-fallback.json)
        var fallbackPath = Path.Combine(Content, $"{FileName}-fallback.json");
        if (File.Exists(fallbackPath))
        {
            Console.WriteLine($"Runtime cache not found, loading from static fallback: {fallbackPath}");
            return await File.ReadAllTextAsync(fallbackPath);
        }

        return await Task.FromResult<string>(null);
    }

    public Task WriteStats(string stats)
    {
        var path = Path.Combine(AppData, $"{FileName}.json");
        _ = Directory.CreateDirectory(AppData);
        return File.WriteAllTextAsync(path, stats, Encoding.UTF8);
    }
}
