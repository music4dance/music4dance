using System.Collections.Generic;
using System.IO;
using System.Linq;
using DanceLibrary;

namespace m4d.ViewModels
{
    public class SiteMapEntry
    {
        public virtual string Title { get; set; }
        public virtual string Reference { get; set; }
        public virtual string Description { get; set; }
        public virtual bool OneTime { get; set; }
        public virtual IEnumerable<SiteMapEntry> Children { get; set; }

        public string FullPath => MakeFullPath(Reference);

        protected string MakeFullPath(string rel)
        {
            return $"https://www.music4dance.net/{rel}";
        }
    }

    public sealed class SiteMapDance : SiteMapEntry
    {
        public SiteMapDance(DanceObject dance)
        {
            _dance = dance;
        }

        public override string Title => _dance.Name;
        public override string Reference => $"dances/{_dance.CleanName}";

        public string CatalogReference => $"song/search?dances={_dance.Id}";

        public string CatalogFullPath => MakeFullPath(CatalogReference);

        private readonly DanceObject _dance;
    }

    public sealed class SiteMapFile : SiteMapEntry
    {
        public SiteMapFile(string filename)
        {
            var path = Path.Combine(System.Web.Hosting.HostingEnvironment.MapPath("~/Content")??"",$"{filename}.txt");
            var lines = File.ReadAllLines(path);
            var children = new List<SiteMapEntry>();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length < 4) continue;

