using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class ServiceTrack
    {
        public ServiceType Service { get; set; }
        public string TrackId { get; set; }
        public string Name { get; set; }
        public string CollectionId { get; set; }
        public string AltId { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string ImageUrl { get; set; }
        public string Link { get; set; }
        public string ReleaseDate {get;set;}
        public string Genre { get; set; }
        public int? Duration { get; set; }
        public int? TrackNumber { get; set; }
    }
}
