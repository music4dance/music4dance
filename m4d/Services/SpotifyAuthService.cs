using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using m4dModels;
using m4d.Utilities;
using System.Security.Claims;

namespace m4d.Services;

/// <summary>
/// Service for handling Spotify authentication, authorization, and user validation.
/// Provides reusable methods for checking Spotify OAuth status, premium subscription,
/// and generating OAuth redirect URLs.
/// </summary>
public class SpotifyAuthService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public SpotifyAuthService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    /// <summary>
    /// Checks if the current user has Spotify OAuth access configured.
    /// </summary>
    /// <param name="user">The current user's claims principal</param>
    /// <param name="authResult">The authentication result from HttpContext</param>
    /// <returns>True if user has Spotify OAuth access, false otherwise</returns>
    public async Task<bool> CanSpotify(ClaimsPrincipal user, AuthenticateResult authResult)
    {
        return await AdmAuthentication.HasAccess(_configuration, ServiceType.Spotify, user, authResult);
    }

    /// <summary>
    /// Checks if the current user has a premium or trial subscription.
    /// </summary>
    /// <param name="user">The current user's claims principal</param>
    /// <returns>True if user has premium or trial role, false otherwise</returns>
    public bool IsPremium(ClaimsPrincipal user)
    {
        if (user == null) return false;
        return user.IsInRole(DanceMusicCoreService.PremiumRole) ||
               user.IsInRole(DanceMusicCoreService.TrialRole);
    }

    /// <summary>
    /// Gets the Spotify provider key (OAuth access token) for the given user.
    /// </summary>
    /// <param name="userName">The user's username</param>
    /// <returns>The Spotify provider key, or null if not found</returns>
    public async Task<string> GetSpotifyLoginKey(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return null;

        var user = await _userManager.FindByNameAsync(userName);
        if (user == null)
            return null;

        var services = await _userManager.GetLoginsAsync(user);
        return services.FirstOrDefault(s => s.LoginProvider == "Spotify")?.ProviderKey;
    }

    /// <summary>
    /// Checks if the user has a Spotify login associated with their account.
    /// </summary>
    /// <param name="user">The application user</param>
    /// <returns>True if user has Spotify login, false otherwise</returns>
    public async Task<bool> HasSpotifyLogin(ApplicationUser user)
    {
        if (user == null)
            return false;

        var logins = await _userManager.GetLoginsAsync(user);
        return logins.Any(l => l.LoginProvider == "Spotify");
    }

    /// <summary>
    /// Generates the OAuth redirect URL for Spotify authentication.
    /// </summary>
    /// <param name="returnUrl">The URL to return to after OAuth completion</param>
    /// <returns>The Spotify OAuth redirect URL</returns>
    public string GetSpotifyOAuthRedirectUrl(string returnUrl)
    {
        return $"/Identity/Account/Login?provider=Spotify&returnUrl={returnUrl}";
    }

    /// <summary>
    /// Gets the user's subscription level.
    /// </summary>
    /// <param name="user">The application user</param>
    /// <returns>The user's subscription level, or None if user is null</returns>
    public SubscriptionLevel GetSubscriptionLevel(ApplicationUser user)
    {
        return user?.SubscriptionLevel ?? SubscriptionLevel.None;
    }

    /// <summary>
    /// Validates that the user meets all requirements for Spotify operations
    /// (authenticated, premium, and has Spotify OAuth).
    /// </summary>
    /// <param name="user">The current user's claims principal</param>
    /// <param name="applicationUser">The application user entity</param>
    /// <param name="authResult">The authentication result from HttpContext</param>
    /// <returns>Validation result with success status and error message if applicable</returns>
    public async Task<SpotifyAuthValidationResult> ValidateSpotifyAccess(
        ClaimsPrincipal user,
        ApplicationUser applicationUser,
        AuthenticateResult authResult)
    {
        // Check authentication
        if (user?.Identity?.IsAuthenticated != true)
        {
            return SpotifyAuthValidationResult.Unauthenticated();
        }

        // Check premium status
        if (!IsPremium(user))
        {
            return SpotifyAuthValidationResult.NotPremium();
        }

        // Check Spotify OAuth
        if (!await CanSpotify(user, authResult))
        {
            return SpotifyAuthValidationResult.NoSpotifyOAuth();
        }

        return SpotifyAuthValidationResult.Success();
    }
}

/// <summary>
/// Result of Spotify authentication/authorization validation.
/// </summary>
public class SpotifyAuthValidationResult
{
    public bool IsValid { get; private set; }
    public string ErrorMessage { get; private set; }
    public SpotifyAuthErrorType ErrorType { get; private set; }

    private SpotifyAuthValidationResult(bool isValid, SpotifyAuthErrorType errorType, string errorMessage = null)
    {
        IsValid = isValid;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    public static SpotifyAuthValidationResult Success() =>
        new(true, SpotifyAuthErrorType.None);

    public static SpotifyAuthValidationResult Unauthenticated() =>
        new(false, SpotifyAuthErrorType.Unauthenticated, "User is not authenticated");

    public static SpotifyAuthValidationResult NotPremium() =>
        new(false, SpotifyAuthErrorType.NotPremium, "Premium subscription required");

    public static SpotifyAuthValidationResult NoSpotifyOAuth() =>
        new(false, SpotifyAuthErrorType.NoSpotifyOAuth, "Spotify account not connected");
}

/// <summary>
/// Types of errors that can occur during Spotify auth validation.
/// </summary>
public enum SpotifyAuthErrorType
{
    None,
    Unauthenticated,
    NotPremium,
    NoSpotifyOAuth
}
