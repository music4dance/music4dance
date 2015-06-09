using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using m4d.Context;
using m4dModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Facebook;
using Microsoft.Owin.Security.Google;
using Microsoft.Owin.Security.MicrosoftAccount;
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
                LoginPath = new PathString("/Account/Login"),
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
            // This is similar to the RememberMe option when you log in.
            app.UseTwoFactorRememberBrowserCookie(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie);

            // Uncomment the following lines to enable logging in with third party login providers
            var ms = new MicrosoftAccountAuthenticationOptions
            {
                ClientId = "000000004412450B",
                ClientSecret = "0C6Mny6RCkYuHTF62sHVD-ZdQ-fypRuL"
            };
            ms.Scope.Add("wl.basic");
            ms.Scope.Add("wl.emails");
            app.UseMicrosoftAccountAuthentication(ms);


            //app.UseTwitterAuthentication(
            //   consumerKey: "",
            //   consumerSecret: "");

            var fb = new FacebookAuthenticationOptions
            {
                AppId = "503791763032198", 
                AppSecret = "***REMOVED***"
            };
            fb.Scope.Add("email");
            fb.SignInAsAuthenticationType = DefaultAuthenticationTypes.ExternalCookie;
            app.UseFacebookAuthentication(fb);
            
            var gg = new GoogleOAuth2AuthenticationOptions
            {
                ClientId = "818567039467-mjpj2i4712esqje2rrd4mjjb6lntmqq7.apps.googleusercontent.com",
                ClientSecret = "***REMOVED***"
            };
            app.UseGoogleAuthentication(gg);

            var sp = new SpotifyAuthenticationOptions
            {
                ClientId = "***REMOVED***",
                ClientSecret = "***REMOVED***",
                Provider = new SpotifyAuthenticationProvider
                {
                    OnAuthenticated = (context) =>
                    {
                        const string xmlSchemaString = "http://www.w3.org/2001/XMLSchema#string";

                        var accessToken = context.AccessToken;
                        var refreshToken = context.RefreshToken;
                        var timeout = context.ExpiresIn;
                        context.Identity.AddClaim(new Claim("urn:spotify:access_token", accessToken,xmlSchemaString,"Spotify"));
                        context.Identity.AddClaim(new Claim("urn:spotify:refresh_token", refreshToken,xmlSchemaString,"Spotify"));
                        context.Identity.AddClaim(new Claim("urn:spotify:expires_in", timeout.ToString(), xmlSchemaString, "Spotify"));
                        context.Identity.AddClaim(new Claim("urn:spotify:start_time", DateTime.Now.ToString(CultureInfo.InvariantCulture), xmlSchemaString, "Spotify"));

                        foreach (var x in context.User)
                        {
                            var claimType = string.Format("urn:spotify:{0}", x.Key);
                            var claimValue = x.Value.ToString();
                            if (!context.Identity.HasClaim(claimType, claimValue))
                                context.Identity.AddClaim(new Claim(claimType, claimValue, xmlSchemaString, "Facebook"));

                        }

                        return Task.FromResult(0);
                    }
                }
            };
            sp.Scope.Add("user-read-email");
            app.UseSpotifyAuthentication(sp);
        }
    }
}