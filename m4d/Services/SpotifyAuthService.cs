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
    /// Same check as <see cref="CanSpotify"/>, but also reports whether a "no access" result
    /// was specifically caused by Spotify rejecting a refresh (e.g. an expired/revoked refresh
    /// token) rather than the user simply never having done the Spotify OAuth handshake this
    /// session. Use this instead of a separate <see cref="CanSpotify"/> call when the caller
    /// needs to show a distinct "reconnect" message - calling both would trigger the refresh
    /// attempt against Spotify twice.
    /// </summary>
    /// <param name="user">The current user's claims principal</param>
    /// <param name="authResult">The authentication result from HttpContext</param>
    /// <returns>Whether the user currently has Spotify access, and whether a rejection caused the lack of it</returns>
    public async Task<(bool CanSpotify, bool WasRejected)> CheckSpotifyAccess(
        ClaimsPrincipal user, AuthenticateResult authResult)
    {
        return await AdmAuthentication.CheckAccess(_configuration, ServiceType.Spotify, user, authResult);
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
    /// <param name="expired">
    /// True if the redirect is happening because a previously-working Spotify connection was
    /// rejected (expired/revoked refresh token), rather than the user never having connected -
    /// the login page shows a distinct message for this case.
    /// </param>
    /// <returns>The Spotify OAuth redirect URL</returns>
    public string GetSpotifyOAuthRedirectUrl(string returnUrl, bool expired = false)
    {
        var url = $"/Identity/Account/Login?provider=Spotify&returnUrl={returnUrl}";
        return expired ? $"{url}&reason=expired" : url;
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
        var (canSpotify, wasRejected) = await CheckSpotifyAccess(user, authResult);
        if (!canSpotify)
        {
            return SpotifyAuthValidationResult.NoSpotifyOAuth(wasRejected);
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

    /// <summary>
    /// True when <see cref="SpotifyAuthErrorType.NoSpotifyOAuth"/> was caused by Spotify
    /// rejecting a refresh (expired/revoked refresh token) rather than the user simply never
    /// having connected Spotify - callers can use this to show a "reconnect" message instead of
    /// a "connect" message.
    /// </summary>
    public bool ReauthRequired { get; private set; }

    private SpotifyAuthValidationResult(bool isValid, SpotifyAuthErrorType errorType, string errorMessage = null,
        bool reauthRequired = false)
    {
        IsValid = isValid;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
        ReauthRequired = reauthRequired;
    }

    public static SpotifyAuthValidationResult Success() =>
        new(true, SpotifyAuthErrorType.None);

    public static SpotifyAuthValidationResult Unauthenticated() =>
        new(false, SpotifyAuthErrorType.Unauthenticated, "User is not authenticated");

    public static SpotifyAuthValidationResult NotPremium() =>
        new(false, SpotifyAuthErrorType.NotPremium, "Premium subscription required");

    public static SpotifyAuthValidationResult NoSpotifyOAuth(bool reauthRequired = false) =>
        new(false, SpotifyAuthErrorType.NoSpotifyOAuth,
            reauthRequired
                ? "Your Spotify connection has expired. Please reconnect your account."
                : "Spotify account not connected",
            reauthRequired);
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
