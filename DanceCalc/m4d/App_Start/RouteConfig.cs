using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace m4d
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Dances",
                url: "Dances/{group}/{dance}",
                defaults: new { controller = "Dance", action = "GroupRedirect" }
            );
            routes.MapRoute(
                name: "DanceGroup",
                url: "Dances/{dance}",
                defaults: new { controller = "Dance", action = "Index", dance = UrlParameter.Optional }
            );
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
