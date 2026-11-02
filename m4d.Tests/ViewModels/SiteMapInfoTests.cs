using m4d.ViewModels;

using Microsoft.Extensions.FileProviders;

namespace m4d.Tests.ViewModels;

[TestClass]
public class SiteMapInfoTests
{
    [TestMethod]
    public void GetCrawlerUrls_ListsSitePagesInOrderWithoutDuplicates()
    {
        SiteMapCategory[] categories =
        [
            new()
            {
                Name = "Music",
                Entries =
                [
                    new()
                    {
                        Title = "Song Library", Reference = "song",
                        Children =
                        [
                            new() { Title = "Advanced Search", Reference = "song/advancedsearchform" },
                            new() { Title = "Add Songs", Reference = "song/augment" },
                            new() { Title = "New Music", Reference = "song/newmusic" },
                            new() { Title = "Saved Searches", Reference = "searches/index" },
                        ],
                    },
                    new()
                    {
                        Title = "Ballroom", Reference = "dances/ballroom-competition-categories",
                        Children = [new() { Title = "Standard", Reference = "dances/international-standard" }],
                    },
                    new() { Title = "Holiday Music", Reference = "customsearch?name=holiday" },
                ],
            },
            new()
            {
                Name = "Info",
                Entries =
                [
                    new() { Title = "Home Page", Reference = "" },
                    new() { Title = "Song Library again", Reference = "song" },
                    new() { Title = "Blog", Reference = "blog" },
                    new() { Title = "Help", Reference = "blog/music4dance-help" },
                    new() { Title = "No link" },
                ],
            },
            new()
            {
                Name = "Account",
                Entries =
                [
                    new() { Title = "Profile", Reference = "identity/Account/Manage" },
                    new() { Title = "Log In", Reference = "identity/account/login" },
                ],
            },
        ];

        var urls = SiteMapInfo.GetCrawlerUrls(categories).ToList();

        CollectionAssert.AreEqual(
            new[]
            {
                "https://www.music4dance.net/song",
                "https://www.music4dance.net/dances/ballroom-competition-categories",
                "https://www.music4dance.net/dances/international-standard",
                "https://www.music4dance.net/customsearch?name=holiday",
                "https://www.music4dance.net/",
            },
            urls);
    }

    [TestMethod]
    public void ChoosePath_UsesThePackagedCopyWithoutAnOverride()
    {
        WithFolders((packaged, overrides) =>
        {
            var packagedPath = Write(packaged, "blogmap.txt", "Packaged", Then);

            Assert.AreEqual(packagedPath, SiteMapFile.ChoosePath(packagedPath, null));
            Assert.AreEqual(packagedPath, SiteMapFile.ChoosePath(packagedPath, Path.Combine(overrides, "blogmap.txt")));
        });
    }

    [TestMethod]
    public void ChoosePath_UsesAnOverrideUploadedSinceTheDeploy()
    {
        WithFolders((packaged, overrides) =>
        {
            var packagedPath = Write(packaged, "blogmap.txt", "Packaged", Then);
            var overridePath = Write(overrides, "blogmap.txt", "Uploaded", Then.AddHours(1));

            Assert.AreEqual(overridePath, SiteMapFile.ChoosePath(packagedPath, overridePath));
        });
    }

    [TestMethod]
    public void ChoosePath_UsesTheNewerPackagedCopyOverAStaleOverride()
    {
        // The deploy that ships the committed edit takes over without the upload being deleted
        WithFolders((packaged, overrides) =>
        {
            var packagedPath = Write(packaged, "blogmap.txt", "Packaged", Then.AddHours(1));
            var overridePath = Write(overrides, "blogmap.txt", "Uploaded", Then);

            Assert.AreEqual(packagedPath, SiteMapFile.ChoosePath(packagedPath, overridePath));
        });
    }

    [TestMethod]
    public void ChoosePath_UsesThePackagedCopyWhenTheOverrideIsTheSameFile()
    {
        // Defensive: an override folder pointed at the packaged one never counts as an override
        WithFolders((packaged, _) =>
        {
            var packagedPath = Write(packaged, "blogmap.txt", "Packaged", Then);

            Assert.AreEqual(packagedPath, SiteMapFile.ChoosePath(packagedPath, packagedPath));
        });
    }

    [TestMethod]
    public void SiteMapFile_ReadsTheNewerOverride()
    {
        WithFolders((contentRoot, overrides) =>
        {
            var packaged = Path.Combine(contentRoot, "wwwroot", "content");
            _ = Directory.CreateDirectory(packaged);
            _ = Write(packaged, "blogmap.txt", "Packaged Post	blog/packaged", Then);
            _ = Write(overrides, "blogmap.txt", "Uploaded Post	blog/uploaded", Then.AddHours(1));

            using var provider = new PhysicalFileProvider(contentRoot);
            var file = new SiteMapFile("blogmap", provider, overrides);

            Assert.AreEqual("Uploaded Post", file.Children.Single().Title);
            Assert.AreEqual("blog/uploaded", file.Children.Single().Reference);
        });
    }

    private static readonly DateTime Then = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static string Write(string folder, string name, string text, DateTime written)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, written);
        return path;
    }

    private static void WithFolders(Action<string, string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), $"m4d-sitemap-{Guid.NewGuid():N}");
        var packaged = Path.Combine(root, "packaged");
        var overrides = Path.Combine(root, "overrides");
        _ = Directory.CreateDirectory(packaged);
        _ = Directory.CreateDirectory(overrides);
        try
        {
            test(packaged, overrides);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
