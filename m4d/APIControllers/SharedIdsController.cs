using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

/// <summary>
/// The shared-id review's JSON actions, posted by the shared-id-review page app. The page itself
/// and the background tasks are on m4d.Controllers.SharedIdsController. See
/// architecture/songs/shared-id-review.md.
/// </summary>
[ApiController]
[Authorize(Roles = "dbAdmin")]
[ValidateAntiForgeryToken]
[Route("api/sharedids")]
public class SharedIdsController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<SharedIdsController> logger,
    ServiceHealthManager serviceHealth, SharedIdPreviewStore previews)
    : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration,
        logger, serviceHealth: serviceHealth)
{
    //
    // POST: /api/sharedids/apply
    // Applies the choice made for each cluster on a page of the queue: merge its groups of songs,
    // not the same, or leave for later, after any removals
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] SharedIdApplyRequest request)
    {
        var user = await Database.FindUser(User.Identity?.Name);
        if (user == null)
        {
            return Forbid();
        }

        var choices = (request?.Choices ?? []).Where(c => c != null).Select(c => c.ToChoice()).ToList();
        var result = await Database.SharedIds.Apply(user, choices);
        foreach (var (from, into) in result.Merges)
        {
            previews.Merged(from, into);
        }
        // Left for later goes to the end of the queue, so the next load of the page is new clusters
        previews.Defer(choices.Where(c => c.Action == SharedIdAction.Leave).SelectMany(c => c.SongIds));
        if (result.Merged > 0 || result.Edited > 0 || result.Deleted > 0)
        {
            await DanceStatsManager.ClearCache(Database, false);
        }

        Logger.LogInformation("Shared-id apply by {User}: {Summary}", user.UserName, result.Summary());
        return JsonCamelCase(SharedIdApplyResponse.Build(result));
    }

    //
    // POST: /api/sharedids/tracks
    // What Spotify and iTunes say each of the ids is, for telling a cluster's songs apart
    [HttpPost("tracks")]
    public async Task<IActionResult> Tracks([FromBody] SharedIdTracksRequest request) =>
        JsonCamelCase(await SharedIdServiceTrack.Lookup(request?.Ids, MusicServiceManager.GetMusicServiceTrack));

    //
    // POST: /api/sharedids/owners
    // Which of a cluster's songs each id they share belongs to, looked up on Spotify and iTunes,
    // and the removals that would give each id to its song
    [HttpPost("owners")]
    public async Task<IActionResult> Owners([FromBody] SharedIdOwnersRequest request)
    {
        var songIds = (request?.SongIds ?? []).Take(SharedIdOwnersRequest.MaxSongs).ToList();
        var ownership = await Database.SharedIds.ProposeOwners(songIds, TrackLookup());
        return JsonCamelCase(SharedIdOwnersResponse.Build(ownership));
    }

    //
    // POST: /api/sharedids/artists
    // The cluster's songs' artists combined, for Merge artist (checked against the songs'
    // Spotify and iTunes tracks) and Force merge artist (force, not checked)
    [HttpPost("artists")]
    public async Task<IActionResult> Artists([FromBody] SharedIdArtistsRequest request)
    {
        var songIds = (request?.SongIds ?? []).Take(SharedIdOwnersRequest.MaxSongs).ToList();
        var combined = await Database.SharedIds.CombineArtists(songIds, TrackLookup(), request?.Force == true);
        return JsonCamelCase(SharedIdArtistsResponse.Build(combined));
    }

    // An extended id ("S:…", "I:…") to its track, through MusicServiceManager's track cache
    private Func<string, Task<ServiceTrack>> TrackLookup()
    {
        var manager = MusicServiceManager;
        return id => id is { Length: > 2 } && id[1] == ':' && MusicService.GetService(id[0]) is { } service
            ? manager.GetMusicServiceTrack(id[2..], service)
            : Task.FromResult<ServiceTrack>(null);
    }
}
