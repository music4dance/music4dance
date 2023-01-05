using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AutoMapper;
using DanceLibrary;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.Controllers
{
    public class CompetitionGroupModel
    {
        public string CurrentCategoryName { get; set; }
        public CompetitionGroup Group { get; set; }

        internal static CompetitionGroupModel Get(string group, string category)
        {
            var g = CompetitionGroup.Get(group);
            var cat = g.Categories.FirstOrDefault(c => string.Equals(c.CanonicalName, category));
            if (cat == null)
            {
                return null;
            }

            return new CompetitionGroupModel
            {
                CurrentCategoryName = cat.Name,
                Group = g
            };
        }
    }

    public class DanceController : ContentController
    {
        private readonly IMapper _mapper;

        public DanceController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration, IMapper mapper) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            _mapper = mapper;
            UseVue = true;
        }

        // GET: Dances/{dance}
        [AllowAnonymous]
        public ActionResult Index(string dance)
        {
            BuildEnvironment(danceEnvironment: true);
            if (string.IsNullOrWhiteSpace(dance))
            {
                return Vue(
                    "Dance Style Index",
                    "A list of partner dancing styles, including Ballroom, Salsa, Swing, and Tango.",
                    "dance-index",
                    helpPage: "dance-styles",
                    danceEnvironment: true
                );
            }

            var stats = DanceStatsManager.Instance;
            if (string.Equals(
                dance, "ballroom-competition-categories",
                StringComparison.OrdinalIgnoreCase))
            {
                return Vue(
                    "Ballroom Competition Categories",
                    "An overview of competitive ballroom categories along with tempo ranges and song lists.",
                    "ballroom-index",
                    CompetitionGroup.Get(CompetitionCategory.Ballroom),
                    danceEnvironment: true
                );
            }

            if (string.Equals(dance, "wedding-music", StringComparison.OrdinalIgnoreCase))
            {
                return Vue(
                    "Wedding Dance Music",
                    "Help finding wedding dance music: First Dance, Mother/Son, Father/Daughter - Foxtrot, Waltz, Swing and others.",
                    "wedding-dance-music",
                    BuildWeddingTagMatrix(stats),
                    danceEnvironment: true,
                    helpPage: "dance-styles"
                );
            }

            var category = CompetitionGroupModel.Get(CompetitionCategory.Ballroom, dance);
            if (category != null)
            {
                return Vue(
                    category.CurrentCategoryName,
                    $"A description of the competition dance category {category.CurrentCategoryName} along with tempo ranges and song lists.",
                    "competition-category",
                    category,
                    danceEnvironment:true,
                    helpPage: "dance-category"
                    );
            }

            HelpPage = "dance-details";

            var ds = stats.FromName(dance);
            var dbDance = ds == null ? null : Database.Dances.FirstOrDefault(d => d.Id == ds.DanceId);

            if (dbDance == null)
            {
                return ReturnError(
                    HttpStatusCode.NotFound,
                    $"The dance with the name = {dance} isn't defined.");
            }


            if (ds.SongCount == 0)
            {
                UseVue = false;
                return View("emptydance", ds);
            }

            return Vue(
                $"music4dance catalog: {ds.DanceName} Page",
                $"{ds.DanceName} Information, Top Ten List, and Resources.",
                "dance-details",
                new DanceModel(dbDance, Database, _mapper)
            );
        }

        // GET: GroupRedirect/group/dance
        [AllowAnonymous]
        public ActionResult GroupRedirect(string group, string dance)
        {
            return RedirectToActionPermanent("Index", new { dance });
        }

        private TagMatrix BuildWeddingTagMatrix(DanceStatsInstance stats)
        {
            var columns = new List<TagColumn>
            {
                new() { Title = "Wedding", Tag = "Wedding:Other" },
                new() { Title = "First Dance", Tag = "First Dance:Other" },
                new() { Title = "Mother/Son", Tag = "Mother Son:Other" },
                new() { Title = "Father/Daughter", Tag = "Father Daughter:Other" }
            };
            var rows = new List<TagRowGroup>();

            foreach (var group in stats.Groups)
            {
                var row = BuildTagRow(columns, group);
                if (row != null)
                {
                    var groupRow = new TagRowGroup
                    {
                        Dance = row.Dance,
                        Counts = row.Counts
                    };
                    rows.Add(groupRow);

                    var rowsT = new List<TagRow>();
                    foreach (var dance in group.Children)
                    {
                        row = BuildTagRow(columns, dance);
                        if (row != null)
                        {
                            rowsT.Add(row);
                        }
                    }

                    groupRow.Children = rowsT;
                }
            }

            return new TagMatrix { Columns = columns, Groups = rows };
        }

        private TagRow BuildTagRow(List<TagColumn> columns, DanceStats dance)
        {
            var counts = new List<int>();
            foreach (var column in columns)
            {
                counts.Add(dance.SongTags?.TagCount(column.Tag) ?? 0);
            }

            return counts.Any(c => c > 0)
                ? new TagRow { Dance = dance.DanceObject, Counts = counts }
                : null;
        }
    }
}
