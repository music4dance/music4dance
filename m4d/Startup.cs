using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(m4d.Startup))]
namespace m4d
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
#if DEBUG
            TelemetryConfiguration.Active.DisableTelemetry = true;
#endif
        }
    }
}
