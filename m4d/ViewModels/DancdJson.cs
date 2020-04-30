using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DanceLibrary;

namespace m4d.ViewModels
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

        public static IEnumerable<DanceJson> Convert(IEnumerable<DanceType> dances) =>
            dances.Select(x => new DanceJson(x));
    }
}
