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
            if (string.IsNullOrWhiteSpace(dance))
            {
                HelpPage = "dance-styles";
                return View();
            }

            var stats = DanceStatsManager.Instance;
            if (string.Equals(
                dance, "ballroom-competition-categories",
                StringComparison.OrdinalIgnoreCase))
            {
                return View(
                    "BallroomCompetitionCategories",
                    CompetitionGroup.Get(CompetitionCategory.Ballroom));
            }

            if (string.Equals(dance, "wedding-music", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Wedding dance help page?
                HelpPage = "dance-styles";
                return View("weddingdancemusic", BuildWeddingTagMatrix(stats));
            }

            if (string.Equals(dance, "holiday-music", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToActionPermanent("HolidayMusic", "Song");
            }

            var category = CompetitionGroupModel.Get(CompetitionCategory.Ballroom, dance);
            if (category != null)
            {
                HelpPage = "dance-category";

                return View("category", category);
            }

            HelpPage = "dance-details";

            var ds = stats.FromName(dance);
            var dbDance = Database.Dances.FirstOrDefault(d => d.Id == ds.DanceId);

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

            return View("details", new DanceModel(dbDance, Database, _mapper));
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

            foreach (var group in stats.Tree)
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
                counts.Add(dance.AggregateSongTags?.TagCount(column.Tag) ?? 0);
            }

            return counts.Any(c => c > 0)
                ? new TagRow { Dance = dance.DanceObject, Counts = counts }
                : null;
        }
    }
}
