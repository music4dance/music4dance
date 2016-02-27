using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using m4d.Context;
using m4d.Utilities;
using m4dModels;
using Microsoft.ApplicationInsights;
using Microsoft.AspNet.Identity;

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
            if (authenticationHeader.Scheme != "Token" || token != SecurityToken)
            {
                var properties = new Dictionary<string, string> { { "AuthScheme", authenticationHeader.Scheme }, { "ClientToken", token }, {"ServerToken", SecurityToken} };
                var client = TelemetryClient;
                client.TrackEvent("RecomputeAuthentication", properties);

                return Unauthorized();
            }

            if (!force && !HasChanged(id))
                return Ok(new {changed = false, message = "No updates."});

            if (!AdminMonitor.StartTask(id))
            {
                return Conflict();
            }

            string message;
            bool success;
            switch (id)
            {
                case "songstats":
                    success = HandleSongStats();
                    message = "Updated song stats.";
                    break;
                case "danceinfo":
                    success = HandleDanceInfo();
                    message = "Rebuilt Dances, Dance Tags, and updated Song Counts.";
                    break;
                default:
                    return BadRequest();
            }

            if (!success) return InternalServerError(AdminMonitor.LastException);

            return Ok(new {changed = true, message});
        }

        private bool HasChanged(string id)
        {
            var updated = RecomputeMarker.GetMarker(id);
            return Database.Songs.Any(s => s.Modified > updated);
        }

        private static void Completed(string id)
        {
            RecomputeMarker.SetMarker(id);
        }

        private bool HandleSongStats()
        {
            Task.Run(() => DoHandleSongStats(CreateDisconnectedService()));
            return true;
        }

        private static bool DoHandleSongStats(DanceMusicService dms)
        {
            try
            {
                SongCounts.RebuildSongCounts(dms);
                Completed("songstats");
                AdminMonitor.CompleteTask(true, "Song Counts Successfully Rebuilt!");
                return true;
            }
            catch (Exception e)
            {
                AdminMonitor.CompleteTask(false, e.Message, e);
                return false;
            }
        }

        private bool HandleDanceInfo()
        {
            Task.Run(() => DoHandleDanceInfo(CreateDisconnectedService()));
            return true;
        }

        private static bool DoHandleDanceInfo(DanceMusicService dms)
        {
            try
            {
                dms.RebuildDanceInfo();
                Completed("danceinfo");
                AdminMonitor.CompleteTask(true, "Rebuilt Dance Info");
                return true;
            }
            catch (Exception e)
            {
                AdminMonitor.CompleteTask(false, e.Message, e);
                return false;
            }
        }

        private DanceMusicService CreateDisconnectedService()
        {
            var context = DanceMusicContext.Create();
            return new DanceMusicService(context,ApplicationUserManager.Create(null,context));
        }

        private string SecurityToken => _securityToken ?? (_securityToken = Environment.GetEnvironmentVariable("RECOMPUTEJOB_KEY"));

        private string _securityToken;
    }
}
