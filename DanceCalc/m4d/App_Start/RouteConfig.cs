using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

using m4d.Utilities;

namespace m4d
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRouteLowercase(
                name: "Dances",
                url: "dances/{group}/{dance}",
                defaults: new { controller = "dance", action = "GroupRedirect" }
            );
            routes.MapRouteLowercase(
                name: "DanceGroup",
                url: "dances/{dance}",
                defaults: new { controller = "dance", action = "index", dance = UrlParameter.Optional }
            );
            routes.MapRouteLowercase(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "home", action = "index", id = UrlParameter.Optional }
            );
        }
    }
}
