using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json.Linq;

namespace m4dModels.Tests;

// Cases are shared with m4d/ClientApp/src/models/__tests__/SongAlbumTrim.test.ts, which checks
// that the client builds the same list-row values from the full and the trimmed log.
[TestClass]
public class SongHistoryTrimTests
{
    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }

    private static IEnumerable<JToken> Cases()
    {
        var path = FindRepoFile(Path.Combine(
            "m4d", "ClientApp", "src", "models", "__tests__", "album-trim-cases.json"));
        return JObject.Parse(File.ReadAllText(path))["cases"]!;
    }

    private static SongHistory History(IEnumerable<string> log)
    {
        return new SongHistory
        {
            Id = Guid.NewGuid(),
            Properties = [.. log.Select(entry =>
            {
                var eq = entry.IndexOf('=');
                return new SongPropertySparse { Name = entry[..eq], Value = entry[(eq + 1)..] };
            })],
        };
    }

    private static List<string> Log(SongHistory history)
    {
        return [.. history.Properties.Select(p => $"{p.Name}={p.Value}")];
    }

    [TestMethod]
    public void Trim_MatchesSharedCases()
    {
        var cases = Cases().ToList();
        Assert.IsNotEmpty(cases);

        foreach (var c in cases)
        {
            var name = c["name"]!.Value<string>();
            var history = History(c["log"]!.Values<string>());
            var trimmed = history.TrimAlbumsForList();

            if (c["trimmed"]!.Type == JTokenType.Null)
            {
                Assert.AreSame(history, trimmed, name);
                continue;
            }

            CollectionAssert.AreEqual(c["trimmed"]!.Values<string>().ToList(), Log(trimmed), name);
            Assert.AreEqual(history.Id, trimmed.Id, name);
        }
    }
}
