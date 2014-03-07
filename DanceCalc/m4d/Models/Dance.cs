using DanceLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4d.Models
{
    public class Dance
    {
        public string Id { get; set; }
        public virtual ICollection<DanceRating> DanceRatings { get; set; }

        public DanceObject Info
        {
            get
            {
                if (_info == null)
                {
                    _info = DanceMusicContext.DanceLibrary.DanceDictionary[Id];
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
    }
}
