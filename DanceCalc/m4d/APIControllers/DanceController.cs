using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web.Http;
using DanceLibrary;
using m4dModels;

namespace m4d.APIControllers
{
    public class DanceJson
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
        public virtual string Id { get; set; }
        public virtual string Name { get; set; }
        public virtual Meter Meter { get; set; }
        public virtual TempoRange TempoRange { get; set; }
        public virtual decimal TempoDelta { get; set; }
        public virtual string SeoName { get; set; }
    }

    public class DanceController : DMApiController
    {
        public IHttpActionResult GetAllDances(bool details=false)
        {
            // This should eventually take a filter (or multiple filter) paramter
            var dances = Dance.DanceLibrary.AllDanceTypes;  //global::DanceLibrary.Dances.Reset().AllDanceTypes;
            if (details)
            {
                return Ok(dances);
            }
            else
            {
                var jdance = dances.Select(x => new DanceJson(x));

                return Ok(jdance);
            }
        }

        public IHttpActionResult GetDance(string id)
        {
            DanceObject o;
            if (Dance.DanceLibrary.DanceDictionary.TryGetValue(id, out o))
            {
                return Ok(new DanceJson(o));
            }
            else
            {
                return NotFound();
            }
        }
    }
}
