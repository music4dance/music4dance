using m4d.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
[ValidateAntiForgeryToken]
public class UsageLogController(
    DanceMusicContext context,
    UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService,
    IDanceStatsManager danceStatsManager,
    IConfiguration configuration,
    ILogger<UsageLogController> logger,
    IBackgroundTaskQueue backgroundTaskQueue) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger, backgroundTaskQueue)
{
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogBatch([FromForm] string events)
    {
        // 1. Validate input is not null/empty
        if (string.IsNullOrWhiteSpace(events))
        {
            return BadRequest("No events provided");
        }

        // 2. Deserialize events from form field
        List<UsageEventDto> eventList;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            eventList = JsonSerializer.Deserialize<List<UsageEventDto>>(events, options);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Invalid JSON format in events field");
            return BadRequest("Invalid JSON format");
        }

        // 3. Validate payload
        if (eventList == null || eventList.Count == 0)
        {
            return BadRequest("No events provided");
        }

        if (eventList.Count > 100)
        {
            return BadRequest("Batch size exceeds limit (100 events)");
        }

        // 4. Detect authenticated user
        var isAuthenticated = User?.Identity?.IsAuthenticated == true;
        var userName = isAuthenticated ? User.Identity.Name : null;

        // Get full user object if authenticated
        ApplicationUser authenticatedUser = null;
        if (isAuthenticated)
        {
            authenticatedUser = await UserManager.GetUserAsync(User);
        }

        // 5. Rate limit check (basic implementation)
        // TODO: Implement proper rate limiting using IMemoryCache

        // 6. Enqueue to background task
        TaskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) =>
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();

                var usageLogs = eventList.Select(eventDto => new UsageLog
                {
                    UsageId = eventDto.UsageId,
                    UserName = userName ?? eventDto.UserName, // Server-side auth takes precedence
                    Date = DateTimeOffset.FromUnixTimeMilliseconds(eventDto.Timestamp).UtcDateTime,
                    Page = eventDto.Page,
                    Query = eventDto.Query,
                    Filter = eventDto.Filter,
                    Referrer = eventDto.Referrer,
                    UserAgent = eventDto.UserAgent
                });

                await dbContext.UsageLog.AddRangeAsync(usageLogs, cancellationToken);

                // Update user LastActive and HitCount if authenticated
                if (authenticatedUser != null)
                {
                    // Get fresh copy from context
                    var user = await dbContext.Users.FindAsync([authenticatedUser.Id], cancellationToken);
                    if (user != null)
                    {
                        user.LastActive = DateTime.Now;
                        user.HitCount += eventList.Count;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to save usage log batch");
            }
        });

        // 7. Return 202 Accepted immediately (background processing)
        return Accepted();
    }
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
