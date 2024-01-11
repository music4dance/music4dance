using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DanceLibrary.Tests
{
    public class DanceTestBase
    {
        protected Dances Dances;

        protected async Task InitializeDances()
        {
            Dances = Dances.Load(
                await ReadResourceFile("test-dances.json"),
                await ReadResourceFile("test-groups.json"));
        }

        private static async Task<string> ReadResourceFile(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(name));

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
