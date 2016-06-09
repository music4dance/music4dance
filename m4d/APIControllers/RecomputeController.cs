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
                case "danceinfo":
                    recompute = DoHandleDanceInfo;
                    message = "Rebuilt Dances, Dance Tags, and updated Song Counts.";
                    break;
                case "propertycleanup":
                    recompute = DoHandlePropertyCleanup;
                    message = "Cleaned up properties";
                    break;
                case "indexsongs":
                    recompute = DoHandleSongIndex;
                    message = "Updated song index";
                    break;
                case "tagtypes":
                    recompute = DoHandleTagTypes;
                    message = "Updated {0} tag types";
                    break;
                default:
                    client.TrackEvent("Recompute",
                        new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "Unknown" } });
                    AdminMonitor.CompleteTask(false, $"Bad Id: {id}");
                    return BadRequest();
            }

            client.TrackEvent("Recompute",
                new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "Okay" } });

            if (sync)
            {
                HandleSyncRecompute(recompute, id, message, force);
            }
            else
            {
                HandleRecompute(recompute, id, message, force);
            }

            return Ok(new {changed = true, message});
        }

        private bool HasChanged(string id)
        {
            var updated = RecomputeMarker.GetMarker(id);
            return Database.Songs.Any(s => s.Modified > updated);
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
                    TelemetryClient.TrackEvent("Recompute",
                        new Dictionary<string, string> { { "Id", id }, { "Phase", "Iteration" }, { "Code", "Conflict" }, { "Task", AdminMonitor.Name }, { "Iteration", i[0].ToString() } });
                    return;
                }

                TelemetryClient.TrackEvent("Recompute",
                    new Dictionary<string, string> { { "Id", id }, { "Phase", "Iteration" }, { "Code", "Pending" }, { "Task", AdminMonitor.Name }, { "Iteration", i[0].ToString() } });

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

        private static bool DoHandleDanceInfo(DanceMusicService dms, string id, string message, int iteration, bool force)
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

        private static bool DoHandleTagTypes(DanceMusicService dms, string id, string message, int iteration, bool force)
        {
            try
            {
                var count = dms.RebuildTagTypes(true);
                Complete(id, string.Format(message,count));
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

        private static bool DoHandleSongIndex(DanceMusicService dms, string id, string message, int iteration, bool force)
        {
            try
            {
                var rgid = id.Split('-');
                var name = rgid.Length > 1 ? rgid[1] : "default";

                if (force && iteration == 0)
                {
                    dms.ResetIndex(name);
                }

                var from = RecomputeMarker.GetMarker(id);

                var info = dms.IndexSongs(250, from, force, new SongFilter(), name);

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

        private string SecurityToken => _securityToken ?? (_securityToken = Environment.GetEnvironmentVariable("RECOMPUTEJOB_KEY"));

        private string _securityToken;
    }
}
