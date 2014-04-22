using DanceLibrary;
using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace m4d.Controllers
{
    public class DanceJson
    {
        public DanceJson(DanceObject d)
        {
            Id = d.Id;
            Name = d.Name;
            Meter = d.Meter;
            TempoRange = d.TempoRange;
        }
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

    public class DanceController : ApiController
    {
        public IHttpActionResult GetAllDances()
        {
            return Ok(Dance.DanceLibrary.AllDances);
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

        public IHttpActionResult GetDancesByTempo(decimal tempo, int numerator=1)
        {
            Tempo t = null;
            if (numerator == 1)
                t = new Tempo(tempo, new TempoType(TempoKind.BPM)); // Tempo in beats per minute
            else
                t = new Tempo(tempo, new TempoType(TempoKind.MPM, new Meter(numerator, 4))); // Tempo in measures per minute

            var dances = Dance.DanceLibrary.DancesFiltered(t, 10M);
            var jdance = dances.Select(x => new DanceJson(x));
            return Ok(jdance);
        }
    }
}
