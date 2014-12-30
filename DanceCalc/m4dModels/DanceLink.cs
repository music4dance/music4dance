using System;
using System.ComponentModel.DataAnnotations;

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
