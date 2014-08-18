using m4d.Utilities;
using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace m4d
{
    public class MvcApplication : System.Web.HttpApplication
    {
        //private System.Diagnostics.TraceSwitch _generalSwitch = new System.Diagnostics.TraceSwitch("General", "Entire application");

        //public System.Diagnostics.TraceSwitch GeneralSwitch
        //{
        //    get { return _generalSwitch; }
        //}

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            ModelBinders.Binders[typeof(SongDetails)] = new SongBinder();
        }
    }
}