                children.Add(new SiteMapEntry {Title = parts[0],Reference=parts[1], Description = parts[2], OneTime = parts[3] == "OneTime"});
            }
            Children = children;
        }
    }

    public class SiteMapCategory
    {
        public virtual string Name { get; set; }
        public virtual string Type { get; set; }
        public virtual IEnumerable<SiteMapEntry> Entries { get; set; }
    }

    public class SiteMapDances : SiteMapCategory
    {
        public override string Name => "Dances";
        public override string Type => "music";
        public override IEnumerable<SiteMapEntry> Entries => Dances.Instance.AllDances.Where(d => !(d is DanceInstance)).OrderBy(d => d.Name).Select(d => new SiteMapDance(d));
    }

    public static class SiteMapInfo
    {
        static SiteMapInfo()
        {
            LoadCategories();
        }

        public static IEnumerable<SiteMapCategory> Categories { get; private set; }

        public static void LoadCategories()
        {
            Categories = new List<SiteMapCategory>
            {
                new SiteMapCategory
                {
                    Name = "Music",
                    Type = "music",
                    Entries = new List<SiteMapEntry>
                    {
                        new SiteMapEntry
                        {
                            Title =  "Song Library", Reference="song/azuresearch",
                            Children = new List<SiteMapEntry>
                            {
                                new SiteMapEntry {Title = "Advanced Search", Reference = "song/advancedsearchform?filter=azure+advanced"},
                                new SiteMapEntry {Title = "Saved Searches", Reference = "searches/index"},
                            }
                        },
                        new SiteMapEntry {Title =  "Dance Index", Reference="dances"},
                        new SiteMapEntry
                        {
                            Title =  "Ballroom Competition Categories", Reference="dances/ballroom-competition-categories",
                            Children = new List<SiteMapEntry>
                            {
                                new SiteMapEntry {Title = "International Standard", Reference = "dances/international-standard"},
                                new SiteMapEntry {Title = "International Latin", Reference = "dances/international-latin"},
                                new SiteMapEntry {Title = "American Smooth", Reference = "dances/american-smooth"},
                                new SiteMapEntry {Title = "American Rhythm", Reference = "dances/american-rhythm"},
                            }
                        },
                        new SiteMapEntry {Title =  "Wedding Music", Reference="dances/wedding-music"},
                    }
                },
                new SiteMapDances(),
                new SiteMapCategory
                {
                    Name = "Info",
                    Type = "info",
                    Entries = new List<SiteMapEntry>
                    {
                        new SiteMapEntry {Title =  "Home Page", Reference=""},
                        new SiteMapEntry {Title =  "About Us", Reference="home/about"},
                        new SiteMapEntry {Title =  "FAQ", Reference="home/faq"},
                        new SiteMapEntry {Title =  "Privacy Policy", Reference="home/privacypolicy"},
                        new SiteMapEntry {Title =  "Terms of Service", Reference="home/termsofservice"},
                        new SiteMapEntry {Title =  "Credits", Reference="home/credits"},
                        new SiteMapEntry {Title =  "Reading List", Reference="blog/reading-list"},
                        new SiteMapFile("blogmap") {Title =  "Blog", Reference="blog"},
                        new SiteMapEntry
                        {
                            Title =  "Help", Reference="blog/music4dance-help",
                            Children = new List<SiteMapEntry>
                            {
                                new SiteMapEntry {Title = "Song List", Reference = "blog/music4dance-help/song-list"},
                                new SiteMapEntry
                                {
                                    Title = "Song Details", Reference = "blog/music4dance-help/song-details",
                                    Children = new List<SiteMapEntry>
                                    {
                                        new SiteMapEntry {Title = "Advanced Search", Reference = "blog/music4dance-help/advanced-search"},
                                        new SiteMapEntry {Title = "Simple Search (BETA)", Reference = "blog/music4dance-help/simple-search"},
                                        new SiteMapEntry {Title = "Full Search (BETA)", Reference = "blog/music4dance-help/full-search"},
                                    }
                                },
                                new SiteMapEntry {Title = "Dance Styles", Reference = "blog/music4dance-help/dance-styles-help"},
                                new SiteMapEntry {Title = "Dance Categories", Reference = "blog/music4dance-help/dance-category"},
                                new SiteMapEntry {Title = "Dance Details", Reference = "blog/music4dance-help/dance-details"},
                                new SiteMapEntry {Title = "Tag Cloud", Reference = "blog/music4dance-help/tag-cloud"},
                                new SiteMapEntry {Title = "Tag Definition", Reference = "blog/music4dance-help/tag-definitions"},
                                new SiteMapEntry {Title = "Tag Filtering", Reference = "blog/music4dance-help/tag-filtering"},
                                new SiteMapEntry {Title = "Tag Editing", Reference = "blog/music4dance-help/tag-editing"},
                                new SiteMapEntry {Title = "Tempo Counter", Reference = "blog/music4dance-help/tempo-counter"},
                                new SiteMapEntry {Title = "Dance Tempi", Reference = "blog/music4dance-help/dance-tempi"},
                                new SiteMapEntry {Title = "Account Management", Reference = "blog/music4dance-help/account-management"},
                                new SiteMapEntry
                                {
                                    Title = "Playing or Purchasing Songs", Reference = "blog/music4dance-help/playing-or-purchasing-songs/",
                                    Children = new List<SiteMapEntry>
                                    {
                                        new SiteMapEntry {Title = "ITunes", Reference = "blog/music4dance-help/playing-or-purchasing-songs/itunes"},
                                        new SiteMapEntry {Title = "Amazon", Reference = "blog/music4dance-help/playing-or-purchasing-songs/amazon"},
                                        new SiteMapEntry {Title = "Spotify", Reference = "blog/music4dance-help/playing-or-purchasing-songs/spotify"},
                                        new SiteMapEntry {Title = "Groove", Reference = "blog/music4dance-help/playing-or-purchasing-songs/groove"},
                                        new SiteMapEntry {Title = "EchoNest", Reference = "blog/music4dance-help/playing-or-purchasing-songs/echonest"},
                                    }
                                },
                                new SiteMapEntry {Title = "Beta", Reference = "blog/music4dance-help/beta"},
                                new SiteMapEntry {Title = "Feedback", Reference = "blog/feedback"},
                                new SiteMapEntry {Title = "Bug Report", Reference = "blog/bug-report"},
                            }
                        },
                    }
                },
                new SiteMapCategory
                {
                    Name = "Tools",
                    Type = "tools",
                    Entries = new List<SiteMapEntry>
                    {
                        new SiteMapEntry {Title =  "Dance Counter", Reference="home/counter", Description="Count out the tempo of a song and see a list of dance styles that can be danced at that tempo."},
                        new SiteMapEntry {Title =  "Dance Tempi", Reference="home/tempi", Description="A table of ballroom and social dances organized by tempo."},
                    }
                },
                new SiteMapCategory
                {
                    Name = "Account",
                    Type = "tools",
                    Entries = new List<SiteMapEntry>
                    {
                        new SiteMapEntry {Title =  "Profile", Reference="manage/userprofile"},
                        new SiteMapEntry {Title =  "Settings", Reference="manage/settings"},
                        new SiteMapEntry {Title =  "Sign In", Reference="account/signin"},
                        new SiteMapEntry {Title =  "Sign Up", Reference="account/signup"},
                    }
                },
            };
        }
    }
}
