using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class Tag : DbObject
    {
        public DateTime Modified { get; set; }
        public string Id { get; set; }
        public TagList Tags { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }
}
