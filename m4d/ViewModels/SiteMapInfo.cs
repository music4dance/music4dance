using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
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

        // TODONEXT: Verify that mapping works
        protected string MakeFullPath(string rel)
        {
            const string blogPrefix = "blog/";
            if (rel == null)
            {
                return string.Empty;
            }
            if (rel == "blog")
            {
                return "https://music4dance.blog/";
            }
            if (rel.StartsWith(blogPrefix))
            {
                return $"https://music4dance.blog/{rel.Substring(blogPrefix.Length)}";
            }
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
            var path = Path.Combine(HttpContext.Current.Server.MapPath("~/Content")??"",$"{filename}.txt");
            var lines = File.ReadAllLines(path);
            var family = new Stack<List<SiteMapEntry>>();
            family.Push(new List<SiteMapEntry>());
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var line in lines)
            {
                var parts = line.Split('\t').ToList();
                var curdepth = 0;
                var depth = family.Count - 1;

                while (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[0]))
                {
                    parts.RemoveAt(0);
                    curdepth += 1;
                }

                if (Math.Abs(curdepth - depth) > 1)
                {
                    continue;
                }

                if (curdepth > depth)
                {
                    family.Push(new List<SiteMapEntry>());
                }
                else if (curdepth < depth)
                {
                    var t = family.Pop();
                    family.Peek()[family.Peek().Count - 1].Children = t;
                }

                family.Peek().Add(new SiteMapEntry
                    {
                        Title = parts[0],
                        Reference = parts.Count > 1 ? parts[1] : null,
                        Description = parts.Count > 2 ? parts[2] : null,
                        OneTime = (parts.Count > 3 && parts[3] == "OneTime")
                    });
            }
            Children = family.Pop();
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
                            Title =  "Song Library", Reference="song",
                            Children = new List<SiteMapEntry>
                            {
                                new SiteMapEntry {Title = "Advanced Search", Reference = "song/advancedsearchform"},
                                new SiteMapEntry {Title = "Add Songs", Reference = "song/augment"},
                                new SiteMapEntry {Title = "New Music", Reference = "song/newmusic"},
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
                        new SiteMapEntry {Title =  "Holiday Music", Reference="dances/holiday-music"},
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
                        new SiteMapEntry {Title =  "Contribute", Reference="home/contribute"},
                        new SiteMapEntry {Title =  "About Us", Reference="home/about"},
                        new SiteMapEntry {Title =  "FAQ", Reference="home/faq"},
                        new SiteMapEntry {Title =  "Privacy Policy", Reference="home/privacypolicy"},
                        new SiteMapEntry {Title =  "Terms of Service", Reference="home/termsofservice"},
                        new SiteMapEntry {Title =  "Credits", Reference="home/credits"},
                        new SiteMapEntry {Title =  "Reading List", Reference="blog/reading-list"},
                        new SiteMapFile("blogmap") {Title =  "Blog", Reference="blog"},
                        new SiteMapFile("helpmap") {Title =  "Help", Reference="blog/music4dance-help"},
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
