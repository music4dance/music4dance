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
        public RecomputeController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, RecomputeMarkerService recomputeMarkerService, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            _markerService = recomputeMarkerService;
        }

        private readonly RecomputeMarkerService _markerService;

        // id should be the type to update - currently songstats, propertycleanup
        [HttpGet("{id}")]
        public IActionResult Get([FromServices] IConfiguration configuration, string id, bool force = false, bool sync = false)
        {
            if (!TokenRequirement.Authorize(Request, configuration))
            {
                return Unauthorized();
            }

            if (!force && !HasChanged(id))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"RecomputeController: id = {id}, changed = false");
                return Ok(new {changed = false, message = "No updates."});
            }

            if (!AdminMonitor.StartTask(id))
            {
                return Conflict();
            }

            if (force)
            {
                _markerService.ResetMarker(id);
            }

            string message;
            DoHandleRecompute recompute;

            var rgid = id.Split('-');

            switch (rgid[0])
            {
                case "songstats":
                    recompute= DoHandleSongStats;
                    message = "Updated song stats.";
                    break;
                case "propertycleanup":
                    recompute = DoHandlePropertyCleanup;
                    message = "Cleaned up properties";
                    break;
                default:
                    AdminMonitor.CompleteTask(false, $"Bad Id: {id}");
                    return BadRequest();
            }


            if (sync)
            {
                HandleSyncRecompute(recompute, id, message, force);
            }
            else
            {
                HandleRecompute(recompute, id, message, force);
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"RecomputeController: id = {id}, changed = true, message = {message}");
            return Ok(new {changed = true, message});
        }

        private bool HasChanged(string id)
        {
            var updated = _markerService.GetMarker(id);
            var changed = Database.GetLastModified();
            return changed > updated;
        }

        private delegate bool DoHandleRecompute(RecomputeMarkerService markerService, DanceMusicCoreService dms, IDanceStatsManager dsm, string id, string message, int iteration, bool force);

        private void HandleRecompute(DoHandleRecompute recompute, string id, string message, bool force)
        {
            var dms = Database.GetTransientService();
            Task.Run(() => recompute.Invoke(_markerService, dms, DanceStatsManager, id, message, 0, force));
        }

        private void HandleSyncRecompute(DoHandleRecompute recompute,string id, string message, bool force)
        {
            var dms = Database.GetTransientService();
            int[] i = {0};
            while (!Task.Run(() => recompute.Invoke(_markerService, dms, DanceStatsManager, id,message,i[0],force)).Result)
            {
                if (!AdminMonitor.StartTask(id))
                {
                    return;
                }

                i[0] += 1;
            }
        }

        private static bool DoHandleSongStats(RecomputeMarkerService markerService, DanceMusicCoreService dms, IDanceStatsManager dsm, string id, string message, int iteration, bool force)
        {
            try
            {
                dsm.ClearCache(dms, true);
                Complete(markerService, id, message);
            }
            catch (Exception e)
            {
                Fail(e);
            }
            return true;
        }

        private static bool DoHandlePropertyCleanup(RecomputeMarkerService markerService, DanceMusicCoreService dms, IDanceStatsManager dsm, string id, string message, int iteration, bool force)
        {
            try
            {
                var from = markerService.GetMarker(id);

                var info = dms.CleanupProperties(250, from, new SongFilter());

                if (info.Succeeded > 0 || info.Failed > 0)
                {
                    markerService.SetMarker(id, info.LastTime);
                }

                if (info.Complete)
                {
                    Complete(markerService, id, message);
                }
                else
                {
                    AdminMonitor.CompleteTask(true, message);
                }
                return info.Complete;
            }
            catch (Exception e)
            {
                Fail(e);
                return true;
            }
        }


        private static void Complete(RecomputeMarkerService markerService, string id, string message)
        {
            markerService.SetMarker(id);
            AdminMonitor.CompleteTask(true, message);

        }

        private static void Fail(Exception e)
        {
            AdminMonitor.CompleteTask(false, e.Message, e);
        }
    }
}
