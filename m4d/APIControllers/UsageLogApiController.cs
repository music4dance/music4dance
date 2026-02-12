using m4d.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
[ValidateAntiForgeryToken]
public class UsageLogApiController(
    DanceMusicContext context,
    UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService,
    IDanceStatsManager danceStatsManager,
    IConfiguration configuration,
    ILogger<UsageLogApiController> logger,
    IBackgroundTaskQueue backgroundTaskQueue) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger, backgroundTaskQueue)
{
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LogBatch([FromBody] UsageLogBatchRequest request)
    {
        // 1. Validate payload
        if (request?.Events == null || request.Events.Count == 0)
        {
            return BadRequest("No events provided");
        }

        if (request.Events.Count > 100)
        {
            return BadRequest("Batch size exceeds limit (100 events)");
        }

        // 2. Detect authenticated user
        var isAuthenticated = User?.Identity?.IsAuthenticated == true;
        var userName = isAuthenticated ? User.Identity.Name : null;

        // Get full user object if authenticated
        ApplicationUser authenticatedUser = null;
        if (isAuthenticated)
        {
            authenticatedUser = await UserManager.GetUserAsync(User);
        }

        // 3. Rate limit check (basic implementation)
        // TODO: Implement proper rate limiting using IMemoryCache

        // 4. Enqueue to background task
        TaskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) =>
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();

                foreach (var eventDto in request.Events)
                {
                    var usageLog = new UsageLog
                    {
                        UsageId = eventDto.UsageId,
                        UserName = userName ?? eventDto.UserName, // Server-side auth takes precedence
                        Date = DateTimeOffset.FromUnixTimeMilliseconds(eventDto.Timestamp).DateTime,
                        Page = eventDto.Page,
                        Query = eventDto.Query,
                        Filter = eventDto.Filter,
                        Referrer = eventDto.Referrer,
                        UserAgent = eventDto.UserAgent
                    };

                    await dbContext.UsageLog.AddAsync(usageLog, cancellationToken);
                }

                // Update user LastActive and HitCount if authenticated
                if (authenticatedUser != null)
                {
                    // Get fresh copy from context
                    var user = await dbContext.Users.FindAsync([authenticatedUser.Id], cancellationToken);
                    if (user != null)
                    {
                        user.LastActive = DateTime.Now;
                        user.HitCount += request.Events.Count;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to save usage log batch");
            }
        });

        // 5. Return 202 Accepted immediately (background processing)
        return Accepted();
    }
}

public class UsageLogBatchRequest
{
    public List<UsageEventDto> Events { get; set; }
}

public class UsageEventDto
{
    [Required]
    public string UsageId { get; set; }

    [Required]
    public long Timestamp { get; set; }

    [Required]
    public string Page { get; set; }

    public string Query { get; set; }

    public string Referrer { get; set; }

    [Required]
    public string UserAgent { get; set; }

    public string Filter { get; set; }

    public string UserName { get; set; }
}
