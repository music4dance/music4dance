using m4d.ViewModels;

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
}
