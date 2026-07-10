using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Services;
using m4dModels;
using System.Security.Claims;

namespace m4d.Tests.Services;

[TestClass]
public class SpotifyAuthServiceTests
{
    private Mock<IConfiguration> _mockConfiguration = null!;
    private Mock<UserManager<ApplicationUser>> _mockUserManager = null!;
    private SpotifyAuthService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup UserManager mock (requires complex setup due to constructor)
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        _service = new SpotifyAuthService(_mockConfiguration.Object, _mockUserManager.Object);
    }

    #region Constructor Tests

    [TestMethod]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null).Object;
#pragma warning restore CS8625

        Assert.ThrowsExactly<ArgumentNullException>(() => new SpotifyAuthService(null!, userManager));
    }

    [TestMethod]
    public void Constructor_NullUserManager_ThrowsArgumentNullException()
    {
        var config = new Mock<IConfiguration>().Object;
        Assert.ThrowsExactly<ArgumentNullException>(() => new SpotifyAuthService(config, null!));
    }

    #endregion

    #region IsPremium Tests

    [TestMethod]
    public void IsPremium_NullUser_ReturnsFalse()
    {
        var result = _service.IsPremium(null!);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsPremium_UserWithPremiumRole_ReturnsTrue()
    {
        var user = CreateUserWithRole(DanceMusicCoreService.PremiumRole);
        var result = _service.IsPremium(user);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsPremium_UserWithTrialRole_ReturnsTrue()
    {
        var user = CreateUserWithRole(DanceMusicCoreService.TrialRole);
        var result = _service.IsPremium(user);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsPremium_UserWithoutPremiumOrTrial_ReturnsFalse()
    {
        var user = CreateUserWithRole("basic");
        var result = _service.IsPremium(user);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsPremium_UserWithNoRoles_ReturnsFalse()
    {
        var claims = new List<Claim>();
        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);

        var result = _service.IsPremium(user);
        Assert.IsFalse(result);
    }

    #endregion

    #region GetSpotifyLoginKey Tests

    [TestMethod]
    public async Task GetSpotifyLoginKey_NullUserName_ReturnsNull()
    {
        var result = await _service.GetSpotifyLoginKey(null);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSpotifyLoginKey_EmptyUserName_ReturnsNull()
    {
        var result = await _service.GetSpotifyLoginKey("");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSpotifyLoginKey_WhitespaceUserName_ReturnsNull()
    {
        var result = await _service.GetSpotifyLoginKey("   ");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSpotifyLoginKey_UserNotFound_ReturnsNull()
    {
        _mockUserManager.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.GetSpotifyLoginKey("testuser");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSpotifyLoginKey_UserHasSpotifyLogin_ReturnsProviderKey()
    {
        var user = new ApplicationUser { UserName = "testuser" };
        var logins = new List<UserLoginInfo>
        {
            new UserLoginInfo("Spotify", "spotify-key-123", "Spotify")
        };

        _mockUserManager.Setup(m => m.FindByNameAsync("testuser"))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetLoginsAsync(user))
            .ReturnsAsync(logins);

        var result = await _service.GetSpotifyLoginKey("testuser");
        Assert.AreEqual("spotify-key-123", result);
    }

    [TestMethod]
    public async Task GetSpotifyLoginKey_UserHasNoSpotifyLogin_ReturnsNull()
    {
        var user = new ApplicationUser { UserName = "testuser" };
        var logins = new List<UserLoginInfo>
        {
            new UserLoginInfo("Google", "google-key-123", "Google")
        };

        _mockUserManager.Setup(m => m.FindByNameAsync("testuser"))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetLoginsAsync(user))
            .ReturnsAsync(logins);

        var result = await _service.GetSpotifyLoginKey("testuser");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSpotifyLoginKey_UserHasMultipleLogins_ReturnsSpotifyKey()
    {
        var user = new ApplicationUser { UserName = "testuser" };
        var logins = new List<UserLoginInfo>
        {
            new UserLoginInfo("Google", "google-key-123", "Google"),
            new UserLoginInfo("Spotify", "spotify-key-456", "Spotify"),
            new UserLoginInfo("Facebook", "fb-key-789", "Facebook")
        };

        _mockUserManager.Setup(m => m.FindByNameAsync("testuser"))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetLoginsAsync(user))
            .ReturnsAsync(logins);

        var result = await _service.GetSpotifyLoginKey("testuser");
        Assert.AreEqual("spotify-key-456", result);
    }

    #endregion

    #region HasSpotifyLogin Tests

    [TestMethod]
    public async Task HasSpotifyLogin_NullUser_ReturnsFalse()
    {
        var result = await _service.HasSpotifyLogin(null);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasSpotifyLogin_UserWithSpotifyLogin_ReturnsTrue()
    {
        var user = new ApplicationUser { UserName = "testuser" };
        var logins = new List<UserLoginInfo>
        {
            new UserLoginInfo("Spotify", "spotify-key-123", "Spotify")
        };

        _mockUserManager.Setup(m => m.GetLoginsAsync(user))
            .ReturnsAsync(logins);

        var result = await _service.HasSpotifyLogin(user);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HasSpotifyLogin_UserWithoutSpotifyLogin_ReturnsFalse()
    {
        var user = new ApplicationUser { UserName = "testuser" };
        var logins = new List<UserLoginInfo>
        {
            new UserLoginInfo("Google", "google-key-123", "Google")
        };

        _mockUserManager.Setup(m => m.GetLoginsAsync(user))
            .ReturnsAsync(logins);

        var result = await _service.HasSpotifyLogin(user);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasSpotifyLogin_UserWithNoLogins_ReturnsFalse()
    {
        var user = new ApplicationUser { UserName = "testuser" };
        var logins = new List<UserLoginInfo>();

        _mockUserManager.Setup(m => m.GetLoginsAsync(user))
            .ReturnsAsync(logins);

        var result = await _service.HasSpotifyLogin(user);
        Assert.IsFalse(result);
    }

    #endregion

    #region GetSpotifyOAuthRedirectUrl Tests

    // The exact percent-encoding is an implementation detail (QueryHelpers.AddQueryString) -
    // these assert on the decoded query values instead of a hardcoded encoded string, so they
    // don't need updating if the encoding style ever changes.
    private static Dictionary<string, string> ParseQuery(string url)
    {
        var queryString = url[url.IndexOf('?')..];
        return QueryHelpers.ParseQuery(queryString)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
    }

    [TestMethod]
    public void GetSpotifyOAuthRedirectUrl_ValidReturnUrl_ReturnsCorrectUrl()
    {
        var returnUrl = "/Song/CreateSpotify";
        var result = _service.GetSpotifyOAuthRedirectUrl(returnUrl);

        Assert.IsTrue(result.StartsWith("/Identity/Account/Login?"));
        var query = ParseQuery(result);
        Assert.AreEqual("Spotify", query["provider"]);
        Assert.AreEqual(returnUrl, query["returnUrl"]);
        Assert.IsFalse(query.ContainsKey("reason"));
    }

    [TestMethod]
    public void GetSpotifyOAuthRedirectUrl_EncodedReturnUrl_PreservesEncoding()
    {
        var returnUrl = "/Song/Details?id=123&filter=cha";
        var result = _service.GetSpotifyOAuthRedirectUrl(returnUrl);

        // The '?' and '&' from returnUrl must round-trip through decoding intact - not be
        // parsed as extra top-level query parameters (which would mean they leaked in unescaped).
        var query = ParseQuery(result);
        Assert.AreEqual(2, query.Count);
        Assert.AreEqual("Spotify", query["provider"]);
        Assert.AreEqual(returnUrl, query["returnUrl"]);
    }

    [TestMethod]
    public void GetSpotifyOAuthRedirectUrl_EmptyReturnUrl_ReturnsValidUrl()
    {
        var result = _service.GetSpotifyOAuthRedirectUrl("");
        var query = ParseQuery(result);
        Assert.AreEqual("Spotify", query["provider"]);
        Assert.AreEqual("", query["returnUrl"]);
    }

    [TestMethod]
    public void GetSpotifyOAuthRedirectUrl_Expired_AppendsReasonExpired()
    {
        var result = _service.GetSpotifyOAuthRedirectUrl("/Song/CreateSpotify", expired: true);
        var query = ParseQuery(result);
        Assert.AreEqual("expired", query["reason"]);
    }

    [TestMethod]
    public void GetSpotifyOAuthRedirectUrl_NotExpired_OmitsReason()
    {
        var result = _service.GetSpotifyOAuthRedirectUrl("/Song/CreateSpotify", expired: false);
        var query = ParseQuery(result);
        Assert.IsFalse(query.ContainsKey("reason"));
    }

    [TestMethod]
    public void GetSpotifyOAuthRedirectUrl_ReturnUrlWithHtmlAndQuoteCharacters_IsFullyEscaped()
    {
        // Guards against HTML/attribute injection when this URL is later interpolated into an
        // href attribute and rendered via Html.Raw (SongController.CreateSpotify) - none of
        // these characters should survive unescaped in the returned URL.
        var returnUrl = "/Song/CreateSpotify?x=\"><script>alert(1)</script>&y='";
        var result = _service.GetSpotifyOAuthRedirectUrl(returnUrl);

        foreach (var dangerous in new[] { "\"", "'", "<", ">" })
        {
            Assert.IsFalse(result.Contains(dangerous), $"Result should not contain unescaped '{dangerous}': {result}");
        }

        var query = ParseQuery(result);
        Assert.AreEqual(returnUrl, query["returnUrl"]);
    }

    #endregion

    #region GetSubscriptionLevel Tests

    [TestMethod]
    public void GetSubscriptionLevel_NullUser_ReturnsNone()
    {
        var result = _service.GetSubscriptionLevel(null);
        Assert.AreEqual(SubscriptionLevel.None, result);
    }

    [TestMethod]
    public void GetSubscriptionLevel_UserWithGoldLevel_ReturnsGold()
    {
        var user = new ApplicationUser
        {
            UserName = "testuser",
            SubscriptionLevel = SubscriptionLevel.Gold
        };

        var result = _service.GetSubscriptionLevel(user);
        Assert.AreEqual(SubscriptionLevel.Gold, result);
    }

    [TestMethod]
    public void GetSubscriptionLevel_UserWithTrialLevel_ReturnsTrial()
    {
        var user = new ApplicationUser
        {
            UserName = "testuser",
            SubscriptionLevel = SubscriptionLevel.Trial
        };

        var result = _service.GetSubscriptionLevel(user);
        Assert.AreEqual(SubscriptionLevel.Trial, result);
    }

    [TestMethod]
    public void GetSubscriptionLevel_UserWithNoneLevel_ReturnsNone()
    {
        var user = new ApplicationUser
        {
            UserName = "testuser",
            SubscriptionLevel = SubscriptionLevel.None
        };

        var result = _service.GetSubscriptionLevel(user);
        Assert.AreEqual(SubscriptionLevel.None, result);
    }

    #endregion

    #region ValidateSpotifyAccess Tests

    [TestMethod]
    public async Task ValidateSpotifyAccess_UnauthenticatedUser_ReturnsUnauthenticatedError()
    {
        var user = new ClaimsPrincipal(); // No identity
        var appUser = new ApplicationUser();
        var authResult = AuthenticateResult.NoResult();

        var result = await _service.ValidateSpotifyAccess(user, appUser, authResult);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.Unauthenticated, result.ErrorType);
        Assert.AreEqual("User is not authenticated", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateSpotifyAccess_NullUser_ReturnsUnauthenticatedError()
    {
        var appUser = new ApplicationUser();
        var authResult = AuthenticateResult.NoResult();

        var result = await _service.ValidateSpotifyAccess(null!, appUser, authResult);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.Unauthenticated, result.ErrorType);
    }

    [TestMethod]
    public async Task ValidateSpotifyAccess_NonPremiumUser_ReturnsNotPremiumError()
    {
        var user = CreateAuthenticatedUserWithRole("basic");
        var appUser = new ApplicationUser();
        var authResult = AuthenticateResult.NoResult();

        var result = await _service.ValidateSpotifyAccess(user, appUser, authResult);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.NotPremium, result.ErrorType);
        Assert.AreEqual("Premium subscription required", result.ErrorMessage);
    }

    // Note: Testing CanSpotify requires mocking AdmAuthentication.HasAccess which is a static method
    // This would require refactoring AdmAuthentication or using a wrapper interface
    // For now, we'll mark this as a limitation and test the other validation paths

    #endregion

    #region SpotifyAuthValidationResult Tests

    [TestMethod]
    public void SpotifyAuthValidationResult_Success_HasCorrectProperties()
    {
        var result = SpotifyAuthValidationResult.Success();

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.None, result.ErrorType);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void SpotifyAuthValidationResult_Unauthenticated_HasCorrectProperties()
    {
        var result = SpotifyAuthValidationResult.Unauthenticated();

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.Unauthenticated, result.ErrorType);
        Assert.AreEqual("User is not authenticated", result.ErrorMessage);
    }

    [TestMethod]
    public void SpotifyAuthValidationResult_NotPremium_HasCorrectProperties()
    {
        var result = SpotifyAuthValidationResult.NotPremium();

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.NotPremium, result.ErrorType);
        Assert.AreEqual("Premium subscription required", result.ErrorMessage);
    }

    [TestMethod]
    public void SpotifyAuthValidationResult_NoSpotifyOAuth_HasCorrectProperties()
    {
        var result = SpotifyAuthValidationResult.NoSpotifyOAuth();

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.NoSpotifyOAuth, result.ErrorType);
        Assert.AreEqual("Spotify account not connected", result.ErrorMessage);
        Assert.IsFalse(result.ReauthRequired);
    }

    [TestMethod]
    public void SpotifyAuthValidationResult_NoSpotifyOAuthReauthRequired_HasCorrectProperties()
    {
        var result = SpotifyAuthValidationResult.NoSpotifyOAuth(reauthRequired: true);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpotifyAuthErrorType.NoSpotifyOAuth, result.ErrorType);
        Assert.AreEqual("Your Spotify connection has expired. Please reconnect your account.", result.ErrorMessage);
        Assert.IsTrue(result.ReauthRequired);
    }

    #endregion

    #region Helper Methods

    private ClaimsPrincipal CreateUserWithRole(string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private ClaimsPrincipal CreateAuthenticatedUserWithRole(string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    #endregion
}
