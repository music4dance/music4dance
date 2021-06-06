using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace m4d.Controllers
{
    public class DanceMusicController : Controller
    {
        public DanceMusicController(
            DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration)
        {
            Database =
                new DanceMusicService(context, userManager, searchService, danceStatsManager);
            SearchService = searchService;
            DanceStatsManager = danceStatsManager;
            Configuration = configuration;
        }

        protected bool UseVue { get; set; } = false;
        public DanceMusicService Database { get; set; }

        protected MusicServiceManager MusicServiceManager =>
            _musicServiceManager ??= new MusicServiceManager(Configuration);

        private MusicServiceManager _musicServiceManager;
        protected IConfiguration Configuration { get; }

        public ISearchServiceManager SearchService { get; }

        public IDanceStatsManager DanceStatsManager { get; }

        public UserManager<ApplicationUser> UserManager => Database.UserManager;

        public DanceMusicContext Context => Database.Context;

        public string HelpPage { get; set; }

        public ActionResult ReturnError(
            HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string message = null,
            Exception exception = null)
        {
            var model = new ErrorModel
                {HttpStatusCode = (int) statusCode, Message = message, Exception = exception};

            Response.StatusCode = (int) statusCode;
            // Response.TrySkipIisCustomErrors = true;

            return View("HttpError", model);
        }

        protected IActionResult JsonCamelCase(object json)
        {
            return new JsonResult(json, JsonHelpers.CamelCaseSerializer);
        }

        public override ViewResult View(string viewName, object model)
        {
            ViewBag.Help = HelpPage;
            ViewBag.UseView = UseVue;
            return base.View(viewName, model);
        }

        public ActionResult CheckSpiders()
        {
            return SpiderManager.CheckBadSpiders(Request.Headers[HeaderNames.UserAgent])
                ? View("BotWarning")
                : null;
        }

        protected void SaveSong(Song song)
        {
            Database.SaveSong(song);
        }

        protected static readonly JsonSerializerSettings CamelCaseSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            ContractResolver = new StatsContractResolver(true, true)
        };


        protected void SaveSongs(IEnumerable<Song> songs = null)
        {
            Database.SaveSongs(songs);
        }

        #region AdminTaskHelpers

        protected void StartAdminTask(string name)
        {
            ViewBag.Name = name;
            if (!AdminMonitor.StartTask(name))
                throw new AdminTaskException(name +
                    "failed to start because there is already an admin task running");
        }

        protected ActionResult CompleteAdminTask(bool completed, string message)
        {
            ViewBag.Success = completed;
            ViewBag.Message = message;
            AdminMonitor.CompleteTask(completed, message);

            return View("Results");
        }

        protected ActionResult FailAdminTask(string message, Exception e)
        {
            ViewBag.Success = false;
            ViewBag.Message = message;

            if (!(e is AdminTaskException)) AdminMonitor.CompleteTask(false, message, e);

            return View("Results");
        }

        protected ActionResult RestoreBatch()
        {
            ViewBag.Success = false;
            ViewBag.Message =
                "This functionality hasn't been re-implemented after the azure-search migration - do we really need it?";

            return View("Results");
        }

        #endregion

        protected int CommitCatalog(DanceMusicCoreService dms, Review review,
            ApplicationUser user, string danceIds = null)
        {
            List<string> dances = null;
            if (!string.IsNullOrWhiteSpace(danceIds))
                dances = new List<string>(danceIds.Split(';'));

            if (review.Merge.Count <= 0) return 0;

            var modified = dms.MergeCatalog(user, review.Merge, dances).ToList();

            var i = 0;

            foreach (var song in modified)
            {
                AdminMonitor.UpdateTask("UpdateService", i);
                MusicServiceManager.UpdateSongAndServices(dms, song, true);
                i += 1;
            }

            dms.SaveSongs(modified);

            if (!string.IsNullOrEmpty(review.PlayList))
                dms.UpdatePlayList(review.PlayList, review.Merge.Select(m => m.Left));

            return modified.Count;
        }
    }
}