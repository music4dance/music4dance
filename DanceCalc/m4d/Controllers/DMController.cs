using System;
using System.Net;
using System.Web;
using System.Web.Mvc;
using m4d.Context;
using m4dModels;
using Microsoft.AspNet.Identity.Owin;

namespace m4d.Controllers
{
    /// <summary>
    /// Base controller for dance music
    /// </summary>
    public class DMController : Controller
    {
        public DMController() : base()
        {
        }

        public readonly string MusicTheme = "music";
        public readonly string ToolTheme = "tools";
        public readonly string BlogTheme = "blog";
        public readonly string AdminTheme = "admin";

        public virtual string DefaultTheme { get { return BlogTheme; } }
        public string ThemeName 
        {
            get { return _themeName ?? DefaultTheme; }
            set { _themeName = value; }
        }
        private string _themeName;

        public string HelpPage { get; set; }

        public ActionResult ReturnError(HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string message = null, Exception exception = null)
        {
            var model = new ErrorModel { HttpStatusCode = (int)statusCode, Message=message, Exception = exception };

            Response.StatusCode = (int)statusCode;
            Response.TrySkipIisCustomErrors = true;

            return View("HttpError",model);
        }
        protected override ViewResult View(string viewName, string masterName, object model)
        {
            ViewBag.Theme = ThemeName;
            ViewBag.Help = HelpPage;
            return base.View(viewName, masterName, model);
        }

        protected DanceMusicService Database 
        {
            get {
                return _database ??
                       (_database =
                           new DanceMusicService(HttpContext.GetOwinContext().Get<DanceMusicContext>(),
                               HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>()));
            }
        }
        private DanceMusicService _database = null;

        protected void ResetContext()
        {
            var temp = _database;
            _database = null;
            temp.Dispose();
        }

        protected DanceMusicContext Context { get { return Database.Context as DanceMusicContext; } }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            protected set
            {
                _userManager = value;
            }
        }
        private ApplicationUserManager _userManager;

        //// Used for XSRF protection when adding external logins
        //protected const string XsrfKey = "XsrfId";

        //protected IAuthenticationManager AuthenticationManager
        //{
        //    get
        //    {
        //        return HttpContext.GetOwinContext().Authentication;
        //    }
        //}

        //protected async Task<ExternalLoginInfo> GetExternalLoginInfoAsync()
        //{
        //    var userId = User.Identity.GetUserId();
        //    var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, userId);
        //    if (loginInfo == null)
        //    {
        //        return null;
        //    }

        //    if (loginInfo.Email == null)
        //    {
        //        var authResult = await AuthenticationManager.AuthenticateAsync(DefaultAuthenticationTypes.ExternalCookie);

        //        if (authResult != null && authResult.Identity != null && authResult.Identity.IsAuthenticated)
        //        {
        //            var claimsIdentity = authResult.Identity;
        //            var providerKeyClaim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

        //            var providerKey = providerKeyClaim.Value;
        //            var issuer = providerKeyClaim.Issuer;
        //            var name = claimsIdentity.FindFirstValue(ClaimTypes.Name);
        //            var emailAddress = claimsIdentity.FindFirstValue(ClaimTypes.Email);

        //            Trace.WriteLineIf(TraceLevels.General.TraceError,string.Format("providerKey={0};issuer={1};name={2};emailAddress={3}", providerKey, issuer, name, emailAddress));

        //            loginInfo.Email = emailAddress;
        //        }
        //    }

        //    if (loginInfo.Email == null)
        //    {
        //        loginInfo.Email = await UserManager.GetEmailAsync(userId);
        //    }

        //    return loginInfo;
        //}

        //protected void AddErrors(IdentityResult result)
        //{
        //    foreach (var error in result.Errors)
        //    {
        //        ModelState.AddModelError("", error);
        //    }
        //}

        //protected ActionResult RedirectToLocal(string returnUrl)
        //{
        //    if (Url.IsLocalUrl(returnUrl))
        //    {
        //        return Redirect(returnUrl);
        //    }
        //    return RedirectToAction("Index", "Home");
        //}


        protected override void Dispose(bool disposing)
        {
            if (_database != null)
            {
                _database.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}