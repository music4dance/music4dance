using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DanceLibrary;
using Microsoft.Extensions.FileProviders;

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

    // https://stackoverflow.com/questions/43992261/how-to-get-absolute-path-in-asp-net-core-alternative-way-for-server-mappath
    public sealed class SiteMapFile : SiteMapEntry
    {
        public SiteMapFile(string filename, IFileProvider fileProvider)
        {
            var path = fileProvider.GetFileInfo($"/wwwroot/content/{filename}.txt").PhysicalPath;
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

                family.Peek().Add(
                    new SiteMapEntry
                    {
                        Title = parts[0],
                        Reference = parts.Count > 1 ? parts[1] : null,
                        Description = parts.Count > 2 ? parts[2] : null,
                        OneTime = parts.Count > 3 && parts[3] == "OneTime"
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

        public override IEnumerable<SiteMapEntry> Entries => Dances.Instance.AllDances
            .Where(d => !(d is DanceInstance)).OrderBy(d => d.Name)
            .Select(d => new SiteMapDance(d));
    }

    public static class SiteMapInfo
    {
        public static void ReloadCategories(IFileProvider fileProvider)
        {
            Categories = LoadCategories(fileProvider);
        }

        public static IEnumerable<SiteMapCategory> GetCategories(IFileProvider fileProvider)
        {
            return Categories ??= LoadCategories(fileProvider);
        }

        private static IEnumerable<SiteMapCategory> Categories { get; set; } = null;

        private static IEnumerable<SiteMapCategory> LoadCategories(IFileProvider fileProvider)
        {
            return new List<SiteMapCategory>
            {
                new()
                {
                    Name = "Music",
                    Type = "music",
                    Entries = new List<SiteMapEntry>
                    {
                        new()
                        {
                            Title = "Song Library", Reference = "song",
                            Children = new List<SiteMapEntry>
                            {
                                new()
                                {
                                    Title = "Advanced Search", Reference = "song/advancedsearchform"
                                },
                                new() { Title = "Add Songs", Reference = "song/augment" },
                                new() { Title = "New Music", Reference = "song/newmusic" },
                                new() { Title = "Saved Searches", Reference = "searches/index" }
                            }
                        },
                        new() { Title = "Dance Index", Reference = "dances" },
                        new()
                        {
                            Title = "Ballroom Competition Categories",
                            Reference = "dances/ballroom-competition-categories",
                            Children = new List<SiteMapEntry>
                            {
                                new()
                                {
                                    Title = "International Standard",
                                    Reference = "dances/international-standard"
                                },
                                new()
                                {
                                    Title = "International Latin",
                                    Reference = "dances/international-latin"
                                },
                                new()
                                {
                                    Title = "American Smooth", Reference = "dances/american-smooth"
                                },
                                new()
                                {
                                    Title = "American Rhythm", Reference = "dances/american-rhythm"
                                }
                            }
                        },
                        new() { Title = "Wedding Music", Reference = "dances/wedding-music" },
                        new() { Title = "Holiday Music", Reference = "song/holidaymusic" },
                        new()
                        {
                            Title = "Halloween Music",
                            Reference = "song/addtags/?tags=%2BHalloween%3AOther"
                        }
                    }
                },
                new SiteMapDances(),
                new()
                {
                    Name = "Info",
                    Type = "info",
                    Entries = new List<SiteMapEntry>
                    {
                        new() { Title = "Home Page", Reference = "" },
                        new() { Title = "Contribute", Reference = "home/contribute" },
                        new() { Title = "About Us", Reference = "home/about" },
                        new() { Title = "FAQ", Reference = "home/faq" },
                        new() { Title = "Privacy Policy", Reference = "home/privacypolicy" },
                        new() { Title = "Terms of Service", Reference = "home/termsofservice" },
                        new() { Title = "Credits", Reference = "home/credits" },
                        new() { Title = "Reading List", Reference = "blog/reading-list" },
                        new SiteMapFile("blogmap", fileProvider)
                            { Title = "Blog", Reference = "blog" },
                        new SiteMapFile("helpmap", fileProvider)
                            { Title = "Help", Reference = "blog/music4dance-help" }
                    }
                },
                new()
                {
                    Name = "Tools",
                    Type = "tools",
                    Entries = new List<SiteMapEntry>
                    {
                        new()
                        {
                            Title = "Dance Counter", Reference = "home/counter",
                            Description =
                                "Count out the tempo of a song and see a list of dance styles that can be danced at that tempo."
                        },
                        new()
                        {
                            Title = "Dance Tempi", Reference = "home/tempi",
                            Description =
                                "A table of ballroom and social dances organized by tempo."
                        }
                    }
                },
                new()
                {
                    Name = "Account",
                    Type = "tools",
                    Entries = new List<SiteMapEntry>
                    {
                        new() { Title = "Profile", Reference = "identity/Account/Manage" },
                        new() { Title = "Log In", Reference = "identity/account/login" },
                        new() { Title = "Register", Reference = "identity/account/register" }
                    }
                }
            };
        }
    }
}
