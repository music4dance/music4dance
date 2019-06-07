using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using m4d.Context;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Host.SystemWeb;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Facebook;
using Microsoft.Owin.Security.Google;
using Microsoft.Owin.Security.MicrosoftAccount;
using Newtonsoft.Json.Linq;
using Owin;
using Owin.Security.Providers.Spotify;
using Owin.Security.Providers.Spotify.Provider;

namespace m4d
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"Start Time = {SpiderManager.StartTime}");
            // Configure the db context, user manager and signin manager to use a single instance per request
            app.CreatePerOwinContext(DanceMusicContext.Create);
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);

            // Enable the application to use a cookie to store information for the signed in user
            // and to use a cookie to temporarily store information about a user logging in with a third party login provider
            // Configure the sign in cookie
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/account/signin"),
                CookieManager = new SystemWebChunkingCookieManager(),
                Provider = new CookieAuthenticationProvider
                {
                    // Enables the application to validate the security stamp when the user logs in.
                    // This is a security feature which is used when you change a password or add an external login to your account.  
                    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser>(
                        validateInterval: TimeSpan.FromMinutes(30),
                        regenerateIdentity: (manager, user) => user.GenerateUserIdentityAsync(manager))
                }
            });

            // Use a cookie to temporarily store information about a user logging in with a third party login provider
            app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

            // Enables the application to temporarily store user information when they are verifying the second factor in the two-factor authentication process.
            app.UseTwoFactorSignInCookie(DefaultAuthenticationTypes.TwoFactorCookie, TimeSpan.FromMinutes(5));

            // Enables the application to remember the second login verification factor such as phone or email.
            // Once you check this option, your second step of verification during the login process will be remembered on the device where you logged in from.
            // This is similar to the RememberMe option when you sign in.
            app.UseTwoFactorRememberBrowserCookie(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie);

            // Uncomment the following lines to enable logging in with third party login providers
            var msenv = new EnvAuthentication("ms");
            var ms = new MicrosoftAccountAuthenticationOptions
            {
                ClientId = msenv.ClientId,
                ClientSecret = msenv.ClientSecret
            };
            //ms.Scope.Add("wl.basic");
            ms.Scope.Add("https://graph.microsoft.com/.default");
            app.UseMicrosoftAccountAuthentication(ms);


            //app.UseTwitterAuthentication(
            //   consumerKey: "",
            //   consumerSecret: "");

            var fbenv = new EnvAuthentication("fb");
            var fb = new FacebookAuthenticationOptions
            {
                AppId = fbenv.ClientId, 
                AppSecret = fbenv.ClientSecret
            };
            fb.Scope.Add("email");
            fb.SignInAsAuthenticationType = DefaultAuthenticationTypes.ExternalCookie;
            app.UseFacebookAuthentication(fb);
            
            var ggenv = new EnvAuthentication("goog");
            var gg = new GoogleOAuth2AuthenticationOptions
            {
                ClientId = ggenv.ClientId,
                ClientSecret = ggenv.ClientSecret
            };
            app.UseGoogleAuthentication(gg);

            var spenv = new EnvAuthentication("spot");
            var sp = new SpotifyAuthenticationOptions
            {
                ClientId = spenv.ClientId,
                ClientSecret = spenv.ClientSecret,
                Provider = new SpotifyAuthenticationProvider
                {
                    OnAuthenticated = async context =>
                    {
                        var dmc = DanceMusicContext.Create();
                        var userManager = ApplicationUserManager.Create(null, dmc);

                        var id = GetClaimValue(context.User, "id");

                        Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"Spotify Authentication for {id}: Enter");

                        if (string.IsNullOrWhiteSpace(id)) return;
                        var email = GetClaimValue(context.User, "email");

                        var user = dmc.Users.FirstOrDefault(u => u.Logins.Any(l => l.LoginProvider == "Spotify" && l.ProviderKey == id)) ??
                                   await userManager.FindByEmailAsync(email);

                        if (user == null)
                        {
                            var userName = $"{id}@spotify.music4dance.net";

                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"Spotify Authentication for {id}: Creating Temporary User {userName}");

                            var result = await userManager.CreateAsync(new ApplicationUser
                            {
                                UserName = userName,
                                Email = email,
                                EmailConfirmed = true,
                                CanContact = ContactStatus.Default
                            });

                            if (!result.Succeeded)
                            {
                                Trace.WriteLineIf(TraceLevels.General.TraceError,$"Failed to create Spotify user {userName}");
                                return;
                            }

                            user = await userManager.FindByNameAsync(userName);
                            if (user == null)
                            {
                                Trace.WriteLineIf(TraceLevels.General.TraceError,$"Failed to find spotify user {userName}");
                                return;
                            }
                        }
                        else
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"Spotify Authentication for {id}: Existing User {user.UserName}");
                        }

                        var accessToken = context.AccessToken;
                        var refreshToken = context.RefreshToken;
                        var timeout = context.ExpiresIn;

                        var oldClaims = await userManager.GetClaimsAsync(user.Id);

                        await AddClaimToUser(userManager, user, "urn:spotify:access_token", accessToken, "Spotify", oldClaims);
                        await AddClaimToUser(userManager, user, "urn:spotify:refresh_token", refreshToken, "Spotify", oldClaims);
                        await AddClaimToUser(userManager, user, "urn:spotify:expires_in", timeout.ToString(), "Spotify", oldClaims);
                        await AddClaimToUser(userManager, user, "urn:spotify:start_time", DateTime.Now.ToString(CultureInfo.InvariantCulture), "Spotify", oldClaims);

                        foreach (var x in context.User)
                        {
                            var claimType = $"urn:spotify:{x.Key}";
                            var claimValue = x.Value.ToString();
                            if (!context.Identity.HasClaim(claimType, claimValue))
                            {
                                await AddClaimToUser(userManager, user, claimType, claimValue, "Spotify", oldClaims);
                            }
                        }

                        Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"Spotify Authentication for {id}: Exit");
                    }
                }
            };
            sp.Scope.Add("user-read-email");
            sp.Scope.Add("playlist-modify-public");
            //sp.Scope.Add("playlist-read-private");
            //sp.Scope.Add("playlist-read-collaborative");
            app.UseSpotifyAuthentication(sp);
        }

        private static string GetClaimValue(JObject user, string key)
        {
            return !user.TryGetValue(key, out var tok) ? null : tok.ToString();
        }

        private static async Task AddClaimToUser(ApplicationUserManager manager, ApplicationUser user, string claimType, string claimValue, string issuer, IEnumerable<Claim> oldClaims)
        {
            const string schema = "http://www.w3.org/2001/XMLSchema#string";

            var oldClaim = oldClaims.FirstOrDefault(c => c.Type == claimType);

            var claim = new Claim(claimType, claimValue, schema, issuer);

            if (oldClaim != null)
            {
                if (oldClaim.Value == claimValue)
                {
                    return;
                }
                await manager.RemoveClaimAsync(user.Id, claim);
            }

            await manager.AddClaimAsync(user.Id, claim);
        }
    }
}