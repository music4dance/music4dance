using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using m4dModels;
using m4d.Context;

using Owin;
using Microsoft.Owin.Security;
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
            Database = new DanceMusicService(new DanceMusicContext());
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
        private string _themeName = null;

        protected override ViewResult View(string viewName, string masterName, object model)
        {
            ViewBag.Theme = ThemeName;
            return base.View(viewName, masterName, model);
        }

        protected DanceMusicService Database {get; private set;}
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


        protected override void Dispose(bool disposing)
        {
            Database.Dispose();
            base.Dispose(disposing);
        }
    }
}