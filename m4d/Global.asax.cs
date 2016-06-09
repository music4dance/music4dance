using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using m4d.Context;
using m4d.Controllers;
using m4d.Utilities;
using m4dModels;

namespace m4d
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            ModelBinders.Binders[typeof(SongDetails)] = new SongBinder();

            DanceStatsManager.AppData = System.Web.Hosting.HostingEnvironment.MapPath("~/app_data");
            DanceMusicService.Factory = new DanceMusicFactory();
        }
        protected void Application_Error(object sender, EventArgs e)
        {
            var lastError = Server.GetLastError();
            Server.ClearError();

            var statusCode = 0;

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if ((lastError != null) && (lastError.GetType() == typeof(HttpException)))
            {
                statusCode = ((HttpException)lastError).GetHttpCode();
            }
            else
            {
                // Not an HTTP related error so this is a problem in our code, set status to
                // 500 (internal server error)
                statusCode = 500;
            }

            var contextWrapper = new HttpContextWrapper(Context);

            var routeData = new RouteData();
            routeData.Values.Add("controller", "Error");
            routeData.Values.Add("action", "Index");
            routeData.Values.Add("statusCode", statusCode);
            routeData.Values.Add("exception", lastError??new Exception("Something Really Went WRONG!!!"));
            routeData.Values.Add("isAjaxRequest", contextWrapper.Request.IsAjaxRequest());

            IController controller = new ErrorController();

            var requestContext = new RequestContext(contextWrapper, routeData);

            controller.Execute(requestContext);
            Response.End();
        }
    }
}
