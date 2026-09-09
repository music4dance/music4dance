using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using HtmlAgilityPack;

using m4d.PublicApi;
using m4d.Services.ServiceHealth;
using m4d.Utilities;

using m4dModels;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

using Moq;

using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore;

using Vite.AspNetCore;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.Tests.PublicApi;

internal sealed class PublicApiTestHost(WebApplication app, TestClock clock) : IAsyncDisposable
{
    public const string UserId = "public-api-test-user";
    public const string OtherUserId = "public-api-other-user";
    public const string DefaultScopes = "account:read songs:read offline_access";
    public const string ConnectedAppsPath = "/Identity/Account/Manage/ConnectedApps";
    public static readonly string Verifier = new('a', 43);
    public static readonly string Challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    public WebApplication App { get; } = app;
    public TestClock Clock { get; } = clock;
    public HttpClient Browser { get; } = new(new BrowserCookies(app.GetTestServer().CreateHandler()))
    {
        BaseAddress = new Uri("https://m4d.test")
    };

    public static async Task<PublicApiTestHost> StartAsync(bool enabled = true, int tokenLimit = 60)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PublicApiDefaults).Assembly.GetName().Name,
            EnvironmentName = Environments.Staging
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Configuration[$"FeatureManagement:{FeatureFlags.PublicApi}"] = enabled.ToString();
        builder.Configuration["PublicApi:RequestsPerMinute"] = tokenLimit.ToString();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        var clock = new TestClock();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var databaseName = $"public-api-flow-{Guid.NewGuid()}";
        builder.Services.AddDbContext<DanceMusicContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<DanceMusicContext>();
        builder.Services.ConfigureApplicationCookie(options => options.LoginPath = "/Identity/Account/Login");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(PublicApiDefaults).Assembly);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(PublicApiDefaults).Assembly);
        builder.Services.AddFeatureManagement();
        builder.Services.AddSingleton<ServiceHealthManager>();
        builder.Services.AddViteServices();
        builder.Services.Replace(ServiceDescriptor.Singleton(Mock.Of<IViteManifest>()));
        var search = new Mock<ISearchServiceManager>();
        search.Setup(service => service.GetInfo(It.IsAny<string>())).Returns(new SearchServiceInfo("Test", 1, "test", null!, null!, search.Object));
        builder.Services.AddSingleton(search.Object);
        builder.Services.AddPublicApiFoundation(builder.Configuration, builder.Environment);
        builder.Services.Configure<OpenIddictEntityFrameworkCoreOptions>(options => options.DisableBulkOperations = true);
        builder.Services.AddAuthorization(options => options.AddPolicy("test:bearer", policy =>
            policy.AddAuthenticationSchemes(PublicApiDefaults.BearerScheme).RequireAuthenticatedUser()));

        var app = builder.Build();
        app.UsePublicApiProtection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapRazorPages();
        app.MapGet("/", () => "site");
        // These endpoints exist only in this in-memory test host, never in the application.
        app.MapPost("/test/sign-in/{userId}", async (string userId, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn) =>
        {
            var user = await users.FindByIdAsync(userId);
            Assert.IsNotNull(user);
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.NoContent();
        });
        if (enabled)
        {
            app.MapMethods("/test/protected", ["GET", "POST"], (HttpContext context) => Results.Json(new
            {
                subject = context.User.GetClaim(Claims.Subject),
                scopes = context.User.GetScopes(),
                claims = context.User.Claims.Select(claim => claim.Type).ToArray()
            })).RequireAuthorization("test:bearer");
        }

        await app.StartAsync();
        var host = new PublicApiTestHost(app, clock);
        await host.AddUserAsync(UserId);
        await host.AddUserAsync(OtherUserId);
        return host;
    }

    public async Task SignInAsync(string userId = UserId)
    {
        using var response = await Browser.PostAsync($"/test/sign-in/{userId}", null);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static Dictionary<string, string> AuthorizationParameters(string scopes = DefaultScopes) => new()
    {
        [Parameters.ClientId] = PublicApiDefaults.Clients.DanzQ,
        [Parameters.RedirectUri] = PublicApiDefaults.Clients.DanzQRedirectUri,
        [Parameters.ResponseType] = ResponseTypes.Code,
        [Parameters.Scope] = scopes,
        [Parameters.CodeChallenge] = Challenge,
        [Parameters.CodeChallengeMethod] = CodeChallengeMethods.Sha256,
        [Parameters.State] = "state-with~tilde+and&equals=ü"
    };

    public async Task<HttpResponseMessage> AuthorizeAsync(Dictionary<string, string> parameters)
    {
        using var content = new FormUrlEncodedContent(parameters);
        var response = await Browser.GetAsync("/connect/authorize?" + await content.ReadAsStringAsync());
        return await FollowAuthorizationRedirectsAsync(response);
    }

    public async Task<HttpResponseMessage> FollowAuthorizationRedirectsAsync(HttpResponseMessage response)
    {
        for (var count = 0; count < 4 && response.StatusCode == HttpStatusCode.Redirect; count++)
        {
            Assert.IsNotNull(response.Headers.Location);
            var destination = new Uri(Browser.BaseAddress!, response.Headers.Location);
            if (destination.Scheme != "https" || destination.Host != "m4d.test" || destination.AbsolutePath != "/connect/authorize")
            {
                break;
            }

            response.Dispose();
            response = await Browser.GetAsync(destination);
        }

        return response;
    }

    public async Task<string> GrantCodeAsync(string scopes = DefaultScopes)
    {
        using var response = await AuthorizeAsync(AuthorizationParameters(scopes));
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            return CallbackValue(response, Parameters.Code);
        }

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        var form = await ReadFormAsync(response);
        form.Fields["decision"] = "accept";
        using var accepted = await PostFormAsync(form);
        return CallbackValue(accepted, Parameters.Code);
    }

    public Task<HttpResponseMessage> ExchangeAsync(string code, Action<Dictionary<string, string>> change = null!)
    {
        var fields = new Dictionary<string, string>
        {
            [Parameters.ClientId] = PublicApiDefaults.Clients.DanzQ,
            [Parameters.GrantType] = GrantTypes.AuthorizationCode,
            [Parameters.Code] = code,
            [Parameters.CodeVerifier] = Verifier,
            [Parameters.RedirectUri] = PublicApiDefaults.Clients.DanzQRedirectUri
        };
        change?.Invoke(fields);
        return PostAsync("/connect/token", fields);
    }

    public Task<HttpResponseMessage> RefreshAsync(string token, Action<Dictionary<string, string>> change = null!)
    {
        var fields = new Dictionary<string, string>
        {
            [Parameters.ClientId] = PublicApiDefaults.Clients.DanzQ,
            [Parameters.GrantType] = GrantTypes.RefreshToken,
            [Parameters.RefreshToken] = token
        };
        change?.Invoke(fields);
        return PostAsync("/connect/token", fields);
    }

    public async Task<TokenSet> IssueTokensAsync(string scopes = DefaultScopes)
    {
        var code = await GrantCodeAsync(scopes);
        using var response = await ExchangeAsync(code);
        return await ReadTokensAsync(response);
    }

    public static async Task<TokenSet> ReadTokensAsync(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = document.RootElement;
        Assert.IsFalse(result.TryGetProperty(Parameters.IdToken, out _));
        Assert.AreEqual("Bearer", result.GetProperty(Parameters.TokenType).GetString());
        Assert.AreEqual(3600, result.GetProperty(Parameters.ExpiresIn).GetInt32());
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        return new TokenSet(result.GetProperty(Parameters.AccessToken).GetString()!,
            result.TryGetProperty(Parameters.RefreshToken, out var refresh) ? refresh.GetString()! : string.Empty);
    }

    public async Task<HttpResponseMessage> ProtectedAsync(string accessToken = "")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/protected");
        if (accessToken.Length > 0)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await Browser.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, string> fields)
    {
        using var content = new FormUrlEncodedContent(fields);
        return await Browser.PostAsync(path, content);
    }

    public Task<HttpResponseMessage> PostFormAsync(HtmlForm form) => PostAsync(form.Action, form.Fields);

    public static async Task<HtmlForm> ReadFormAsync(HttpResponseMessage response, string selector = "//form")
    {
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        var document = new HtmlDocument();
        document.LoadHtml(await response.Content.ReadAsStringAsync());
        var form = document.DocumentNode.SelectSingleNode(selector);
        Assert.IsNotNull(form, "Expected a rendered HTML form.");
        var fields = form.SelectNodes(".//input[@name]").ToDictionary(
            node => HtmlEntity.DeEntitize(node.GetAttributeValue("name", "")),
            node => HtmlEntity.DeEntitize(node.GetAttributeValue("value", "")));
        var action = HtmlEntity.DeEntitize(form.GetAttributeValue("action", ""));
        return new HtmlForm(action.Length == 0 ? response.RequestMessage!.RequestUri!.PathAndQuery : action, fields);
    }

    public static string CallbackValue(HttpResponseMessage response, string parameter)
    {
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);
        Assert.AreEqual(PublicApiDefaults.Clients.DanzQRedirectUri, response.Headers.Location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(response.Headers.Location.Query);
        Assert.IsTrue(query.ContainsKey(parameter), $"Callback did not include {parameter}.");
        return query[parameter].ToString();
    }

    public async Task WithServicesAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = App.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public Task ChangeUserAsync(Action<ApplicationUser> change) => WithServicesAsync(async services =>
    {
        var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByIdAsync(UserId);
        Assert.IsNotNull(user);
        change(user);
        Assert.IsTrue((await manager.UpdateAsync(user)).Succeeded);
    });

    public async ValueTask DisposeAsync()
    {
        Browser.Dispose();
        await App.DisposeAsync();
    }

    private Task AddUserAsync(string id) => WithServicesAsync(async services =>
    {
        var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await manager.CreateAsync(new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = $"{id}@example.test",
            EmailConfirmed = true,
            LockoutEnabled = true,
            SubscriptionLevel = SubscriptionLevel.None
        });
        Assert.IsTrue(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
    });

    private sealed class BrowserCookies(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        private readonly CookieContainer _cookies = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var cookie = _cookies.GetCookieHeader(uri);
            if (cookie.Length > 0)
            {
                request.Headers.Add("Cookie", cookie);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                foreach (var value in values)
                {
                    _cookies.SetCookies(uri, value);
                }
            }

            return response;
        }
    }
}

internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan interval) => _now += interval;
}

internal sealed record HtmlForm(string Action, Dictionary<string, string> Fields);
internal sealed record TokenSet(string AccessToken, string RefreshToken);
