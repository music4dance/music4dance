using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DanceLibrary;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    public sealed class DanceJson
    {
        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DanceJson(DanceObject d)
        {
            Id = d.Id;
            Name = d.Name;
            Meter = d.Meter;
            TempoRange = d.TempoRange;
            SeoName = d.CleanName;
        }
        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DanceJson(DanceSample d) : this(d.DanceType)
        {
            TempoDelta = d.TempoDelta;
        }
        public string Id { get; set; }
        public string Name { get; set; }
        public Meter Meter { get; set; }
        public TempoRange TempoRange { get; set; }
        public decimal TempoDelta { get; set; }
        public string SeoName { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class DancesController : DanceMusicApiController
    {
        public DancesController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        [HttpGet]
        public IActionResult GetDances(bool details=false)
        {
            // This should eventually take a filter (or multiple filter) parameter
            var dances = Dance.DanceLibrary.NonPerformanceDanceTypes;
            if (details)
            {
                return Ok(dances);
            }
            var jsonDances = dances.Select(x => new DanceJson(x));

            return JsonCamelCase(jsonDances);
        }

        [HttpGet("{id}")]

        public IActionResult GetDance(string id)
        {
            var o = Dance.DanceLibrary.DanceFromId(id);
            if (o != null)
            {
                return JsonCamelCase(new DanceJson(o));
            }
            return NotFound();
        }
    }
}
