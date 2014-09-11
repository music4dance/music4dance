using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace m4d.Controllers
{
    /// <summary>
    /// Base controller for dance music
    /// </summary>
    public class DMController : Controller
    {
        public readonly string MusicTheme = "music";
        public readonly string ToolTheme = "tools";
        public readonly string BlogTheme = "blog";

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
    }
}