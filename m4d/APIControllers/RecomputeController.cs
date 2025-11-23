using m4d.Utilities;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace m4d.APIControllers;

//public class RecomputeInfo
//{
//    public bool Changed { get; set; }
//    public string Message { get; set; }
//}
[ApiController]
[Route("api/[controller]")]
public class RecomputeController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<RecomputeController> logger) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{

    // id should be the type to update - currently songstats, propertycleanup
    [HttpGet("{id}")]
    public async Task<IActionResult> Get([FromServices] IServiceScopeFactory serviceScopeFactory, string id)
    {
        if (!TokenRequirement.Authorize(Request, Configuration))
        {
            return Unauthorized();
        }

        if (!AdminMonitor.StartTask(id))
        {
            return Conflict();
        }


        var rgid = id.Split('-');
        string message;

        switch (rgid[0])
        {
            case "songstats":
                message = await DoHandleSongStats(serviceScopeFactory, Database.GetTransientService());
                break;
            case "subscription":
                message = await DoHandleSubscriptions(serviceScopeFactory);
                break;
            default:
                AdminMonitor.CompleteTask(false, $"Bad Id: {id}");
                return BadRequest();
        }


        Logger.LogInformation($"RecomputeController: id = {id}, changed = true, message = {message}");
        return Ok(new { changed = true, message });
    }

    private static async Task<string> DoHandleSongStats(IServiceScopeFactory serviceScopeFactory, DanceMusicCoreService dms)
    {
        var message = "Clear Song Stats Cache";
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dsm = scope.ServiceProvider.GetRequiredService<IDanceStatsManager>();
            await dsm.ClearCache(dms, true);
            Complete(message);
        }
        catch (Exception e)
        {
            Fail(e);
            message = $"Failed to {message}";
        }

        return message;
    }

    private async Task<string> DoHandleSubscriptions(IServiceScopeFactory serviceScopeFactory)
    {
        var message = "Updated Subscriptions.";
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
            var userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var expired = context.Users.Where(
                u => u.SubscriptionEnd.HasValue && u.SubscriptionEnd < DateTime.Now);

            foreach (var user in await expired.ToListAsync())
            {
                if (await userManager.IsInRoleAsync(user, DanceMusicCoreService.PremiumRole))
                {
                    _ = await userManager.RemoveFromRoleAsync(user, DanceMusicCoreService.PremiumRole);
                    Logger.LogInformation($"Remove: {user.UserName}");
                }
            }
            Complete("Updated Subscriptions.");
        }
        catch (Exception e)
        {
            Fail(e);
            message = $"Failed to {message}";
        }

        return message;
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
