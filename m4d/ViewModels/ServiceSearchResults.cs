using System.Collections.Generic;
using m4dModels;

namespace m4d.ViewModels
{
    public class ServiceSearchResults
    {
        public string ServiceType { get; set; }
        public Song Song { get; set; }
        public IList<ServiceTrack> Tracks { get; set; }
    }
}