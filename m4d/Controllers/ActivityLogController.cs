using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace m4d.Controllers
{
    public class ActivityLogController : DanceMusicController
    {
        public ActivityLogController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            HelpPage = "song-list";
        }

        // GET: ActivityLogs
        public IActionResult Index()
        {
            var list = Database.Context.ActivityLog.OrderByDescending(l => l.Date).Take(500).Include(a => a.User);
            return View(list);
        }
    }
}
