using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using m4d.Utilities;
using m4dModels;

namespace m4d.APIControllers
{
    //public class RecomputeInfo
    //{
    //    public bool Changed { get; set; }
    //    public string Message { get; set; }
    //}
    public class RecomputeController : DMApiController
    {
        // id should be the type to update - currently songstats, propertycleanup
        //   future tags, purchase, spotify, albums, tagtypes, tagsummaries, 
        //   timesfromproperties, compressregions, spotifyregions, rebuildusertags, rebuildtags
        public IHttpActionResult Get(string id, bool force = false, bool sync = false)
        {
            if (!TokenAuthorizeAttribute.Authorize(Request))
            {
                return Unauthorized();
            }

            if (!force && !HasChanged(id))
            {
                Trace.WriteLine($"RecomputeController: id = {id}, changed = false");
                return Ok(new {changed = false, message = "No updates."});
            }

            if (!AdminMonitor.StartTask(id))
            {
                return Conflict();
            }

            if (force)
            {
                RecomputeMarker.ResetMarker(id);
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

            Trace.WriteLine($"RecomputeController: id = {id}, changed = true, message = {message}");
            return Ok(new {changed = true, message});
        }

        private bool HasChanged(string id)
        {
            var updated = RecomputeMarker.GetMarker(id);
            var changed = Database.GetLastModified();
            return changed > updated;
        }

        private delegate bool DoHandleRecompute(DanceMusicService dms, string id, string message, int iteration, bool force);

        private static void HandleRecompute(DoHandleRecompute recompute, string id, string message, bool force)
        {
            Task.Run(() => recompute.Invoke(DanceMusicService.GetService(),id,message,0,force));
        }

        private static void HandleSyncRecompute(DoHandleRecompute recompute, string id, string message, bool force)
        {
            int[] i = {0};
            while (!Task.Run(() => recompute.Invoke(DanceMusicService.GetService(), id,message,i[0],force)).Result)
            {
                if (!AdminMonitor.StartTask(id))
                {
                    return;
                }

                i[0] += 1;
            }
        }

        private static bool DoHandleSongStats(DanceMusicService dms, string id, string message, int iteration, bool force)
        {
            try
            {
                DanceStatsManager.ClearCache(dms);
                Complete(id,message);
            }
            catch (Exception e)
            {
                Fail(e);
            }
            return true;
        }

        private static bool DoHandlePropertyCleanup(DanceMusicService dms, string id, string message, int iteration, bool force)
        {
            try
            {
                var from = RecomputeMarker.GetMarker(id);

                var info = dms.CleanupProperties(250, from, new SongFilter());

                if (info.Succeeded > 0 || info.Failed > 0)
                {
                    RecomputeMarker.SetMarker(id, info.LastTime);
                }

                if (info.Complete)
                {
                    Complete(id, message);
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


        private static void Complete(string id, string message)
        {
            RecomputeMarker.SetMarker(id);
            AdminMonitor.CompleteTask(true, message);

        }

        private static void Fail(Exception e)
        {
            AdminMonitor.CompleteTask(false, e.Message, e);
        }
    }
}
