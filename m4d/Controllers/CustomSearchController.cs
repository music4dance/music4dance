using AutoMapper;

using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.Utilities;
using m4d.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;
using Microsoft.Net.Http.Headers;

using System.Net;

namespace m4d.Controllers;

public class CustomSearchController : ContentController
{
    private static readonly HttpClient HttpClient = new();

    public CustomSearchController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManagerSnapshot featureManager, ILogger<CustomSearchController> logger, LinkGenerator linkGenerator, IMapper mapper,
        ServiceHealthManager serviceHealth) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger, linkGenerator, mapper, serviceHealth)
    {
        UseVue = UseVue.V3;
        HelpPage = "song-list";
    }

    [AllowAnonymous]
    public async Task<ActionResult> Index(string name, string dance = null, int page = 1)
    {
        // An unknown name made CreateCustomSearchFilter throw (a 500 for every injection
        // probe on ?name=), and a missing one reached name[0] below.
        name = string.IsNullOrWhiteSpace(name) ? "holiday" : name;
        if (!SongFilter.IsCustomSearchName(name))
        {
            return ReturnError(HttpStatusCode.NotFound, "There's no custom search with that name.");
        }

        Filter = Database.SearchService.GetSongFilter().CreateCustomSearchFilter(name, dance, page);
        if (!Filter.DanceQuery.IsValid)
        {
            return BadRequest("Unknown dance in filter");
        }

        HelpPage = Filter.IsSimple ? "song-list" : "advanced-search";

        // Check if search service is available
        if (!IsSearchAvailable())
        {
            Logger.LogWarning("Custom search requested but SearchService is unavailable");
            var title = char.ToUpper(name[0]) + name[1..];
            return Vue3(
                $"{title} Dance Music",
                "Help finding holiday dance music for partner dancing - Foxtrot, Waltz, Swing and others.",
                "custom-search",
                new CustomSearchModel
                {
                    Name = name.ToLowerInvariant(),
                    Description = null,
                    Histories = new List<SongHistory>(), // Empty list
                    Filter = Mapper.Map<SongFilterSparse>(Filter),
                    Count = 0,
                    Dance = dance,
                    PlayListId = null
                },
                danceEnvironment: true);
        }

        try
        {
            var title = char.ToUpper(name[0]) + name[1..];

            if (!Filter.IsEmptyBot &&
                SpiderManager.CheckAnySpiders(Request.Headers[HeaderNames.UserAgent], Configuration))
            {
                throw new RedirectException("BotFilter", Filter);
            }

            var results = await new SongSearch(
                Filter, UserName, IsPremium(), SongIndex, UserManager, TaskQueue, ServiceHealth).Search();

            string playListId = null;

            if (!string.IsNullOrWhiteSpace(dance))
            {
                var ds = Database.DanceStats.FromName(dance);
                var danceName = $"{title} {ds.DanceName}";
                var playlist = Database.PlayLists.FirstOrDefault(
                    p => p.Name == danceName && p.Type == PlayListType.SpotifyFromSearch);
                playListId = playlist?.Id;
            }

            var dictionary = await UserMapper.GetUserNameDictionary(UserManager, ServiceHealth);
            var histories = ForListViewer(results.Songs
                .Select(s => UserMapper.AnonymizeHistory(s.GetHistory(Mapper), dictionary,
                    User.Identity?.IsAuthenticated == true, User.Identity?.Name)));
            string description = null;
            switch (name.ToLowerInvariant())
            {
                case "halloween":
                    description = @"'Halloween'";
                    break;
                case "holiday":
                case "christmas":
                    description = @"'Holiday' or 'Christmas'";
                    break;
                case "broadway":
                    description = @"'Broadway' or 'Broadway And Vocal' or 'Musical' or 'Show Tunes'";
                    break;
            }

            return Vue3(
                $"{title} Dance Music",
                "Help finding holiday dance music for partner dancing - Foxtrot, Waltz, Swing and others.",
                "custom-search",
                 new CustomSearchModel
                 {
                     Name = name.ToLowerInvariant(),
                     Description = description.Replace('\'', '"'),
                     Histories = histories,
                     Filter = Mapper.Map<SongFilterSparse>(Filter),
                     Count = (int)results.TotalCount,
                     Dance = dance,
                     PlayListId = playListId,
                 },
                danceEnvironment: true);
        }
        catch (InvalidOperationException ex) when (IsSearchServiceError(ex))
        {
            HandleSearchServiceError(ex);
            var title = char.ToUpper(name[0]) + name[1..];
            return Vue3(
                $"{title} Dance Music",
                "Help finding holiday dance music for partner dancing - Foxtrot, Waltz, Swing and others.",
                "custom-search",
                new CustomSearchModel
                {
                    Name = name.ToLowerInvariant(),
                    Description = null,
                    Histories = new List<SongHistory>(),
                    Filter = Mapper.Map<SongFilterSparse>(Filter),
                    Count = 0,
                    Dance = dance,
                    PlayListId = null
                },
                danceEnvironment: true);
        }
        catch (RedirectException ex)
        {
            return HandleRedirect(ex);
        }
    }
}
