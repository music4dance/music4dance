using System;
using System.Diagnostics;
using System.Threading.Tasks;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    //public class RecomputeInfo
    //{
    //    public bool Changed { get; set; }
    //    public string Message { get; set; }
    //}
    [ApiController]
    [Route("api/[controller]")]
    public class RecomputeController : DanceMusicApiController
    {
        public RecomputeController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        // id should be the type to update - currently songstats, propertycleanup
        [HttpGet("{id}")]
        public IActionResult Get([FromServices]IConfiguration configuration, string id,
            bool sync = false)
        {
            if (!TokenRequirement.Authorize(Request, configuration))
            {
                return Unauthorized();
            }

            if (!AdminMonitor.StartTask(id))
            {
                return Conflict();
            }

            string message;
            DoHandleRecompute recompute;

            var rgid = id.Split('-');

            switch (rgid[0])
            {
                case "songstats":
                    recompute = DoHandleSongStats;
                    message = "Updated song stats.";
                    break;
                default:
                    AdminMonitor.CompleteTask(false, $"Bad Id: {id}");
                    return BadRequest();
            }


            HandleRecompute(recompute, message);

            Trace.WriteLineIf(
                TraceLevels.General.TraceInfo,
                $"RecomputeController: id = {id}, changed = true, message = {message}");
            return Ok(new { changed = true, message });
        }

        private void HandleRecompute(DoHandleRecompute recompute, string message)
        {
            var dms = Database.GetTransientService();
            Task.Run(
                async () =>
                    await recompute.Invoke(dms, DanceStatsManager, message));
        }

        private static async Task<bool> DoHandleSongStats(
            DanceMusicCoreService dms, IDanceStatsManager dsm, string message)
        {
            try
            {
                await dsm.ClearCache(dms, true);
                Complete(message);
            }
            catch (Exception e)
            {
                Fail(e);
            }

            return true;
        }

        private static void Complete(string message)
        {
            AdminMonitor.CompleteTask(true, message);
        }

        private static void Fail(Exception e)
        {
            AdminMonitor.CompleteTask(false, e.Message, e);
        }

        private delegate Task<bool> DoHandleRecompute(DanceMusicCoreService dms, IDanceStatsManager dsm, string message);
    }
}
