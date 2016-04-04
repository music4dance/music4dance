using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using m4d.Context;
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
        // id should be the type to update - currently danceinfo, songstats
        //   future tags, purchase, spotify, albums, tagtypes, tagsummaries, 
        //   timesfromproperties, compressregions, spotifyregions, rebuildusertags, rebuildtags
        public IHttpActionResult Get(string id, bool force = false, bool sync = false)
        {
            var authenticationHeader = Request.Headers.Authorization;
            var token = Encoding.UTF8.GetString(Convert.FromBase64String(authenticationHeader.Parameter));
            var client = TelemetryClient;
            if (authenticationHeader.Scheme != "Token" || token != SecurityToken)
            {
                client.TrackEvent("Recompute",
                    new Dictionary<string, string> { { "Id", id }, { "Phase","Auth"}, {"Code","Fail"}, { "AuthScheme", authenticationHeader.Scheme }, { "ClientToken", token }, { "ServerToken", SecurityToken } });

                return Unauthorized();
            }

            if (!force && !HasChanged(id))
            {
                client.TrackEvent("Recompute",
                    new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "NoChange" } });
                return Ok(new {changed = false, message = "No updates."});
            }

            if (!AdminMonitor.StartTask(id))
            {
                client.TrackEvent("Recompute",
                    new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "Conflict" }, { "Task", AdminMonitor.Name } });
                return Conflict();
            }

            string message;
            DoHandleRecompute recompute;

            switch (id)
            {
                case "songstats":
                    recompute= DoHandleSongStats;
                    message = "Updated song stats.";
                    break;
                case "danceinfo":
                    recompute = DoHandleDanceInfo;
                    message = "Rebuilt Dances, Dance Tags, and updated Song Counts.";
                    break;
                case "propertycleanup":
                    recompute = DoHandlePropertyCleanup;
                    message = "Cleand up properties";
                    break;
                default:
                    client.TrackEvent("Recompute",
                        new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "Unknown" } });
                    return BadRequest();
            }

            client.TrackEvent("Recompute",
                new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "Okay" } });

            if (sync)
            {
                HandleSyncRecompute(recompute, id,message);
            }
            else
            {
                HandleRecompute(recompute, id, message);
            }

            return Ok(new {changed = true, message});
        }

        private bool HasChanged(string id)
        {
            var updated = RecomputeMarker.GetMarker(id);
            return Database.Songs.Any(s => s.Modified > updated);
        }

        private delegate bool DoHandleRecompute(DanceMusicService dms, string id, string message);

        // TODO: Should Do*** functions take name of task and message so that we can clean up duplicate code?
        private static void HandleRecompute(DoHandleRecompute recompute, string id, string message)
        {
            Task.Run(() => recompute.Invoke(CreateDisconnectedService(),id,message));
        }

        private static void HandleSyncRecompute(DoHandleRecompute recompute, string id, string message)
        {
            var i = 0;
            while (!Task.Run(() => recompute.Invoke(CreateDisconnectedService(),id,message)).Result)
            {
                if (!AdminMonitor.StartTask(id))
                {
                    TelemetryClient.TrackEvent("Recompute",
                        new Dictionary<string, string> { { "Id", id }, { "Phase", "Iteration" }, { "Code", "Conflict" }, { "Task", AdminMonitor.Name }, { "Iteration", i.ToString() } });
                    return;
                }

                TelemetryClient.TrackEvent("Recompute",
                    new Dictionary<string, string> { { "Id", id }, { "Phase", "Iteration" }, { "Code", "Pending" }, { "Task", AdminMonitor.Name }, { "Iteration", i.ToString() } });
                i += 1;
            }
        }

        private static bool DoHandleSongStats(DanceMusicService dms, string id, string message)
        {
            try
            {
                SongCounts.RebuildSongCounts(dms);
                Complete(id,message);
            }
            catch (Exception e)
            {
                Fail(e);
            }
            return true;
        }

        private static bool DoHandleDanceInfo(DanceMusicService dms, string id, string message)
        {
            try
            {
                dms.RebuildDanceInfo();
                Complete(id, message);
            }
            catch (Exception e)
            {
                Fail(e);
            }
            return true;
        }

        private static bool DoHandlePropertyCleanup(DanceMusicService dms, string id, string message)
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
                    TelemetryClient.TrackEvent("Recompute",
                        new Dictionary<string, string> { { "Id", id }, { "Phase", "Intermediate" }, { "Code", "Success" }, { "Message", AdminMonitor.Status.ToString() }, { "Time", AdminMonitor.Duration.ToString() } });
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
            TelemetryClient.TrackEvent("Recompute",
                new Dictionary<string, string> { { "Id", id }, {"Phase","End"}, { "Code", "Success" }, { "Message", AdminMonitor.Status.ToString() }, { "Time", AdminMonitor.Duration.ToString() } });

        }

        private static void Fail(Exception e)
        {
            TelemetryClient.TrackEvent("Recompute",
                new Dictionary<string, string> { { "Id", AdminMonitor.Name }, { "Phase", "End" }, { "Code", "Exception" }, { "Message", e.Message }, {"Time", AdminMonitor.Duration.ToString()} });
            AdminMonitor.CompleteTask(false, e.Message, e);
        }

        private static DanceMusicService CreateDisconnectedService()
        {
            var context = DanceMusicContext.Create();
            return new DanceMusicService(context,ApplicationUserManager.Create(null,context));
        }

        private string SecurityToken => _securityToken ?? (_securityToken = Environment.GetEnvironmentVariable("RECOMPUTEJOB_KEY"));

        private string _securityToken;
    }
}
