using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using m4dModels;

namespace m4d.ViewModels
{
    public class ServiceSearchResults
    {
        public string ServiceType { get; set; }
        public SongDetails Song { get; set; }
        public IList<ServiceTrack> Tracks { get; set; }
    }
}