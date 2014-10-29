using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class DanceLink
    {
        public Guid Id { get; set; }
        public string DanceId { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }
        public string Link { get; set; }
    }
}
