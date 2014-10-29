using DanceLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class Dance
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<DanceLink> DanceLinks { get; set; }

        public DanceObject Info
        {
            get
            {
                if (_info == null)
                {
                    _info = DanceLibrary.DanceDictionary[Id];
                }

                return _info;
            }
        }

        public string Name
        {
            get
            {
                return Info.Name;
            }
        }

        private DanceObject _info;

        public static Dances DanceLibrary
        {
            get
            {
                return _dances;
            }
        }

        private static Dances _dances = global::DanceLibrary.Dances.Instance;
    }
}
