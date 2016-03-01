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
        public IHttpActionResult Get(string id, bool force = false)
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
            switch (id)
            {
                case "songstats":
                    HandleRecompute(DoHandleSongStats);
                    message = "Updated song stats.";
                    break;
                case "danceinfo":
                    HandleRecompute(DoHandleDanceInfo);
                    message = "Rebuilt Dances, Dance Tags, and updated Song Counts.";
                    break;
                default:
                    client.TrackEvent("Recompute",
                        new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "Unknown" } });
                    return BadRequest();
            }

            client.TrackEvent("Recompute",
                new Dictionary<string, string> { { "Id", id }, { "Phase", "Start" }, { "Code", "Okay" } });

            return Ok(new {changed = true, message});
        }

        private bool HasChanged(string id)
        {
            var updated = RecomputeMarker.GetMarker(id);
            return Database.Songs.Any(s => s.Modified > updated);
        }

        private delegate void DoHandleRecompute(DanceMusicService dms);

        // TODONEXT: Should Do*** functions take name of task and message so that we can clean up duplicate code?
        //  Definitely want to send an AppInsights event and seem to have similar invoce patterns.
        private static void HandleRecompute(DoHandleRecompute recompute)
        {
            Task.Run(() => recompute.Invoke(CreateDisconnectedService()));
        }

        private static void DoHandleSongStats(DanceMusicService dms)
        {
            try
            {
                SongCounts.RebuildSongCounts(dms);
                Complete("songstats", "Song Counts Successfully Rebuilt!");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static void DoHandleDanceInfo(DanceMusicService dms)
        {
            try
            {
                dms.RebuildDanceInfo();
                Complete("danceinfo", "Rebuilt Dance Info");
            }
            catch (Exception e)
            {
                Fail(e);
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
