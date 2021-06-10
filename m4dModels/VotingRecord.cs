using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class VotingRecord
    {
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int Votes { get; set; }
        public int Likes { get; set; }
        public int Hates { get; set; }
        public int Total => Votes + Likes + Hates;
    }
}
