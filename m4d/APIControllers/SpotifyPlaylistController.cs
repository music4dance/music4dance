using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using m4d.Services;
using m4d.Utilities;
using m4dModels;

namespace m4d.APIControllers;

/// <summary>
/// API controller for Spotify playlist operations (adding songs to playlists).
/// </summary>
[ApiController]
[Route("api/spotify/playlist")]
[Authorize]
[ValidateAntiForgeryToken]
public class SpotifyPlaylistController : DanceMusicApiController
{
    private readonly SpotifyAuthService _spotifyAuthService;
    private readonly IFeatureManager _featureManager;

    public SpotifyPlaylistController(
        DanceMusicContext context,
        UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService,
        IDanceStatsManager danceStatsManager,
        IConfiguration configuration,
        ILogger<SpotifyPlaylistController> logger,
        SpotifyAuthService spotifyAuthService,
        IFeatureManager featureManager)
        : base(context, userManager, searchService, danceStatsManager, configuration, logger)
    {
        _spotifyAuthService = spotifyAuthService ?? throw new ArgumentNullException(nameof(spotifyAuthService));
        _featureManager = featureManager ?? throw new ArgumentNullException(nameof(featureManager));
    }

    /// <summary>
    /// Gets the current user's Spotify playlists.
    /// </summary>
    /// <returns>List of playlist metadata</returns>
    /// <response code="200">Returns the user's playlists</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="402">Premium subscription required</response>
    /// <response code="403">Spotify account not connected</response>
    [HttpGet("user")]
    [ProducesResponseType(typeof(IEnumerable<PlaylistMetadata>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserPlaylists()
    {
        // Validate authentication and authorization
        var authResult = await HttpContext.AuthenticateAsync();
        var applicationUser = await UserManager.GetUserAsync(User);
        var validation = await _spotifyAuthService.ValidateSpotifyAccess(User, applicationUser, authResult);

        if (!validation.IsValid)
        {
            return validation.ErrorType switch
            {
                SpotifyAuthErrorType.Unauthenticated => Unauthorized(new { message = validation.ErrorMessage }),
                SpotifyAuthErrorType.NotPremium => StatusCode(StatusCodes.Status402PaymentRequired,
                    new { message = validation.ErrorMessage, upgradeUrl = "/home/contribute" }),
                SpotifyAuthErrorType.NoSpotifyOAuth => StatusCode(StatusCodes.Status403Forbidden,
                    new { message = validation.ErrorMessage, connectUrl = "/identity/account/manage/externallogins" }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Unexpected validation error" })
            };
        }

        try
        {
            var service = MusicService.GetService(ServiceType.Spotify);
            var playlists = await MusicServiceManager.GetUserPlaylists(service, User);

            return JsonCamelCase(playlists);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving user playlists");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Unable to retrieve playlists. Please try again later." });
        }
    }

    /// <summary>
    /// Adds a song to a Spotify playlist.
    /// </summary>
    /// <param name="request">Request containing song ID and playlist ID</param>
    /// <returns>Result indicating success or failure</returns>
    /// <response code="200">Song successfully added to playlist</response>
    /// <response code="400">Invalid request (missing song ID or playlist ID)</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="402">Premium subscription required</response>
    /// <response code="403">Spotify account not connected</response>
    /// <response code="404">Song not found or not available on Spotify</response>
    /// <response code="500">Server error while adding to playlist</response>
    [HttpPost("add")]
    [ProducesResponseType(typeof(AddToPlaylistResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddTrackToPlaylist(
        [FromBody] AddToPlaylistRequest request)
    {
        // Validate request
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Invalid request", errors = ModelState });
        }

        // Validate authentication and authorization
        var authResult = await HttpContext.AuthenticateAsync();
        var applicationUser = await UserManager.GetUserAsync(User);
        var validation = await _spotifyAuthService.ValidateSpotifyAccess(User, applicationUser, authResult);

        if (!validation.IsValid)
        {
            return validation.ErrorType switch
            {
                SpotifyAuthErrorType.Unauthenticated => Unauthorized(new { message = validation.ErrorMessage }),
                SpotifyAuthErrorType.NotPremium => StatusCode(StatusCodes.Status402PaymentRequired,
                    new { message = validation.ErrorMessage, upgradeUrl = "/home/contribute" }),
                SpotifyAuthErrorType.NoSpotifyOAuth => StatusCode(StatusCodes.Status403Forbidden,
                    new { message = validation.ErrorMessage, connectUrl = "/identity/account/manage/externallogins" }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Unexpected validation error" })
            };
        }

        try
        {
            // Get the song and validate it has a Spotify track
            if (!Guid.TryParse(request.SongId, out var songGuid))
            {
                return BadRequest(AddToPlaylistResult.CreateFailure("Invalid song ID format"));
            }

            var song = await SongIndex.FindSong(songGuid);
            if (song == null)
            {
                return NotFound(AddToPlaylistResult.CreateFailure("Song not found"));
            }

            var service = MusicService.GetService(ServiceType.Spotify);
            var spotifyId = song.GetPurchaseId(ServiceType.Spotify);

            if (string.IsNullOrWhiteSpace(spotifyId))
            {
                return NotFound(AddToPlaylistResult.CreateFailure("Song not available on Spotify"));
            }

            // Add track to playlist
            var snapshotId = await MusicServiceManager.AddTrackToPlaylist(
                service, User, request.PlaylistId, spotifyId);

            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    AddToPlaylistResult.CreateFailure("Failed to add song to playlist"));
            }

            // Log activity if enabled
            if (await _featureManager.IsEnabledAsync(FeatureFlags.ActivityLogging))
            {
                var user = await Database.FindUser(User.Identity?.Name);
                var activityData = new
                {
                    songId = request.SongId,
                    playlistId = request.PlaylistId,
                    spotifyTrackId = spotifyId,
                    snapshotId
                };
                _ = Database.Context.ActivityLog.Add(
                    new ActivityLog("SpotifyAddTrack", user, activityData));
                _ = await Database.SaveChanges();
            }

            return JsonCamelCase(AddToPlaylistResult.CreateSuccess(snapshotId));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding song {SongId} to playlist {PlaylistId}",
                request.SongId, request.PlaylistId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                AddToPlaylistResult.CreateFailure("Unable to add song to playlist. Please try again later."));
        }
    }
}
