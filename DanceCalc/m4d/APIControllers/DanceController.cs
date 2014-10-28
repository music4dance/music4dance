using DanceLibrary;
using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace m4d.APIControllers
{
    public class DanceJson
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DanceJson(DanceObject d)
        {
            Id = d.Id;
            Name = d.Name;
            Meter = d.Meter;
            TempoRange = d.TempoRange;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DanceJson(DanceSample d) : this(d.DanceType)
        {
            TempoDelta = d.TempoDelta;
        }
        public virtual string Id { get; set; }
        public virtual string Name { get; set; }
        public virtual Meter Meter { get; set; }
        public virtual TempoRange TempoRange { get; set; }
        public virtual decimal TempoDelta { get; set; }
    }

    public class DanceController : DMApiController
    {
        public IHttpActionResult GetAllDances(bool details=false)
        {
            // This should eventually take a filter (or multiple filter) paramter
            var dances = Dance.DanceLibrary.AllDanceTypes;  //global::DanceLibrary.Dances.Reset().AllDanceTypes;
            if (details == true)
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
            DanceObject o = null;
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
