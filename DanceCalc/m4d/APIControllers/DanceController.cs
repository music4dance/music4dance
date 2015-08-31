using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web.Http;
using DanceLibrary;
using m4dModels;

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

    public class DanceController : DMApiController
    {
        public IHttpActionResult GetAllDances(bool details=false)
        {
            // This should eventually take a filter (or multiple filter) paramter
            var dances = Dance.DanceLibrary.NonPerformanceDanceTypes;
            if (details)
            {
                return Ok(dances);
            }
            var jdance = dances.Select(x => new DanceJson(x));

            return Ok(jdance);
        }

        public IHttpActionResult GetDance(string id)
        {
            DanceObject o;
            if (Dance.DanceLibrary.DanceDictionary.TryGetValue(id.ToUpper(), out o))
            {
                return Ok(new DanceJson(o));
            }
            return NotFound();
        }
    }
}
