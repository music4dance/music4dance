namespace m4dModels.Sandbox;

/// <summary>
/// In-memory-friendly IDanceStatsFileManager backed by the small, already-public, PII-cleaned
/// dataset embedded in this assembly (TestData/), for tests and the no-external-service sandbox
/// host. Mirrors the production DanceStatsFileManager's "fall back to what's in source control"
/// shape, just reading from embedded resources instead of the filesystem.
/// </summary>
public class LocalDanceStatsFileManager : IDanceStatsFileManager
{
    public Task<string> GetDances()
    {
        return SandboxServiceFactory.ReadResourceFile("test-dances.json");
    }

    public Task<string> GetGroups()
    {
        return SandboxServiceFactory.ReadResourceFile("test-groups.json");
    }

    public async Task<string> GetStats()
    {
        // Load the real stats file (which has proper tag groups and dance stats)
        // but clear the cachedSongs array to start with empty cache for each run
        var statsJson = await SandboxServiceFactory.ReadResourceFile("dancestatistics.txt");

        if (string.IsNullOrEmpty(statsJson))
        {
            // Fallback to minimal valid structure if file not found
            return @"{
                ""dances"": [],
                ""groups"": [],
                ""cachedSongs"": [],
                ""tagGroups"": []
            }";
        }

        // Parse, clear cachedSongs, and re-serialize
        var stats = Newtonsoft.Json.JsonConvert.DeserializeObject<DanceStatsInstance>(statsJson);

        // Return the stats with empty song cache (ensures a fresh start each run)
        // but preserve tag groups and dance stats for proper tag processing
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            dances = stats.Dances,
            groups = stats.Groups,
            cachedSongs = new string[0], // Empty song cache for a clean starting state
            tagGroups = stats.TagGroups
        });
    }

    public Task WriteStats(string stats)
    {
        // No-op - nothing to persist to in-memory/sandbox usage
        return Task.CompletedTask;
    }
}
