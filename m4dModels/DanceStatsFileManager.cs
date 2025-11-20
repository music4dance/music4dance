using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public interface IDanceStatsFileManager
    {
        Task<string> GetDances();
        Task<string> GetGroups();
        Task<string> GetStats();
        Task WriteStats(string stats);

    }

    public class DanceStatsFileManager(string appRoot, string fileName = "dance-environment") : IDanceStatsFileManager
    {
        private string AppRoot { get; } = appRoot;
        private string AppData => Path.Combine(AppRoot, "AppData");
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
            var path = Path.Combine(AppData, $"{FileName}.json");
            if (!File.Exists(path))
            {
                return await Task.FromResult<string>(null);
            }
            return await File.ReadAllTextAsync(path);
        }

        public Task WriteStats(string stats)
        {
            var path = Path.Combine(AppData, $"{FileName}.json");
            Directory.CreateDirectory(AppData);
            return File.WriteAllTextAsync(path, stats, Encoding.UTF8);
        }
    }
}
